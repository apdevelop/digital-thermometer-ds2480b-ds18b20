using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalThermometer.Hardware;

namespace DigitalThermometer.UnitTests
{
    /// <summary>
    /// Simple software emulator of DS2480B + DS18B20
    /// </summary>
    sealed class ThermoStringEmulator : ISerialConnection
    {
        private bool isOpened = false;

        private readonly List<ulong> romCodes = new List<ulong>();

        private readonly List<byte> rxBuffer = new List<byte>();

        public ThermoStringEmulator(IEnumerable<ulong> romCodes)
        {
            this.romCodes = romCodes.ToList();
            foreach (var romCode in this.romCodes)
            {
                // TODO: check family code 0x28
                if (Crc8Utility.CalculateCrc8(BitConverter.GetBytes(romCode)) != 0)
                {
                    throw new ArgumentException($"CRC Error for ROM Code = {romCode:X8}");
                }
            }
        }

        #region ISerialConnection Members

        void ISerialConnection.OpenPort()
        {
            this.isOpened = true;
        }

        async Task ISerialConnection.ClosePortAsync()
        {
            await Task.Delay(5);
            this.isOpened = false;
        }

        async Task ISerialConnection.TransmitDataAsync(byte[] data)
        {
            if (!this.isOpened)
            {
                throw new InvalidOperationException();
            }

            this.rxBuffer.AddRange(data);

            var response = this.ProcessRxBuffer();
            if (response != null)
            {
                this.OnDataReceived?.Invoke(response.ToArray());
                this.rxBuffer.Clear();
            }

            await Task.Delay(1);
        }

        public event Action<byte[]> OnDataReceived;

        #endregion

        private IList<byte> ProcessRxBuffer()
        {
            // TODO: save current mode
            if ((this.rxBuffer[0] == DS2480B.SwitchToCommandMode) && (this.rxBuffer.Count == 2))
            {
                if (this.rxBuffer[1] == DS2480B.CommandResetAtFlexSpeed)
                {
                    if (this.romCodes.Count == 0)
                    {
                        return new byte[] { 0xCF, };
                    }
                    else
                    {
                        return new byte[] { 0xCD, };
                    }
                }
            }
            else if (this.rxBuffer[0] == DS2480B.SwitchToDataMode)
            {
                if (this.rxBuffer.SequenceEqual(new[] { DS2480B.SwitchToDataMode, DS18B20.SKIP_ROM, DS18B20.CONVERT_T, }))
                {
                    return new byte[] { DS18B20.SKIP_ROM, DS18B20.CONVERT_T, };
                }
                // TODO: split to MATCH_ROM + READ_SCRATCHPAD
                else if ((this.rxBuffer.Count == 20) &&
                         (this.rxBuffer[1] == DS18B20.MATCH_ROM) && 
                         (this.rxBuffer[10] == DS18B20.READ_SCRATCHPAD))
                {
                    var result = new List<byte>();
                    result.Add(DS18B20.MATCH_ROM); // TODO: check ROM presence in this.romCodes
                    for (var i = 0; i < 8; i++) result.Add(this.rxBuffer[2 + i]); // Copy ROM code 
                    result.Add(DS18B20.READ_SCRATCHPAD);

                    var temperatureValue = DS18B20.Scratchpad.DecodeTemperature12bit(0x019F);
                    var scratchpad = DS18B20.Scratchpad.EncodeScratchpad12bit(temperatureValue, 0x4B, 0x46, 0xFF, 0x01, 0x10);
                    result.AddRange(scratchpad);

                    return result;
                }
                // TODO: encode real serial numbers (here used 0x4D000000BE736128, 0x91000000BED06928)
                else if (this.rxBuffer.SequenceEqual(new byte[] 
                {                
                    DS2480B.SwitchToDataMode, DS18B20.SEARCH_ROM, 
                    DS2480B.SwitchToCommandMode, DS2480B.CommandSearchAcceleratorControlOnAtRegularSpeed,
                    DS2480B.SwitchToDataMode,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    DS2480B.SwitchToCommandMode,
                    DS2480B.CommandSearchAcceleratorControlOffAtRegularSpeed,
                }))
                {
                    return new byte[] 
                    { 
                        DS18B20.SEARCH_ROM,
                        0x80, 0x08, 0x42, 0x28, 0x0A, 0x2A, 0xA8, 0x8A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xA2, 0x20,
                    };
                }
                else if (this.rxBuffer.SequenceEqual(new byte[] {                
                    DS2480B.SwitchToDataMode, DS18B20.SEARCH_ROM,
                    DS2480B.SwitchToCommandMode, DS2480B.CommandSearchAcceleratorControlOnAtRegularSpeed,
                    DS2480B.SwitchToDataMode,
                    0x80, 0x08, 0x82, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    DS2480B.SwitchToCommandMode, DS2480B.CommandSearchAcceleratorControlOffAtRegularSpeed, }))
                {
                    return new byte[] 
                    { 
                        DS18B20.SEARCH_ROM, 
                        0x80, 0x08, 0xC2, 0x28, 0x00, 0xA2, 0xA8, 0x8A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x82,
                    };
                }
            }
            else if (this.rxBuffer.SequenceEqual(new byte[] { 0x17, 0x29, 0x39, 0x47, 0x5F, 0x69, 0x71, }))
            {
                // TODO: interpretation
                return new byte[] { 0x16, 0x28, 0x38, 0x46, 0x5E, 0x68, 0x70, };
            }

            return null;
        }
    }
}