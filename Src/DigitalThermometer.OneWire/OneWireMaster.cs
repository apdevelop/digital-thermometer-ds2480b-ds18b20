using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        /// <summary>
        /// Merge requests for minimize count of serial connection requests/responses; useful with serial-over-RF link to DS2480B
        /// </summary>
        public bool UseMergedRequests { get; set; } = false;

        /// <summary>
        /// Commands list for setting custom bus configuration parameters
        /// </summary>
        private readonly List<byte> BusConfigurationCommands = new List<byte>();

        private readonly ISerialConnection serialConnection;

        private readonly List<byte> receiveBuffer = new List<byte>(); // TODO: threading issues ?

        private const int BusResetResponseLength = 1;

        #region Search devices private fields

        private byte lastDiscrepancy = 0;

        private bool lastDeviceFlag = false;

        private readonly byte[] romCodeTempBuffer = new byte[21];

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serialConnection">Serial connection (UART connection to DS2480B) which implements ISerialConnection</param>
        /// <param name="config">Flexible Speed custom configuration</param>
        public OneWireMaster(ISerialConnection serialConnection, FlexibleSpeedConfiguration config = null)
        {
            this.serialConnection = serialConnection;
            this.serialConnection.OnDataReceived += this.SerialConnectionDataReceived;

            // Convert configuration to commands sequence
            if (config != null)
            {
                // TODO: other parameters

                if (config.PulldownSlewRateControl.HasValue)
                {
                    this.BusConfigurationCommands.Add((byte)config.PulldownSlewRateControl.Value);
                }

                if (config.Write1LowTime.HasValue)
                {
                    this.BusConfigurationCommands.Add((byte)config.Write1LowTime.Value);
                }

                if (config.DataSampleOffsetAndWrite0RecoveryTime.HasValue)
                {
                    this.BusConfigurationCommands.Add((byte)config.DataSampleOffsetAndWrite0RecoveryTime.Value);
                }
            }
        }

        #region Application-level public methods

        /// <summary>
        /// Opens connection, initializing 1-Wire bus
        /// </summary>
        /// <returns>1-Wire bus reset response</returns>
        public async Task<OneWireBusResetResponse> OpenAsync()
        {
            await this.serialConnection.OpenAsync();

            if (this.UseMergedRequests)
            {
                return await this.SetBusParametersMergedRequestAsync();
            }
            else
            {
                return await this.SetBusParametersAsync();
            }
        }

        /// <summary>
        /// Closes connection
        /// </summary>
        /// <returns></returns>
        public async Task CloseAsync()
        {
            await this.serialConnection.CloseAsync();
        }

        /// <summary>
        /// Performs Read ROM
        /// </summary>
        /// <returns></returns>
        public async Task<UInt64> ReadRomCodeAsync()
        {
            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse}).");
            }

            var request = new List<byte>(ReadScratchpadRequestLength);
            request.Add(DS2480B.SwitchToDataMode);
            request.Add(DS18B20.READ_ROM);
            for (var i = 0; i < Utils.RomCodeLength; i++)
            {
                request.Add(0xFF); // Buffer for receiving response (ROM Code)
            }

            await this.TransmitDataPacketAsync(request);
            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Count - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}].");
            }

            var romCodeBuffer = new byte[Utils.RomCodeLength];
            this.receiveBuffer.CopyTo(2, romCodeBuffer, 0, romCodeBuffer.Length);
            var romCode = BitConverter.ToUInt64(romCodeBuffer, 0);

            return romCode;
        }

        /// <summary>
        /// Search for slave devices on bus
        /// </summary>
        /// <returns>List of ROM codes of devices found</returns>
        public async Task<List<UInt64>> SearchDevicesOnBusAsync(Action<UInt64> deviceFound = null)
        {
            this.lastDiscrepancy = 0;
            this.lastDeviceFlag = false;

            var result = new List<UInt64>();

            var timeoutControl = Stopwatch.StartNew();

            for (; ; )
            {
                await this.SendSearchCommandAsync();

                var romCode = this.DecodeSearchResponse();
                if (romCode.HasValue)
                {
                    // TODO: ? check duplicates
                    result.Add(romCode.Value);
                    deviceFound?.Invoke(romCode.Value);

                    timeoutControl.Restart();

                    if (this.lastDeviceFlag || (this.lastDiscrepancy <= 0))
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
        public async Task<IDictionary<UInt64, DS18B20.Scratchpad>> PerformDS18B20TemperatureMeasurementAsync(
            IList<UInt64> romCodes,
            Action<Tuple<UInt64, DS18B20.Scratchpad>> measurementCompleted = null)
        {
            var result = new Dictionary<UInt64, DS18B20.Scratchpad>(romCodes.Count);

            if (this.UseMergedRequests)
            {
                await this.PerformDS18B20TemperatureConversionMergedRequestAsync();

                var scratchpadDataList = await this.ReadDS18B20ScratchpadDataMergedRequestAsync(romCodes);
                for (var i = 0; i < romCodes.Count; i++)
                {
                    var scratchpad = new DS18B20.Scratchpad(scratchpadDataList[i]);
                    result.Add(romCodes[i], scratchpad);
                    measurementCompleted?.Invoke(new Tuple<UInt64, DS18B20.Scratchpad>(romCodes[i], scratchpad));
                }
            }
            else
            {
                await this.PerformDS18B20TemperatureConversionAsync();

                foreach (var romCode in romCodes)
                {
                    var scratchpadData = await this.ReadDS18B20ScratchpadDataAsync(romCode);
                    var scratchpad = new DS18B20.Scratchpad(scratchpadData);
                    result.Add(romCode, scratchpad);
                    measurementCompleted?.Invoke(new Tuple<UInt64, DS18B20.Scratchpad>(romCode, scratchpad));
                }
            }

            return result;
        }

        /// <summary>
        /// Perform temperature measure on specified DS18B20 slave device
        /// </summary>
        /// <param name="romCode">ROM code of DS18B20.</param>
        /// <returns>Scratchpad contents</returns>
        public async Task<DS18B20.Scratchpad> PerformDS18B20TemperatureMeasurementAsync(UInt64 romCode)
        {
            await this.PerformDS18B20TemperatureConversionAsync(romCode); // TODO: use merged requests version

            byte[] scratchpadData;
            if (this.UseMergedRequests)
            {
                scratchpadData = await this.ReadDS18B20ScratchpadDataMergedRequestAsync(romCode);
            }
            else
            {
                scratchpadData = await this.ReadDS18B20ScratchpadDataAsync(romCode);
            }

            var scratchpad = new DS18B20.Scratchpad(scratchpadData);

            return scratchpad;
        }

        /// <summary>
        /// Perform writing given configuration values to scratchpad.
        /// </summary>
        /// <param name="romCode">ROM code of DS18B20.</param>
        /// <param name="Th">Value of Th register.</param>
        /// <param name="Tl">Value of Tl register.</param>
        /// <param name="resolution">Thermometer resolution.</param>
        /// <param name="saveToEeprom">Save values to EEPROM.</param>
        /// <returns></returns>
        public async Task WriteConfigurationAsync(UInt64 romCode, int Th, int Tl, DS18B20.ThermometerResolution resolution, bool saveToEeprom)
        {
            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse}).");
            }
            
            // TODO: test for request building function
            var request = new List<byte>();
            request.Add(DS2480B.SwitchToDataMode);

            request.Add(DS18B20.MATCH_ROM);
            request.AddRange(BitConverter.GetBytes(romCode));

            request.Add(DS18B20.WRITE_SCRATCHPAD);
            request.Add(unchecked((byte)(sbyte)Th));
            request.Add(unchecked((byte)(sbyte)Tl));
            request.Add(DS18B20.Scratchpad.ConfigurationRegisterFromResolution(resolution));

            await this.TransmitDataPacketAsync(request);
            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Count - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}].");
            }

            if (saveToEeprom)
            {
                await this.CopyScratchpadToEepromAsync(romCode);
            }
        }

        private async Task CopyScratchpadToEepromAsync(UInt64 romCode)
        {
            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse}).");
            }

            var request = new List<byte>();
            request.Add(DS2480B.SwitchToDataMode);

            request.Add(DS18B20.MATCH_ROM);
            request.AddRange(BitConverter.GetBytes(romCode));
        
            request.Add(DS18B20.COPY_SCRATCHPAD);

            await this.TransmitDataPacketAsync(request);
            await Task.Delay(10);

            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Count - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}].");
            }
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
                return OneWireBusResetResponse.NoResponseReceived;
            }
            else
            {
                return DS2480B.GetBusResetResponse(this.receiveBuffer[0]);
            }
        }

        private async Task ReadCurrentParametersValuesAsync()
        {
            // Default parameters values:
            // PDSRC=000 = 15 V/μs  PPD=100 = 512 μs  SPUD=100 = 524 ms 
            // W1LT=000 = 8 μs  DSO/W0RT=000 = 3 μs 
            // LOAD=100 = 3 mA  RBR=000 = 9.6 kbps
            // Read current parameters values, if needed
            await this.TransmitRawDataAsync(new[]
            {
                (byte)DS2480B.ReadParameterCommand.PulldownSlewRateControl,
                (byte)DS2480B.ReadParameterCommand.ProgrammingPulseDuration,
                (byte)DS2480B.ReadParameterCommand.StrongPullupDuration,
                (byte)DS2480B.ReadParameterCommand.Write1LowTime,
                (byte)DS2480B.ReadParameterCommand.DataSampleOffsetAndWrite0RecoveryTime,
                (byte)DS2480B.ReadParameterCommand.LoadSensorThreshold,
                (byte)DS2480B.ReadParameterCommand.RS232BaudRate,
            });

            // TODO: wait response, decode and return result
        }

        /// <summary>
        /// Performs configuration of 1-Wire bus in Flex mode
        /// </summary>
        /// <returns>1-Wire bus reset response</returns>
        private async Task<OneWireBusResetResponse> SetBusParametersAsync()
        {
            // Calibration procedure: will be NoResponse on first run after DS2480B power-on, so just ignoring bus reset response presence / absence
            var calibrationResetResponse = await this.OneWireBusResetAsync();

            var busResetResponse = await this.OneWireBusResetAsync();

            // Custom configuration
            if (this.BusConfigurationCommands.Count > 0)
            {
                await this.TransmitRawDataAsync(this.BusConfigurationCommands.ToArray());
                var response = await this.WaitResponseAsync(BusResetResponseLength + this.BusConfigurationCommands.Count, this.BusResponseTimeout);
                if (response)
                {
                    this.ValidateBusParametersResponse(BusResetResponseLength);
                }
                else
                {
                    throw new IOException($"No proper response on bus configuration commands was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
                }
            }

            return busResetResponse;
        }

        /// <summary>
        /// Performs configuration of 1-Wire bus in Flex mode
        /// </summary>
        /// <returns>1-Wire bus reset response</returns>
        private async Task<OneWireBusResetResponse> SetBusParametersMergedRequestAsync()
        {
            var busResetRequest = new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            };

            // Calibration
            await this.TransmitRawDataAsync(busResetRequest);
            await Task.Delay(this.BusResetTimeout);

            var request = new List<byte>();
            request.AddRange(busResetRequest);
            if (this.BusConfigurationCommands.Count > 0) // Custom configuration
            {
                request.Add(DS2480B.SwitchToCommandMode);
                request.Add(DS2480B.SwitchToCommandMode);
                request.AddRange(this.BusConfigurationCommands);
            }

            this.ClearReceiveBuffer();
            await this.TransmitRawDataAsync(request.ToArray());

            var response = await this.WaitResponseAsync(BusResetResponseLength + this.BusConfigurationCommands.Count, this.BusResponseTimeout);
            if (response)
            {
                var busResetResponse = DS2480B.GetBusResetResponse(this.receiveBuffer[0]);
                if (this.BusConfigurationCommands.Count > 0)
                {
                    var offset = this.receiveBuffer.Count - this.BusConfigurationCommands.Count; // Could be 1 or 2 bus reset responses
                    this.ValidateBusParametersResponse(offset);
                }

                return busResetResponse;
            }
            else
            {
                throw new IOException($"No proper response on bus configuration commands was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }
        }

        private void ValidateBusParametersResponse(int offset)
        {
            for (var i = 0; i < this.BusConfigurationCommands.Count; i++)
            {
                // Table 6. CONFIGURATION COMMAND RESPONSE BYTE
                if (this.receiveBuffer[offset + i] != (this.BusConfigurationCommands[i] & 0b11111110))
                {
                    throw new IOException($"Malformed response on bus configuration commands was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
                }
            }
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
        /// Transmit data to serial connection without any conversion / escaping
        /// </summary>
        /// <param name="data">Array of bytes to transmit to serial connection as-is</param>
        private async Task TransmitRawDataAsync(byte[] data)
        {
            Debug.WriteLine($"TX > {Utils.ByteArrayToHexSpacedString(data)}");
            await this.serialConnection.TransmitDataAsync(data);
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

        private void SerialConnectionDataReceived(byte[] data)
        {
            Debug.WriteLine($"RX < {Utils.ByteArrayToHexSpacedString(data)}");
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

                await Task.Delay(1); // TODO: ! async version of AutoResetEvent: this.rxDataWaitHandle.WaitOne(1);
            }

            return false;
        }

        private async Task<bool> WaitResponseAtLeastAsync(int minBytesCount, int millisecondsTimeout)
        {
            var t = Stopwatch.StartNew();
            while (t.ElapsedMilliseconds < millisecondsTimeout)
            {
                if (this.receiveBuffer.Count >= minBytesCount)
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
            var busResetResponse = await this.OneWireBusResetAsync(); // TODO: check response

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
            await this.WaitResponseAsync(1 + 17, this.BusSearchTimeout); // TODO: check response
        }

        private UInt64? DecodeSearchResponse()
        {
            if (this.receiveBuffer.Count < 18)
            {
                return null; // TODO: ? throw exception ?
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
                return null; // TODO: ? throw exception ?
            }
            else
            {	// successful search
                this.lastDiscrepancy = lastZero;
                // check for last device
                if (this.lastDiscrepancy == 0)
                {
                    this.lastDeviceFlag = true;
                }

                return BitConverter.ToUInt64(romCodeTempBuffer, 0);
            }
        }

        #endregion

        #region DS18B20 specific methods

        /// <summary>
        /// Perform Read Power Supply command on all DS18B20 slave devices on bus
        /// </summary>
        /// <returns>Response of Read Power Supply command</returns>
        public async Task<byte> ReadDS18B20PowerSupplyAsync()
        {
            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse})");
            }

            var request = CreateReadDS18B20PowerSupplyRequest();
            await this.TransmitRawDataAsync(request); // Without escaping
            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Length - 1 - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            // Process Single Bit response
            var singleBitResponse = this.receiveBuffer[this.receiveBuffer.Count - 1];
            var bits27 = singleBitResponse & 0b11111100;
            if (bits27 != (request[request.Length - 1] & 0b11111100))
            {
                throw new IOException($"Invalid Single Bit response: 0x{singleBitResponse:X2}");
            }

            var bits01 = singleBitResponse & 0b00000011;
            switch (bits01)
            {
                case 0b00: return 0;
                case 0b11: return 1;
                default: throw new IOException($"Invalid Single Bit response: 0x{singleBitResponse:X2}");
            }
        }

        /// <summary>
        /// Perform temperature conversion on all DS18B20 slave devices on bus
        /// </summary>
        private async Task PerformDS18B20TemperatureConversionAsync()
        {
            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse})");
            }

            var request = CreatePerformDS18B20TemperatureConversionRequest();
            await this.TransmitDataPacketAsync(request);
            await Task.Delay((int)DS18B20.MaxConversionTime12bit);
            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Length - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            // Check presence of DS18B20 commands
            if ((this.receiveBuffer[1] != DS18B20.SKIP_ROM) || (this.receiveBuffer[2] != DS18B20.CONVERT_T))
            {
                throw new IOException($"Malformed response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }
        }

        /// <summary>
        /// Perform temperature conversion on all DS18B20 slave devices on bus
        /// </summary>
        private async Task PerformDS18B20TemperatureConversionMergedRequestAsync()
        {
            var busResetRequest = new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            };

            var temperatureConversionRequest = CreatePerformDS18B20TemperatureConversionRequest();

            var request = new List<byte>();
            request.AddRange(busResetRequest);
            request.Add(DS2480B.SwitchToCommandMode); // Dummy 'switch to Command Mode' for making a pause after bus reset
            ////request.Add((byte)DS2480B.ReadParameterCommand.RS232BaudRate); // Dummy 'read configuration parameter' command for making a pause after bus reset
            request.AddRange(DS2480B.EscapeDataPacket(temperatureConversionRequest));

            this.ClearReceiveBuffer();
            await this.TransmitRawDataAsync(request.ToArray());
            await Task.Delay((int)DS18B20.MaxConversionTime12bit);

            var offset = 0;
            var response = await this.WaitResponseAsync(offset + BusResetResponseLength + temperatureConversionRequest.Length - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            var busResetResponse = DS2480B.GetBusResetResponse(this.receiveBuffer[offset + 0]);
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse})");
            }

            // Check presence of DS18B20 commands
            if ((this.receiveBuffer[offset + 1] != DS18B20.SKIP_ROM) || (this.receiveBuffer[offset + 2] != DS18B20.CONVERT_T))
            {
                throw new IOException($"Malformed response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }
        }

        /// <summary>
        /// Perform temperature conversion on specified DS18B20 slave device 
        /// </summary>
        /// <param name="romCode">ROM code of DS18B20</param>
        private async Task PerformDS18B20TemperatureConversionAsync(UInt64 romCode)
        {
            ValidateDS18B20RomCode(romCode);

            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse})");
            }

            var request = CreatePerformDS18B20TemperatureConversionRequest(romCode);
            await this.TransmitDataPacketAsync(request);
            await Task.Delay((int)DS18B20.MaxConversionTime12bit);
            var response = await this.WaitResponseAsync(BusResetResponseLength + request.Length - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            // Check presence of DS18B20 commands
            if ((this.receiveBuffer[1] != DS18B20.MATCH_ROM) || (this.receiveBuffer[2 + 8] != DS18B20.CONVERT_T))
            {
                throw new IOException($"Malformed response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }
        }

        ///<summary>
        /// Read scratchpad data of specified DS18B20 slave device
        ///</summary> 
        ///<param name="romCode">ROM code of DS18B20</param>
        ///<returns>Scratchpad contents</returns>
        private async Task<byte[]> ReadDS18B20ScratchpadDataAsync(UInt64 romCode)
        {
            ValidateDS18B20RomCode(romCode);

            var busResetResponse = await this.OneWireBusResetAsync();
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse})");
            }

            var readScratchpadRequest = CreateReadDS18B20ScratchpadRequest(romCode);
            await this.TransmitDataPacketAsync(readScratchpadRequest);
            var response = await this.WaitResponseAsync(BusResetResponseLength + readScratchpadRequest.Length - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            var result = GetDS18B20ScratchpadContentsFromReceiveBuffer(romCode);

            return result;
        }

        ///<summary>
        /// Read scratchpad data of specified DS18B20 slave device
        ///</summary> 
        ///<param name="romCode">ROM code of DS18B20</param>
        ///<returns>Scratchpad contents</returns>
        private async Task<byte[]> ReadDS18B20ScratchpadDataMergedRequestAsync(UInt64 romCode)
        {
            ValidateDS18B20RomCode(romCode);

            var busResetRequest = new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            };

            var readScratchpadRequest = CreateReadDS18B20ScratchpadRequest(romCode);

            var request = new List<byte>();
            request.AddRange(busResetRequest);
            request.Add(DS2480B.SwitchToCommandMode); // Dummy 'switch to Command Mode' for making a pause after bus reset
            ////request.Add((byte)DS2480B.ReadParameterCommand.RS232BaudRate); // Dummy 'read configuration parameter' command for making a pause after bus reset
            request.AddRange(DS2480B.EscapeDataPacket(readScratchpadRequest));

            this.ClearReceiveBuffer();
            await this.TransmitRawDataAsync(request.ToArray());

            var offset = 0;
            var response = await this.WaitResponseAsync(offset + BusResetResponseLength + readScratchpadRequest.Length - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            var result = GetDS18B20ScratchpadContentsFromReceiveBuffer(romCode, offset);

            return result;
        }

        ///<summary>
        /// Read scratchpad data of several DS18B20 slave devices using serial connection single write - wait response cycle
        ///</summary> 
        ///<param name="romCodes">ROM codes of DS18B20</param>
        ///<returns>Scratchpad contents</returns>
        private async Task<List<byte[]>> ReadDS18B20ScratchpadDataMergedRequestAsync(IList<UInt64> romCodes)
        {
            foreach (var romCode in romCodes)
            {
                ValidateDS18B20RomCode(romCode);
            }

            var busResetRequest = new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            };

            var request = new List<byte>();

            foreach (var romCode in romCodes)
            {
                var readScratchpadRequest = CreateReadDS18B20ScratchpadRequest(romCode);
                request.AddRange(busResetRequest);
                request.Add(DS2480B.SwitchToCommandMode); // Dummy 'switch to Command Mode' for making a pause after bus reset
                ////request.Add((byte)DS2480B.ReadParameterCommand.RS232BaudRate); // Dummy 'read configuration parameter' command for making a pause after bus reset
                request.AddRange(DS2480B.EscapeDataPacket(readScratchpadRequest));
            }

            var addLength = 0;
            this.ClearReceiveBuffer();
            await this.TransmitRawDataAsync(request.ToArray());

            var response = await this.WaitResponseAsync(romCodes.Count * (addLength + BusResetResponseLength + ReadScratchpadRequestLength - 1), romCodes.Count * this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            var offset = 0;
            var result = new List<byte[]>();
            foreach (var romCode in romCodes)
            {
                result.Add(GetDS18B20ScratchpadContentsFromReceiveBuffer(romCode, offset + addLength));
                offset += addLength + BusResetResponseLength + ReadScratchpadRequestLength - 1;
            }

            return result;
        }

        private byte[] GetDS18B20ScratchpadContentsFromReceiveBuffer(UInt64 romCode, int offset = 0)
        {
            // Check receive buffer contents:
            // <Reset response code> [55] <ROM code> [BE] <Scratchpad contents>

            var busResetResponse = DS2480B.GetBusResetResponse(this.receiveBuffer[offset + 0]);
            if (busResetResponse != OneWireBusResetResponse.PresencePulse)
            {
                throw new IOException($"No proper bus reset response was received ({busResetResponse})");
            }

            // Check presence of DS18B20 commands
            if ((this.receiveBuffer[offset + 1] != DS18B20.MATCH_ROM) || (this.receiveBuffer[offset + 10] != DS18B20.READ_SCRATCHPAD))
            {
                throw new IOException($"Malformed response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            // Compare ROM code in response with given one
            var romCodeBytes = BitConverter.GetBytes(romCode);
            for (int i = 0; i < Utils.RomCodeLength; i++)
            {
                if (this.receiveBuffer[offset + i + 2] != romCodeBytes[i])
                {
                    throw new IOException($"ROM code mismatch in response: expected [{Utils.ByteArrayToHexSpacedString(romCodeBytes)}], received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer, offset + 2, Utils.RomCodeLength)}]");
                }
            }

            var result = new byte[DS18B20.ScratchpadSize];
            this.receiveBuffer.CopyTo(offset + BusResetResponseLength + 1 + Utils.RomCodeLength + 1, result, 0, result.Length);

            return result;
        }

        #endregion

        #region DS18B20 specific methods requests creating

        private static void ValidateDS18B20RomCode(UInt64 romCode)
        {
            if (!DS18B20.IsValidRomCode(romCode))
            {
                throw new ArgumentException($"Invalid ROM code of DS18B20: {Utils.RomCodeToLEString(romCode)}");
            }
        }

        private static byte[] CreateReadDS18B20PowerSupplyRequest()
        {
            var request = new byte[]
            {
                DS2480B.SwitchToDataMode,
                DS18B20.SKIP_ROM,
                DS18B20.READ_POWER_SUPPLY,
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandSingleBitReadDataAtFlexSpeed,
            };

            return request;
        }

        private static byte[] CreatePerformDS18B20TemperatureConversionRequest()
        {
            var request = new byte[]
            {
                DS2480B.SwitchToDataMode,
                DS18B20.SKIP_ROM,
                DS18B20.CONVERT_T,
            };

            return request;
        }

        private static byte[] CreatePerformDS18B20TemperatureConversionRequest(UInt64 romCode)
        {
            var request = new List<byte>();

            request.Add(DS2480B.SwitchToDataMode);

            request.Add(DS18B20.MATCH_ROM);
            request.AddRange(BitConverter.GetBytes(romCode));
            request.Add(DS18B20.CONVERT_T);

            return request.ToArray();
        }

        private const int ReadScratchpadRequestLength = 20;

        private static byte[] CreateReadDS18B20ScratchpadRequest(UInt64 romCode)
        {
            var request = new List<byte>(ReadScratchpadRequestLength);

            request.Add(DS2480B.SwitchToDataMode);

            // 1. Select slave device by ROM code
            request.Add(DS18B20.MATCH_ROM);
            request.AddRange(BitConverter.GetBytes(romCode));

            // 2. Read scratchpad contents
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