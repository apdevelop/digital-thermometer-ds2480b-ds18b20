using System;

using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.App.Models
{
    public class SensorStateModel
    {
        public ulong RomCode { get; set; }

        public double? TemperatureValue { get; set; }

        public UInt16? TemperatureRawCode { get; set; }

        public OW.DS18B20.ThermometerResolution? ThermometerResolution { get; set; }

        public byte[] RawData { get; set; }

        public byte? ComputedCrc { get; set; }

        public bool? IsValidCrc { get; set; }
    }
}