using System;
using System.Collections.Generic;
using System.IO;

namespace DigitalThermometer.App.Utils
{
    public class SerialPortUtils
    {
        public static IList<string> GetSerialPortNames(bool sort = true)
        {
            string[] portnames = null;

            try
            {
                portnames = System.IO.Ports.SerialPort.GetPortNames();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new IOException($"Error getting list of serial ports: {ex.Message}, error code {ex.ErrorCode:X8}");
            }

            if (sort)
            {
                if ((portnames != null) && (portnames.Length > 0))
                {
                    Array.Sort<string>(portnames, StringLogicalComparer.Compare);
                }
            }

            return portnames;
        }
    }
}
