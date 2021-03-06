﻿namespace Lykke.Service.LiquidityEngine.Domain
{
    /// <summary>
    /// Represents asset pair markup.
    /// </summary>
    public class AssetPairMarkup
    {
        /// <summary>
        /// The identifier of asset pair.
        /// </summary>
        public string AssetPairId { get; set; }

        /// <summary>
        /// Total markup for both asks and bids, without level markups.
        /// </summary>
        public decimal TotalMarkup { get; set; }

        /// <summary>
        /// Total asks markups, without level markups.
        /// </summary>
        public decimal TotalAskMarkup { get; set; }

        /// <summary>
        /// Total bids markups, without level markups.
        /// </summary>
        public decimal TotalBidMarkup { get; set; }
    }
}
