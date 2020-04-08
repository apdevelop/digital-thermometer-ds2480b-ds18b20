using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using DigitalThermometer.App.Models;
using DigitalThermometer.App.Utils;

using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.App.ViewModels
{
    public class MainViewModel : BaseNotifyPropertyChanged
    {
        public ICommand PerformMeasureCommand { get; private set; }

        public ICommand MeasureInDemoModeCommand { get; private set; }

        public MainViewModel()
        {
            this.PerformMeasureCommand = new RelayCommand(async (o) => await this.PerformMeasurementsAsync());
            this.MeasureInDemoModeCommand = new RelayCommand(async (o) => await this.PerformMeasurementsInDemoModeAsync());

            // TODO: config and save/restore settings
            if (this.SerialPortNames.Count > 0)
            {
                this.SelectedSerialPortName = this.SerialPortNames[0];
            }

            this.measurementsTimer.Tick += async (s, e) =>
            {
                if (this.IsMeasuresEnabled)
                {
                    await this.PerformMeasurementsAsync();
                }
            };
        }

        public IList<string> SerialPortNames
        {
            get
            {
                return SerialPortUtils.GetSerialPortNames();
            }
        }

        private string selectedSerialPortName = null;

        public string SelectedSerialPortName
        {
            get
            {
                return this.selectedSerialPortName;
            }

            set
            {
                if (this.selectedSerialPortName != value)
                {
                    this.selectedSerialPortName = value;
                    base.OnPropertyChanged(nameof(SelectedSerialPortName));
                }
            }
        }

        private bool isBusy = false;

        public bool IsBusy
        {
            get
            {
                return this.isBusy;
            }

            private set
            {
                if (this.isBusy != value)
                {
                    this.isBusy = value;
                    base.OnPropertyChanged("IsBusy");
                    base.OnPropertyChanged("IsMeasuresEnabled");
                }
            }
        }

        public bool IsMeasuresEnabled
        {
            get
            {
                return (!this.IsBusy);
            }
        }

        private bool isSimultaneousMeasurementsMode = true;

        public bool IsSimultaneousMeasurementsMode
        {
            get
            {
                return this.isSimultaneousMeasurementsMode;
            }

            set
            {
                if (this.isSimultaneousMeasurementsMode != value)
                {
                    this.isSimultaneousMeasurementsMode = value;
                    base.OnPropertyChanged(nameof(IsSimultaneousMeasurementsMode));
                }
            }
        }

        private readonly DispatcherTimer measurementsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15), IsEnabled = false, };

        private bool isTimerMeasurementsMode = false;

        public bool IsTimerMeasurementsMode
        {
            get
            {
                return this.isTimerMeasurementsMode;
            }

            set
            {
                if (this.isTimerMeasurementsMode != value)
                {
                    this.isTimerMeasurementsMode = value;
                    this.measurementsTimer.IsEnabled = value;
                    base.OnPropertyChanged(nameof(IsTimerMeasurementsMode));
                }
            }
        }

        private async Task PerformMeasurementsInDemoModeAsync()
        {
            this.IsBusy = true;
            this.SensorsState = null;
            this.DisplayState(String.Empty);

            var sensors = new List<SensorStateModel>(new[]
            {
                new SensorStateModel { RomCode = 0x01000002F81B3428, TemperatureValue = +10.0, TemperatureRawCode = 0x00A0, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x1200000078BA0D28, TemperatureValue = +25.0625, TemperatureRawCode = 0x0191, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x8E00000078CEAB28, TemperatureValue = -10.125, TemperatureRawCode= 0xFF5E, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0xEA00000078B0FC28, TemperatureValue = null, TemperatureRawCode = null, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x91000000BED06928, TemperatureValue = +85.0, TemperatureRawCode = 0x0550, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
            });

            var list = sensors
                .Select(s => new SensorStateModel
                {
                    RomCode = s.RomCode,
                    TemperatureValue = null,
                    TemperatureRawCode = null,
                    ThermometerResolution = null,
                })
                .ToList();

            await Task.Delay(1000);

            this.MarshalToMainThread((items) => this.SensorsState = items, list);

            await Task.Delay(1000);

            foreach (var s in sensors)
            {
                await Task.Delay(200);
                this.MarshalToMainThread((state) => this.UpdateSensorState(state), s);
            }

            this.MarshalToMainThread(() => this.IsBusy = false);
        }

        private void MarshalToMainThread(Action action)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
            {
                action();
            });
        }

        private void MarshalToMainThread<T>(Action<T> action, T parameter)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
            {
                action(parameter);
            });
        }

        private string busState = String.Empty;

        public string BusState
        {
            get
            {
                return this.busState;
            }

            set
            {
                if (this.busState != value)
                {
                    this.busState = value;
                    base.OnPropertyChanged(nameof(BusState));
                }
            }
        }

        private void DisplayState(string state)
        {
            this.MarshalToMainThread(s => this.BusState = s, state);
        }

        private async Task PerformMeasurementsAsync()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();

            this.measuresRuns++;
            var stopwatch = Stopwatch.StartNew();

            this.DisplayState("Initializing...");
            var portConnection = new SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection);

            var result = new Dictionary<UInt64, OW.DS18B20.Scratchpad>();

            try
            {
                this.DisplayState("Performing bus reset...");
                var busResult = await busMaster.OpenAsync();

                // Bus diagnostic
                switch (busResult)
                {
                    case OW.OneWireBusResetResponse.NoResponse:
                        {
                            this.DisplayState("Bus reset response was not received");
                            result = null;
                            break;
                        }
                    case OW.OneWireBusResetResponse.NoPresencePulse:
                        {
                            this.DisplayState("No presence pulse");
                            result = null;
                            break;
                        }
                    case OW.OneWireBusResetResponse.BusShorted:
                        {
                            this.DisplayState("Bus is shorted");
                            result = null;
                            break;
                        }
                    case OW.OneWireBusResetResponse.PresencePulse:
                        {
                            this.DisplayState("Presence pulse OK");
                            break;
                        }
                    case OW.OneWireBusResetResponse.InvalidResponse:
                        {
                            this.DisplayState("Invalid response received");
                            result = null;
                            break;
                        }
                }

                if (result != null)
                {
                    var count = 0;
                    this.DisplayState("Searching devices on bus...");
                    var list = await busMaster.SearchDevicesOnBusAsync((romCode) =>
                    {
                        count++;
                        this.DisplayState($"Sensor found: {count} <{OW.Utils.RomCodeToLEString(romCode)}>");
                        this.MarshalToMainThread(
                            (s) => this.AddFoundSensor(s),
                            new SensorStateModel
                            {
                                RomCode = romCode,
                                TemperatureValue = null,
                                TemperatureRawCode = null,
                                ThermometerResolution = null,
                            });
                    });

                    // TODO: order list of sensors

                    if (list != null)
                    {
                        // http://www.claassen.net/geek/blog/2007/07/inotifypropertychanged-and-cross-thread-exceptions.html

                        this.DisplayState($"Totoal sensors found: {list.Count}");
                        this.DisplayState("Performing measure...");

                        var results = new Dictionary<ulong, OW.DS18B20.Scratchpad>();
                        if (this.IsSimultaneousMeasurementsMode)
                        {
                            var counter = 0;
                            results = (Dictionary<ulong, OW.DS18B20.Scratchpad>)(await busMaster.PerformDS18B20TemperatureMeasurementAsync(list, (r) =>
                                {
                                    counter++;
                                    this.MarshalToMainThread(
                                        (s) => this.UpdateSensorState(s),
                                        new SensorStateModel
                                        {
                                            RomCode = r.Item1,
                                            TemperatureValue = r.Item2.Temperature,
                                            TemperatureRawCode = r.Item2.TemperatureRawData,
                                            ThermometerResolution = r.Item2.ThermometerActualResolution,
                                            RawData = r.Item2.RawData,
                                            ComputedCrc = r.Item2.ComputedCrc,
                                            IsValidCrc = r.Item2.IsValidCrc,
                                        });
                                    this.DisplayState($"Result: {counter}/{list.Count}");
                                }));
                        }
                        else
                        {
                            var counter = 0;
                            foreach (var romCode in list)
                            {
                                counter++;
                                this.DisplayState($"Performing measure: {counter}/{list.Count}");
                                var r = await busMaster.PerformDS18B20TemperatureMeasurementAsync(romCode);
                                if (r != null)
                                {
                                    results.Add(romCode, r);
                                    this.MarshalToMainThread(
                                        (s) => this.UpdateSensorState(s),
                                        new SensorStateModel
                                        {
                                            RomCode = romCode,
                                            TemperatureValue = r.Temperature,
                                            TemperatureRawCode = r.TemperatureRawData,
                                            ThermometerResolution = r.ThermometerActualResolution,
                                            RawData = r.RawData,
                                            ComputedCrc = r.ComputedCrc,
                                            IsValidCrc = r.IsValidCrc,
                                        });
                                    this.DisplayState($"Result: {counter}/{list.Count}");
                                }
                                else
                                {
                                    this.MarshalToMainThread(
                                        (s) => this.UpdateSensorState(s),
                                        new SensorStateModel
                                        {
                                            RomCode = romCode,
                                            TemperatureValue = null,
                                            TemperatureRawCode = null,
                                            ThermometerResolution = null,
                                        });
                                    this.DisplayState($"Error: {counter}/{list.Count}");
                                }
                            }
                        }

                        this.DisplayState($"Completed ({stopwatch.Elapsed})");
                        result = results;
                    }
                    else
                    {
                        result = null;
                    }
                }
            }
            catch (Exception ex)
            {
                this.DisplayState($"Fatal error: {ex.Message}");
                result = null;
            }
            finally
            {
                await busMaster.CloseAsync();
            }

            this.IsBusy = false;
            if (result != null)
            {
                this.measuresCompleted++;
            }

            this.MarshalToMainThread(() => base.OnPropertyChanged("MeasuresCounter"));
        }

        private int measuresRuns = 0;

        private int measuresCompleted = 0;

        public string MeasuresCounter
        {
            get
            {
                return $"{this.measuresCompleted}/{this.measuresRuns}";
            }
        }

        private IList<SensorStateModel> sensorsState;

        public IList<SensorStateModel> SensorsState
        {
            get
            {
                return this.sensorsState;
            }

            set
            {
                this.sensorsState = value;
                base.OnPropertyChanged("SensorsStateItems");
            }
        }

        public void AddFoundSensor(SensorStateModel state)
        {
            this.SensorsState.Add(state);
            base.OnPropertyChanged("SensorsStateItems");
        }

        public void UpdateSensorState(SensorStateModel state)
        {
            for (var i = 0; i < this.SensorsState.Count; i++)
            {
                if (this.SensorsState[i].RomCode == state.RomCode)
                {
                    this.SensorsState[i] = state;
                    base.OnPropertyChanged("SensorsStateItems");
                    return;
                }
            }
        }

        public IList<SensorStateViewModel> SensorsStateItems
        {
            get
            {
                return (this.SensorsState != null) ?
                    this.SensorsState
                        .Select((state, index) => new SensorStateViewModel(index, state))
                        .ToList() :
                        null;
            }
        }
    }
}