namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// 1-Wire bus reset response
    /// </summary>
    public enum OneWireBusResetResponse
    {
        /// <summary>
        /// No bus reset response was received
        /// </summary>
        NoResponse,

        /// <summary>
        /// Invalid bus reset response value
        /// </summary>
        InvalidResponse,

        /// <summary>
        /// 1-Wire bus shorted
        /// </summary>
        BusShorted,

        /// <summary>
        /// Presence pulse
        /// </summary>
        PresencePulse,

        /// <summary>
        /// Alarming presence pulse
        /// </summary>
        AlarmingPresencePulse,

        /// <summary>
        /// No presence pulse
        /// </summary>
        NoPresencePulse,
    }
}