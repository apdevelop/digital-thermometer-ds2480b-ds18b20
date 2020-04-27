namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// Flexible Speed configuration
    /// </summary>
    public class FlexibleSpeedConfiguration
    {
        /// <summary>
        /// (PDSRC) Pulldown Slew Rate Control
        /// </summary>
        public DS2480B.PulldownSlewRateControl PulldownSlewRateControl { get; set; }

        /// <summary>
        /// (W1LT) Write-1 Low Time
        /// </summary>
        public DS2480B.Write1LowTime Write1LowTime { get; set; }

        /// <summary>
        /// (DSO/W0RT) Data Sample Offset and Write 0 Recovery Time
        /// </summary>
        public DS2480B.DataSampleOffsetAndWrite0RecoveryTime DataSampleOffsetAndWrite0RecoveryTime { get; set; }
    }
}