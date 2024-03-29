﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
    public class MainWindowViewModel : BaseNotifyPropertyChanged
    {
        public ICommand RefreshSerialPortsListCommand { get; private set; }

        public ICommand PerformOpenCommand { get; private set; }

        public ICommand PerformReadRomCommand { get; private set; }

        public ICommand PerformSearchCommand { get; private set; }

        public ICommand PerformMeasureCommand { get; private set; }

        public ICommand MeasureInDemoModeCommand { get; private set; }

        public ICommand CopyTableToClipboardCommand { get; private set; }

        public ICommand OpenConfigurationBlockCommand { get; private set; }

        public ICommand WriteConfigurationCommand { get; private set; }

        public ICommand CloseConfigurationBlockCommand { get; private set; }

        public MainWindowViewModel()
        {
            this.RefreshSerialPortsListCommand = new RelayCommand((o) => this.UpdateSerialPortNames());
            this.PerformOpenCommand = new RelayCommand((o) => this.OpenDevicesListFile());
            this.PerformReadRomCommand = new RelayCommand(async (o) => await this.PerformReadRomAsync());
            this.PerformSearchCommand = new RelayCommand(async (o) => await this.PerformSearchAsync());
            this.PerformMeasureCommand = new RelayCommand(async (o) => await this.PerformMeasurementsAsync());
            this.MeasureInDemoModeCommand = new RelayCommand(async (o) => await this.PerformMeasurementsInDemoModeAsync());
            this.CopyTableToClipboardCommand = new RelayCommand((o) => this.CopyTableToClipboard());
            this.OpenConfigurationBlockCommand = new RelayCommand((o) => this.OpenConfigurationBlock(o as SensorStateViewModel));
            this.WriteConfigurationCommand = new RelayCommand(async (o) => await this.WriteConfigurationAsync());
            this.CloseConfigurationBlockCommand = new RelayCommand((o) => this.CloseConfigurationBlock());

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
            get => this.selectedSerialPortName;

            set
            {
                if (this.selectedSerialPortName != value)
                {
                    this.selectedSerialPortName = value;
                    base.OnPropertyChanged(nameof(SelectedSerialPortName));
                    base.OnPropertyChanged(nameof(IsSearchEnabled));
                    base.OnPropertyChanged(nameof(IsMeasuresEnabled));
                }
            }
        }

        private void OpenDevicesListFile()
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var path = openFileDialog.FileName;
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

                base.OnPropertyChanged(nameof(this.IsMeasuresEnabled));
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
                    base.OnPropertyChanged(nameof(this.IsBusy));
                    base.OnPropertyChanged(nameof(this.IsNotBusy));
                    base.OnPropertyChanged(nameof(this.IsSearchEnabled));
                    base.OnPropertyChanged(nameof(this.IsMeasuresEnabled));
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

        public bool IsMeasuresEnabled => !this.IsBusy &&
                    this.SelectedSerialPortName != null &&
                    this.sensorsList.Count > 0;

        private bool isSimultaneousMeasurementsMode = true;

        public bool IsSimultaneousMeasurementsMode
        {
            get => this.isSimultaneousMeasurementsMode;

            set
            {
                if (this.isSimultaneousMeasurementsMode != value)
                {
                    this.isSimultaneousMeasurementsMode = value;
                    base.OnPropertyChanged(nameof(this.IsSimultaneousMeasurementsMode));
                }
            }
        }

        private bool useMergedRequests = false;

        public bool UseMergedRequests
        {
            get => this.useMergedRequests;

            set
            {
                if (this.useMergedRequests != value)
                {
                    this.useMergedRequests = value;
                    base.OnPropertyChanged(nameof(this.UseMergedRequests));
                }
            }
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

        private bool? IsPowerUpTemperatureValue => (this.SensorsStateItems != null) && (this.SensorsStateItems.Count > 0)
                    ? this.SensorsStateItems.Any(s => s.IsPowerUpTemperatureValue == true)
                    : (bool?)null;

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
                return (this.SensorsStateItems != null) && (this.SensorsStateItems.Count > 0)
                    ? this.SensorsStateItems.Any(s => s.IsValidCrc == false)
                    : (bool?)null;
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

        private static Color StateOffColor => Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0);

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
                    base.OnPropertyChanged(nameof(IsTimerMeasurementsMode));
                }
            }
        }

        private async Task PerformMeasurementsInDemoModeAsync()
        {
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();
            this.DisplayState(String.Empty);
            this.IsParasitePower = false;

            var sensors = new List<SensorStateModel>(new[]
            {
                new SensorStateModel { RomCode = 0x01000002F81B3428, TemperatureValue = +10.0, TemperatureRawCode = 0x00A0, HighAlarmTemperature = 75, LowAlarmTemperature = 10, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x1200000078BA0D28, TemperatureValue = +25.0625, TemperatureRawCode = 0x0191, HighAlarmTemperature = 45, LowAlarmTemperature = 20, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x8E00000078CEAB28, TemperatureValue = -10.125, TemperatureRawCode= 0xFF5E, HighAlarmTemperature = 99, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0xEA00000078B0FC28, TemperatureValue = null, TemperatureRawCode = null, HighAlarmTemperature = 75, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
                new SensorStateModel { RomCode = 0x91000000BED06928, TemperatureValue = +85.0, TemperatureRawCode = 0x0550, HighAlarmTemperature = 25, LowAlarmTemperature = 70, ThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit },
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

            await Task.Delay(500);

            this.MarshalToMainThread((items) => this.SensorsState = items, list);

            await Task.Delay(500);

            foreach (var s in sensors)
            {
                await Task.Delay(100);
                this.MarshalToMainThread((state) => this.UpdateSensorState(state), s);
            }

            this.MarshalToMainThread(() => this.IsBusy = false);
        }

        private void CopyTableToClipboard()
        {
            const string Tab = "\t"; // Tab-separated lines

            var tsv = new StringBuilder();
            tsv.AppendLine(String.Join(Tab, new[]
                {
                    "№",
                    App.Locale["DataGridRomCodeHeader"],
                    "T, °C",
                    App.Locale["DataGridCodeHeader"],
                    "TH",
                    "TL",
                    App.Locale["DataGridModeHeader"],
                    "CRC",
                    App.Locale["DataGridRawDataHeader"],
                }
            ));

            foreach (var state in this.SensorsStateItems)
            {
                tsv.AppendLine(String.Join(Tab, new[]
                    {
                        state.IndexNumberString.ToString(),
                        state.RomCodeString,
                        state.TemperatureValueString,
                        state.TemperatureRawCodeString,
                        state.THString,
                        state.TLString,
                        state.ThermometerResolutionString,
                        state.ComputedCrcString,
                        state.RawDataString,
                    }
                ));
            }

            Clipboard.SetText(tsv.ToString());
        }

        private void OpenConfigurationBlock(SensorStateViewModel s)
        {
            this.ConfigurationBlockRomCodeString = s.RomCodeString;
            this.ConfigurationBlockTh = s.Th ?? 75;
            this.ConfigurationBlockTl = s.Tl ?? 70;
            this.ConfigurationBlockThermometerResolution = s.ThermometerResolution ?? OW.DS18B20.ThermometerResolution.Resolution12bit;
            this.IsConfigurationBlockOpened = true;
        }

        private void CloseConfigurationBlock()
        {
            this.IsConfigurationBlockOpened = false;
        }

        private bool isConfigurationBlockOpened = false;

        public bool IsConfigurationBlockOpened
        {
            get => this.isConfigurationBlockOpened;

            set
            {
                if (this.isConfigurationBlockOpened != value)
                {
                    this.isConfigurationBlockOpened = value;
                    base.OnPropertyChanged(nameof(this.IsConfigurationBlockOpened));
                    base.OnPropertyChanged(nameof(this.IsConfigurationBlockEnabled));
                }
            }
        }

        private string configurationBlockRomCodeString = null;

        public string ConfigurationBlockRomCodeString
        {
            get => this.configurationBlockRomCodeString;

            set
            {
                if (this.configurationBlockRomCodeString != value)
                {
                    this.configurationBlockRomCodeString = value;
                    base.OnPropertyChanged(nameof(this.ConfigurationBlockRomCodeString));
                }
            }
        }

        private int configurationBlockTh = 75;

        public int ConfigurationBlockTh
        {
            get => this.configurationBlockTh;

            set
            {
                if (this.configurationBlockTh != value)
                {
                    this.configurationBlockTh = value;
                    base.OnPropertyChanged(nameof(this.ConfigurationBlockTh));
                }
            }
        }

        private int configurationBlockTl = 70;

        public int ConfigurationBlockTl
        {
            get => this.configurationBlockTl;

            set
            {
                if (this.configurationBlockTl != value)
                {
                    this.configurationBlockTl = value;
                    base.OnPropertyChanged(nameof(this.ConfigurationBlockTl));
                }
            }
        }

        private bool configurationBlockSaveToEeprom = false;

        public bool ConfigurationBlockSaveToEeprom
        {
            get => this.configurationBlockSaveToEeprom;

            set
            {
                if (this.configurationBlockSaveToEeprom != value)
                {
                    this.configurationBlockSaveToEeprom = value;
                    base.OnPropertyChanged(nameof(this.ConfigurationBlockSaveToEeprom));
                }
            }
        }

        public bool IsConfigurationBlockEnabled => !this.IsConfigurationBlockOpened;

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

        #region Flexible Speed options

        private OW.DS2480B.PulldownSlewRateControl selectedPulldownSlewRateControl = OW.DS2480B.PulldownSlewRateControl._1p37_Vpus;

        public OW.DS2480B.PulldownSlewRateControl SelectedPulldownSlewRateControl
        {
            get => this.selectedPulldownSlewRateControl;

            set
            {
                if (this.selectedPulldownSlewRateControl != value)
                {
                    this.selectedPulldownSlewRateControl = value;
                    base.OnPropertyChanged(nameof(this.SelectedPulldownSlewRateControl));
                }
            }
        }

        private OW.DS2480B.Write1LowTime selectedWrite1LowTime = OW.DS2480B.Write1LowTime._11us;

        public OW.DS2480B.Write1LowTime SelectedWrite1LowTime
        {
            get => this.selectedWrite1LowTime;

            set
            {
                if (this.selectedWrite1LowTime != value)
                {
                    this.selectedWrite1LowTime = value;
                    base.OnPropertyChanged(nameof(this.SelectedWrite1LowTime));
                }
            }
        }

        private OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime selectedDataSampleOffsetAndWrite0RecoveryTime = OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime._10us;

        public OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime SelectedDataSampleOffsetAndWrite0RecoveryTime
        {
            get
            {
                return this.selectedDataSampleOffsetAndWrite0RecoveryTime;
            }

            set
            {
                if (this.selectedDataSampleOffsetAndWrite0RecoveryTime != value)
                {
                    this.selectedDataSampleOffsetAndWrite0RecoveryTime = value;
                    base.OnPropertyChanged(nameof(this.SelectedDataSampleOffsetAndWrite0RecoveryTime));
                }
            }
        }

        public List<Tuple<OW.DS2480B.PulldownSlewRateControl, string>> PulldownSlewRateControlItems => new List<Tuple<OW.DS2480B.PulldownSlewRateControl, string>>(new[]
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

        public List<Tuple<OW.DS2480B.Write1LowTime, string>> Write1LowTimeItems => new List<Tuple<OW.DS2480B.Write1LowTime, string>>(new[]
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

        public List<Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>> DataSampleOffsetAndWrite0RecoveryTimeItems => new List<Tuple<OW.DS2480B.DataSampleOffsetAndWrite0RecoveryTime, string>>(new[]
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

        private OW.FlexibleSpeedConfiguration FlexibleSpeedConfiguration => new OW.FlexibleSpeedConfiguration
        {
            PulldownSlewRateControl = this.SelectedPulldownSlewRateControl,
            Write1LowTime = this.SelectedWrite1LowTime,
            DataSampleOffsetAndWrite0RecoveryTime = this.SelectedDataSampleOffsetAndWrite0RecoveryTime,
        };

        #endregion

        public List<int> ThTlRegisterValues => Enumerable.Range(SByte.MinValue, 256).ToList();

        public List<Tuple<OW.DS18B20.ThermometerResolution, string>> ThermometerResolutionItems => new List<Tuple<OW.DS18B20.ThermometerResolution, string>>(new[]
        {
            new Tuple<OW.DS18B20.ThermometerResolution, string>(OW.DS18B20.ThermometerResolution.Resolution9bit, "9-bit"),
            new Tuple<OW.DS18B20.ThermometerResolution, string>(OW.DS18B20.ThermometerResolution.Resolution10bit, "10-bit"),
            new Tuple<OW.DS18B20.ThermometerResolution, string>(OW.DS18B20.ThermometerResolution.Resolution11bit, "11-bit"),
            new Tuple<OW.DS18B20.ThermometerResolution, string>(OW.DS18B20.ThermometerResolution.Resolution12bit, "12-bit"),
        });

        private OW.DS18B20.ThermometerResolution configurationBlockThermometerResolution = OW.DS18B20.ThermometerResolution.Resolution12bit;

        public OW.DS18B20.ThermometerResolution ConfigurationBlockThermometerResolution
        {
            get => this.configurationBlockThermometerResolution;

            set
            {
                if (this.configurationBlockThermometerResolution != value)
                {
                    this.configurationBlockThermometerResolution = value;
                    base.OnPropertyChanged(nameof(this.ConfigurationBlockThermometerResolution));
                }
            }
        }

        private async Task PerformReadRomAsync()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;
            this.SensorsState = new List<SensorStateModel>();

            this.measuresRuns++;
            var stopwatch = Stopwatch.StartNew();

            this.DisplayState(App.Locale["MessageInitializing"]);
            var portConnection = new SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection, this.FlexibleSpeedConfiguration)
            {
                UseMergedRequests = this.UseMergedRequests
            };

            try
            {
                this.DisplayState(App.Locale["MessagePerformingBusReset"]);
                var busResult = await busMaster.OpenAsync();
                var busResetResult = this.DisplayBusResult(busResult);
                if (busResetResult)
                {
                    var romCode = await busMaster.ReadRomCodeAsync();
                    this.sensorsList = new List<ulong> { romCode };
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
                this.DisplayState($"{App.Locale["MessageFatalError"]}: {ex.Message}");
            }
            finally
            {
                await busMaster.CloseAsync();
                this.IsBusy = false;
            }
        }
       
        private async Task WriteConfigurationAsync()
        {
            this.BusState = String.Empty;
            this.IsBusy = true;

            this.measuresRuns++;

            this.DisplayState(App.Locale["MessageInitializing"]);
            var portConnection = new SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection, this.FlexibleSpeedConfiguration)
            {
                UseMergedRequests = this.UseMergedRequests
            };

            try
            {
                this.DisplayState(App.Locale["MessagePerformingBusReset"]);
                var busResult = await busMaster.OpenAsync();
                var busResetResult = this.DisplayBusResult(busResult);
                if (busResetResult)
                {
                    await busMaster.WriteConfigurationAsync(
                        OW.Utils.RomCodeFromLEString(this.ConfigurationBlockRomCodeString),
                        this.ConfigurationBlockTh,
                        this.ConfigurationBlockTl,
                        this.ConfigurationBlockThermometerResolution,
                        this.ConfigurationBlockSaveToEeprom);
                    this.DisplayState(App.Locale["ConfigurationSaveCompletedText"]);
                }
            }
            catch (Exception ex)
            {
                this.DisplayState($"{App.Locale["MessageFatalError"]}: {ex.Message}");
            }
            finally
            {
                await busMaster.CloseAsync();
                this.CloseConfigurationBlock();
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
            var portConnection = new SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection, this.FlexibleSpeedConfiguration)
            {
                UseMergedRequests = this.UseMergedRequests
            };

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
            var portConnection = new SerialPortConnection(this.SelectedSerialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection, this.FlexibleSpeedConfiguration)
            {
                UseMergedRequests = this.UseMergedRequests
            };

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
                this.DisplayState($"{App.Locale["MessageFatalError"]}: {ex.Message}");
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

            this.MarshalToMainThread(() => base.OnPropertyChanged(nameof(MeasuresCounter)));
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

        private List<SensorStateModel> sensorsState = new List<SensorStateModel>();

        public List<SensorStateModel> SensorsState
        {
            get => this.sensorsState;

            set
            {
                this.sensorsState = value;
                base.OnPropertyChanged(nameof(this.SensorsStateItems));
                base.OnPropertyChanged(nameof(this.PowerUpTemperatureVisibility));
                base.OnPropertyChanged(nameof(this.PowerUpTemperatureColor));
                base.OnPropertyChanged(nameof(this.CrcErrorVisibility));
                base.OnPropertyChanged(nameof(this.CrcErrorColor));
            }
        }

        public void AddFoundSensor(SensorStateModel state)
        {
            this.SensorsState.Add(state);
            base.OnPropertyChanged(nameof(this.SensorsStateItems));
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
                    base.OnPropertyChanged(nameof(this.SensorsStateItems));
                    base.OnPropertyChanged(nameof(this.PowerUpTemperatureVisibility));
                    base.OnPropertyChanged(nameof(this.PowerUpTemperatureColor));
                    base.OnPropertyChanged(nameof(this.CrcErrorVisibility));
                    base.OnPropertyChanged(nameof(this.CrcErrorColor));
                    return;
                }
            }
        }

        public IList<SensorStateViewModel> SensorsStateItems => this.SensorsState?
                        .Select((state, index) => new SensorStateViewModel(index, state))
                        .ToList();
    }
}
