using System.Collections.Generic;

namespace DigitalThermometer.Hardware
{
    /// <summary>
    /// DS2480B Serial 1-Wire Line Driver
    /// </summary>
    public class DS2480B
    {
        /// <summary>
        /// [0xE1] Switch to Data Mode
        /// </summary>
        public static readonly byte SwitchToDataMode = 0xE1;

        /// <summary>
        /// [0xE3] Switch to Command Mode
        /// </summary>
        public static readonly byte SwitchToCommandMode = 0xE3;

        /// <summary>
        /// [0xC5] = 110 0 01=flex 01
        /// </summary>
        public static readonly byte CommandResetAtFlexSpeed = 0xC5;

        /// <summary>
        /// [0xA1] = 101 0=acc.off 00=reg speed 01
        /// </summary>
        public static readonly byte CommandSearchAcceleratorControlOffAtRegularSpeed = 0xA1;

        /// <summary>
        /// [0xB1] = 101 1=acc.on 00=reg speed 01
        /// </summary>
        public static readonly byte CommandSearchAcceleratorControlOnAtRegularSpeed = 0xB1;

        /// <summary>
        /// [0xB5] = 101 1=acc.on 01=flex speed 01
        /// </summary>
        public static readonly byte CommandSearchAcceleratorControlOnAtFlexSpeed = 0xB5;

        // See Table 4. CONFIGURATION PARAMETER VALUE CODES
        // PDSRC  - Pulldown Slew Rate Control V/mks
        public static readonly byte PDSRC_15Vpus = 0x11;
        public static readonly byte PDSRC_2p2Vpus = 0x13;
        public static readonly byte PDSRC_1p65Vpus = 0x15;
        public static readonly byte PDSRC_1p37Vpus = 0x17;
        public static readonly byte PDSRC_1p1Vpus = 0x19;
        public static readonly byte PDSRC_0p83Vpus = 0x1B;
        public static readonly byte PDSRC_0p7Vpus = 0x1D;
        public static readonly byte PDSRC_0p55Vpus = 0x1F;

        // PPD - Programming Pulse Duration, mks
        public static readonly byte PPD_32us = 0x21;
        public static readonly byte PPD_64us = 0x23;
        public static readonly byte PPD_128us = 0x25;
        public static readonly byte PPD_256us = 0x27;
        public static readonly byte PPD_512us = 0x29;
        public static readonly byte PPD_1024us = 0x2B;
        public static readonly byte PPD_2048us = 0x2D;
        public static readonly byte PPD_INF = 0x2F;

        // Strong Pullup Duration, ms
        public static readonly byte SPUD_16p4ms = 0x31;
        public static readonly byte SPUD_65p5ms = 0x33;
        public static readonly byte SPUD_131ms = 0x35;
        public static readonly byte SPUD_262ms = 0x37;
        public static readonly byte SPUD_524ms = 0x39;
        public static readonly byte SPUD_1048ms = 0x3B;
        public static readonly byte SPUD_DYN = 0x3D;
        public static readonly byte SPUD_INF = 0x3F;

        // Write-1 Low Time, mks
        public static readonly byte W1LT_8us = 0x41;
        public static readonly byte W1LT_9us = 0x43;
        public static readonly byte W1LT_10us = 0x45;
        public static readonly byte W1LT_11us = 0x47;
        public static readonly byte W1LT_12us = 0x49;
        public static readonly byte W1LT_13us = 0x4B;
        public static readonly byte W1LT_14us = 0x4D;
        public static readonly byte W1LT_15us = 0x4F;

        // Data Sample Offset and Write 0 Recovery Time, mks
        public static readonly byte DSO_3us = 0x51;
        public static readonly byte DSO_4us = 0x53;
        public static readonly byte DSO_5us = 0x55;
        public static readonly byte DSO_6us = 0x57;
        public static readonly byte DSO_7us = 0x59;
        public static readonly byte DSO_8us = 0x5B;
        public static readonly byte DSO_9us = 0x5D;
        public static readonly byte DSO_10us = 0x5F;

        // Load Sensor Threshold, mA
        public static readonly byte LOAD_1p8mA = 0x61;
        public static readonly byte LOAD_2p1mA = 0x63;
        public static readonly byte LOAD_2p4mA = 0x65;
        public static readonly byte LOAD_2p7mA = 0x67;
        public static readonly byte LOAD_3p0mA = 0x69;
        public static readonly byte LOAD_3p3mA = 0x6B;
        public static readonly byte LOAD_3p6mA = 0x6D;
        public static readonly byte LOAD_3p9mA = 0x6F;

        // RS232 Baud Rate, kbps
        public static readonly byte RBR_9p6kbps = 0x71;
        public static readonly byte RBR_19p2kbps = 0x73;
        public static readonly byte RBR_57p6kbps = 0x75;
        public static readonly byte RBR_115p2kbps = 0x77;

        /// <summary>
        /// Table 2. COMMUNICATION COMMAND RESPONSE
        /// </summary>
        /// <param name="data">Response</param>
        /// <returns>Response check result</returns>
        public static OneWireBusResetResponse CheckResetResponse(byte data)
        {
            if ((data & 0xDC) == 0xCC)
            {
                var bits01 = data & 0x03;
                switch (bits01)
                {
                    case 0x00: return OneWireBusResetResponse.OneWireShorted;
                    case 0x01: return OneWireBusResetResponse.PresencePulse;
                    case 0x02: return OneWireBusResetResponse.AlarmingPresencePulse;
                    case 0x03: return OneWireBusResetResponse.NoPresencePulse;
                    default: return OneWireBusResetResponse.InvalidResponse;
                }
            }

            return OneWireBusResetResponse.InvalidResponse;
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