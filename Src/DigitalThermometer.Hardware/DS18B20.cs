using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DigitalThermometer.Hardware
{
    /// <summary>
    /// DS18B20 1-Wire Digital Thermometer
    /// </summary>
    public class DS18B20
    {
        #region ROM Commands

        /// <summary>
        /// SEARCH ROM [F0h]
        /// </summary>
        public const byte SEARCH_ROM = 0xF0;

        public const byte READ_ROM = 0x33;

        /// <summary>
        /// MATCH ROM [55h]
        /// The match ROM command followed by a 64-bit ROM code sequence allows the bus master to address a
        /// specific slave device on a multidrop or single-drop bus. Only the slave that exactly matches the 64-bit
        /// ROM code sequence will respond to the function command issued by the master; all other slaves on the
        /// bus will wait for a reset pulse.
        /// </summary>
        public const byte MATCH_ROM = 0x55;

        /// <summary>
        /// SKIP ROM [CCh]
        /// The master can use this command to address all devices on the bus simultaneously without sending out
        /// any ROM code information. For example, the master can make all DS18B20s on the bus perform
        /// simultaneous temperature conversions by issuing a Skip ROM command followed by a Convert T [44h] command.
        /// </summary>
        public const byte SKIP_ROM = 0xCC;

        public const byte ALARM_SEARCH = 0xEC;

        #endregion

        #region DS18B20 FUNCTION COMMANDS

        /// <summary>
        /// CONVERT T [44h]
        /// This command initiates a single temperature conversion. Following the conversion, the resulting thermal
        /// data is stored in the 2-byte temperature register in the scratchpad memory and the DS18B20 returns to its
        /// low-power idle state.
        /// </summary>
        public const byte CONVERT_T = 0x44;

        public const byte WRITE_SCRATCHPAD = 0x4E;

        /// <summary>
        /// READ SCRATCHPAD [BEh]
        /// This command allows the master to read the contents of the scratchpad. The data transfer starts with the
        /// least significant bit of byte 0 and continues through the scratchpad until the 9th byte (byte 8 – CRC) is
        /// read. The master may issue a reset to terminate reading at any time if only part of the scratchpad data is needed.
        /// </summary>
        public const byte READ_SCRATCHPAD = 0xBE;

        public const byte COPY_SCRATCHPAD = 0x48;

        public const byte RECALL_E2 = 0xB8;

        public const byte READ_POWER_SUPPLY = 0xB4;

        #endregion

        /// <summary>
        /// Power-on reset value of the temperature register (+85°C)
        /// </summary>
        public const UInt16 PowerOnTemperatureCode = 0x550;

        /// <summary>
        /// The power-on reset value of the temperature register is +85°C
        /// </summary>
        public static readonly double PowerOnTemperature = +85.0;

        public static readonly UInt16 MinTemperatureCode = 0xFC90;

        public static readonly double MinTemperature = -55.0;

        public static readonly UInt16 MaxTemperatureCode = 0x07D0;

        public static readonly double MaxTemperature = +125.0;

        /// <summary>
        /// DS18B20’s 1-Wire family code
        /// </summary>
        public const byte FamilyCode = 0x28;

        /// <summary>
        /// Temperature step in 12-bit resolution, Celsius degrees
        /// </summary>
        public const double TemperatureStep12bit = 0.0625;

        public static bool IsValidTemperatureCode(UInt16 temperatureCode)
        {
            if ((temperatureCode >= 0x0000) && (temperatureCode <= DS18B20.MaxTemperatureCode))
            {
                return true;
            }
            else if ((temperatureCode >= DS18B20.MinTemperatureCode) && (temperatureCode <= 0xFFFF))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Convert temperature value to temperature code (12-bit resolution)
        /// </summary>
        /// <param name="temperature">Temperature value in Celsius degrees</param>
        /// <returns>Temperature code</returns>
        public static UInt16 EncodeTemperature12bit(double temperature)
        {
            if ((temperature < DS18B20.MinTemperature) || (temperature > DS18B20.MaxTemperature))
            {
                throw new ArgumentOutOfRangeException(nameof(temperature), $"temperature = {temperature}");
            }

            if (temperature >= 0.0)
                return (UInt16)(temperature / TemperatureStep12bit);
            else
                return (UInt16)(0xFFFF - (UInt16)(-temperature / TemperatureStep12bit) + 1);
        }

        public static bool CheckRomCodeFormat(string s)
        {
            return Regex.IsMatch(s, @"^[0-9A-F]{16}$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Get ROM code from its string representation
        /// </summary>
        /// <param name="s">ROM code in HEX string format, little-endian, like 28xxxxxxCRC</param>
        /// <returns>ROM code</returns>
        public static UInt64 RomCodeFromLEString(string s)
        {
            var cleanString = s
                .Trim()
                .Replace(" ", String.Empty)
                .Replace("\t", String.Empty);

            return BitConverter.ToUInt64(BitConverter.GetBytes(Convert.ToUInt64(cleanString, 16)).Reverse().ToArray(), 0);
        }

        public static string RomCodeToLEString(UInt64 romCode)
        {
            return BitConverter.ToUInt64(BitConverter.GetBytes(romCode).Reverse().ToArray(), 0).ToString("X16");
        }

        public static readonly int ConversionTime12bit = 750;

        /// <summary>
        /// Size of SRAM scratchpad in bytes
        /// </summary>
        public const int ScratchpadSize = 9;

        public enum ThermometerResolution
        {
            Resolution9bit,
            Resolution10bit,
            Resolution11bit,
            Resolution12bit,
        }

        /// <summary>
        /// Scratchpad
        /// </summary>
        public class Scratchpad
        {
            #region Memory map offsets

            private const int MemoryMapOffsetTemperatureLsb = 0;

            private const int MemoryMapOffsetTemperatureMsb = 1;

            private const int MemoryMapOffsetThRegister = 2;

            private const int MemoryMapOffsetTlRegister = 3;

            private const int MemoryMapOffsetConfigurationRegister = 4;

            private const int MemoryMapOffsetReserved1 = 5;

            private const int MemoryMapOffsetReserved2 = 6;

            private const int MemoryMapOffsetReserved3 = 7;

            private const int MemoryMapOffsetCrc = 8;

            #endregion

            private readonly byte[] scratchpad;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="rawData">Raw contents of scratchpad, can contains invalid (bad CRC) data</param>
            public Scratchpad(byte[] rawData)
            {
                if (rawData == null)
                {
                    throw new ArgumentNullException(nameof(rawData));
                }

                if (rawData.Length != ScratchpadSize)
                {
                    throw new ArgumentException($"rawData.Length = {rawData.Length} (should be {ScratchpadSize} bytes)");
                }

                this.scratchpad = rawData;
            }

            /// <summary>
            /// Raw contents of scratchpad, can contains invalid (bad CRC) data
            /// </summary>
            public byte[] RawData
            {
                get
                {
                    return this.scratchpad.ToArray();
                }
            }

            /// <summary>
            /// Actual CRC value from scratchpad
            /// </summary>
            public byte ActualCrc
            {
                get
                {
                    return this.scratchpad[MemoryMapOffsetCrc];
                }
            }

            /// <summary>
            /// Computed CRC of scratchpad contents
            /// </summary>
            public byte ComputedCrc
            {
                get
                {
                    return Crc8Utility.CalculateCrc8(this.scratchpad, 0, ScratchpadSize - 1 - 1); // bytes 0..7
                }
            }

            public bool IsValidCrc
            {
                get
                {
                    return Crc8Utility.CalculateCrc8(this.scratchpad) == 0;
                }
            }

            public UInt16? TemperatureRawData
            {
                get
                {
                    return this.IsValidCrc ? (UInt16)((this.scratchpad[MemoryMapOffsetTemperatureMsb] << 8) | this.scratchpad[MemoryMapOffsetTemperatureLsb]) : (UInt16?)null;
                }
            }

            public double? Temperature
            {
                get
                {
                    // TODO: check config register for using actual resolution
                    // TODO: ? IsValidTemperatureCode(temperatureCode)
                    return this.IsValidCrc ? DS18B20.Scratchpad.DecodeTemperature12bit(this.TemperatureRawData.Value) : (double?)null;
                }
            }

            public bool IsPowerOnTemperature
            {
                get
                {
                    return this.TemperatureRawData == DS18B20.PowerOnTemperatureCode;
                }
            }

            // TODO: add Th, Tl

            public ThermometerResolution? ThermometerActualResolution
            {
                get
                {
                    if (this.IsValidCrc)
                    {
                        var configurationRegister = this.scratchpad[MemoryMapOffsetConfigurationRegister];

                        // See Figure 8, Table 3
                        var bits56 = (configurationRegister & 0x60) >> 5;
                        switch (bits56)
                        {
                            case 0x00: return ThermometerResolution.Resolution9bit;
                            case 0x01: return ThermometerResolution.Resolution10bit;
                            case 0x02: return ThermometerResolution.Resolution11bit;
                            case 0x03: return ThermometerResolution.Resolution12bit;
                            default: throw new InvalidOperationException(configurationRegister.ToString("X2"));
                        }
                    }
                    else
                    {
                        return (ThermometerResolution?)null;
                    }
                }
            }

            /// <summary>
            /// Convert temperature code to value (12-bit resolution)
            /// </summary>
            /// <param name="temperatureCode">Temperature code</param>
            /// <returns>Temperature value in Celsius degrees</returns>
            public static double DecodeTemperature12bit(UInt16 temperatureCode)
            {
                if ((temperatureCode >= 0x0000) && (temperatureCode <= DS18B20.MaxTemperatureCode))
                {
                    return +TemperatureStep12bit * ((double)temperatureCode);
                }
                else if ((temperatureCode >= DS18B20.MinTemperatureCode) && (temperatureCode <= 0xFFFF))
                {
                    return -TemperatureStep12bit * (double)(((~temperatureCode) + 1) & 0xFFFF);
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(temperatureCode), $"temperatureCode={temperatureCode:X4}");
                }
            }

            // TODO: ? add set {} for using in device emulator scenario

            public static byte[] EncodeScratchpad12bit(double temperatureValue, byte thCode, byte tlCode, byte reserved1, byte reserved2, byte reserved3)
            {
                var temperatureCode = EncodeTemperature12bit(temperatureValue);

                var scratchpad = new List<byte>(ScratchpadSize);

                scratchpad.Add((byte)(temperatureCode & 0x00FF));
                scratchpad.Add((byte)((temperatureCode & 0xFF00) >> 8));

                scratchpad.Add(thCode); // Th
                scratchpad.Add(tlCode); // Tl
                scratchpad.Add(0x7F); // Configuration

                // Reserved
                scratchpad.Add(reserved1);
                scratchpad.Add(reserved2);
                scratchpad.Add(reserved3);

                var crc = Crc8Utility.CalculateCrc8(scratchpad, 0, ScratchpadSize - 1 - 1);
                scratchpad.Add(crc);

                return scratchpad.ToArray();
            }
        }
    }
}