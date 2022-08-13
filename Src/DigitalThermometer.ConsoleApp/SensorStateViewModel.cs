using System;

using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.ConsoleApp
{
    class SensorStateViewModel
    {
        private readonly OW.DS18B20.Scratchpad scratchpad;

        public SensorStateViewModel(OW.DS18B20.Scratchpad scratchpad)
        {
            this.scratchpad = scratchpad;
        }

        public string TemperatureValueString => this.scratchpad.Temperature.HasValue ?
                    ((this.scratchpad.Temperature > 0.0) ? "+" : String.Empty) +
                      this.scratchpad.Temperature.Value.ToString("F4") :
                      "?";

        public string TemperatureRawCodeString => this.scratchpad.TemperatureRawData.HasValue ?
                    "0x" + this.scratchpad.TemperatureRawData.Value.ToString("X4") :
                    "?";

        public string ComputedCrcString => "0x" + this.scratchpad.ComputedCrc.ToString("X2") + " (" + (this.scratchpad.IsValidCrc ? "OK" : "Bad") + ")";

        public string RawDataString => this.scratchpad.RawData != null ? OW.Utils.ByteArrayToHexSpacedString(this.scratchpad.RawData) : "-";
    }
}
