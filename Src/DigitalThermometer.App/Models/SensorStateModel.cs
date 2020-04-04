namespace DigitalThermometer.App.Models
{
    // TODO: ? move to HAL
    public class SensorStateModel
    {
        public ulong RomCode { get; set; }

        public double? TemperatureValue { get; set; }
    }
}