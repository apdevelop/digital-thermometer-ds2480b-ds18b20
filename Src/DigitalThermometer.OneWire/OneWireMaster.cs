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
        /// Default settings for Flexible Speed
        /// </summary>
        private readonly byte[] OneWireBusFlexibleConfiguration = new[]
        {
            (byte)DS2480B.PulldownSlewRateControl._1p37_Vpus,
            (byte)DS2480B.ProgrammingPulseDuration._512us,
            (byte)DS2480B.StrongPullupDuration._524ms,
            (byte)DS2480B.Write1LowTime._11us,
            (byte)DS2480B.DataSampleOffsetAndWrite0RecoveryTime._10us,
            (byte)DS2480B.LoadSensorThreshold._3p0mA,
            (byte)DS2480B.RS232BaudRate._9p6kbps,
        };

        private readonly ISerialConnection port;

        private readonly List<byte> receiveBuffer = new List<byte>(); // TODO: threading issues ?

        /// <summary>
        /// Length of 1-Wire devices ROM code, in bytes
        /// </summary>
        public const int RomCodeLength = 8;

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
        /// <param name="config">Flexible Speed custom configuration</param>
        public OneWireMaster(ISerialConnection portConnection, FlexibleSpeedConfiguration config = null)
        {
            this.port = portConnection;
            this.port.OnDataReceived += this.PortDataReceived;

            if (config != null)
            {
                this.OneWireBusFlexibleConfiguration = new[]
                {
                    (byte)config.PulldownSlewRateControl,
                    // TODO: other params
                    (byte)DS2480B.ProgrammingPulseDuration._512us,
                    (byte)DS2480B.StrongPullupDuration._524ms,
                    (byte)config.Write1LowTime,
                    (byte)config.DataSampleOffsetAndWrite0RecoveryTime,
                    (byte)DS2480B.LoadSensorThreshold._3p0mA,
                    (byte)DS2480B.RS232BaudRate._9p6kbps,
                };
            }
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
                if (romCode.HasValue)
                {
                    // TODO: ? check duplicates
                    result.Add(romCode.Value);
                    deviceFound?.Invoke(romCode.Value);

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
                var scratchpad = new DS18B20.Scratchpad(scratchpadData);
                result.Add(romCode, scratchpad);
                measurementCompleted?.Invoke(new Tuple<UInt64, DS18B20.Scratchpad>(romCode, scratchpad));
            }

            // Experimental
            ////var scratchpadDataList = await ReadDS18B20ScratchpadDataMergedRequestAsync(romCodes);
            ////for (var i = 0; i < romCodes.Count; i++)
            ////{
            ////    var scratchpad = new DS18B20.Scratchpad(scratchpadDataList[i]);
            ////    result.Add(romCodes[i], scratchpad);
            ////    measurementCompleted?.Invoke(new Tuple<UInt64, DS18B20.Scratchpad>(romCodes[i], scratchpad));
            ////}

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
            var scratchpad = new DS18B20.Scratchpad(scratchpadData);

            return scratchpad;
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

        /// <summary>
        /// Performs configuration of 1-Wire bus in Flex mode
        /// </summary>
        /// <returns>1-Wire bus reset response</returns>
        private async Task<OneWireBusResetResponse> SetOneWireBusFlexParametersAsync()
        {
            // Calibration procedure: will be NoResponse on first run after DS2480B power-on, just ignoring result
            var calibrationResetResponse = await this.OneWireBusResetAsync();

            var busResetResponse = await this.OneWireBusResetAsync();
            if ((busResetResponse == OneWireBusResetResponse.PresencePulse) || (busResetResponse == OneWireBusResetResponse.NoPresencePulse))
            {
                await this.TransmitRawDataAsync(this.OneWireBusFlexibleConfiguration);
                var response = await this.WaitResponseAsync(BusResetResponseLength + this.OneWireBusFlexibleConfiguration.Length, this.BusResponseTimeout);
                if (response)
                {
                    for (var i = 0; i < this.OneWireBusFlexibleConfiguration.Length; i++)
                    {
                        // Table 6. CONFIGURATION COMMAND RESPONSE BYTE
                        if (this.receiveBuffer[i + 1] != (this.OneWireBusFlexibleConfiguration[i] & 0b11111110))
                        {
                            throw new IOException($"Malformed response on bus configuration commands was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
                        }
                    }
                }
                else
                {
                    throw new IOException($"No proper response on bus configuration commands was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
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
            Debug.WriteLine($"TX > {Utils.ByteArrayToHexSpacedString(data)}");
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
            ////request.Add((byte)DS2480B.LoadSensorThreshold._3p0mA); // Dummy configuration command for making a pause after bus reset
            request.AddRange(DS2480B.EscapeDataPacket(readScratchpadRequest));

            this.ClearReceiveBuffer();
            await this.TransmitRawDataAsync(request.ToArray());

            var response = await this.WaitResponseAsync(BusResetResponseLength + readScratchpadRequest.Length - 1, this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            var result = GetDS18B20ScratchpadContentsFromReceiveBuffer(romCode);

            return result;
        }

        ///<summary>
        /// Read scratchpad data of several DS18B20 slave devices using serial port single write - wait response cycle
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
                ////request.Add((byte)DS2480B.LoadSensorThreshold._3p0mA); // Dummy configuration command for making a pause after bus reset
                request.AddRange(DS2480B.EscapeDataPacket(readScratchpadRequest));
            }

            this.ClearReceiveBuffer();
            await this.TransmitRawDataAsync(request.ToArray());

            var response = await this.WaitResponseAsync(romCodes.Count * (BusResetResponseLength + ReadScratchpadRequestLength - 1), romCodes.Count * this.BusResponseTimeout);
            if (!response)
            {
                throw new IOException($"No proper response was received [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
            }

            var offset = 0;
            var result = new List<byte[]>();
            foreach (var romCode in romCodes)
            {
                result.Add(GetDS18B20ScratchpadContentsFromReceiveBuffer(romCode, offset));
                offset += BusResetResponseLength + ReadScratchpadRequestLength - 1;
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

            // Compare received ROM code with given one
            var romCodeBytes = BitConverter.GetBytes(romCode);
            for (int i = 0; i < RomCodeLength; i++)
            {
                if (this.receiveBuffer[offset + i + 2] != romCodeBytes[i])
                {
                    throw new IOException($"ROM code mismatch in response [{Utils.ByteArrayToHexSpacedString(this.receiveBuffer)}]");
                }
            }

            var result = new byte[DS18B20.ScratchpadSize];
            this.receiveBuffer.CopyTo(offset + BusResetResponseLength + 1 + RomCodeLength + 1, result, 0, result.Length);

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