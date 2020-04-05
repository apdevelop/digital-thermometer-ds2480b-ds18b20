using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace DigitalThermometer.Hardware
{
    /// <summary>
    /// MicroLan (1-Wire) master working with SerialPort + DS2480B driver + DS18B20 slave devices
    /// </summary>
    public class OneWireMaster
    {
        private const int ResetBusTimeout = 500; // TODO: configurable timeouts

        private readonly ISerialConnection port;

        private readonly List<byte> receiveBuffer = new List<byte>(); // TODO: threading issues ?

        /// <summary>
        /// Length of 1-Wire devices ROM code, in bytes
        /// </summary>
        private const int ROMCodeLength = 8;

        private byte lastDiscrepancy = 0;

        private bool lastDeviceFlag = false;

        private byte[] romCodeTempBuffer = new byte[21];

        public OneWireMaster(ISerialConnection portConnection)
        {
            this.port = portConnection;
            this.port.OnDataReceived += this.PortDataReceived;
        }

        public OneWireBusResetResponse Open()
        {
            this.rxDataWaitHandle = new AutoResetEvent(false);
            this.port.OpenPort();
            this.Set1WireMode();

            this.ClearReceiveBuffer();
            this.ResetBus();
            if (!this.WaitResponse(1, 1000))
            {
                this.ClearReceiveBuffer();
                return OneWireBusResetResponse.NoBusResetResponse;
            }

            var resetResponse = DS2480B.CheckResetResponse(this.receiveBuffer[0]);
            this.ClearReceiveBuffer();

            return resetResponse;
        }

        public void Close()
        {
            this.rxDataWaitHandle.Close();
            this.port.ClosePort();
        }

        /// <summary>
        /// Transmit data to port without any conversion / escaping
        /// </summary>
        /// <param name="data">Data to transmit to port as-is</param>
        private void TransmitRawData(byte[] data)
        {
            Debug.WriteLine($"> {String.Join(" ", data.Select(b => b.ToString("X2")))}");
            this.port.TransmitData(data);
        }

        private AutoResetEvent rxDataWaitHandle;

        private void PortDataReceived(byte[] data)
        {
            Debug.WriteLine($"< {String.Join(" ", data.Select(b => b.ToString("X2")))}");
            this.receiveBuffer.AddRange(data);
            this.rxDataWaitHandle.Set();
        }

        private bool WaitResponse(int bytesCount, int millisecondsTimeout)
        {
            var t = Stopwatch.StartNew();
            while (t.ElapsedMilliseconds < millisecondsTimeout)
            {
                if (this.receiveBuffer.Count == bytesCount)
                {
                    return true;
                }

                this.rxDataWaitHandle.WaitOne(1);
            }

            return false;
        }

        private void Set1WireMode()
        {
            this.ClearReceiveBuffer();
            this.Set1WireFlexParams();
            Thread.Sleep(500); // TODO: async
            this.ClearReceiveBuffer();
        }

        private void ResetBus()
        {
            this.TransmitRawData(new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            });
        }

        /// <summary>
        /// Transmit data to 1-Wire bus with Switch to Command Mode reserved byte escaping
        /// </summary>
        /// <param name="data">Data to transmit</param>
        private void TransmitDataPacket(IList<byte> data)
        {
            var rawData = DS2480B.EscapeDataPacket(data);
            this.TransmitRawData(rawData);
        }

        private void Set1WireFlexParams()
        {
            // TODO: calibration ? (DS2480B p.3 at bottom)
            this.ClearReceiveBuffer();
            this.TransmitRawData(new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            });

            Thread.Sleep(150); // TODO: async

            this.ClearReceiveBuffer();
            this.TransmitRawData(new[]
            {
                DS2480B.SwitchToCommandMode,
                DS2480B.CommandResetAtFlexSpeed,
            });

            Thread.Sleep(15); // TODO: async

            this.ClearReceiveBuffer();
            this.TransmitRawData(new[]
            {
                DS2480B.PDSRC_1p37Vpus,
                DS2480B.PPD_512us,
                DS2480B.SPUD_524ms,
                DS2480B.W1LT_11us,
                DS2480B.DSO_10us,
                DS2480B.LOAD_3p0mA,
                DS2480B.RBR_9p6kbps,
            });
        }

        private void ClearReceiveBuffer()
        {
            this.receiveBuffer.Clear();
        }

        private void SendSearchCommand()
        {
            this.ClearReceiveBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout);

            // TODO: check result var resetResponse = DS2480B.CheckResetResponse(this.inputbuffer[0]); // TODO: use response

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
                            BitUtility.WriteBit(bpois, (i * 2 + 1), 1);
                        }
                    }
                }

                for (int i = 0; i < 16; i++)
                {
                    buffer[5 + i] = bpois[i];
                }
            }

            this.TransmitRawData(buffer);
            this.WaitResponse(1 + 17, 1000);
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

        #region DS18B20 specific methods

        /// <summary>
        /// Perform temperature conversion on all DS18B20 slave devices on bus
        /// </summary>
        private void PerformDS18B20TemperatureConversion()
        {
            var selectStartAllMeasurePacket = new byte[]
            {
                DS2480B.SwitchToDataMode,
                DS18B20.SKIP_ROM,
                DS18B20.CONVERT_T,
            };

            this.ClearReceiveBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout); // TODO: check result

            this.TransmitDataPacket(selectStartAllMeasurePacket);
            Thread.Sleep(DS18B20.ConversionTime12bit); // TODO: async
        }

        /// <summary>
        /// Perform temperature conversion on specified DS18B20 slave device 
        /// </summary>
        /// <param name="romCode">ROM code of DS18B20</param>
        private void PerformDS18B20TemperatureConversion(UInt64 romCode)
        {
            var dataPacket = new List<byte>();
            dataPacket.Add(DS2480B.SwitchToDataMode);
            dataPacket.Add(DS18B20.MATCH_ROM);
            dataPacket.AddRange(BitConverter.GetBytes(romCode));
            dataPacket.Add(DS18B20.CONVERT_T);

            this.ClearReceiveBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout); // TODO: check result

            this.TransmitDataPacket(dataPacket);
            Thread.Sleep(DS18B20.ConversionTime12bit); // TODO: async
        }

        private static byte[] CreateReadDS18B20ScratchpadRequest(ulong romCode)
        {
            var dataPacket = new List<byte>();

            // 1. Select slave device
            dataPacket.Add(DS2480B.SwitchToDataMode);
            dataPacket.Add(DS18B20.MATCH_ROM);
            dataPacket.AddRange(BitConverter.GetBytes(romCode));

            // 2. Read scratchpad
            dataPacket.Add(DS18B20.READ_SCRATCHPAD);
            for (var i = 0; i < DS18B20.ScratchpadSize; i++)
            {
                dataPacket.Add(0xFF); // Buffer for receiving response (scratchpad contents)
            }

            return dataPacket.ToArray();
        }

        ///<summary>
        /// Read scratchpad data of specified DS18B20 slave device
        ///</summary> 
        ///<param name="romCode">ROM code of DS18B20</param>
        private byte[] ReadDS18B20ScratchpadData(UInt64 romCode)
        {
            var dataPacket = CreateReadDS18B20ScratchpadRequest(romCode);

            this.ClearReceiveBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout); // TODO: check result

            this.TransmitDataPacket(dataPacket);
            this.WaitResponse(dataPacket.Length, 1000); // TODO: check result

            var romCodeBytes = BitConverter.GetBytes(romCode);

            // Check response format
            // [CD] [55] <ROM code> [BE] <Scratchpad>

            if (this.receiveBuffer.Count != (2 + ROMCodeLength + 1 + DS18B20.ScratchpadSize))
            {
                return null;
            }

            if (DS2480B.CheckResetResponse(this.receiveBuffer[0]) != OneWireBusResetResponse.PresencePulse)
            {
                return null;
            }

            if ((this.receiveBuffer[1] != DS18B20.MATCH_ROM) || (this.receiveBuffer[10] != DS18B20.READ_SCRATCHPAD))
            {
                return null;
            }

            // Compare received ROM code with given one
            for (int i = 0; i < ROMCodeLength; i++)
            {
                if (this.receiveBuffer[i + 2] != romCodeBytes[i])
                {
                    return null;
                }
            }

            var result = new byte[DS18B20.ScratchpadSize];
            this.receiveBuffer.CopyTo(2 + ROMCodeLength + 1, result, 0, result.Length);

            return result;
        }

        #endregion

        #region High-level methods

        /// <summary>
        /// Search for devices on bus
        /// </summary>
        /// <returns>List of ROM codes of devices found</returns>
        public IList<UInt64> SearchDevicesOnBus(Action<ulong> deviceFound = null)
        {
            this.lastDiscrepancy = 0;
            this.lastDeviceFlag = false;

            var result = new List<UInt64>();

            var timeoutControl = Stopwatch.StartNew();

            for (; ; )
            {
                this.SendSearchCommand();

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

        public IDictionary<UInt64, DS18B20.Scratchpad> PerformDS18B20TemperatureMeasure(IList<UInt64> romCodes, Action<Tuple<UInt64, DS18B20.Scratchpad>> measurementCompleted = null)
        {
            this.PerformDS18B20TemperatureConversion();

            var result = new Dictionary<UInt64, DS18B20.Scratchpad>(romCodes.Count);

            foreach (var romCode in romCodes)
            {
                var scratchpadData = this.ReadDS18B20ScratchpadData(romCode);
                if (scratchpadData != null)
                {
                    var scratchpad = new DS18B20.Scratchpad(scratchpadData);
                    if (scratchpad.IsValidCrc)
                    {
                        result.Add(romCode, scratchpad);
                        measurementCompleted?.Invoke(new Tuple<ulong, DS18B20.Scratchpad>(romCode, scratchpad));
                    }

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
        public DS18B20.Scratchpad PerformDS18B20TemperatureMeasure(UInt64 romCode)
        {
            this.PerformDS18B20TemperatureConversion(romCode);

            var scratchpadData = this.ReadDS18B20ScratchpadData(romCode);
            if (scratchpadData != null)
            {
                var scratchpad = new DS18B20.Scratchpad(scratchpadData);
                if (scratchpad.IsValidCrc)
                {
                    return scratchpad;
                }

                // TODO: ? check power-on state (85C) as error
                // Errors: NoResponse, BadCrc, InitialTempValue(?), TempOutOfRange
            }

            return null;
        }

        #endregion
    }
}