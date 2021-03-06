﻿using System;

namespace Lykke.Service.LiquidityEngine.Domain
{
    /// <summary>
    /// Represents a pnl stop loss engine.
    /// </summary>
    public class PnLStopLossEngine
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Asset pair identifier.
        /// </summary>
        public string AssetPairId { get; set; }

        /// <summary>
        /// PnL stop loss settings identifier (for global setting only).
        /// </summary>
        public string PnLStopLossSettingsId { get; set; }

        /// <summary>
        /// Time interval for calculating loss.
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// PnL threshold.
        /// </summary>
        public decimal Threshold { get; set; }

        /// <summary>
        /// Markup.
        /// </summary>
        public decimal Markup { get; set; }

        /// <summary>
        /// Total negative PnL.
        /// </summary>
        public decimal TotalNegativePnL { get; set; }

        /// <summary>
        /// First time when negative PnL occured.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Last time when negative PnL occured.
        /// </summary>
        public DateTime? LastTime { get; set; }

        /// <summary>
        /// The mode of the pnl stop loss engine.
        /// </summary>
        public PnLStopLossEngineMode Mode { get; set; }

        public PnLStopLossEngine()
        {
        }

        public PnLStopLossEngine(PnLStopLossSettings pnLStopLossSettings)
        {
            Interval = pnLStopLossSettings.Interval;
            Threshold = pnLStopLossSettings.Threshold;
            Markup = pnLStopLossSettings.Markup;
            PnLStopLossSettingsId = null;
            TotalNegativePnL = 0;
            StartTime = null;
            LastTime = null;
            Mode = PnLStopLossEngineMode.Idle;
        }

        public bool AddNegativePnL(decimal positionNegativePnL)
        {
            if (TotalNegativePnL == 0)
                StartTime = DateTime.UtcNow;

            TotalNegativePnL += positionNegativePnL;

            LastTime = DateTime.UtcNow;

            return Refresh();
        }

        public void Disable()
        {
            Reset();

            Mode = PnLStopLossEngineMode.Disabled;
        }

        public void Enable()
        {
            Mode = PnLStopLossEngineMode.Idle;
        }

        public void Update(PnLStopLossEngine pnLStopLossEngine)
        {
            Interval = pnLStopLossEngine.Interval;
            Threshold = pnLStopLossEngine.Threshold;
            Markup = pnLStopLossEngine.Markup;
        }

        public bool Refresh()
        {
            if (StartTime == null)
                return false;

            if (LastTimeExpired || StartTimeExpired && !ThresholdExceeded)
            {
                Reset();
                return true;
            }

            if (ThresholdExceeded && Mode != PnLStopLossEngineMode.Active)
            {
                Mode = PnLStopLossEngineMode.Active;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            TotalNegativePnL = 0;
            StartTime = null;
            LastTime = null;
            Mode = PnLStopLossEngineMode.Idle;
        }

        private bool ThresholdExceeded => TotalNegativePnL <= Threshold * -1;

        private bool StartTimeExpired => DateTime.UtcNow - StartTime > Interval;

        private bool LastTimeExpired => DateTime.UtcNow - LastTime > Interval;
    }
}
