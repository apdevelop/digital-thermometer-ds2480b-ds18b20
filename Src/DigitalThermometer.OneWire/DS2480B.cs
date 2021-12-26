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
        // See Table 5. CONFIGURATION COMMAND CODES
        // See Table 6. CONFIGURATION COMMAND RESPONSE BYTE

        /// <summary>
        /// Read Parameter commands
        /// </summary>
        public enum ReadParameterCommand : byte // 0_000_bbb_1
        {
            /// <summary>
            /// Read PDSRC parameter [0x03]
            /// </summary>
            PulldownSlewRateControl = 0b0_000_001_1,

            /// <summary>
            /// Read PPD parameter [0x05]
            /// </summary>
            ProgrammingPulseDuration = 0b0_000_010_1,

            /// <summary>
            /// Read SPUD parameter [0x07]
            /// </summary>
            StrongPullupDuration = 0b0_000_011_1,

            /// <summary>
            /// Read W1LT parameter [0x09]
            /// </summary>
            Write1LowTime = 0b0_000_100_1,

            /// <summary>
            /// Read DSO/W0RT parameter [0x0B]
            /// </summary>
            DataSampleOffsetAndWrite0RecoveryTime = 0b0_000_101_1,

            /// <summary>
            /// Read LOAD parameter [0x0D]
            /// </summary>
            LoadSensorThreshold = 0b0_000_110_1,

            /// <summary>
            /// Read RBR parameter [0x0F]
            /// </summary>
            RS232BaudRate = 0b0_000_111_1,
        }

        /// <summary>
        /// (PDSRC) Pulldown Slew Rate Control, V/μs
        /// Configurable at flexible mode only
        /// </summary>
        public enum PulldownSlewRateControl : byte // 0_001_bbb_1
        {
            /// <summary>
            /// 15 V/μs [0x11] (default) 
            /// </summary>
            _15_Vpus = 0b0_001_000_1,

            /// <summary>
            /// 2.2 V/μs [0x13]
            /// </summary>
            _2p2_Vpus = 0b0_001_001_1,

            /// <summary>
            /// 1.65 V/μs [0x15]
            /// </summary>
            _1p65_Vpus = 0b0_001_010_1,

            /// <summary>
            /// 1.37 V/μs [0x17]
            /// </summary>
            _1p37_Vpus = 0b0_001_011_1,

            /// <summary>
            /// 1.1 V/μs [0x19]
            /// </summary>
            _1p1_Vpus = 0b0_001_100_1,

            /// <summary>
            /// 0.83 V/μs [0x1B]
            /// </summary>
            _0p83_Vpus = 0b0_001_101_1,

            /// <summary>
            /// 0.7 V/μs  [0x1D]
            /// </summary>
            _0p7_Vpus = 0b0_001_110_1,

            /// <summary>
            /// 0.55 V/μs [0x1F]
            /// </summary>
            _0p55_Vpus = 0b0_001_111_1,
        }

        /// <summary>
        /// (PPD) Programming Pulse Duration, μs
        /// </summary>
        internal enum ProgrammingPulseDuration : byte // 0_010_bbb_1
        {
            /// <summary>
            /// 32 μs [0x21]
            /// </summary>
            _32us = 0b0_010_000_1,

            /// <summary>
            /// 64 μs [0x23]
            /// </summary>
            _64us = 0b0_010_001_1,

            /// <summary>
            /// 128 μs [0x25]
            /// </summary>
            _128us = 0b0_010_010_1,

            /// <summary>
            /// 256 μs [0x27]
            /// </summary>
            _256us = 0b0_010_011_1,

            /// <summary>
            /// 512 μs [0x29] (default)
            /// </summary>
            _512us = 0b0_010_100_1,

            /// <summary>
            /// 1024 μs [0x2B]
            /// </summary>
            _1024us = 0b0_010_101_1,

            /// <summary>
            /// 2048 μs [0x2D]
            /// </summary>
            _2048us = 0b0_010_110_1,

            /// <summary>
            /// Infinite [0x2F]
            /// </summary>
            _Inf = 0b0_010_111_1,
        }

        /// <summary>
        /// (SPUD) Strong Pullup Duration, ms
        /// </summary>
        internal enum StrongPullupDuration : byte // 0_011_bbb_1
        {
            /// <summary>
            /// 16.4 ms [0x31]
            /// </summary>
            _16p4ms = 0b0_011_000_1,

            /// <summary>
            /// 65.5 ms [0x33]
            /// </summary>
            _65p5ms = 0b0_011_001_1,

            /// <summary>
            /// 131 ms [0x35]
            /// </summary>
            _131ms = 0b0_011_010_1,

            /// <summary>
            /// 262 ms [0x37]
            /// </summary>
            _262ms = 0b0_011_011_1,

            /// <summary>
            /// 524 ms [0x39] (default)
            /// </summary>
            _524ms = 0b0_011_100_1,

            /// <summary>
            /// 1048 ms [0x3B]
            /// </summary>
            _1048ms = 0b0_011_101_1,

            /// <summary>
            /// Dynamic [0x3D]
            /// </summary>
            _DYN = 0b0_011_110_1,

            /// <summary>
            /// Infinite [0x3F]
            /// </summary>
            _INF = 0b0_011_111_1,
        }

        /// <summary>
        /// (W1LT) Write-1 Low Time, μs
        /// Configurable at flexible mode only
        /// </summary>
        public enum Write1LowTime : byte // 0_100_bbb_1
        {
            /// <summary>
            /// 8 μs [0x41] (default in Regular / Flexible mode)
            /// </summary>
            _8us = 0b0_100_000_1,

            /// <summary>
            /// 9 μs [0x43]
            /// </summary>
            _9us = 0b0_100_001_1,

            /// <summary>
            /// 10 μs [0x45]
            /// </summary>
            _10us = 0b0_100_010_1,

            /// <summary>
            /// 11 μs [0x47]
            /// </summary>
            _11us = 0b0_100_011_1,

            /// <summary>
            /// 12 μs [0x49]
            /// </summary>
            _12us = 0b0_100_100_1,

            /// <summary>
            /// 13 μs [0x4B]
            /// </summary>
            _13us = 0b0_100_101_1,

            /// <summary>
            /// 14 μs [0x4D]
            /// </summary>
            _14us = 0b0_100_110_1,

            /// <summary>
            /// 15 μs [0x4F]
            /// </summary>
            _15us = 0b0_100_111_1,
        }

        /// <summary>
        /// (DSO/W0RT) Data Sample Offset and Write 0 Recovery Time, μs
        /// </summary>
        public enum DataSampleOffsetAndWrite0RecoveryTime : byte // 0_101_bbb_1
        {
            /// <summary>
            /// 3 μs [0x51] (default in Regular / Flexible mode)
            /// </summary>
            _3us = 0b0_101_000_1,

            /// <summary>
            /// 4 μs [0x53]
            /// </summary>
            _4us = 0b0_101_001_1,

            /// <summary>
            /// 5 μs [0x55]
            /// </summary>
            _5us = 0b0_101_010_1,

            /// <summary>
            /// 6 μs [0x57]
            /// </summary>
            _6us = 0b0_101_011_1,

            /// <summary>
            /// 7 μs [0x59]
            /// </summary>
            _7us = 0b0_101_100_1,

            /// <summary>
            /// 8 μs [0x5B]
            /// </summary>
            _8us = 0b0_101_101_1,

            /// <summary>
            /// 9 μs [0x5D]
            /// </summary>
            _9us = 0b0_101_110_1,

            /// <summary>
            /// 10 μs [0x5F]
            /// </summary>
            _10us = 0b0_101_111_1,
        }

        /// <summary>
        /// (LOAD) Load Sensor Threshold, mA
        /// </summary>
        internal enum LoadSensorThreshold : byte // 0_110_bbb_1
        {
            /// <summary>
            /// 1.8 mA [0x61]
            /// </summary>
            _1p8mA = 0b0_110_000_1,

            /// <summary>
            /// 2.1 mA [0x63]
            /// </summary>
            _2p1mA = 0b0_110_001_1,

            /// <summary>
            /// 2.4 mA [0x65]
            /// </summary>
            _2p4mA = 0b0_110_010_1,

            /// <summary>
            /// 2.7 mA [0x67]
            /// </summary>
            _2p7mA = 0b0_110_011_1,

            /// <summary>
            /// 3.0 mA [0x69] (default)
            /// </summary>
            _3p0mA = 0b0_110_100_1,

            /// <summary>
            /// 3.3 mA [0x6B]
            /// </summary>
            _3p3mA = 0b0_110_101_1,

            /// <summary>
            /// 3.6 mA [0x6D]
            /// </summary>
            _3p6mA = 0b0_110_110_1,

            /// <summary>
            /// 3.9 mA [0x6F]
            /// </summary>
            _3p9mA = 0b0_110_111_1,
        }

        /// <summary>
        /// (RBR) RS232 Baud Rate, kbps
        /// </summary>
        internal enum RS232BaudRate : byte // 0_111_bbb_1
        {
            /// <summary>
            /// 9.6 kbps [0x71] (default)
            /// </summary>
            _9p6kbps = 0b0_111_000_1,

            /// <summary>
            /// 19.2 kbps [0x73]
            /// </summary>
            _19p2kbps = 0b0_111_001_1,

            /// <summary>
            /// 57.6 kbps [0x75]
            /// </summary>
            _57p6kbps = 0b0_111_010_1,

            /// <summary>
            /// 115.2 kbps [0x77]
            /// </summary>
            _115p2kbps = 0b0_111_011_1,

            /// <summary>
            /// 9.6 kbps inverted [0x79]
            /// </summary>
            Inv_9p6kbps = 0b0_111_100_1,

            /// <summary>
            /// 19.2 kbps inverted [0x7B]
            /// </summary>
            Inv_19p2kbps = 0b0_111_101_1,

            /// <summary>
            /// 57.6 kbps inverted [0x7D]
            /// </summary>
            Inv_57p6kbps = 0b0_111_110_1,

            /// <summary>
            /// 115.2 kbps inverted [0x7F]
            /// </summary>
            Inv_115p2kbps = 0b0_111_111_1,
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

        /// <summary>
        /// Checks if input value is valid bus reset response code of DS2480B
        /// </summary>
        /// <param name="response">Response value</param>
        /// <returns>True if input value is valid bus reset response code of DS2480B, otherwise false</returns>
        public static bool IsBusResetResponse(byte response)
        {
            // Table 2. COMMUNICATION COMMAND RESPONSE
            // The response byte includes a code for the reaction on the 1-Wire bus (bits 0 and 1) and a code for the chip revision(bits 2 to 4).
            var bits67 = (response & 0b1100_0000) >> 6;
            var bits23 = (response & 0b0000_1100) >> 2;

            return (bits67 == 0b11) && (bits23 == 0b11);
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