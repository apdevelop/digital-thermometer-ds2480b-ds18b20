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
                return DigitalThermometer.Hardware.SerialPortUtils.GetSerialPortNames();
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
                    base.OnPropertyChanged("SelectedSerialPortName");
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
                    base.OnPropertyChanged("IsSimultaneousMeasurementsMode");
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
                    base.OnPropertyChanged("IsTimerMeasurementsMode");
                }
            }
        }

        private void PerformMeasuresInDemoMode()
        {
            this.IsBusy = true;
            this.SensorsState = null;

            Task.Factory.StartNew(() =>
              {
                  // TODO: implement it in separate application
                  var sensors = new List<SensorStateModel>(new[] 
                        {
                            new SensorStateModel { RomCode = 0x01000002F81B3428, TemperatureValue = +10.0, },
                            new SensorStateModel { RomCode = 0x1200000078BA0D28, TemperatureValue = +15.875, },
                            new SensorStateModel { RomCode = 0x8E00000078CEAB28, TemperatureValue = -25.5, },
                            new SensorStateModel { RomCode = 0xEA00000078B0FC28, TemperatureValue = null, },
                            new SensorStateModel { RomCode = 0x91000000BED06928, TemperatureValue = +85.0, },
                        });

                  var list = sensors.Select(s => new SensorStateModel { RomCode = s.RomCode, TemperatureValue = null, }).ToList();

                  System.Threading.Thread.Sleep(1000);

                  this.MarshalToMainThread<List<SensorStateModel>>((items) => this.SensorsState = items, list);

                  System.Threading.Thread.Sleep(1000);

                  foreach (var s in sensors)
                  {
                      System.Threading.Thread.Sleep(200);
                      this.MarshalToMainThread<SensorStateModel>((state) => this.UpdateSensorState(state), s);
                  }

                  this.MarshalToMainThread(() => this.IsBusy = false);
              });
        }

        private void MarshalToMainThread(Action action)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)delegate()
            {
                action();
            });
        }

        private void MarshalToMainThread<T>(Action<T> action, T parameter)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)delegate()
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
                    base.OnPropertyChanged("BusState");
                }
            }
        }

        private void DisplayState(string state)
        {
            this.MarshalToMainThread<string>(s => this.BusState = s, state);
        }

        private void PerformMeasures()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();

            // TODO: ? use .NET 4.5 with async/await

            var task = Task<Dictionary<UInt64, double>>.Factory.StartNew(() =>
                {
                    this.measuresRuns++;
                    var stopwatch = Stopwatch.StartNew();

                    var portConnection = new Hardware.SerialPortConnection();
                    var busMaster = new Hardware.OneWireMaster(portConnection);

                    try
                    {
                        var busResult = busMaster.Open(this.SelectedSerialPortName);

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
                        var list = busMaster.SearchDevicesOnBus((romCode) =>
                        {
                            count++;
                            this.MarshalToMainThread<SensorStateModel>(
                                (s) => this.AddFoundedSensor(s),
                                new SensorStateModel { RomCode = romCode, TemperatureValue = null, });
                            this.DisplayState(String.Format(CultureInfo.CurrentCulture, "Sensors found: {0}", count));
                        });

                        // TODO: order list of sensors

                        if (list != null)
                        {
                            // http://www.claassen.net/geek/blog/2007/07/inotifypropertychanged-and-cross-thread-exceptions.html

                            this.DisplayState(String.Format(CultureInfo.CurrentCulture, "Sensors found: {0}", list.Count));
                            this.DisplayState("Performing measure...");

                            var results = new Dictionary<ulong, double>();
                            if (this.IsSimultaneousMeasurementsMode)
                            {
                                var counter = 0;
                                results = (Dictionary<ulong, double>)busMaster.PerformMeasureOnAll(list, (v) =>
                                    {
                                        counter++;
                                        this.MarshalToMainThread<SensorStateModel>(
                                            (s) => this.UpdateSensorState(s),
                                            new SensorStateModel { RomCode = v.Item1, TemperatureValue = v.Item2 });
                                        this.DisplayState(String.Format(CultureInfo.CurrentCulture, "Result: {0}/{1}", counter, list.Count));
                                    });
                            }
                            else
                            {
                                var counter = 0;
                                foreach (var romCode in list)
                                {
                                    counter++;
                                    this.DisplayState(String.Format(CultureInfo.CurrentCulture, "Performing measure: {0}/{1}", counter, list.Count));
                                    var t = busMaster.PerformMeasure(romCode);
                                    if (t.HasValue)
                                    {
                                        results.Add(romCode, t.Value);
                                        this.MarshalToMainThread<SensorStateModel>(
                                            (s) => this.UpdateSensorState(s),
                                            new SensorStateModel { RomCode = romCode, TemperatureValue = t.Value });
                                        this.DisplayState(String.Format(CultureInfo.CurrentCulture, "Result: {0}/{1}", counter, list.Count));
                                    }
                                    else
                                    {
                                        this.MarshalToMainThread<SensorStateModel>(
                                            (s) => this.UpdateSensorState(s),
                                            new SensorStateModel { RomCode = romCode, TemperatureValue = null });
                                        this.DisplayState(String.Format(CultureInfo.CurrentCulture, "Error: {0}/{1}", counter, list.Count));
                                    }
                                }
                            }

                            this.DisplayState(String.Format(CultureInfo.CurrentCulture, "Completed ({0:0.0} s)", stopwatch.Elapsed.TotalSeconds));
                            return results;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.DisplayState(String.Format("Fatal error: {0}", ex.Message));
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
                return String.Format(CultureInfo.CurrentCulture, "{0}/{1}", this.measuresCompleted, this.measuresRuns);
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

        public void AddFoundedSensor(SensorStateModel state)
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
