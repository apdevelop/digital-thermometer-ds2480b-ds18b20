using System;
using System.Windows;
using System.Windows.Input;

using DigitalThermometer.App.Models;
using DigitalThermometer.App.Utils;
using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.App.ViewModels
{
    public class SensorStateViewModel
    {
        private readonly int indexNumber = 0;

        private readonly SensorStateModel sensorState;

        public ICommand CopyRomCodeHexLEStringCommand { get; private set; }

        public ICommand CopyRomCodeHexNumberCommand { get; private set; }

        public SensorStateViewModel(int indexNumber, SensorStateModel sensorState)
        {
            this.indexNumber = indexNumber;
            this.sensorState = sensorState;

            this.CopyRomCodeHexLEStringCommand = new RelayCommand((o) => { Clipboard.SetText(this.RomCodeString); });
            this.CopyRomCodeHexNumberCommand = new RelayCommand((o) => { Clipboard.SetText("0x" + this.sensorState.RomCode.ToString("X16")); });
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

        public bool? IsPowerUpTemperatureValue
        {
            get
            {
                return this.sensorState.TemperatureRawCode.HasValue ?
                    (bool?)(this.sensorState.TemperatureRawCode.Value == OW.DS18B20.PowerOnTemperatureCode) :
                    null;
            }
        }

        public string THString
        {
            get
            {
                if (this.sensorState.HighAlarmTemperature.HasValue)
                {
                    return this.sensorState.HighAlarmTemperature.Value.ToString();
                }
                else
                {
                    return "?";
                }
            }
        }

        public string TLString
        {
            get
            {
                if (this.sensorState.LowAlarmTemperature.HasValue)
                {
                    return this.sensorState.LowAlarmTemperature.Value.ToString();
                }
                else
                {
                    return "?";
                }
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
                    return OW.Utils.ByteArrayToHexSpacedString(this.sensorState.RawData);
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
                    ("0x" + this.sensorState.ComputedCrc.Value.ToString("X2") + " (" + ((this.sensorState.IsValidCrc.Value) ? "OK" : "!") + ")") :
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