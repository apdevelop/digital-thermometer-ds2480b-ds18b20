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

        #region Configuration commands

        // See Table 3. CONFIGURATION PARAMETER OVERVIEW
        // See Table 4. CONFIGURATION PARAMETER VALUE CODES

        /// <summary>
        /// (PDSRC) Pulldown Slew Rate Control, V/mks
        /// Only Flex mode
        /// </summary>
        internal enum PulldownSlewRateControl : byte
        {
            // 0001 xxx1
            ___15_Vpus = 0b0001_0001,
            __2p2_Vpus = 0b0001_0011,
            _1p65_Vpus = 0b0001_0101,
            _1p37_Vpus = 0b0001_0111,
            _1p10_Vpus = 0b0001_1001,
            _0p83_Vpus = 0b0001_1011,
            _0p70_Vpus = 0b0001_1101,
            _0p55_Vpus = 0b0001_1111,
        }

        /// <summary>
        /// (PPD) Programming Pulse Duration, mks
        /// </summary>
        internal enum ProgrammingPulseDuration : byte
        {
            // 0010 xxx1
            ___32us = 0b0010_0001,
            ___64us = 0b0010_0011,
            __128us = 0b0010_0101,
            __256us = 0b0010_0111,
            __512us = 0b0010_1001,
            _1024us = 0b0010_1011,
            _2048us = 0b0010_1101,
            ____Inf = 0b0010_1111,
        }

        /// <summary>
        /// (SPUD) Strong Pullup Duration, ms
        /// </summary>
        internal enum StrongPullupDuration : byte
        {
            // 0011 xxx1
            _16p4ms = 0b0011_0001,
            _65p5ms = 0b0011_0011,
            __131ms = 0b0011_0101,
            __262ms = 0b0011_0111,
            __524ms = 0b0011_1001, // Default value
            _1048ms = 0b0011_1011,
            ____DYN = 0b0011_1101,
            ____INF = 0b0011_1111,
        }

        /// <summary>
        /// (W1LT) Write-1 Low Time, mks
        /// </summary>
        internal enum Write1LowTime : byte
        {
            // 0100 xxx1
            __8us = 0b0100_0001,
            __9us = 0b0100_0011,
            _10us = 0b0100_0101,
            _11us = 0b0100_0111,
            _12us = 0b0100_1001,
            _13us = 0b0100_1011,
            _14us = 0b0100_1101,
            _15us = 0b0100_1111,
        }

        /// <summary>
        /// (DSO/W0RT) Data Sample Offset and Write 0 Recovery Time, mks
        /// </summary>
        internal enum DataSampleOffsetAndWrite0RecoveryTime : byte
        {
            // 0101 xxx1
            __3us = 0b0101_0001,
            __4us = 0b0101_0011,
            __5us = 0b0101_0101,
            __6us = 0b0101_0111,
            __7us = 0b0101_1001,
            __8us = 0b0101_1011,
            __9us = 0b0101_1101,
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
            ______9p6kbps = 0b0111_0001, // Default
            _____19p2kbps = 0b0111_0011,
            _____57p6kbps = 0b0111_0101,
            ____115p2kbps = 0b0111_0111,
            Inv___9p6kbps = 0b0111_1001,
            Inv__19p2kbps = 0b0111_1011,
            Inv__57p6kbps = 0b0111_1101,
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
            // Table 2. COMMUNICATION COMMAND RESPONSE
            if ((response & 0xDC) == 0xCC)
            {
                var bits01 = response & 0x03;
                switch (bits01)
                {
                    case 0x00: return OneWireBusResetResponse.BusShorted;
                    case 0x01: return OneWireBusResetResponse.PresencePulse;
                    case 0x02: return OneWireBusResetResponse.AlarmingPresencePulse;
                    case 0x03: return OneWireBusResetResponse.NoPresencePulse;
                    default: return OneWireBusResetResponse.InvalidResponse;
                }
            }
            else
            {
                return OneWireBusResetResponse.InvalidResponse;
            }
        }

        public static byte[] EscapeDataPacket(IList<byte> data)
        {
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