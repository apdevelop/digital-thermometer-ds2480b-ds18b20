using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// Various utility methods
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Length of 1-Wire devices ROM code, in bytes
        /// </summary>
        public const int RomCodeLength = 8;

        /// <summary>
        /// Converts byte sequence to hex string representation, with spaces between bytes
        /// </summary>
        /// <param name="data">Byte sequence</param>
        /// <returns>String with data hex representation</returns>
        public static string ByteArrayToHexSpacedString(IEnumerable<byte> data)
        {
            return String.Join(" ", data.Select(b => b.ToString("X2")));
        }

        internal static string ByteArrayToHexSpacedString(IEnumerable<byte> data, int offset, int count)
        {
            return String.Join(" ", data.Skip(offset).Take(count).Select(b => b.ToString("X2")));
        }

        /// <summary>
        /// Get ROM code from its little-endian hex string representation
        /// </summary>
        /// <param name="s">ROM code in hex string format, little-endian (28xxxxxxCRC for DS18B20)</param>
        /// <returns>ROM code</returns>
        public static UInt64 RomCodeFromLEString(string s)
        {
            var cleanString = s
                .Trim()
                .Replace(" ", String.Empty)
                .Replace("\t", String.Empty);

            return BitConverter.ToUInt64(BitConverter.GetBytes(Convert.ToUInt64(cleanString, 16)).Reverse().ToArray(), 0);
        }

        /// <summary>
        /// Converts ROM code to little-endian hex string representation
        /// </summary>
        /// <param name="romCode">ROM code</param>
        /// <returns></returns>
        public static string RomCodeToLEString(UInt64 romCode)
        {
            return BitConverter.ToUInt64(BitConverter.GetBytes(romCode).Reverse().ToArray(), 0).ToString("X16");
        }

        /// <summary>
        /// Check ROM code hex string format
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool CheckRomCodeFormat(string s)
        {
            return Regex.IsMatch(s, @"^[0-9A-F]{16}$", RegexOptions.IgnoreCase);
        }
    }
}