﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// DS18B20 1-Wire Digital Thermometer.
    /// </summary>
    public class DS18B20
    {
        /// <summary>
        /// DS18B20’s 1-Wire family code.
        /// </summary>
        public const byte FamilyCode = 0x28;

        #region ROM Commands

        /// <summary>
        /// SEARCH ROM [F0h]
        /// When a system is initially powered up, the master must identify the ROM codes of all slave devices on
        /// the bus, which allows the master to determine the number of slaves and their device types.
        /// </summary>
        public const byte SEARCH_ROM = 0xF0;

        /// <summary>
        /// READ ROM [33h]
        /// This command can only be used when there is one slave on the bus. It allows the bus master to read the
        /// slave’s 64-bit ROM code without using the Search ROM procedure.
        /// </summary>
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

        /// <summary>
        /// ALARM SEARCH [ECh]
        /// The operation of this command is identical to the operation of the Search ROM command except that
        /// only slaves with a set alarm flag will respond.
        /// </summary>
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

        /// <summary>
        /// WRITE SCRATCHPAD [4Eh]
        /// This command allows the master to write 3 bytes of data to the DS18B20’s scratchpad.
        /// </summary>
        public const byte WRITE_SCRATCHPAD = 0x4E;

        /// <summary>
        /// READ SCRATCHPAD [BEh]
        /// This command allows the master to read the contents of the scratchpad. The data transfer starts with the
        /// least significant bit of byte 0 and continues through the scratchpad until the 9th byte (byte 8 – CRC) is
        /// read. The master may issue a reset to terminate reading at any time if only part of the scratchpad data is needed.
        /// </summary>
        public const byte READ_SCRATCHPAD = 0xBE;

        /// <summary>
        /// COPY SCRATCHPAD [48h]
        /// This command copies the contents of the scratchpad TH, TL and configuration registers (bytes 2, 3 and 4) to EEPROM.
        /// </summary>
        public const byte COPY_SCRATCHPAD = 0x48;

        /// <summary>
        /// RECALL E2 [B8h]
        /// This command recalls the alarm trigger values (TH and TL) and configuration data from EEPROM and
        /// places the data in bytes 2, 3, and 4, respectively, in the scratchpad memory.
        /// </summary>
        public const byte RECALL_E2 = 0xB8;

        /// <summary>
        /// READ POWER SUPPLY [B4h]
        /// The master device issues this command followed by a read time slot to determine if any DS18B20s on the
        /// bus are using parasite power.During the read time slot, parasite powered DS18B20s will pull the bus
        /// low, and externally powered DS18B20s will let the bus remain high.
        /// </summary>
        public const byte READ_POWER_SUPPLY = 0xB4;

        #endregion

        /// <summary>
        /// Power-on reset value of the temperature register (+85°C).
        /// </summary>
        public const UInt16 PowerOnTemperatureCode = 0x550;

        /// <summary>
        /// The power-on reset value of the temperature register is +85°C.
        /// </summary>
        public const double PowerOnTemperature = +85.0;

        /// <summary>
        /// Minimum supported temperature value, degrees Celsius.
        /// </summary>
        public const double MinTemperature = -55.0;

        /// <summary>
        /// Maximum supported temperature value, degrees Celsius.
        /// </summary>
        public const double MaxTemperature = +125.0;

        /// <summary>
        /// Minimum supported temperature value, raw value.
        /// </summary>
        public const UInt16 MinTemperatureCode = 0xFC90;

        /// <summary>
        /// Maximum supported temperature value, raw value.
        /// </summary>
        public const UInt16 MaxTemperatureCode = 0x07D0;

        /// <summary>
        /// Temperature step in 12-bit resolution, degrees Celsius.
        /// </summary>
        private const double TemperatureStep12bit = 0.0625;

        #region Temperature conversion time

        /// <summary>
        /// Temperature conversion time (max) in 9-bit mode, milliseconds
        /// </summary>
        public const double MaxConversionTime9bit = 93.75;

        /// <summary>
        /// Temperature conversion time (max) in 10-bit mode, milliseconds
        /// </summary>
        public const double MaxConversionTime10bit = 187.5;

        /// <summary>
        /// Temperature conversion time (max) in 11-bit mode, milliseconds
        /// </summary>
        public const double MaxConversionTime11bit = 375.0;

        /// <summary>
        /// Temperature conversion time (max) in 12-bit mode, milliseconds
        /// </summary>
        public const double MaxConversionTime12bit = 750.0;

        #endregion

        /// <summary>
        /// Size of SRAM scratchpad in bytes.
        /// </summary>
        public const int ScratchpadSize = 9;

        /// <summary>
        /// Checks the validity of ROM code of DS18B20 (Family code, CRC).
        /// </summary>
        /// <param name="romCode">DS18B20 ROM code</param>
        /// <returns></returns>
        public static bool IsValidRomCode(UInt64 romCode)
        {
            var bytes = BitConverter.GetBytes(romCode);
            var isValidFamilyCode = bytes[0] == DS18B20.FamilyCode;
            var isValidCrc = bytes[bytes.Length - 1] == Crc8Utility.CalculateCrc8(bytes, 0, bytes.Length - 2);

            return isValidCrc && isValidFamilyCode;
        }

        /// <summary>
        /// Checks the validity of raw value temperature code.
        /// </summary>
        /// <param name="temperatureCode">Raw value of temperature.</param>
        /// <returns></returns>
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
        /// Convert temperature value to temperature code (12-bit resolution).
        /// </summary>
        /// <param name="temperature">Temperature value in degrees Celsius.</param>
        /// <returns>Temperature code.</returns>
        public static UInt16 EncodeTemperature12bit(double temperature)
        {
            if ((temperature < DS18B20.MinTemperature) || (temperature > DS18B20.MaxTemperature))
            {
                throw new ArgumentOutOfRangeException(nameof(temperature), $"temperature = {temperature}");
            }

            return temperature >= 0.0
                ? (UInt16)(temperature / TemperatureStep12bit)
                : (UInt16)(0xFFFF - (UInt16)(-temperature / TemperatureStep12bit) + 1);
        }

        /// <summary>
        /// Interprets Read Power Supply command response.
        /// </summary>
        /// <param name="readPowerSupplyResponse">Read Power Supply command response.</param>
        /// <returns>Parasite powered DS18B20 are on bus.</returns>
        public static bool IsParasitePowerMode(byte readPowerSupplyResponse)
        {
            switch (readPowerSupplyResponse)
            {
                case 0: return true;
                case 1: return false;
                default: throw new ArgumentOutOfRangeException(nameof(readPowerSupplyResponse));
            }
        }

        /// <summary>
        /// Converts <see cref="ThermometerResolution"/> to string representation.
        /// </summary>
        /// <param name="resolution">Thermometer resolution.</param>
        /// <returns>String representation of given resolution.</returns>
        public static string ThermometerResolutionToString(ThermometerResolution resolution)
        {
            switch (resolution)
            {
                case ThermometerResolution.Resolution9bit: return "9-bit";
                case ThermometerResolution.Resolution10bit: return "10-bit";
                case ThermometerResolution.Resolution11bit: return "11-bit";
                case ThermometerResolution.Resolution12bit: return "12-bit";
                default: throw new ArgumentOutOfRangeException(nameof(resolution));
            }
        }

        /// <summary>
        /// Thermometer resolution mode.
        /// </summary>
        public enum ThermometerResolution
        {
            /// <summary>
            /// 9-bit.
            /// </summary>
            Resolution9bit,

            /// <summary>
            /// 10-bit.
            /// </summary>
            Resolution10bit,

            /// <summary>
            /// 11-bit.
            /// </summary>
            Resolution11bit,

            /// <summary>
            /// 12-bit.
            /// </summary>
            Resolution12bit,
        }

        /// <summary>
        /// Scratchpad.
        /// </summary>
        public class Scratchpad
        {
            #region Memory map offsets

            private const int MemoryMapOffsetTemperatureLsb = 0;

            private const int MemoryMapOffsetTemperatureMsb = 1;

            private const int MemoryMapOffsetThRegister = 2;

            private const int MemoryMapOffsetTlRegister = 3;

            private const int MemoryMapOffsetConfigurationRegister = 4;

#pragma warning disable IDE0051
            private const int MemoryMapOffsetReserved1 = 5;

            private const int MemoryMapOffsetReserved2 = 6;

            private const int MemoryMapOffsetReserved3 = 7;
#pragma warning restore IDE0051

            private const int MemoryMapOffsetCrc = 8;

            #endregion

            /// <summary>
            /// Raw contents of scratchpad, can contain invalid (bad CRC) data.
            /// </summary>
            private readonly byte[] scratchpad;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="rawData">Raw contents of scratchpad, can contain invalid (bad CRC) data.</param>
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
            /// Raw contents of scratchpad, can contain invalid (bad CRC) data.
            /// </summary>
            public byte[] RawData => this.scratchpad.ToArray();

            /// <summary>
            /// Actual CRC value from scratchpad.
            /// </summary>
            public byte ActualCrc => this.scratchpad[MemoryMapOffsetCrc];

            /// <summary>
            /// Computed CRC of scratchpad contents.
            /// </summary>
            public byte ComputedCrc => Crc8Utility.CalculateCrc8(this.scratchpad, 0, ScratchpadSize - 1 - 1); // bytes 0..7

            /// <summary>
            /// The computed value of scratchpad contents CRC is correct.
            /// </summary>
            public bool IsValidCrc => Crc8Utility.CalculateCrc8(this.scratchpad) == 0;

            /// <summary>
            /// Temperature value raw data; null if CRC mismatch.
            /// </summary>
            public UInt16? TemperatureRawData
            {
                get
                {
                    return this.IsValidCrc ?
                        (UInt16)((this.scratchpad[MemoryMapOffsetTemperatureMsb] << 8) | this.scratchpad[MemoryMapOffsetTemperatureLsb]) :
                        (UInt16?)null;
                }
            }

            /// <summary>
            /// Temperature value, in degrees Celsius; null if CRC mismatch or raw code value out of allowed range.
            /// </summary>
            public double? Temperature
            {
                get
                {
                    // TODO: ? IsValidTemperatureCode(temperatureCode)
                    return (this.TemperatureRawData.HasValue &&
                            this.ThermometerActualResolution.HasValue &&
                            IsValidTemperatureCode(this.TemperatureRawData.Value) &&
                            (this.TemperatureRawData.Value != 0x07FF)) // Temperature conversion was unsuccessful (undocumented)
                                ? DS18B20.Scratchpad.DecodeTemperature(this.TemperatureRawData.Value, this.ThermometerActualResolution.Value)
                                : (double?)null;
                }
            }

            /// <summary>
            /// Returns true if temperature code in allowed range (Table 2 in datasheet).
            /// </summary>
            public static bool IsValidTemperatureCode(ushort temperatureCode)
            {
                return (temperatureCode >= 0x0000) && (temperatureCode <= DS18B20.MaxTemperatureCode) ||
                        (temperatureCode >= DS18B20.MinTemperatureCode) && (temperatureCode <= 0xFFFF);
            }

            /// <summary>
            /// Temperature value is equal to power-on value (+85°C).
            /// </summary>
            public bool IsPowerOnTemperature => this.TemperatureRawData == DS18B20.PowerOnTemperatureCode;

            /// <summary>
            /// Actual resolution, according to configuration register contents.
            /// </summary>
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
                        return null;
                    }
                }
            }

            /// <summary>
            /// Alarm trigger register (TH).
            /// </summary>
            public int? HighAlarmTemperature => this.IsValidCrc ?
                        (int)unchecked((sbyte)this.scratchpad[MemoryMapOffsetThRegister]) :
                        (int?)null;

            /// <summary>
            /// Alarm trigger register (TL).
            /// </summary>
            public int? LowAlarmTemperature => this.IsValidCrc ?
                        (int)unchecked((sbyte)this.scratchpad[MemoryMapOffsetTlRegister]) :
                        (int?)null;

            /// <summary>
            /// Convert temperature code to value.
            /// </summary>
            /// <param name="temperatureCode">Temperature code.</param>
            /// <param name="resolution">Thermometer resolution.</param>
            /// <returns>Temperature value in degrees Celsius.</returns>
            public static double DecodeTemperature(UInt16 temperatureCode, ThermometerResolution resolution = ThermometerResolution.Resolution12bit)
            {
                /*
                    If the DS18B20 is configured for 12-bit resolution, all bits in the temperature register will contain valid data.
                    For 11-bit resolution, bit 0 is undefined. For 10-bit resolution, bits 1 and 0 are undefined, and for 9-bit
                    resolution bits 2, 1 and 0 are undefined. 
                */

                // Zero undefined bits
                switch (resolution)
                {
                    case ThermometerResolution.Resolution9bit: { temperatureCode = (ushort)(temperatureCode & 0b1111_1111_1111_1000); break; }
                    case ThermometerResolution.Resolution10bit: { temperatureCode = (ushort)(temperatureCode & 0b1111_1111_1111_1100); break; }
                    case ThermometerResolution.Resolution11bit: { temperatureCode = (ushort)(temperatureCode & 0b1111_1111_1111_1110); break; }
                    case ThermometerResolution.Resolution12bit: { break; }
                    default: throw new ArgumentOutOfRangeException(nameof(resolution));
                }

                if ((temperatureCode >= 0x0000) && (temperatureCode <= MaxTemperatureCode))
                {
                    return +TemperatureStep12bit * ((double)temperatureCode);
                }
                else if ((temperatureCode >= MinTemperatureCode) && (temperatureCode <= 0xFFFF))
                {
                    return -TemperatureStep12bit * (double)(((~temperatureCode) + 1) & 0xFFFF);
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(temperatureCode), $"temperatureCode={temperatureCode:X4}");
                }
            }

            /// <summary>
            /// Creates configuration register value from given resolution.
            /// </summary>
            /// <param name="resolution">Thermometer resolution.</param>
            /// <returns>Configuration register value</returns>
            public static byte ConfigurationRegisterFromResolution(ThermometerResolution resolution)
            {
                // Figure 8 / Table 3 from datasheet
                switch (resolution)
                {
                    case ThermometerResolution.Resolution9bit: return 0b_0_00_11111;
                    case ThermometerResolution.Resolution10bit: return 0b_0_01_11111;
                    case ThermometerResolution.Resolution11bit: return 0b_0_10_11111;
                    case ThermometerResolution.Resolution12bit: return 0b_0_11_11111;
                    default: throw new ArgumentOutOfRangeException(nameof(resolution));
                }
            }

            // TODO: ? add set {} for using in device emulator scenario

            /// <summary>
            /// Creating scratchpad contents (with computed CRC), simulating real DS18B20.
            /// </summary>
            /// <param name="temperatureValue">Temperature value, in degrees Celsius.</param>
            /// <param name="thCode">Alarm high trigger raw value.</param>
            /// <param name="tlCode">Alarm high trigger raw value.</param>
            /// <param name="thermometerResolution">Thermometer resolution.</param>
            /// <returns>Scratchpad contents.</returns>
            public static byte[] EncodeScratchpad(double temperatureValue, byte thCode, byte tlCode, ThermometerResolution thermometerResolution = ThermometerResolution.Resolution12bit)
            {
                if (thermometerResolution != ThermometerResolution.Resolution12bit) // TODO: other resolutions
                {
                    throw new NotSupportedException();
                }

                var temperatureCode = EncodeTemperature12bit(temperatureValue); // TODO: other resolutions

                var scratchpad = new List<byte>(ScratchpadSize)
                {
                    (byte)(temperatureCode & 0x00FF),
                    (byte)((temperatureCode & 0xFF00) >> 8),
                    thCode, // Th
                    tlCode, // Tl
                    ConfigurationRegisterFromResolution(thermometerResolution), // Configuration
                    0xFF, // Reserved
                    0x0C, // Reserved
                    0x10, // Reserved
                };

                var crc = Crc8Utility.CalculateCrc8(scratchpad, 0, ScratchpadSize - 1 - 1);
                scratchpad.Add(crc);

                return scratchpad.ToArray();
            }
        }
    }
}
