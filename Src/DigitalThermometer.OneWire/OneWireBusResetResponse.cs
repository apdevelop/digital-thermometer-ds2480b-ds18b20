namespace DigitalThermometer.OneWire
{
    public enum OneWireBusResetResponse
    {
        /// <summary>
        /// Invalid bus reset response value
        /// </summary>
        InvalidResponse,

        /// <summary>
        /// No bus reset response was received
        /// </summary>
        NoBusResetResponse,

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