using System;

namespace DigitalThermometer.App.Models
{
    public class SensorStateModel
    {
        public ulong RomCode { get; set; }

        public double? TemperatureValue { get; set; }

        public UInt16? TemperatureRawCode { get; set; }

        public Hardware.DS18B20.ThermometerResolution? ThermometerResolution { get; set; }
    }
}