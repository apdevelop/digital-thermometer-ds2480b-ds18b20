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
    }
}