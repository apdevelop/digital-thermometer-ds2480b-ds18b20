using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

using DigitalThermometer.AvaloniaApp.Models;
using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.AvaloniaApp.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        public ReactiveCommand<Unit, Unit> RefreshSerialPortsListCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> PerformOpenCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> PerformReadRomCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> PerformSearchCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> PerformMeasureCommand { get; private set; }

        public ReactiveCommand<Unit, Unit> MeasureInDemoModeCommand { get; private set; }

        private readonly Window window = null;

        public MainWindowViewModel(Window window)
        {
            this.RefreshSerialPortsListCommand = ReactiveCommand.Create(this.UpdateSerialPortNames);
            this.PerformOpenCommand = ReactiveCommand.CreateFromTask(this.OpenDevicesListFile);
            this.PerformReadRomCommand = ReactiveCommand.CreateFromTask(this.PerformReadRomAsync);
            this.PerformSearchCommand = ReactiveCommand.CreateFromTask(this.PerformSearchAsync);
            this.PerformMeasureCommand = ReactiveCommand.CreateFromTask(this.PerformMeasurementsAsync);
            this.MeasureInDemoModeCommand = ReactiveCommand.CreateFromTask(this.PerformMeasurementsInDemoModeAsync);

            this.selectedPulldownSlewRateControl = this.PulldownSlewRateControlItems[0];
            this.selectedWrite1LowTime = this.Write1LowTimeItems[0];
            this.selectedDataSampleOffsetAndWrite0RecoveryTime = this.DataSampleOffsetAndWrite0RecoveryTimeItems[0];

            this.window = window;

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

        /// <summary>
        /// List of sensors
        /// </summary>
        private List<UInt64> sensorsList = new List<ulong>();

        private List<string> serialPortNames = new List<string>();

        public List<string> SerialPortNames
        {
            get => this.serialPortNames;
            set
            {
                this.RaiseAndSetIfChanged(ref this.serialPortNames, value);
                this.RaisePropertyChanged(nameof(this.IsSelectSerialPortEnabled));
            }
        }

        private void UpdateSerialPortNames()
        {
            this.SerialPortNames = System.IO.Ports.SerialPort.GetPortNames().ToList();
            if (this.SerialPortNames.Count > 0)
            {
                this.SelectedSerialPortName = this.SerialPortNames[0];
            }
        }

        private string selectedSerialPortName = null;

        public string SelectedSerialPortName
        {
            get => this.selectedSerialPortName;
            set
            {
                this.RaiseAndSetIfChanged(ref this.selectedSerialPortName, value);
                this.RaisePropertyChanged(nameof(this.IsSearchEnabled));
                this.RaisePropertyChanged(nameof(this.IsMeasuresEnabled));
            }
        }

        private async Task OpenDevicesListFile()
        {
            var result = await this.window.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { AllowMultiple = false, });
            if (result != null && result.Count > 0)
            {
                var path = result[0].Path.AbsolutePath;
                var text = System.IO.File.ReadAllText(path);

                this.sensorsList = text
                    .Split('\r', '\n')
                    .Select(line => line
                        .Trim()
                        .Replace(" ", String.Empty)
                        .Replace("\t", String.Empty))
                        .Where(line => !String.IsNullOrWhiteSpace(line) && OW.Utils.CheckRomCodeFormat(line))
                        .Select(line => OW.Utils.RomCodeFromLEString(line))
                        .ToList();

                this.SensorsState = new List<SensorStateModel>();
                foreach (var romCode in this.sensorsList) // TODO: reactive property
                {
                    this.MarshalToMainThread(
                        (s) => this.AddFoundSensor(s),
                        new SensorStateModel
                        {
                            RomCode = romCode,
                            TemperatureValue = null,
                            TemperatureRawCode = null,
                            ThermometerResolution = null,
                        });
                }

                this.RaisePropertyChanged(nameof(this.IsMeasuresEnabled));
            }
        }

        private bool isBusy = false;

        public bool IsBusy
        {
            get => this.isBusy;

            private set
            {
                if (this.isBusy != value)
                {
                    this.isBusy = value;
                    this.RaiseAndSetIfChanged(ref this.isBusy, value);
                    this.RaisePropertyChanged(nameof(this.IsNotBusy));
                    this.RaisePropertyChanged(nameof(this.IsSearchEnabled));
                    this.RaisePropertyChanged(nameof(this.IsMeasuresEnabled));
                    this.RaisePropertyChanged(nameof(this.IsSelectSerialPortEnabled));
                }
            }
        }

        public bool IsNotBusy
        {
            get => !this.IsBusy;
        }

        public bool IsSearchEnabled
        {
            get => !this.IsBusy && this.SelectedSerialPortName != null;
        }

        public bool IsMeasuresEnabled
        {
            get
            {
                return !this.IsBusy &&
                    this.SelectedSerialPortName != null &&
                    this.sensorsList.Count > 0;
            }
        }

        public bool IsSelectSerialPortEnabled
        {
            get => !this.IsBusy && this.SerialPortNames.Count > 0;
        }

        private bool isSimultaneousMeasurementsMode = true;

        public bool IsSimultaneousMeasurementsMode
        {
            get => this.isSimultaneousMeasurementsMode;
            set => this.RaiseAndSetIfChanged(ref this.isSimultaneousMeasurementsMode, value);
        }

        private bool useMergedRequests = false;

        public bool UseMergedRequests
        {
            get => this.useMergedRequests;
            set => this.RaiseAndSetIfChanged(ref this.useMergedRequests, value);
        }

        private bool? isParasitePower = null;

        private bool? IsParasitePower
        {
            get => this.isParasitePower;
            set
            {
                if (this.isParasitePower != value)
                {
                    this.isParasitePower = value;
                    this.RaiseAndSetIfChanged(ref this.isParasitePower, value);
                    this.RaisePropertyChanged(nameof(this.ParasitePowerColor));
                }
            }
        }

        public IBrush ParasitePowerColor
        {
            get
            {
                switch (this.IsParasitePower)
                {
                    case null: return new SolidColorBrush(StateOffColor);
                    case false: return new SolidColorBrush(StateOffColor);
                    case true: return new SolidColorBrush(Color.FromArgb(0xFF, 0xE3, 0xA2, 0x1A)); // TODO: from ResourceDictionary
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

        public IBrush PowerUpTemperatureColor
        {
            get
            {
                switch (this.IsPowerUpTemperatureValue)
                {
                    case null: return new SolidColorBrush(StateOffColor);
                    case false: return new SolidColorBrush(StateOffColor);
                    case true: return new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0x11, 0x11)); // TODO: from ResourceDictionary
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

        private static Color StateOffColor
        {
            get
            {
                return Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0);
            }
        }

        public IBrush CrcErrorColor
        {
            get
            {
                switch (this.IsCrcError)
                {
                    case null: return new SolidColorBrush(StateOffColor);
                    case true: return new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0x11, 0x11)); // TODO: from ResourceDictionary
                    case false: return new SolidColorBrush(StateOffColor);
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        private readonly DispatcherTimer measurementsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30), IsEnabled = false, };

        private bool isTimerMeasurementsMode = false;

        public bool IsTimerMeasurementsMode
        {
            get => this.isTimerMeasurementsMode;
            set
            {
                if (this.isTimerMeasurementsMode != value)
                {
                    this.isTimerMeasurementsMode = value;
                    this.measurementsTimer.IsEnabled = value;
                    this.RaiseAndSetIfChanged(ref this.isTimerMeasurementsMode, value);
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
                new SensorStateModel { RomCode = 0x01000002F81B3428, TemperatureValue = +10.0, TemperatureRawCode = 0x00A0, HighAlarmTemperature = 75, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x1200000078BA0D28, TemperatureValue = +25.0625, TemperatureRawCode = 0x0191, HighAlarmTemperature = 75, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x8E00000078CEAB28, TemperatureValue = -10.125, TemperatureRawCode= 0xFF5E, HighAlarmTemperature = 75, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0xEA00000078B0FC28, TemperatureValue = null, TemperatureRawCode = null, HighAlarmTemperature = 75, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x91000000BED06928, TemperatureValue = +85.0, TemperatureRawCode = 0x0550, HighAlarmTemperature = 75, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
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
            ////Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
            ////{
            action();
            ////});
        }

        private void MarshalToMainThread<T>(Action<T> action, T parameter)
        {
            ////Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
            ////{
            action(parameter);
            ////});
        }

        private string busState = String.Empty;

        public string BusState
        {
            get => this.busState;
            set => this.RaiseAndSetIfChanged(ref this.busState, value);
        }

        private void DisplayState(string state)
        {
            this.BusState = state;
        }

        #region Flexible Speed options

        private Tuple<OW.DS2480B.PulldownSlewRateControl, string> selectedPulldownSlewRateControl;

        public Tuple<OW.DS2480B.PulldownSlewRateControl, string> SelectedPulldownSlewRateControl
        {
            get => this.selectedPulldownSlewRateControl;
            set => this.RaiseAndSetIfChanged(ref this.selectedPulldownSlewRateControl, value);
        }

        private Tuple<OW.DS2480B.Write1LowTime, string> selectedWrite1LowTime;

        public Tuple<OW.DS2480B.Write1LowTime, string> SelectedWrite1LowTime
        {
            get => this.selectedWrite1LowTime;
            set => this.RaiseAndSetIfChanged(ref this.selectedWrite1LowTime, value);
        }

        private Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string> selectedDataSampleOffsetAndWrite0RecoveryTime;

        public Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string> SelectedDataSampleOffsetAndWrite0RecoveryTime
        {
            get => this.selectedDataSampleOffsetAndWrite0RecoveryTime;
            set => this.RaiseAndSetIfChanged(ref this.selectedDataSampleOffsetAndWrite0RecoveryTime, value);
        }

        public List<Tuple<OW.DS2480B.PulldownSlewRateControl, string>> PulldownSlewRateControlItems
        {
            get
            {
                return new List<Tuple<OW.DS2480B.PulldownSlewRateControl, string>>(new[]
                {
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._15_Vpus, "15 V/μs"),
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._2p2_Vpus, "2.2 V/μs"),
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._1p65_Vpus, "1.65 V/μs"),
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._1p37_Vpus, "1.37 V/μs"),
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._1p1_Vpus, "1.1 V/μs"),
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._0p83_Vpus, "0.83 V/μs"),
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._0p7_Vpus, "0.7 V/μs"),
                    new Tuple<OW.DS2480B.PulldownSlewRateControl, string>(OW.DS2480B.PulldownSlewRateControl._0p55_Vpus, "0.55 V/μs"),
                });
            }
        }

        public List<Tuple<OW.DS2480B.Write1LowTime, string>> Write1LowTimeItems
        {
            get
            {
                return new List<Tuple<OW.DS2480B.Write1LowTime, string>>(new[]
                {
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._8us, "8 μs"),
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._9us, "9 μs"),
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._10us, "10 μs"),
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._11us, "11 μs"),
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._12us, "12 μs"),
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._13us, "13 μs"),
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._14us, "14 μs"),
                    new Tuple<OW.DS2480B.Write1LowTime, string>(OW.DS2480B.Write1LowTime._15us, "15 μs"),
                });
            }
        }

        public List<Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>> DataSampleOffsetAndWrite0RecoveryTimeItems
        {
            get
            {
                return new List<Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>>(new[]
                {
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._3us, "3 μs"),
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._4us, "4 μs"),
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._5us, "5 μs"),
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._6us, "6 μs"),
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._7us, "7 μs"),
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._8us, "8 μs"),
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._9us, "9 μs"),
                    new Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>(OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._10us, "10 μs"),
                });
            }
        }

        private OW.FlexibleSpeedConfiguration FlexibleSpeedConfiguration
        {
            get
            {
                return new OW.FlexibleSpeedConfiguration
                {
                    PulldownSlewRateControl = this.SelectedPulldownSlewRateControl.Item1,
                    Write1LowTime = this.SelectedWrite1LowTime.Item1,
                    DataSampleOffsetAndWrite0RecoveryTime = this.SelectedDataSampleOffsetAndWrite0RecoveryTime.Item1,
                };
            }
        }

        #endregion

        private async Task PerformReadRomAsync()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();

            this.measuresRuns++;
            var stopwatch = Stopwatch.StartNew();

            this.DisplayState(App.Locale["MessageInitializing"]);
            var portConnection = new Utils.SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection, this.FlexibleSpeedConfiguration);
            busMaster.UseMergedRequests = this.UseMergedRequests;

            try
            {
                this.DisplayState(App.Locale["MessagePerformingBusReset"]);
                var busResult = await busMaster.OpenAsync();
                var busResetResult = this.DisplayBusResult(busResult);
                if (busResetResult)
                {
                    var romCode = await busMaster.ReadRomCodeAsync();
                    this.MarshalToMainThread(
                        (s) => this.AddFoundSensor(s),
                        new SensorStateModel
                        {
                            RomCode = romCode,
                            TemperatureValue = null,
                            TemperatureRawCode = null,
                            ThermometerResolution = null,
                        });
                    this.DisplayState(String.Empty);
                }
            }
            catch (Exception ex)
            {
                this.DisplayState($"{App.Locale["MessageFatalError"]}: {ex.Message.Replace(Environment.NewLine, " ")}");
            }
            finally
            {
                await busMaster.CloseAsync();
                this.IsBusy = false;
            }
        }

        private bool DisplayBusResult(OW.OneWireBusResetResponse busResult)
        {
            switch (busResult)
            {
                case OW.OneWireBusResetResponse.NoResponseReceived:
                    {
                        this.DisplayState(App.Locale["MessageNoResponseReceived"]);
                        return false;
                    }
                case OW.OneWireBusResetResponse.NoPresencePulse:
                    {
                        this.DisplayState(App.Locale["MessageNoPresencePulse"]);
                        return false;
                    }
                case OW.OneWireBusResetResponse.BusShorted:
                    {
                        this.DisplayState(App.Locale["MessageBusShorted"]);
                        return false;
                    }
                case OW.OneWireBusResetResponse.PresencePulse:
                    {
                        this.DisplayState(App.Locale["MessagePresencePulseOk"]);
                        return true;
                    }
                default:
                    {
                        throw new ArgumentException();
                    }
            }
        }

        private async Task PerformSearchAsync()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();
            this.IsParasitePower = null;

            var stopwatch = Stopwatch.StartNew();

            this.DisplayState(App.Locale["MessageInitializing"]);
            var portConnection = new Utils.SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection, this.FlexibleSpeedConfiguration);
            busMaster.UseMergedRequests = this.UseMergedRequests;

            var result = new Dictionary<UInt64, OW.DS18B20.Scratchpad>();

            try
            {
                this.DisplayState(App.Locale["MessagePerformingBusReset"]);
                var busResult = await busMaster.OpenAsync();
                var busResetResult = this.DisplayBusResult(busResult);
                if (busResetResult)
                {
                    var count = 0;
                    this.DisplayState(App.Locale["MessageSearchingDevicesOnBus"]);
                    this.sensorsList = await busMaster.SearchDevicesOnBusAsync((romCode) =>
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

                    this.DisplayState($"{App.Locale["MessageTotalSensorsFound"]}: {this.sensorsList.Count}");
                    this.DisplayState($"{App.Locale["MessageCompleted"]} ({stopwatch.Elapsed})");
                }
            }
            catch (Exception ex)
            {
                this.DisplayState($"{App.Locale["MessageFatalError"]}: {ex.Message}");
            }
            finally
            {
                await busMaster.CloseAsync();
                this.IsBusy = false;
            }
        }

        private async Task PerformMeasurementsAsync()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.IsParasitePower = null;

            this.measuresRuns++;
            var stopwatch = Stopwatch.StartNew();

            this.DisplayState(App.Locale["MessageInitializing"]);
            var portConnection = new Utils.SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection, this.FlexibleSpeedConfiguration);
            busMaster.UseMergedRequests = this.UseMergedRequests;

            var result = new Dictionary<UInt64, OW.DS18B20.Scratchpad>();

            try
            {
                this.DisplayState(App.Locale["MessagePerformingBusReset"]);
                var busResult = await busMaster.OpenAsync();
                var busResetResult = this.DisplayBusResult(busResult);
                if (busResetResult)
                {
                    this.IsParasitePower = OW.DS18B20.IsParasitePowerMode(await busMaster.ReadDS18B20PowerSupplyAsync());

                    this.DisplayState($"{App.Locale["MessagePerformingMeasure"]}...");
                    var results = new Dictionary<ulong, OW.DS18B20.Scratchpad>();
                    if (this.IsSimultaneousMeasurementsMode)
                    {
                        var counter = 0;
                        results = (Dictionary<ulong, OW.DS18B20.Scratchpad>)(await busMaster.PerformDS18B20TemperatureMeasurementAsync(this.sensorsList, (r) =>
                        {
                            counter++;
                            this.MarshalToMainThread(
                                (s) => this.UpdateSensorState(s),
                                new SensorStateModel
                                {
                                    RomCode = r.Item1,
                                    TemperatureValue = r.Item2.Temperature,
                                    TemperatureRawCode = r.Item2.TemperatureRawData,
                                    HighAlarmTemperature = r.Item2.HighAlarmTemperature,
                                    LowAlarmTemperature = r.Item2.LowAlarmTemperature,
                                    ThermometerResolution = r.Item2.ThermometerActualResolution,
                                    RawData = r.Item2.RawData,
                                    ComputedCrc = r.Item2.ComputedCrc,
                                    IsValidCrc = r.Item2.IsValidCrc,
                                });
                            this.DisplayState($"{App.Locale["MessageResult"]}: {counter}/{this.sensorsList.Count}");
                        }));
                    }
                    else
                    {
                        var counter = 0;
                        foreach (var romCode in this.sensorsList)
                        {
                            counter++;
                            this.DisplayState($"{App.Locale["MessagePerformingMeasure"]}: {counter}/{this.sensorsList.Count}  <{OW.Utils.RomCodeToLEString(romCode)}>");
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
                                        HighAlarmTemperature = r.HighAlarmTemperature,
                                        LowAlarmTemperature = r.LowAlarmTemperature,
                                        ThermometerResolution = r.ThermometerActualResolution,
                                        RawData = r.RawData,
                                        ComputedCrc = r.ComputedCrc,
                                        IsValidCrc = r.IsValidCrc,
                                    });
                                this.DisplayState($"{App.Locale["MessageResult"]}: {counter}/{this.sensorsList.Count}");
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
                                this.DisplayState($"{App.Locale["MessageError"]}: {counter}/{this.sensorsList.Count}");
                            }
                        }
                    }

                    this.DisplayState($"{App.Locale["MessageCompleted"]} ({stopwatch.Elapsed})");
                    result = results;
                }
            }
            catch (Exception ex)
            {
                this.DisplayState($"{App.Locale["MessageFatalError"]}: {ex.Message.Replace(Environment.NewLine, " ")}");
                result = null;
            }
            finally
            {
                await busMaster.CloseAsync();
                this.IsBusy = false;
            }

            if (result != null)
            {
                this.measuresCompleted++;
            }

            this.MarshalToMainThread(() => this.RaisePropertyChanged(nameof(this.MeasuresCounter)));
        }

        private int measuresRuns = 0;

        private int measuresCompleted = 0;

        public string MeasuresCounter => $"{this.measuresCompleted}/{this.measuresRuns}";

        private IList<SensorStateModel> sensorsState;

        public IList<SensorStateModel> SensorsState
        {
            get => this.sensorsState;
            set
            {
                this.sensorsState = value;
                this.RaiseAndSetIfChanged(ref this.sensorsState, value);
                this.RaisePropertyChanged(nameof(this.SensorsStateItems));
                this.RaisePropertyChanged(nameof(this.PowerUpTemperatureColor));
                this.RaisePropertyChanged(nameof(this.CrcErrorColor));
            }
        }

        public void AddFoundSensor(SensorStateModel state)
        {
            this.SensorsState.Add(state);
            this.RaisePropertyChanged(nameof(this.SensorsStateItems));
            this.RaisePropertyChanged(nameof(this.PowerUpTemperatureColor));
            this.RaisePropertyChanged(nameof(this.CrcErrorColor));
        }

        public void UpdateSensorState(SensorStateModel state)
        {
            for (var i = 0; i < this.SensorsState.Count; i++)
            {
                if (this.SensorsState[i].RomCode == state.RomCode)
                {
                    this.SensorsState[i] = state;
                    this.RaisePropertyChanged(nameof(this.SensorsStateItems));
                    this.RaisePropertyChanged(nameof(this.PowerUpTemperatureColor));
                    this.RaisePropertyChanged(nameof(this.CrcErrorColor));
                    return;
                }
            }
        }

        public IList<SensorStateViewModel> SensorsStateItems =>
            this.SensorsState != null
                    ? this.SensorsState.Select((state, index) => new SensorStateViewModel(index, state, this.window)).ToList()
                    : null;
    }
}
