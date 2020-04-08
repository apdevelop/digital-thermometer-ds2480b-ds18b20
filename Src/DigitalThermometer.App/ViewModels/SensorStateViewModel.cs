﻿using System;
using System.Linq;

using DigitalThermometer.App.Models;
using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.App.ViewModels
{
    public class SensorStateViewModel
    {
        private readonly int indexNumber = 0;

        private readonly SensorStateModel sensorState;

        public SensorStateViewModel(int indexNumber, SensorStateModel sensorState)
        {
            this.indexNumber = indexNumber;
            this.sensorState = sensorState;
        }

        public int IndexNumberString
        {
            get
            {
                return this.indexNumber + 1;
            }
        }

        public string RomCodeString
        {
            get
            {
                return OW.Utils.RomCodeToLEString(this.sensorState.RomCode);
            }
        }

        public string TemperatureValueString
        {
            get
            {
                return this.sensorState.TemperatureValue.HasValue ?
                    ((this.sensorState.TemperatureValue > 0.0) ? "+" : String.Empty) +
                        this.sensorState.TemperatureValue.Value.ToString("F4") :
                        "?";
            }
        }

        public string TemperatureRawCodeString
        {
            get
            {
                return this.sensorState.TemperatureRawCode.HasValue ?
                    "0x" + this.sensorState.TemperatureRawCode.Value.ToString("X4") :
                    "?";
            }
        }

        public string ThermometerResolutionString
        {
            get
            {
                if (this.sensorState.ThermometerResolution != null)
                {
                    switch (this.sensorState.ThermometerResolution.Value)
                    {
                        case OW.DS18B20.ThermometerResolution.Resolution9bit: return "9-bit";
                        case OW.DS18B20.ThermometerResolution.Resolution10bit: return "10-bit";
                        case OW.DS18B20.ThermometerResolution.Resolution11bit: return "11-bit";
                        case OW.DS18B20.ThermometerResolution.Resolution12bit: return "12-bit";
                        default: throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    return "?";
                }
            }
        }

        public string RawDataString
        {
            get
            {
                if (this.sensorState.RawData != null)
                {
                    return String.Join(" ", this.sensorState.RawData.Select(b => b.ToString("X2")));
                }
                else
                {
                    return "-";
                }
            }
        }

        public string ComputedCrcString
        {
            get
            {
                return (this.sensorState.ComputedCrc.HasValue && this.sensorState.IsValidCrc.HasValue) ?
                    ("0x" + this.sensorState.ComputedCrc.Value.ToString("X2") + " (" + ((this.sensorState.IsValidCrc.Value) ? "OK" : "Bad") + ")") :
                    "?";
            }
        }

        public bool? IsValidCrc
        {
            get
            {
                if (this.sensorState.IsValidCrc.HasValue)
                {
                    return this.sensorState.IsValidCrc.Value;
                }
                else
                {
                    return null;
                }
            }
        }
    }
}