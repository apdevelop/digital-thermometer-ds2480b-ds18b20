using System;
using System.Linq;

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

        public string TemperatureValueString
        {
            get
            {
                return this.scratchpad.Temperature.HasValue ?
                    ((this.scratchpad.Temperature > 0.0) ? "+" : String.Empty) +
                      this.scratchpad.Temperature.Value.ToString("F4") :
                      "?";
            }
        }

        public string TemperatureRawCodeString
        {
            get
            {
                return this.scratchpad.TemperatureRawData.HasValue ?
                    "0x" + this.scratchpad.TemperatureRawData.Value.ToString("X4") :
                    "?";
            }
        }

        public string ComputedCrcString
        {
            get
            {
                return "0x" + this.scratchpad.ComputedCrc.ToString("X2") + " (" + (this.scratchpad.IsValidCrc ? "OK" : "Bad") + ")";
            }
        }

        public string RawDataString
        {
            get
            {
                if (this.scratchpad.RawData != null)
                {
                    return String.Join(" ", this.scratchpad.RawData.Select(b => b.ToString("X2")));
                }
                else
                {
                    return "-";
                }
            }
        }
    }
}