# Digital Thermometer using DS2480B 1-Wire bus driver with DS18B20 sensors
Windows desktop (.NET 4.5.2 / WPF) and cross-platform (.NET 7.0 / AvaloniaUI) applications for working with `DS18B20` 1-Wire digital thermometers connected to `DS2480B` 1-Wire bus controller (using "Flexible Speed" mode).

![Demo screenshot](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerScreenshot.png)

### Why DS2480B ?
The `DS2480B` Serial 1-Wire Driver (UART to 1-Wire bridge) allows fine tuning of bus signal parameters, which is necessary for working on long cables and/or with large number of devices on bus.

### Projects and solutions

| Project     | Platform                    |Target Framework| VS 2019  | VS 2022  |
|-------------|-----------------------------|--------------- |:--------:|:--------:|
| App         | Windows-only, desktop (WPF) | net452         | &#10003; | &mdash;  | 
| UnitTests   | Windows (NUnit)             | net452         | &#10003; | &mdash;  | 
| OneWire     | Cross-platform shared lib   | netstandard1.0 | &#10003; | &#10003; | 
| AvaloniaApp | Cross-platform, desktop     | net7.0         | &mdash;  | &#10003; |
| ConsoleApp  | Cross-platform, console     | net7.0         | &mdash;  | &#10003; |

### Getting started with demo application
* Connect any 1-Wire bus adapter, based on `DS2480B` chip to USB or serial port on PC (For example, with FT232RL USB-UART adapter).
* Connect one or several `DS18B20` 1-Wire temperature sensors to 1-Wire bus (using three wires, i.e. in normal power mode, not parasite mode).
* Run application, select serial port from list, press 'Search' button, then 'Measure' button.
* Run console application using serial port name as first command line argument, for example:

`DigitalThermometer.ConsoleApp.exe COM5`

### Building self-contained .NET 7.0 applications

On development system with VS2022 installed, execute the following command in the solution (`...\Src`) directory:

`dotnet publish DigitalThermometer.VS2022.sln -c Release -r win7-x64 --self-contained true`

It will create the self-contained deployment (SCD) so that target system don't need to have .NET 6.0 Runtime installed.
Output files will be placed into:

`...\Src\DigitalThermometer.ConsoleApp\bin\Release\net7.0\win7-x64\publish\`

Similar steps are for building for Linux:

`dotnet publish DigitalThermometer.VS2022.sln -c Release -r linux-arm --self-contained true`

`...\Src\DigitalThermometer.ConsoleApp\bin\Release\net7.0\linux-arm\publish\`

### Running console application on Linux

May require superuser rights to access serial port.

#### Raspbian OS specific

Run `sudo raspi-config` then select `Interfacing Options` in menu, then select `Serial`. 
Select `No` to `login shell to be accessible over serial`.
Select `Yes` to `serial port hardware to be enabled`.
Select `Finish` in main menu and then reboot system.


#### Self-contained deployment
Copy contents of `publish` console application directory to RPi (for example into `/home/pi/dt` directory).
Set permissions (execute access) to startup file (note that filename is case-sensitive and has no extension)

`chmod +x DigitalThermometer.ConsoleApp`

Run application:

`./DigitalThermometer.ConsoleApp`

It will output list of serial ports in system:
`Serial ports: /dev/ttyAMA0 /dev/ttyS0 /dev/ttyUSB0`

The `/dev/ttyUSB0` in this example is virtual serial port of `FT232RL` chip of USB-UART adapter.

Run application with serial port name provided as command line argument:

`./DigitalThermometer.ConsoleApp /dev/ttyUSB0`

#### Framework-dependent deployment

Check installed .NET 7.0 runtime using command:

`dotnet --info`

The `Microsoft.NETCore.App 7.0.8` (or later version) must present in list.

Run console application with serial port name provided as command line argument:

`dotnet DigitalThermometer.ConsoleApp.dll /dev/ttyUSB0`

![Raspberry Pi OS Lite](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerConsoleRPi.png)

Run desktop application:

`dotnet DigitalThermometer.AvaloniaApp.dll`

![Raspberry Pi OS with desktop](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerAvaloniaAppRPi.png)


### Technologies
Developed using MS Visual Studio 2019 / 2022, C#, .NET Framework 4.5.2 / .NET 7.0, WPF / AvaloniaUI. Application code separation according to MVVM pattern.
1-Wire functions are implemented in separate .NET Standard 1.0 assembly. Unit tests implemented using NUnit. 

### References
* [DS18B20 Programmable Resolution 1-Wire Digital Thermometer](https://www.maximintegrated.com/en/products/DS18B20)
* [DS2480B Serial to 1-Wire Line Driver](https://www.maximintegrated.com/en/products/DS2480B)
* [Download .NET 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
* [AvaloniaUI Nuget package](https://www.nuget.org/packages/Avalonia/)
* [System.IO.Ports Nuget package](https://www.nuget.org/packages/System.IO.Ports/)
