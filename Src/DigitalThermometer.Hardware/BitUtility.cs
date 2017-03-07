using System;
using System.Collections.Generic;
using System.Globalization;

namespace DigitalThermometer.Hardware
{
    /// <summary>
    /// Utility class for bits manipulation
    /// </summary>
    public static class BitUtility
    {
        const int BitsInByte = 8;

        /// <summary>
        /// Read bit value at specified position in byte buffer
        /// </summary>
        /// <param name="buffer">Input buffer</param>
        /// <param name="location">Location of bit</param>
        /// <returns>Value of bit (0/1)</returns>
        public static byte ReadBit(IList<byte> buffer, int location)
        {
            if (location >= buffer.Count * BitsInByte)
            {
                throw new ArgumentOutOfRangeException("location", String.Format(CultureInfo.InvariantCulture, "buffer.Count = {0}  location={1}", buffer.Count, location));
            }

            var nbyte = location / BitsInByte;
            var nbit = location - (nbyte * BitsInByte);

            var b = buffer[nbyte];
            for (var i = 0; i < nbit; i++)
            {
                b = (byte)(b >> 1);
            };

            return ((byte)(b & 0x01));
        }

        /// <summary>
        /// Write bit value at specified position in byte buffer
        /// </summary>
        /// <param name="buffer">Input buffer</param>
        /// <param name="location">Location of bit</param>
        /// <param name="value">Value of bit (0/1)</param>
        public static void WriteBit(IList<byte> buffer, int location, byte value)
        {
            if (location >= buffer.Count * BitsInByte)
            {
                throw new ArgumentOutOfRangeException("location", String.Format(CultureInfo.InvariantCulture, "buffer.Count = {0}  location={1}", buffer.Count, location));
            }

            if (!((value == 0) || (value == 1)))
            {
                throw new ArgumentOutOfRangeException("value", value, "value should be 0 or 1");
            }

            var nbyte = location / BitsInByte;
            var nbit = location - (nbyte * BitsInByte);

            byte mask = 0x01;
            for (var i = 0; i < nbit; i++)
            {
                mask = (byte)(mask << 1);
            };

            if (value == 1)
            {
                buffer[nbyte] = (byte)(buffer[nbyte] | mask);
            }
            else
            {
                buffer[nbyte] = (byte)(buffer[nbyte] & (~mask));
            }
        }
    }
}
