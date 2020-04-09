using System;
using System.Threading.Tasks;

using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var portNames = System.IO.Ports.SerialPort.GetPortNames();
            Console.WriteLine($"Serial ports: {String.Join(" ", portNames)}");

            var portName = String.Empty;
            if (args.Length == 1)
            {
                portName = args[0];
                Console.WriteLine($"Using port: {portName}");
            }
            else
            {
                portName = portNames[0];
            }

            MainAsync(portName).GetAwaiter().GetResult();

            Console.ReadLine();
        }

        static async Task MainAsync(string serialPortName)
        {
            var portConnection = new SerialPortConnection(serialPortName, 9600); // TODO: const
            var busMaster = new OW.OneWireMaster(portConnection);

            var busResult = await busMaster.OpenAsync();
            if (busResult != OW.OneWireBusResetResponse.PresencePulse)
            {
                Console.WriteLine($"1-Wire bus error: {busResult} (serial port: {serialPortName})");
                await busMaster.CloseAsync();
                return;
            }

            var counter = 0;
            Console.WriteLine();
            var list = await busMaster.SearchDevicesOnBusAsync((romCode) =>
            {
                counter++;
                Console.WriteLine($"[{counter:D3}] {OW.Utils.RomCodeToLEString(romCode)}");
            });

            if (list.Count > 0)
            {
                Console.WriteLine();
                foreach (var romCode in list)
                {
                    try
                    {
                        var r = await busMaster.PerformDS18B20TemperatureMeasurementAsync(romCode);
                        var v = new SensorStateViewModel(r);
                        Console.WriteLine($"{OW.Utils.RomCodeToLEString(romCode)}  {v.TemperatureValueString}{(char)176}C  [{v.RawDataString}]  CRC={v.ComputedCrcString}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{OW.Utils.RomCodeToLEString(romCode)} {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine("Sensors are not found on 1-Wire bus");
            }

            await busMaster.CloseAsync();
        }
    }
}