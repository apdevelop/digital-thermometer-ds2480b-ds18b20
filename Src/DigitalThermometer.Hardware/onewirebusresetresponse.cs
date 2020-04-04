namespace DigitalThermometer.Hardware
{
    public enum OneWireBusResetResponse
    {
        InvalidResponse,

        NoBusResetResponse,

        OneWireShorted,

        PresencePulse,

        AlarmingPresencePulse,

        NoPresencePulse,
    }
}