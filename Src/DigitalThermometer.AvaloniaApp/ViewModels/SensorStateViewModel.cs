using System;
using System.Reactive;

using Avalonia.Controls;
using ReactiveUI;

using M = DigitalThermometer.AvaloniaApp.Models;
using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.AvaloniaApp.ViewModels
{
    public class SensorStateViewModel
    {
        private readonly int indexNumber = 0;

        private readonly M.SensorStateModel sensorState;

        private readonly Window window;

        public ReactiveCommand<Unit, Unit> CopyRomCodeHexLEStringCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> CopyRomCodeHexNumberCommand { get; private set; }

        public SensorStateViewModel(int indexNumber, M.SensorStateModel sensorState, Window window)
        {
            this.indexNumber = indexNumber;
            this.sensorState = sensorState;
            this.window = window;

            // instance of Window for clipboard access
            this.CopyRomCodeHexLEStringCommand = ReactiveCommand.CreateFromTask(async () => { await TopLevel.GetTopLevel(this.window)?.Clipboard.SetTextAsync(this.RomCodeString); });
            this.CopyRomCodeHexNumberCommand = ReactiveCommand.CreateFromTask(async () => { await TopLevel.GetTopLevel(this.window)?.Clipboard.SetTextAsync("0x" + this.sensorState.RomCode.ToString("X16")); });
        }

        public int IndexNumberString => this.indexNumber + 1;

        public string RomCodeString => OW.Utils.RomCodeToLEString(this.sensorState.RomCode);

        public string TemperatureValueString => this.sensorState.TemperatureValue.HasValue ?
                    ((this.sensorState.TemperatureValue > 0.0) ? "+" : String.Empty) +
                        this.sensorState.TemperatureValue.Value.ToString("F4") :
                        "?";

        public string TemperatureRawCodeString => this.sensorState.TemperatureRawCode.HasValue ?
                    "0x" + this.sensorState.TemperatureRawCode.Value.ToString("X4") :
                    "?";

        public bool? IsPowerUpTemperatureValue => this.sensorState.TemperatureRawCode.HasValue ?
                    (bool?)(this.sensorState.TemperatureRawCode.Value == OW.DS18B20.PowerOnTemperatureCode) :
                    null;

        public string THString => this.sensorState.HighAlarmTemperature.HasValue ? this.sensorState.HighAlarmTemperature.Value.ToString() : "?";

        public string TLString => this.sensorState.LowAlarmTemperature.HasValue ? this.sensorState.LowAlarmTemperature.Value.ToString() : "?";

        public string ThermometerResolutionString => this.sensorState.ThermometerResolution.HasValue
                    ? OW.DS18B20.ThermometerResolutionToString(this.sensorState.ThermometerResolution.Value)
                    : "?";

        public string RawDataString => this.sensorState.RawData != null ?
                    OW.Utils.ByteArrayToHexSpacedString(this.sensorState.RawData) :
                    "-";

        public string ComputedCrcString => (this.sensorState.ComputedCrc.HasValue && this.sensorState.IsValidCrc.HasValue)
                    ? ("0x" + this.sensorState.ComputedCrc.Value.ToString("X2") + " (" + ((this.sensorState.IsValidCrc.Value) ? "OK" : "!") + ")")
                    : "?";

        public bool? IsValidCrc => this.sensorState.IsValidCrc ?? null;

        public bool? IsValidReadings => this.sensorState.IsValidCrc.HasValue && this.sensorState.TemperatureRawCode.HasValue
                    ? this.sensorState.IsValidCrc.Value && OW.DS18B20.IsValidTemperatureCode(this.sensorState.TemperatureRawCode.Value)
                    : (bool?)null;
    }
}
