using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// MicroLan (1-Wire) master working with DS2480B driver + DS18B20 slave devices
    /// </summary>
    public class OneWireMaster
    {
        /// <summary>
        /// Timeout for waiting 1-Wire bus reset response, in milliseconds
        /// </summary>
        public int BusResetTimeout { get; set; } = 500;

        /// <summary>
        /// Timeout for waiting 1-Wire bus data response receiving, in milliseconds
        /// </summary>
        public int BusResponseTimeout { get; set; } = 1000;

        /// <summary>
        /// Timeout for waiting 1-Wire bus devices search response receiving, in milliseconds
        /// </summary>
        public int BusSearchTimeout { get; set; } = 1000;

        // TODO: ? pass 1-Wire config as parameter ?
        private readonly byte[] OneWireBusFlexConfiguration = new[]
        {
            (byte)DS2480B.PulldownSlewRateControl._1p37_Vpus,
            (byte)DS2480B.ProgrammingPulseDuration.__512us,
            (byte)DS2480B.StrongPullupDuration.__524ms,
            (byte)DS2480B.Write1LowTime._11us,
            (byte)DS2480B.DataSampleOffsetAndWrite0RecoveryTime._10us,
            (byte)DS2480B.LoadSensorThreshold._3p0mA,
            (byte)DS2480B.RS232BaudRate.______9p6kbps,
        };

        private readonly ISerialConnection port;

        private readonly List<byte> receiveBuffer = new List<byte>(); // TODO: threading issues ?

        /// <summary>
        /// Length of 1-Wire devices ROM code, in bytes
        /// </summary>
        private const int ROMCodeLength = 8;

        private const int BusResetResponseLength = 1;

        #region Search devices private fields

        private byte lastDiscrepancy = 0;

        private bool lastDeviceFlag = false;

        private readonly byte[] romCodeTempBuffer = new byte[21];

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="portConnection">Serial port connection which implements ISerialConnection</param>
        public OneWireMaster(ISerialConnection portConnection)
        {
            this.port = portConnection;
            this.port.OnDataReceived += this.PortDataReceived;
        }

        #region Application-level public methods

        /// <summary>
        /// Opens connection, initializing 1-Wire bus
        /// </summary>
        /// <returns>1-Wire bus reset response</returns>
        public async Task<OneWireBusResetResponse> OpenAsync()
        {
            await this.port.OpenPortAsync();

            var resetResponse = await this.SetOneWireBusFlexParametersAsync();

            return resetResponse;
        }

        /// <summary>
        /// Closes connection
        /// </summary>
        /// <returns></returns>
        public async Task CloseAsync()
        {
            await this.port.ClosePortAsync();
        }

        /// <summary>
        /// Search for slave devices on bus
        /// </summary>
        /// <returns>List of ROM codes of devices found</returns>
        public async Task<IList<UInt64>> SearchDevicesOnBusAsync(Action<UInt64> deviceFound = null)
        {
            this.lastDiscrepancy = 0;
            this.lastDeviceFlag = false;

            var result = new List<UInt64>();

            var timeoutControl = Stopwatch.StartNew();

            for (; ; )
            {
                await this.SendSearchCommandAsync();

                var romCode = this.DecodeSearchResponse();
                if (romCode != null)
                {
                    // TODO: check duplicates
                    var code = BitConverter.ToUInt64(romCode, 0);
                    result.Add(code);
                    deviceFound?.Invoke(code);

                    timeoutControl.Restart();

                    if ((this.lastDeviceFlag) || (this.lastDiscrepancy <= 0))
                    {
                        return result;
                    }
                }

                if (timeoutControl.ElapsedMilliseconds > 5000)
                {
                    break;
                };
            }

            return result;
        }

        /// <summary>
        /// Perform temperature measure on given DS18B20 slave devices on bus 
        /// </summary>
        /// <param name="romCodes">ROM code of DS18B20</param>
        /// <param name="measurementCompleted">Callback on measurement completed on each DS18B20</param>
        /// <returns></returns>
        public async Task<IDictionary<UInt64, DS18B20.Scratchpad>> PerformDS18B20TemperatureMeasurementAsync(IList<UInt64> romCodes, Action<Tuple<UInt64, DS18B20.Scratchpad>> measurementCompleted = null)
        {
            await this.PerformDS18B20TemperatureConversionAsync();

            var result = new Dictionary<UInt64, DS18B20.Scratchpad>(romCodes.Count);

            foreach (var romCode in romCodes)
            {
                var scratchpadData = await this.ReadDS18B20ScratchpadDataAsync(romCode);
                if (scratchpadData != null)
                {
                    var scratchpad = new DS18B20.Scratchpad(scratchpadData);
                    result.Add(romCode, scratchpad);
                    measurementCompleted?.Invoke(new Tuple<UInt64, DS18B20.Scratchpad>(romCode, scratchpad));

                    // TODO: callback in case of error
                    // TODO: TResult<result?, isError, errorText>
                    // TODO: ? check power-on state (85C) as error
                    // TODO: detailed diagnostics (NoResponse, BadCrc, InitialTempValue(?), TempOutOfRange)
                }
            }

            return result;
        }

        /// <summary>
        /// Perform temperature measure on specified DS18B20 slave device
        /// </summary>
        /// <param name="romCode">ROM code of DS18B20</param>
        /// <returns>Scratchpad contents</returns>
        public async Task<DS18B20.Scratchpad> PerformDS18B20TemperatureMeasurementAsync(UInt64 romCode)
        {
            await this.PerformDS18B20TemperatureConversionAsync(romCode);

            var scratchpadData = await this.ReadDS18B20ScratchpadDataAsync(romCode);
            if (scratchpadData != null)
            {
                return new DS18B20.Scratchpad(scratchpadData);

                // TODO: ? check power-on state (85C) as error
                // Errors: NoResponse, BadCrc, InitialTempValue(?), TempOutOfRange
            }

            return null;
        }

        #endregion

        #region 1-Wire bus management

        /// <summary>
        /// Performs bus reset with waiting for response
        /// </summary>
        /// <returns>Bus reset response</returns>
        private async Task<OneWireBusResetResponse> OneWireBusResetAsync()
        {
            this.ClearReceiveBuffer();

            await this.TransmitRawDataAsync(new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            });

            if (!await this.WaitResponseAsync(BusResetResponseLength, this.BusResetTimeout))
            {
                return OneWireBusResetResponse.NoResponse;
            }
            else
            {
                return DS2480B.GetBusResetResponse(this.receiveBuffer[0]);
            }
        }

        private async Task<OneWireBusResetResponse> SetOneWireBusFlexParametersAsync()
        {
            // Calibration procedure: will be NoResponse on first run after DS2480B power-on
            var calibrationResetResponse = await this.OneWireBusResetAsync();

            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse == OneWireBusResetResponse.PresencePulse)
            {
                await this.TransmitRawDataAsync(this.OneWireBusFlexConfiguration);
                var response = await this.WaitResponseAsync(BusResetResponseLength + this.OneWireBusFlexConfiguration.Length, this.BusResponseTimeout); // TODO: check response
                if (response)
                {
                    for (var i = 0; i < this.OneWireBusFlexConfiguration.Length; i++)
                    {
                        // Table 6. CONFIGURATION COMMAND RESPONSE BYTE
                        if (this.receiveBuffer[i + 1] != (this.OneWireBusFlexConfiguration[i] & 0b11111110))
                        {
                            ; // TODO: error
                        }
                    }
                }
            }

            return busResetResponse;
        }

        #endregion

        #region 1-Wire bus low-level I/O, buffering and responses waiting

        /// <summary>
        /// Clears receiving buffer
        /// </summary>
        private void ClearReceiveBuffer()
        {
            this.receiveBuffer.Clear();
        }

        /// <summary>
        /// Transmit data to port without any conversion / escaping
        /// </summary>
        /// <param name="data">Data to transmit to port as-is</param>
        private async Task TransmitRawDataAsync(byte[] data)
        {
            Debug.WriteLine($"TX > {String.Join(" ", data.Select(b => b.ToString("X2")))}");
            await this.port.TransmitDataAsync(data);
        }

        /// <summary>
        /// Transmit data to 1-Wire bus with Switch to Command Mode reserved byte escaping
        /// </summary>
        /// <param name="data">Data to transmit</param>
        private async Task TransmitDataPacketAsync(IList<byte> data)
        {
            var rawData = DS2480B.EscapeDataPacket(data);
            await this.TransmitRawDataAsync(rawData);
        }

        private void PortDataReceived(byte[] data)
        {
            Debug.WriteLine($"RX < {String.Join(" ", data.Select(b => b.ToString("X2")))}");
            this.receiveBuffer.AddRange(data);
        }

        private async Task<bool> WaitResponseAsync(int bytesCount, int millisecondsTimeout)
        {
            var t = Stopwatch.StartNew();
            while (t.ElapsedMilliseconds < millisecondsTimeout)
            {
                if (this.receiveBuffer.Count == bytesCount)
                {
                    return true;
                }

                await Task.Delay(1); // TODO: async version of AutoResetEvent: this.rxDataWaitHandle.WaitOne(1); ?
            }

            return false;
        }

        #endregion

        #region Search devices on bus private methods

        private async Task SendSearchCommandAsync()
        {
            var resetResponse = await this.OneWireBusResetAsync(); // TODO: check bus reset result

            var buffer = new byte[]
            {
                DS2480B.SwitchToDataMode, DS18B20.SEARCH_ROM,
                DS2480B.SwitchToCommandMode, DS2480B.CommandSearchAcceleratorControlOnAtRegularSpeed,
                DS2480B.SwitchToDataMode,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                DS2480B.SwitchToCommandMode, DS2480B.CommandSearchAcceleratorControlOffAtRegularSpeed,
            };

            if (this.lastDiscrepancy != 0)
            {
                var bpois = new byte[17];
                for (byte i = 0; i < 64; i++)
                {
                    if (i < (this.lastDiscrepancy - 1))
                    {
                        BitUtility.WriteBit(bpois, i * 2 + 1, BitUtility.ReadBit(this.romCodeTempBuffer, i));
                    }
                    else
                    {
                        if (i == (this.lastDiscrepancy - 1))
                        {
                            BitUtility.WriteBit(bpois, i * 2 + 1, 1);
                        }
                    }
                }

                for (int i = 0; i < 16; i++)
                {
                    buffer[5 + i] = bpois[i];
                }
            }

            await this.TransmitRawDataAsync(buffer);
            await this.WaitResponseAsync(1 + 17, this.BusSearchTimeout);
        }

        private byte[] DecodeSearchResponse()
        {
            if (this.receiveBuffer.Count < 18)
            {
                return null;
            }

            byte lastZero = 0;

            for (byte i = 0; i < 64; i++)
            {
                BitUtility.WriteBit(this.romCodeTempBuffer, i, BitUtility.ReadBit(this.receiveBuffer, i * 2 + 1 + 8 + 8));

                if ((BitUtility.ReadBit(this.receiveBuffer, i * 2 + 8 + 8) == 1) && (BitUtility.ReadBit(this.receiveBuffer, i * 2 + 1 + 8 + 8) == 0))
                {
                    lastZero = (byte)(i + 1);
                }
            }

            var crc8 = Crc8Utility.CalculateCrc8(romCodeTempBuffer, 0, 7);

            // check results - valid romcode, ...
            if ((crc8 != 0) || (this.lastDiscrepancy == 63) || (this.romCodeTempBuffer[0] == 0))
            {
                // error during search
                this.lastDiscrepancy = 0;
                this.lastDeviceFlag = false;
                return null;
            }
            else
            {	// successful search
                this.lastDiscrepancy = lastZero;
                // check for last device
                if (this.lastDiscrepancy == 0)
                {
                    this.lastDeviceFlag = true;
                }

                var romCode = new byte[ROMCodeLength];
                Array.Copy(romCodeTempBuffer, romCode, ROMCodeLength);

                return romCode;
            }
        }

        #endregion

        #region DS18B20 specific methods

        /// <summary>
        /// Perform temperature conversion on all DS18B20 slave devices on bus
        /// </summary>
        private async Task PerformDS18B20TemperatureConversionAsync()
        {
            var request = CreatePerformDS18B20TemperatureConversionRequest();
            var busResetResponse = await this.OneWireBusResetAsync(); // TODO: check bus reset result
            await this.TransmitDataPacketAsync(request);
            await Task.Delay(DS18B20.ConversionTime12bit);
            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Length - 1, this.BusResponseTimeout); // TODO: check result
        }

        /// <summary>
        /// Perform temperature conversion on specified DS18B20 slave device 
        /// </summary>
        /// <param name="romCode">ROM code of DS18B20</param>
        private async Task PerformDS18B20TemperatureConversionAsync(UInt64 romCode)
        {
            var request = CreatePerformDS18B20TemperatureConversionRequest(romCode);

            var busResetResponse = await this.OneWireBusResetAsync(); // TODO: check bus reset result
            await this.TransmitDataPacketAsync(request);
            await Task.Delay(DS18B20.ConversionTime12bit);
            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Length - 1, this.BusResponseTimeout); // TODO: check result
        }

        ///<summary>
        /// Read scratchpad data of specified DS18B20 slave device
        ///</summary> 
        ///<param name="romCode">ROM code of DS18B20</param>
        ///<returns>Scratchpad contents</returns>
        private async Task<byte[]> ReadDS18B20ScratchpadDataAsync(UInt64 romCode)
        {
            // TODO: validate romCode

            var request = CreateReadDS18B20ScratchpadRequest(romCode);

            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                return null; // TODO: error details / throw exception
            }

            await this.TransmitDataPacketAsync(request);
            await this.WaitResponseAsync(BusResetResponseLength + request.Length - 1, this.BusResponseTimeout); // TODO: check result

            // Check response format
            // [CD] [55] <ROM code> [BE] <Scratchpad>

            if (this.receiveBuffer.Count != (BusResetResponseLength + request.Length - 1))
            {
                return null; // TODO: error details / throw exception
            }

            if ((this.receiveBuffer[1] != DS18B20.MATCH_ROM) || (this.receiveBuffer[10] != DS18B20.READ_SCRATCHPAD))
            {
                return null; // TODO: error details / throw exception
            }

            // Compare received ROM code with given one
            var romCodeBytes = BitConverter.GetBytes(romCode);
            for (int i = 0; i < ROMCodeLength; i++)
            {
                if (this.receiveBuffer[i + 2] != romCodeBytes[i])
                {
                    return null; // TODO: error details / throw exception
                }
            }

            var result = new byte[DS18B20.ScratchpadSize];
            this.receiveBuffer.CopyTo(BusResetResponseLength + 1 + ROMCodeLength + 1, result, 0, result.Length);

            return result;
        }

        #endregion

        #region DS18B20 specific methods requests creating

        private static byte[] CreatePerformDS18B20TemperatureConversionRequest()
        {
            var request = new byte[]
            {
                DS2480B.SwitchToDataMode,
                DS18B20.SKIP_ROM,
                DS18B20.CONVERT_T,
            };

            return request.ToArray();
        }

        private static byte[] CreatePerformDS18B20TemperatureConversionRequest(UInt64 romCode)
        {
            // TODO: validate romCode

            var request = new List<byte>();

            request.Add(DS2480B.SwitchToDataMode);

            request.Add(DS18B20.MATCH_ROM);
            request.AddRange(BitConverter.GetBytes(romCode));
            request.Add(DS18B20.CONVERT_T);

            return request.ToArray();
        }

        private static byte[] CreateReadDS18B20ScratchpadRequest(UInt64 romCode)
        {
            // TODO: validate romCode

            var request = new List<byte>();

            request.Add(DS2480B.SwitchToDataMode);

            // 1. Select slave device
            request.Add(DS18B20.MATCH_ROM);
            request.AddRange(BitConverter.GetBytes(romCode));

            // 2. Read scratchpad
            request.Add(DS18B20.READ_SCRATCHPAD);
            for (var i = 0; i < DS18B20.ScratchpadSize; i++)
            {
                request.Add(0xFF); // Buffer for receiving response (scratchpad contents)
            }

            return request.ToArray();
        }

        #endregion
    }
}