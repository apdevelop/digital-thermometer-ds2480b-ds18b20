using System;
using System.Collections.Generic;

namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// DS2480B Serial 1-Wire Line Driver
    /// </summary>
    public class DS2480B
    {
        /// <summary>
        /// [0xE1] Switch to Data Mode
        /// </summary>
        public const byte SwitchToDataMode = 0xE1;

        /// <summary>
        /// [0xE3] Switch to Command Mode
        /// </summary>
        public const byte SwitchToCommandMode = 0xE3;

        /// <summary>
        /// [0xC5] = 110 0 01=flex 01
        /// </summary>
        public const byte CommandResetAtFlexSpeed = 0xC5;

        /// <summary>
        /// [0xA1] = 101 0=acc.off 00=reg speed 01
        /// </summary>
        public const byte CommandSearchAcceleratorControlOffAtRegularSpeed = 0xA1;

        /// <summary>
        /// [0xB1] = 101 1=acc.on 00=reg speed 01
        /// </summary>
        public const byte CommandSearchAcceleratorControlOnAtRegularSpeed = 0xB1;

        /// <summary>
        /// [0xB5] = 101 1=acc.on 01=flex speed 01
        /// </summary>
        public const byte CommandSearchAcceleratorControlOnAtFlexSpeed = 0xB5;

        /// <summary>
        /// Generate Read Data time slot at flex speed without strong pullup
        /// </summary>
        public const byte CommandSingleBitReadDataAtFlexSpeed = 0b10010101;

        #region Configuration commands

        // See Table 3. CONFIGURATION PARAMETER OVERVIEW
        // See Table 4. CONFIGURATION PARAMETER VALUE CODES

        /// <summary>
        /// (PDSRC) Pulldown Slew Rate Control, V/μs
        /// Flexible mode only
        /// </summary>
        public enum PulldownSlewRateControl : byte // 0001 xxx1
        {
            /// <summary>
            /// 15 V/μs
            /// </summary>
            _15_Vpus = 0b0001_0001,

            /// <summary>
            /// 2.2 V/μs
            /// </summary>
            _2p2_Vpus = 0b0001_0011,

            /// <summary>
            /// 1.65 V/μs
            /// </summary>
            _1p65_Vpus = 0b0001_0101,

            /// <summary>
            /// 1.37 V/μs
            /// </summary>
            _1p37_Vpus = 0b0001_0111,

            /// <summary>
            /// 1.1 V/μs
            /// </summary>
            _1p1_Vpus = 0b0001_1001,

            /// <summary>
            /// 0.83 V/μs
            /// </summary>
            _0p83_Vpus = 0b0001_1011,

            /// <summary>
            /// 0.7 V/μs
            /// </summary>
            _0p7_Vpus = 0b0001_1101,

            /// <summary>
            /// 0.55 V/μs
            /// </summary>
            _0p55_Vpus = 0b0001_1111,
        }

        /// <summary>
        /// (PPD) Programming Pulse Duration, μs
        /// </summary>
        internal enum ProgrammingPulseDuration : byte
        {
            // 0010 xxx1
            _32us = 0b0010_0001,
            _64us = 0b0010_0011,
            _128us = 0b0010_0101,
            _256us = 0b0010_0111,
            _512us = 0b0010_1001,
            _1024us = 0b0010_1011,
            _2048us = 0b0010_1101,
            _Inf = 0b0010_1111,
        }

        /// <summary>
        /// (SPUD) Strong Pullup Duration, ms
        /// </summary>
        internal enum StrongPullupDuration : byte
        {
            // 0011 xxx1
            _16p4ms = 0b0011_0001,
            _65p5ms = 0b0011_0011,
            _131ms = 0b0011_0101,
            _262ms = 0b0011_0111,
            _524ms = 0b0011_1001, // Default value
            _1048ms = 0b0011_1011,
            _DYN = 0b0011_1101,
            _INF = 0b0011_1111,
        }

        /// <summary>
        /// (W1LT) Write-1 Low Time, μs
        /// </summary>
        public enum Write1LowTime : byte // 0100 xxx1
        {
            /// <summary>
            /// 8 μs
            /// </summary>
            _8us = 0b0100_0001,

            /// <summary>
            /// 9 μs
            /// </summary>
            _9us = 0b0100_0011,

            /// <summary>
            /// 10 μs
            /// </summary>
            _10us = 0b0100_0101,

            /// <summary>
            /// 11 μs
            /// </summary>
            _11us = 0b0100_0111,

            /// <summary>
            /// 12 μs
            /// </summary>
            _12us = 0b0100_1001,

            /// <summary>
            /// 13 μs
            /// </summary>
            _13us = 0b0100_1011,

            /// <summary>
            /// 14 μs
            /// </summary>
            _14us = 0b0100_1101,

            /// <summary>
            /// 15 μs
            /// </summary>
            _15us = 0b0100_1111,
        }

        /// <summary>
        /// (DSO/W0RT) Data Sample Offset and Write 0 Recovery Time, μs
        /// </summary>
        public enum DataSampleOffsetAndWrite0RecoveryTime : byte
        {
            // 0101 xxx1

            /// <summary>
            /// 3 μs
            /// </summary>
            _3us = 0b0101_0001,

            /// <summary>
            /// 4 μs
            /// </summary>
            _4us = 0b0101_0011,

            /// <summary>
            /// 5 μs
            /// </summary>
            _5us = 0b0101_0101,

            /// <summary>
            /// 6 μs
            /// </summary>
            _6us = 0b0101_0111,

            /// <summary>
            /// 7 μs
            /// </summary>
            _7us = 0b0101_1001,

            /// <summary>
            /// 8 μs
            /// </summary>
            _8us = 0b0101_1011,

            /// <summary>
            /// 9 μs
            /// </summary>
            _9us = 0b0101_1101,

            /// <summary>
            /// 10 μs
            /// </summary>
            _10us = 0b0101_1111,
        }

        /// <summary>
        /// (LOAD) Load Sensor Threshold, mA
        /// </summary>
        internal enum LoadSensorThreshold : byte
        {
            // 0110 xxx1
            _1p8mA = 0b0110_0001,
            _2p1mA = 0b0110_0011,
            _2p4mA = 0b0110_0101,
            _2p7mA = 0b0110_0111,
            _3p0mA = 0b0110_1001, // Default
            _3p3mA = 0b0110_1011,
            _3p6mA = 0b0110_1101,
            _3p9mA = 0b0110_1111,
        }

        /// <summary>
        /// (RBR) RS232 Baud Rate, kbps
        /// </summary>
        internal enum RS232BaudRate : byte
        {
            // 0111 xxx1
            _9p6kbps = 0b0111_0001, // Default
            _19p2kbps = 0b0111_0011,
            _57p6kbps = 0b0111_0101,
            _115p2kbps = 0b0111_0111,
            Inv_9p6kbps = 0b0111_1001,
            Inv_19p2kbps = 0b0111_1011,
            Inv_57p6kbps = 0b0111_1101,
            Inv_115p2kbps = 0b0111_1111,
        }

        #endregion

        /// <summary>
        /// Bus reset response
        /// </summary>
        /// <param name="response">Response</param>
        /// <returns>Response check result</returns>
        public static OneWireBusResetResponse GetBusResetResponse(byte response)
        {
            // TODO: ? check bit7 == 1 ?

            // Table 2. COMMUNICATION COMMAND RESPONSE
            // The response byte includes a code for the reaction on the 1-Wire bus (bits 0 and 1) and a code for the chip revision(bits 2 to 4).
            var bits01 = response & 0b0000_0011;
            switch (bits01)
            {
                case 0b00: return OneWireBusResetResponse.BusShorted;
                case 0b01: return OneWireBusResetResponse.PresencePulse;
                case 0b10: return OneWireBusResetResponse.AlarmingPresencePulse;
                case 0b11: return OneWireBusResetResponse.NoPresencePulse;
                default: throw new InvalidOperationException();
            }
        }

        internal static byte[] EscapeDataPacket(IList<byte> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var result = new List<byte>(data.Count);
            for (var i = 0; i < data.Count; i++)
            {
                // If the reserved code that normally switches to Command Mode is to be written to the 1-Wire bus, this code byte must be sent twice (duplicated).
                if (data[i] == DS2480B.SwitchToCommandMode)
                {
                    // Escape 0xE3 in packet by doubling it
                    result.Add(DS2480B.SwitchToCommandMode);
                }

                result.Add(data[i]);
            }

            return result.ToArray();
        }
    }
}