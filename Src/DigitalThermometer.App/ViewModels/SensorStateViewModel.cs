﻿using System;
using System.Globalization;

using DigitalThermometer.App.Models;

namespace DigitalThermometer.App.ViewModels
{
    public class SensorStateViewModel
    {
        private readonly int indexNumber = 0;

        private SensorStateModel sensorState;

        public SensorStateViewModel(int indexNumber, SensorStateModel sensorState)
        {
            this.indexNumber = indexNumber;
            this.sensorState = sensorState;
        }

        public int IndexNumberString
        {
            get
            {
                return (this.indexNumber + 1);
            }
        }

        public string RomCodeString
        {
            get
            {
                return DigitalThermometer.Hardware.DS18B20.RomCodeToLEString(this.sensorState.RomCode);
            }
        }

        public string TemperatureValueString
        {
            get
            {
                return this.sensorState.TemperatureValue.HasValue ?
                    ((this.sensorState.TemperatureValue > 0.0) ? "+" : String.Empty) + 
                        String.Format(CultureInfo.CurrentCulture, "{0:0.000}", this.sensorState.TemperatureValue) :
                    "?";
            }
        }
    }
}