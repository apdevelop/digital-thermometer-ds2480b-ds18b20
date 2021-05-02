using System;
using System.Threading.Tasks;

using OW = DigitalThermometer.OneWire;

namespace DigitalThermometer.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                MainAsync(args[0]).GetAwaiter().GetResult();
            }
            else
            {
                Console.WriteLine($"Usage: dotnet {System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.dll <SerialPort>");
                var portNames = System.IO.Ports.SerialPort.GetPortNames();
                Console.WriteLine($"Found serial ports:");
                Console.WriteLine($"{String.Join(Environment.NewLine, portNames)}");
            }
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

            var sensors = await busMaster.SearchDevicesOnBusAsync();
            if (sensors.Count > 0)
            {
                Console.WriteLine($"Found DS18B20: {sensors.Count}");
                foreach (var romCode in sensors)
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
                Console.WriteLine("DS18B20 were not found on 1-Wire bus");
            }

            await busMaster.CloseAsync();
        }
    }
}
