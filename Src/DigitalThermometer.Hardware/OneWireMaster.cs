using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace DigitalThermometer.Hardware
{
    /// <summary>
    /// MicroLan (DS18B20) master based on SerialPort + DS2480B bridge
    /// </summary>
    public class OneWireMaster
    {
        private const int SerialPortBaudRate = 9600;

        private const int ResetBusTimeout = 500;

        private ISerialPortConnection portconn;

        private List<byte> inputbuffer = new List<byte>(); // TODO: ? locking ?

        /// <summary>
        /// Length of ROM code, in bytes
        /// </summary>
        private const int ROMCodeSize = 8;

        private byte lastDiscrepancy = 0;

        private bool lastDeviceFlag = false;

        private byte[] romCodeTempBuffer = new byte[21];

        public OneWireMaster(ISerialPortConnection portConnection)
        {
            this.portconn = portConnection;
            this.portconn.OnDataReceived += this.PortByteReceived;
        }

        public OneWireBusResetResponse Open(string serialPortName)
        {
            this.rxDataWaitHandle = new AutoResetEvent(false);
            this.portconn.OpenPort(serialPortName, SerialPortBaudRate);
            this.Set1WireMode();

            this.ClearBuffer();
            this.ResetBus();
            if (!this.WaitResponse(1, 1000))
            {
                this.ClearBuffer();
                return OneWireBusResetResponse.NoBusResetResponse;
            }

            var resetResponse = DS2480B.CheckResetResponse(this.inputbuffer[0]);
            this.ClearBuffer();

            return resetResponse;
        }

        public void Close()
        {
            this.rxDataWaitHandle.Close();
            this.portconn.ClosePort();
        }

        private void SendData(byte[] data)
        {
            this.portconn.TransmitData(data);
        }

        private void SendData(byte data)
        {
            this.portconn.TransmitData(new[] { data });
        }

        private AutoResetEvent rxDataWaitHandle;

        private void PortByteReceived(byte[] data)
        {
            this.inputbuffer.AddRange(data);
            this.rxDataWaitHandle.Set();
        }

        private bool WaitResponse(int bytesCount, int millisecondsTimeout)
        {
            var t = Stopwatch.StartNew();
            while (t.ElapsedMilliseconds < millisecondsTimeout)
            {
                if (this.inputbuffer.Count == bytesCount)
                {
                    return true;
                }

                this.rxDataWaitHandle.WaitOne(1);
            }

            return false;
        }

        private void Set1WireMode()
        {
            this.ClearBuffer();
            //this.portconn.SetDTR(true);
            //this.portconn.SetRTS(false);
            this.Set1WireFlexParams();
            Thread.Sleep(500);
            this.ClearBuffer();
        }

        private void ResetBus()
        {
            this.SendData(new[] 
            { 
                DS2480B.SwitchToCommandMode, 
                DS2480B.CommandResetAtFlexSpeed, 
            });
        }

        private void SendDataPacket(byte[] buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                // Escape 0xE3 in packet by doubling it
                if (buffer[i] == DS2480B.SwitchToCommandMode)
                {
                    this.SendData(DS2480B.SwitchToCommandMode);
                }

                this.SendData(buffer[i]);
            }
        }

        private void Set1WireFlexParams()
        {
            // TODO: calibration ? (DS2480B p.3 at bottom)
            this.ClearBuffer();
            this.SendData(new[] 
            { 
                DS2480B.SwitchToCommandMode, 
                DS2480B.CommandResetAtFlexSpeed, 
            });

            Thread.Sleep(150);

            this.ClearBuffer();
            this.SendData(new[] 
            { 
                DS2480B.SwitchToCommandMode, 
                DS2480B.CommandResetAtFlexSpeed, 
            });

            Thread.Sleep(15);

            this.ClearBuffer();
            this.SendData(new[]
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

        private void ClearBuffer()
        {
            this.inputbuffer.Clear();
        }

        private void SendSearchCommand()
        {
            this.ClearBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout);

            var resetResponse = DS2480B.CheckResetResponse(this.inputbuffer[0]); // TODO: use response

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
                        BitUtility.WriteBit(bpois, (i * 2 + 1), BitUtility.ReadBit(this.romCodeTempBuffer, i));
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

            this.SendData(buffer);
            this.WaitResponse(1 + 17, 1000);
        }

        private byte[] DecodeSearchResponse()
        {
            if (this.inputbuffer.Count < 18)
            {
                return null;
            }

            byte lastZero = 0;

            for (byte i = 0; i < 64; i++)
            {
                BitUtility.WriteBit(this.romCodeTempBuffer, i, BitUtility.ReadBit(this.inputbuffer, i * 2 + 1 + 8 + 8));

                if ((BitUtility.ReadBit(this.inputbuffer, i * 2 + 8 + 8) == 1) && (BitUtility.ReadBit(this.inputbuffer, i * 2 + 1 + 8 + 8) == 0))
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

                var romCode = new byte[ROMCodeSize];
                Array.Copy(romCodeTempBuffer, romCode, ROMCodeSize);

                return romCode;
            }
        }

        private void SendStartAllMeasureCommand()
        {
            var selectStartAllMeasurePacket = new byte[]
            {
                DS2480B.SwitchToDataMode,
                DS18B20.SKIP_ROM,
                DS18B20.CONVERT_T,
            };

            this.ClearBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout);

            this.SendData(selectStartAllMeasurePacket);
            Thread.Sleep(DS18B20.ConversionTime12bit);

            this.ResetBus();
            this.WaitResponse(1 + (selectStartAllMeasurePacket.Length - 1) + 1, ResetBusTimeout);
            this.ClearBuffer();
        }

        private void SendStartMeasureCommand(byte[] romCode)
        {
            var selectDeviceAndStartMeasurePacket = new byte[]
            {
                DS2480B.SwitchToDataMode, 
                DS18B20.MATCH_ROM, 
                romCode[0], romCode[1], romCode[2], romCode[3], romCode[4], romCode[5], romCode[6], romCode[7], 
                DS18B20.CONVERT_T,
            };

            this.ClearBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout);

            this.SendDataPacket(selectDeviceAndStartMeasurePacket);
            Thread.Sleep(DS18B20.ConversionTime12bit);

            this.ResetBus();
            this.WaitResponse(1 + (selectDeviceAndStartMeasurePacket.Length - 1) + 1, ResetBusTimeout);
            this.ClearBuffer();
        }

        ///<summary>
        /// Send read command for device with specified ROM code
        ///</summary> 
        ///<param name="romCode">ROM code (64 bits = 8 bytes)</param>
        private void SelectDeviceAndReadScratchpad(byte[] romCode)
        {
            if (romCode == null)
            {
                throw new ArgumentNullException(nameof(romCode));
            }

            if (romCode.Length != ROMCodeSize)
            {
                throw new ArgumentException($"ROM code length = {romCode.Length}");
            }

            var selectDevicePacket = new byte[]
            {
                DS2480B.SwitchToDataMode, 
                DS18B20.MATCH_ROM, 
                romCode[0], romCode[1], romCode[2], romCode[3], romCode[4], romCode[5], romCode[6], romCode[7], 
            };

            var readDevicePacket = new byte[]
            {
                DS18B20.READ_SCRATCHPAD, 
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            };

            this.ClearBuffer();
            this.ResetBus();
            this.WaitResponse(1, ResetBusTimeout);

            this.SendDataPacket(selectDevicePacket);
            this.SendData(readDevicePacket);

            this.WaitResponse(1 + (selectDevicePacket.Length - 1) + readDevicePacket.Length, 1000);

            this.ResetBus();
            this.WaitResponse(1 + (selectDevicePacket.Length - 1) + readDevicePacket.Length + 1, ResetBusTimeout);
        }

        private byte[] DecodeReadScratchpadResponse(byte[] romCode)
        {
            // Check response format
            // [CD] [55] <ROM code> [BE] <Scratchpad>

            if (this.inputbuffer.Count < (2 + ROMCodeSize + 1 + DS18B20.ScratchpadSize))
            {
                return null;
            }

            if (DS2480B.CheckResetResponse(this.inputbuffer[0]) != OneWireBusResetResponse.PresencePulse)
            {
                return null;
            }

            if ((this.inputbuffer[1] != DS18B20.MATCH_ROM) || (this.inputbuffer[10] != DS18B20.READ_SCRATCHPAD))
            {
                return null;
            }

            // ROM code received 
            for (int i = 0; i < ROMCodeSize; i++)
            {
                if (this.inputbuffer[i + 2] != romCode[i])
                {
                    return null;
                }
            }

            var result = new byte[DS18B20.ScratchpadSize];
            this.inputbuffer.CopyTo(2 + ROMCodeSize + 1, result, 0, result.Length);

            return result;
        }

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

        public IDictionary<UInt64, double> PerformMeasureOnAll(IList<UInt64> romCodes, Action<Tuple<UInt64, double>> measurementCompleted = null)
        {
            this.SendStartAllMeasureCommand();

            var result = new Dictionary<UInt64, double>(romCodes.Count);

            foreach (var romCode in romCodes)
            {
                var romCodeBytes = BitConverter.GetBytes(romCode);

                this.SelectDeviceAndReadScratchpad(romCodeBytes);

                var buffer = this.DecodeReadScratchpadResponse(romCodeBytes);
                if (buffer != null)
                {
                    if (DS18B20.CheckScratchpad(buffer))
                    {
                        var temperatureCode = DS18B20.GetTemperatureCode(buffer);
                        if (DS18B20.IsValidTemperatureCode(temperatureCode))
                        {
                            var temperature = DS18B20.DecodeTemperature12bit(temperatureCode);

                            result.Add(romCode, temperature); // TODO: callback in case of error
                            measurementCompleted?.Invoke(new Tuple<ulong, double>(romCode, temperature));
                        }
                    }

                    // TODO: return class with full measure status (temperature code, error details)
                    // TODO: ? check power-on state (85C) as error
                    // TODO: ! check response, crc and store results
                    // Errors: NoResponse, BadCrc, InitialTempValue(?), TempOutOfRange
                }
            }

            return result;
        }

        public double? PerformMeasure(ulong romCode)
        {
            var romCodeBytes = BitConverter.GetBytes(romCode);

            this.SendStartMeasureCommand(romCodeBytes);
            this.SelectDeviceAndReadScratchpad(romCodeBytes);

            var buffer = this.DecodeReadScratchpadResponse(romCodeBytes);
            if (buffer != null)
            {
                if (DS18B20.CheckScratchpad(buffer))
                {
                    var temperatureCode = DS18B20.GetTemperatureCode(buffer);
                    if (DS18B20.IsValidTemperatureCode(temperatureCode))
                    {
                        return DS18B20.DecodeTemperature12bit(temperatureCode);
                    }
                }

                // TODO: return full measure status (temperature code, error details)
                // TODO: ? check power-on state (85C) as error
                // Errors: NoResponse, BadCrc, InitialTempValue(?), TempOutOfRange
            }

            return null;
        }

        // TODO: add MeasureOneByOne(romCode[]) method

        #endregion
    }
}