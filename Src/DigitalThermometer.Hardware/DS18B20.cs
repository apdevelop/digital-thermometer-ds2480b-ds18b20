using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DigitalThermometer.Hardware
{
    /// <summary>
    /// DS18B20 1-Wire Digital Thermometer
    /// </summary>
    public class DS18B20
    {
        /// <summary>
        /// SEARCH ROM [F0h]
        /// </summary>
        public static readonly byte SEARCH_ROM = 0xF0;

        public static readonly byte READ_ROM = 0x33;

        /// <summary>
        /// MATCH ROM [55h]
        /// The match ROM command followed by a 64-bit ROM code sequence allows the bus master to address a
        /// specific slave device on a multidrop or single-drop bus. Only the slave that exactly matches the 64-bit
        /// ROM code sequence will respond to the function command issued by the master; all other slaves on the
        /// bus will wait for a reset pulse.
        /// </summary>
        public static readonly byte MATCH_ROM = 0x55;

        /// <summary>
        /// SKIP ROM [CCh]
        /// The master can use this command to address all devices on the bus simultaneously without sending out
        /// any ROM code information. For example, the master can make all DS18B20s on the bus perform
        /// simultaneous temperature conversions by issuing a Skip ROM command followed by a Convert T [44h] command.
        /// </summary>
        public static readonly byte SKIP_ROM = 0xCC;

        public static readonly byte ALARM_SEARCH = 0xEC;

        //DS18B20 FUNCTION COMMANDS

        /// <summary>
        /// CONVERT T [44h]
        /// This command initiates a single temperature conversion. Following the conversion, the resulting thermal
        /// data is stored in the 2-byte temperature register in the scratchpad memory and the DS18B20 returns to its
        /// low-power idle state.
        /// </summary>
        public static readonly byte CONVERT_T = 0x44;

        public static readonly byte WRITE_SCRATCHPAD = 0x4E;

        /// <summary>
        /// READ SCRATCHPAD [BEh]
        /// This command allows the master to read the contents of the scratchpad. The data transfer starts with the
        /// least significant bit of byte 0 and continues through the scratchpad until the 9th byte (byte 8 – CRC) is
        /// read. The master may issue a reset to terminate reading at any time if only part of the scratchpad data is needed.
        /// </summary>
        public static readonly byte READ_SCRATCHPAD = 0xBE;

        public static readonly byte COPY_SCRATCHPAD = 0x48;

        public static readonly byte RECALL_E2 = 0xB8;

        public static readonly byte READ_POWER_SUPPLY = 0xB4;

        /// <summary>
        /// Initial value
        /// </summary>
        public static readonly UInt16 PowerOnTemperatureCode = 0x550;

        public static readonly double PowerOnTemperature = +85.0;

        public static readonly UInt16 MinTemperatureCode = 0xFC90;

        public static readonly double MinTemperature = -55.0;

        public static readonly UInt16 MaxTemperatureCode = 0x07D0;

        public static readonly double MaxTemperature = +125.0;

        public static readonly byte FamilyCode = 0x28;

        /// <summary>
        /// Temperature step in 12bit mode
        /// </summary>
        public static readonly double TemperatureStep12bit = 0.0625;

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
        /// Convert temperature code to value (12bit mode)
        /// </summary>
        /// <param name="temperatureCode">Temperature code</param>
        /// <returns>Temperature value (in Celsius degrees)</returns>
        public static double DecodeTemperature12bit(UInt16 temperatureCode)
        {
            if ((temperatureCode >= 0x0000) && (temperatureCode <= DS18B20.MaxTemperatureCode))
            {
                return (+TemperatureStep12bit * ((double)temperatureCode));
            }
            else if ((temperatureCode >= DS18B20.MinTemperatureCode) && (temperatureCode <= 0xFFFF))
            {
                return (-TemperatureStep12bit * (double)(((~temperatureCode) + 1) & 0xFFFF));
            }
            else
            {
                throw new ArgumentOutOfRangeException("temperatureCode", String.Format(CultureInfo.InvariantCulture, "temperatureCode={0:X4}", temperatureCode));
            }
        }

        /// <summary>
        /// Convert temperature value to temperature code (12bit mode)
        /// </summary>
        /// <param name="temperature">Temperature value (in Celsius degrees)</param>
        /// <returns>Temperature code</returns>
        public static UInt16 EncodeTemperature12bit(double temperature)
        {
            if ((temperature < DS18B20.MinTemperature) || (temperature > DS18B20.MaxTemperature))
            {
                throw new ArgumentOutOfRangeException("temperature", String.Format(CultureInfo.InvariantCulture, "temperature = {0}", temperature));
            }

            if (temperature >= 0.0)
                return ((UInt16)(temperature / TemperatureStep12bit));
            else
                return ((UInt16)(0xFFFF - (UInt16)(-temperature / TemperatureStep12bit) + 1));
        }

        public static bool CheckRomCodeFormat(string s)
        {
            return (Regex.IsMatch(s, @"^[0-9A-F]{16}$", RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Get ROM code from its string representation
        /// </summary>
        /// <param name="s">ROM code in string format, like 28xxxxxxCRC</param>
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

        public static bool CheckScratchpad(byte[] scratchpad)
        {
            if (scratchpad == null)
            {
                throw new ArgumentNullException("scratchpad");
            }

            // DS18B20 MEMORY MAP Figure 7

            // TODO: enum with cases
            if (scratchpad.Length != ScratchpadSize)
            {
                return false;
            }

            // TODO: range of temperature code

            var crc = Crc8Utility.CalculateCrc8(scratchpad);
            if (crc != 0)
            {
                return false; // BadCrc
            }

            return true;
        }

        public static readonly int ConversionTime12bit = 750;

        public static readonly int ScratchpadSize = 9;

        private const int MemoryMapOffsetTemperatureLsb = 0;

        private const int MemoryMapOffsetTemperatureMsb = 1;

        private const int MemoryMapOffsetThRegister = 2;

        private const int MemoryMapOffsetTlRegister = 3;

        private const int MemoryMapOffsetConfigurationRegister = 4;

        private const int MemoryMapOffsetReserved1 = 5;

        private const int MemoryMapOffsetReserved2 = 6;

        private const int MemoryMapOffsetReserved3 = 7;

        private const int MemoryMapOffsetCrc = 8;

        // temperature register
        // see page 6
        public static UInt16 GetTemperatureCode(byte[] scratchpad)
        {
            ValidateScratchpad(scratchpad);

            return ((UInt16)((scratchpad[MemoryMapOffsetTemperatureMsb] << 8) | scratchpad[MemoryMapOffsetTemperatureLsb]));
        }

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

        private static void ValidateScratchpad(byte[] scratchpad)
        {
            if (scratchpad.Length != ScratchpadSize)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "scratchpad.Length = {0}", scratchpad.Length));
            }

            var crc = Crc8Utility.CalculateCrc8(scratchpad, 0, ScratchpadSize - 1 - 1);
            if (crc != scratchpad[ScratchpadSize - 1])
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, "Scratchpad Crc error: expected=0x{0:X2} actual=0x{1:X2}", crc, scratchpad[ScratchpadSize - 1]));
            }
        }

        public static ThermometerResolution GetThermometerResolution(byte[] scratchpad)
        {
            ValidateScratchpad(scratchpad);

            return GetThermometerResolution(scratchpad[MemoryMapOffsetConfigurationRegister]);
        }

        public static ThermometerResolution GetThermometerResolution(byte configurationRegister)
        {
            var bits56 = ((configurationRegister & 0x60) >> 5);
            switch (bits56)
            {
                case 0x00: return ThermometerResolution.Resolution9bit;
                case 0x01: return ThermometerResolution.Resolution10bit;
                case 0x02: return ThermometerResolution.Resolution11bit;
                case 0x03: return ThermometerResolution.Resolution12bit;
                default: throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "{0:X2}", configurationRegister));
            }
        }
    }

    public enum ThermometerResolution
    {
        Resolution9bit,
        Resolution10bit,
        Resolution11bit,
        Resolution12bit,
    }
}
