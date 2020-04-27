using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using DigitalThermometer.App.Models;
using DigitalThermometer.App.Utils;

using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.App.ViewModels
{
    public class MainViewModel : BaseNotifyPropertyChanged
    {
        public ICommand RefreshSerialPortsListCommand { get; private set; }

        public ICommand PerformMeasureCommand { get; private set; }

        public ICommand MeasureInDemoModeCommand { get; private set; }

        public MainViewModel()
        {
            this.RefreshSerialPortsListCommand = new RelayCommand((o) => this.UpdateSerialPortNames());
            this.PerformMeasureCommand = new RelayCommand(async (o) => await this.PerformMeasurementsAsync());
            this.MeasureInDemoModeCommand = new RelayCommand(async (o) => await this.PerformMeasurementsInDemoModeAsync());

            // TODO: config and save/restore settings
            this.UpdateSerialPortNames();

            this.measurementsTimer.Tick += async (s, e) =>
            {
                if (this.IsMeasuresEnabled)
                {
                    await this.PerformMeasurementsAsync();
                }
            };
        }

        private List<string> serialPortNames = new List<string>();

        public List<string> SerialPortNames
        {
            get
            {
                return this.serialPortNames;
            }

            set
            {
                this.serialPortNames = value;
                base.OnPropertyChanged(nameof(SerialPortNames));
            }
        }

        private void UpdateSerialPortNames()
        {
            this.SerialPortNames = SerialPortUtils.GetSerialPortNames().ToList();
            if (this.SerialPortNames.Count > 0)
            {
                this.SelectedSerialPortName = this.SerialPortNames[0];
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
                return !this.IsBusy;
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

        private bool? isParasitePower = null;

        private bool? IsParasitePower
        {
            get
            {
                return this.isParasitePower;
            }

            set
            {
                if (this.isParasitePower != value)
                {
                    this.isParasitePower = value;
                    base.OnPropertyChanged(nameof(this.ParasitePowerVisibility));
                    base.OnPropertyChanged(nameof(this.ParasitePowerColor));
                }
            }
        }

        public Visibility ParasitePowerVisibility
        {
            get
            {
                switch (this.IsParasitePower)
                {
                    case null: return Visibility.Hidden;
                    case true: return Visibility.Visible;
                    case false: return Visibility.Hidden;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        public Color ParasitePowerColor
        {
            get
            {
                switch (this.IsParasitePower)
                {
                    case null: return StateOffColor;
                    case true: return Color.FromArgb(0xFF, 0xE3, 0xA2, 0x1A); // TODO: from ResourceDictionary
                    case false: return StateOffColor;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        private bool? IsPowerUpTemperatureValue
        {
            get
            {
                if ((this.SensorsStateItems != null) && (this.SensorsStateItems.Count > 0))
                {
                    return this.SensorsStateItems.Any(s => s.IsPowerUpTemperatureValue == true);
                }
                else
                {
                    return null;
                }
            }
        }

        public Visibility PowerUpTemperatureVisibility
        {
            get
            {
                switch (this.IsPowerUpTemperatureValue)
                {
                    case null: return Visibility.Hidden;
                    case true: return Visibility.Visible;
                    case false: return Visibility.Hidden;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        public Color PowerUpTemperatureColor
        {
            get
            {
                switch (this.IsPowerUpTemperatureValue)
                {
                    case null: return StateOffColor;
                    case true: return Color.FromArgb(0xFF, 0xEE, 0x11, 0x11); // TODO: from ResourceDictionary
                    case false: return StateOffColor;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        public bool? IsCrcError
        {
            get
            {
                if ((this.SensorsStateItems != null) && (this.SensorsStateItems.Count > 0))
                {
                    return this.SensorsStateItems.Any(s => s.IsValidCrc == false);
                }
                else
                {
                    return null;
                }
            }
        }

        public Visibility CrcErrorVisibility
        {
            get
            {
                switch (this.IsCrcError)
                {
                    case null: return Visibility.Hidden;
                    case true: return Visibility.Visible;
                    case false: return Visibility.Hidden;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static Color StateOffColor
        {
            get
            {
                return Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0);
            }
        }

        public Color CrcErrorColor
        {
            get
            {
                switch (this.IsCrcError)
                {
                    case null: return StateOffColor;
                    case true: return Color.FromArgb(0xFF, 0xEE, 0x11, 0x11); // TODO: from ResourceDictionary
                    case false: return StateOffColor;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        private readonly DispatcherTimer measurementsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60), IsEnabled = false, };

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
            this.IsParasitePower = false;

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
            // http://www.claassen.net/geek/blog/2007/07/inotifypropertychanged-and-cross-thread-exceptions.html
            this.MarshalToMainThread(s => this.BusState = s, state);
        }

        private async Task PerformMeasurementsAsync()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();
            this.IsParasitePower = null;

            this.measuresRuns++;
            var stopwatch = Stopwatch.StartNew();

            this.DisplayState(App.Locale["MessageInitializing"]);
            var portConnection = new SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection);

            var result = new Dictionary<UInt64, OW.DS18B20.Scratchpad>();

            try
            {
                this.DisplayState(App.Locale["MessagePerformingBusReset"]);
                var busResult = await busMaster.OpenAsync();
                switch (busResult)
                {
                    case OW.OneWireBusResetResponse.NoResponseReceived:
                        {
                            this.DisplayState(App.Locale["MessageNoResponseReceived"]);
                            result = null;
                            break;
                        }
                    case OW.OneWireBusResetResponse.NoPresencePulse:
                        {
                            this.DisplayState(App.Locale["MessageNoPresencePulse"]);
                            result = null;
                            break;
                        }
                    case OW.OneWireBusResetResponse.BusShorted:
                        {
                            this.DisplayState(App.Locale["MessageBusShorted"]);
                            result = null;
                            break;
                        }
                    case OW.OneWireBusResetResponse.PresencePulse:
                        {
                            this.DisplayState(App.Locale["MessagePresencePulseOk"]);
                            break;
                        }
                }

                if (result != null)
                {
                    var count = 0;
                    this.DisplayState(App.Locale["MessageSearchingDevicesOnBus"]);
                    var list = await busMaster.SearchDevicesOnBusAsync((romCode) =>
                    {
                        count++;
                        this.DisplayState($"{App.Locale["MessageSensorFound"]}: {count}  <{OW.Utils.RomCodeToLEString(romCode)}>");
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

                    if (list != null)
                    {
                        this.DisplayState($"{App.Locale["MessageTotalSensorsFound"]}: {list.Count}");

                        this.IsParasitePower = OW.DS18B20.IsParasitePowerMode(await busMaster.ReadDS18B20PowerSupplyAsync());

                        this.DisplayState($"{App.Locale["MessagePerformingMeasure"]}...");
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
                                    this.DisplayState($"{App.Locale["MessageResult"]}: {counter}/{list.Count}");
                                }));
                        }
                        else
                        {
                            var counter = 0;
                            foreach (var romCode in list)
                            {
                                counter++;
                                this.DisplayState($"{App.Locale["MessagePerformingMeasure"]}: {counter}/{list.Count}  <{OW.Utils.RomCodeToLEString(romCode)}>");
                                try
                                {
                                    var r = await busMaster.PerformDS18B20TemperatureMeasurementAsync(romCode);
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
                                    this.DisplayState($"{App.Locale["MessageResult"]}: {counter}/{list.Count}");
                                }
                                catch (Exception)
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
                                    this.DisplayState($"{App.Locale["MessageError"]}: {counter}/{list.Count}");
                                }
                            }
                        }

                        this.DisplayState($"{App.Locale["MessageCompleted"]} ({stopwatch.Elapsed})");
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
                this.DisplayState($"{App.Locale["MessageFatalError"]}: {ex.Message}");
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
                base.OnPropertyChanged(nameof(this.PowerUpTemperatureVisibility));
                base.OnPropertyChanged(nameof(this.PowerUpTemperatureColor));
                base.OnPropertyChanged(nameof(this.CrcErrorVisibility));
                base.OnPropertyChanged(nameof(this.CrcErrorColor));
           }
        }

        public void AddFoundSensor(SensorStateModel state)
        {
            this.SensorsState.Add(state);
            base.OnPropertyChanged("SensorsStateItems");
            base.OnPropertyChanged(nameof(this.PowerUpTemperatureVisibility));
            base.OnPropertyChanged(nameof(this.PowerUpTemperatureColor));
            base.OnPropertyChanged(nameof(this.CrcErrorVisibility));
            base.OnPropertyChanged(nameof(this.CrcErrorColor));
        }

        public void UpdateSensorState(SensorStateModel state)
        {
            for (var i = 0; i < this.SensorsState.Count; i++)
            {
                if (this.SensorsState[i].RomCode == state.RomCode)
                {
                    this.SensorsState[i] = state;
                    base.OnPropertyChanged("SensorsStateItems");
                    base.OnPropertyChanged(nameof(this.PowerUpTemperatureVisibility));
                    base.OnPropertyChanged(nameof(this.PowerUpTemperatureColor));
                    base.OnPropertyChanged(nameof(this.CrcErrorVisibility));
                    base.OnPropertyChanged(nameof(this.CrcErrorColor));
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