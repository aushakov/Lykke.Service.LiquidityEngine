﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Log;
using JetBrains.Annotations;
using Lykke.B2c2Client;
using Lykke.B2c2Client.Exceptions;
using Lykke.B2c2Client.Models.Rest;
using Lykke.Common.Log;
using Lykke.Service.LiquidityEngine.Domain;
using Lykke.Service.LiquidityEngine.Domain.Consts;
using Lykke.Service.LiquidityEngine.Domain.Exceptions;
using Lykke.Service.LiquidityEngine.Domain.Extensions;
using Lykke.Service.LiquidityEngine.Domain.Services;
using Polly;

namespace Lykke.Service.LiquidityEngine.DomainServices.Exchanges
{
    [UsedImplicitly]
    public class ExternalExchangeService : IExternalExchangeService
    {
        private const int DefaultRetriesCount = 3;

        private readonly IB2С2RestClient _client;
        private readonly IAssetPairLinkService _assetPairLinkService;
        private readonly ILog _log;

        public ExternalExchangeService(
            IB2С2RestClient client,
            IAssetPairLinkService assetPairLinkService,
            ILogFactory logFactory)
        {
            _client = client;
            _assetPairLinkService = assetPairLinkService;
            _log = logFactory.CreateLog(this);
        }

        public Task<IReadOnlyCollection<Balance>> GetBalancesAsync()
            => ExecuteGetBalancesAsync();

        public Task<ExternalTrade> ExecuteSellLimitOrderAsync(string assetPairId, decimal volume)
            => ExecuteLimitOrderAsync(assetPairId, volume, null, Side.Sell);

        public Task<ExternalTrade> ExecuteBuyLimitOrderAsync(string assetPairId, decimal volume)
            => ExecuteLimitOrderAsync(assetPairId, volume, null, Side.Buy);

        public Task<ExternalTrade> ExecuteLimitOrderAsync(string assetPairId, decimal volume, decimal price,
            LimitOrderType limitOrderType)
            => ExecuteLimitOrderAsync(assetPairId, volume, price,
                limitOrderType == LimitOrderType.Sell ? Side.Sell : Side.Buy);

        private async Task<ExternalTrade> ExecuteLimitOrderAsync(string assetPairId, decimal volume, decimal? price,
            Side side)
        {
            string instrument = await GetInstrumentAsync(assetPairId);

            var request = new RequestForQuoteRequest(instrument, side, volume);

            RequestForQuoteResponse response;

            Trade trade = await WrapAsync(async () =>
            {
                _log.InfoWithDetails("Get quote request", request);

                response = await _client.RequestForQuoteAsync(request);

                _log.InfoWithDetails("Get quote response", response);

                if (price.HasValue)
                {
                    if (side == Side.Sell && price.Value > response.Price)
                        throw new NotEnoughLiquidityException();

                    if (side == Side.Buy && price.Value < response.Price)
                        throw new NotEnoughLiquidityException();
                }

                var tradeRequest = new TradeRequest(response);

                _log.InfoWithDetails("Execute trade request", tradeRequest);

                Trade tradeResponse = await _client.TradeAsync(tradeRequest);

                _log.InfoWithDetails("Execute trade response", tradeResponse);

                return tradeResponse;
            });

            return new ExternalTrade
            {
                Id = trade.TradeId,
                LimitOrderId = trade.Order,
                AssetPairId = trade.Instrument,
                Type = trade.Side == Side.Sell ? TradeType.Sell : TradeType.Buy,
                Time = trade.Created,
                Price = trade.Price,
                Volume = trade.Quantity,
                RequestId = trade.RfqId
            };
        }

        private Task<IReadOnlyCollection<Balance>> ExecuteGetBalancesAsync()
        {
            return WrapAsync<IReadOnlyCollection<Balance>>(async () =>
            {
                IReadOnlyDictionary<string, decimal> balances = await _client.BalanceAsync();

                return balances
                    .Select(o => new Balance(ExchangeNames.External, o.Key, o.Value))
                    .ToArray();
            });
        }

        private async Task<string> GetInstrumentAsync(string assetPairId)
        {
            IReadOnlyCollection<AssetPairLink> assetPairLinks = await _assetPairLinkService.GetAllAsync();

            AssetPairLink assetPairLink = assetPairLinks.SingleOrDefault(o => o.AssetPairId == assetPairId);

            return assetPairLink != null ? assetPairLink.ExternalAssetPairId : assetPairId;
        }

        private Task<T> WrapAsync<T>(Func<Task<T>> action)
        {
            return Policy
                .Handle<B2c2RestException>(exception =>
                {
                    if (exception.ErrorResponse.Status == HttpStatusCode.TooManyRequests)
                        return true;

                    Error error = exception.ErrorResponse.Errors.FirstOrDefault();

                    return error != null && error.Code == ErrorCode.PriceNotValid;
                })
                .WaitAndRetryAsync(DefaultRetriesCount, attempt => TimeSpan.FromMilliseconds(500 * attempt),
                    (exception, timeSpan, attempt, context) =>
                    {
                        _log.InfoWithDetails("Request failed waiting next retry", new
                        {
                            Error = exception.Message,
                            Delay = timeSpan,
                            Attempt = attempt
                        });
                    })
                .ExecuteAsync(async () => await action());
        }
    }
}
