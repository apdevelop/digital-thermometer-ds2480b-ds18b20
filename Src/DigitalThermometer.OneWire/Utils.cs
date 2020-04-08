using System;
using System.Linq;

namespace DigitalThermometer.OneWire
{
    public static class Utils
    {
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
    }
}