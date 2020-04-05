using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using DigitalThermometer.App.Models;
using DigitalThermometer.App.Utils;

namespace DigitalThermometer.App.ViewModels
{
    public class MainViewModel : BaseNotifyPropertyChanged
    {
        public ICommand PerformMeasureCommand { get; private set; }

        public ICommand MeasureInDemoModeCommand { get; private set; }

        public MainViewModel()
        {
            this.PerformMeasureCommand = new RelayCommand((o) => this.PerformMeasures());
            this.MeasureInDemoModeCommand = new RelayCommand((o) => this.PerformMeasuresInDemoMode());

            // TODO: config and save/restore settings
            if (this.SerialPortNames.Count > 0)
            {
                this.SelectedSerialPortName = this.SerialPortNames[0];
            }

            this.measurementsTimer.Tick += this.MeasurementsTimerTick;
        }

        void MeasurementsTimerTick(object sender, EventArgs e)
        {
            if (this.IsMeasuresEnabled)
            {
                this.PerformMeasures();
            }
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

        private DispatcherTimer measurementsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15), IsEnabled = false, };

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

        private void PerformMeasuresInDemoMode()
        {
            this.IsBusy = true;
            this.SensorsState = null;

            Task.Factory.StartNew(() =>
              {
                  var sensors = new List<SensorStateModel>(new[]
                    {
                        new SensorStateModel { RomCode = 0x01000002F81B3428, TemperatureValue = +10.0, TemperatureRawCode = 0x00A0, ThermometerResolution = Hardware.DS18B20.ThermometerResolution.Resolution12bit },
                        new SensorStateModel { RomCode = 0x1200000078BA0D28, TemperatureValue = +25.0625, TemperatureRawCode = 0x0191, ThermometerResolution = Hardware.DS18B20.ThermometerResolution.Resolution12bit },
                        new SensorStateModel { RomCode = 0x8E00000078CEAB28, TemperatureValue = -10.125, TemperatureRawCode= 0xFF5E, ThermometerResolution = Hardware.DS18B20.ThermometerResolution.Resolution12bit },
                        new SensorStateModel { RomCode = 0xEA00000078B0FC28, TemperatureValue = null, TemperatureRawCode = null, ThermometerResolution = Hardware.DS18B20.ThermometerResolution.Resolution12bit },
                        new SensorStateModel { RomCode = 0x91000000BED06928, TemperatureValue = +85.0, TemperatureRawCode = 0x0550, ThermometerResolution = Hardware.DS18B20.ThermometerResolution.Resolution12bit },
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

                  System.Threading.Thread.Sleep(1000); // TODO: async

                  this.MarshalToMainThread((items) => this.SensorsState = items, list);

                  System.Threading.Thread.Sleep(1000); // TODO: async

                  foreach (var s in sensors)
                  {
                      System.Threading.Thread.Sleep(200); // TODO: async
                      this.MarshalToMainThread((state) => this.UpdateSensorState(state), s);
                  }

                  this.MarshalToMainThread(() => this.IsBusy = false);
              });
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

        private void PerformMeasures()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();

            // TODO: ? use .NET 4.5 with async/await

            var task = Task<Dictionary<UInt64, Hardware.DS18B20.Scratchpad>>.Factory.StartNew(() =>
                {
                    this.measuresRuns++;
                    var stopwatch = Stopwatch.StartNew();

                    var portConnection = new SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
                    var busMaster = new Hardware.OneWireMaster(portConnection);

                    try
                    {
                        var busResult = busMaster.Open();

                        // Bus diagnostic
                        switch (busResult)
                        {
                            case Hardware.OneWireBusResetResponse.NoBusResetResponse:
                                {
                                    this.DisplayState("Bus reset response was not received");
                                    return null;
                                }
                            case Hardware.OneWireBusResetResponse.NoPresencePulse:
                                {
                                    this.DisplayState("No presence pulse");
                                    return null;
                                }
                            case Hardware.OneWireBusResetResponse.OneWireShorted:
                                {
                                    this.DisplayState("Bus is shorted");
                                    return null;
                                }
                            case Hardware.OneWireBusResetResponse.PresencePulse:
                                {
                                    this.DisplayState("Presence pulse OK");
                                    break;
                                }
                            case Hardware.OneWireBusResetResponse.InvalidResponse:
                                {
                                    this.DisplayState("Invalid response received");
                                    return null;
                                }
                        }

                        var count = 0;
                        this.DisplayState("Searching devices on bus...");
                        var list = busMaster.SearchDevicesOnBus((romCode) =>
                        {
                            count++;
                            this.DisplayState($"Sensor found: {count} <{Hardware.DS18B20.RomCodeToLEString(romCode)}>");
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

                            var results = new Dictionary<ulong, Hardware.DS18B20.Scratchpad>();
                            if (this.IsSimultaneousMeasurementsMode)
                            {
                                var counter = 0;
                                results = (Dictionary<ulong, Hardware.DS18B20.Scratchpad>)busMaster.PerformDS18B20TemperatureMeasure(list, (r) =>
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
                                            });
                                        this.DisplayState($"Result: {counter}/{list.Count}");
                                    });
                            }
                            else
                            {
                                var counter = 0;
                                foreach (var romCode in list)
                                {
                                    counter++;
                                    this.DisplayState($"Performing measure: {counter}/{list.Count}");
                                    var r = busMaster.PerformDS18B20TemperatureMeasure(romCode);
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
                            return results;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.DisplayState($"Fatal error: {ex.Message}");
                        return null;
                    }
                    finally
                    {
                        busMaster.Close();
                    }
                })
                .ContinueWith((t) =>
                {
                    this.IsBusy = false;
                    if (t.Result != null)
                    {
                        this.measuresCompleted++;
                    }

                    this.MarshalToMainThread(() => base.OnPropertyChanged("MeasuresCounter"));
                }, TaskScheduler.FromCurrentSynchronizationContext());
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