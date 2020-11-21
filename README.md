# Digital Thermometer using DS2480B 1-Wire bus driver with DS18B20 sensors
Windows desktop (WPF, .NET 4.5.2) and cross-platform console (.NET Core 2.1) applications for working with `DS18B20` 1-Wire digital thermometers connected to `DS2480B` 1-Wire bus controller (using "Flexible Speed" mode).

![Demo screenshot](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerScreenshot.png)

### Why DS2480B ?
The `DS2480B` Serial 1-Wire Driver (UART to 1-Wire bridge) allows fine tuning of bus signal parameters, which is necessary for working on long cables and/or with large number of devices on bus.

### Three implementations of demo application
* App uses .NET Framework 4.5.2 runs on Windows platform only, due to WPF. 
* ConsoleApp uses .NET Core 2.1, was tested on Raspberry PI 3 (Raspberry Pi OS Lite).
* AvaloniaApp uses .NET Core 2.1 and Avalonia UI Framework, was tested on Raspberry PI 3 (Raspberry Pi OS with desktop).

### Getting started with demo application
* Connect any 1-Wire bus adapter, based on `DS2480B` chip to USB or serial port on PC (For example, with FT232RL USB-UART adapter).
* Connect one or several `DS18B20` 1-Wire temperature sensors to 1-Wire bus (using three wires, i.e. in normal power mode, not parasite mode).
* Run application, select serial port from list, press 'Measure' button.
* Run console application using serial port name as first command line argument, for example:

`DigitalThermometer.ConsoleApp.exe COM5`

### Building .NET Core console application

On development system with VS2017 installed, execute the following command in the solution (`...\Src`) directory:

`dotnet publish -c Release -r win7-x64`

It will create the self-contained deployment (SCD) so that target system don't need to have .NET Core Runtime installed.
Output files will be placed into:

`...\Src\DigitalThermometer.ConsoleApp\bin\Release\netcoreapp2.1\win7-x64\publish\`

Similar steps are for building for Linux:

`dotnet publish -c Release -r linux-arm`

`...\Src\DigitalThermometer.ConsoleApp\bin\Release\netcoreapp2.1\linux-arm\publish\`

(There will be error on compiling `DigitalThermometer.App`, you can ignore them.)

### Running console application on Raspberry Pi (Raspberry Pi OS)
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

Check installed .NET Core runtime using command:

`dotnet --info`

The `Microsoft.NETCore.App 2.1.10` (or later version) should present in list.

Run console application with serial port name provided as command line argument:

`dotnet DigitalThermometer.ConsoleApp.dll /dev/ttyUSB0`

![Raspberry Pi OS Lite](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerConsoleRPi.png)

Run desktop application:

`dotnet DigitalThermometer.AvaloniaApp.dll`

![Raspberry Pi OS with desktop](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerAvaloniaAppRPi.png)

### Technologies
Developed using MS Visual Studio 2017, C#, .NET Framework 4.5.2, WPF and Avalonia for UI, NET Core 2.1. Application code separation according to MVVM pattern.
1-Wire functions are implemented in separate .NET Standard 1.0 assembly. Unit tests implemented using NUnit. 

### References
* [DS18B20 Programmable Resolution 1-Wire Digital Thermometer](https://www.maximintegrated.com/en/products/DS18B20)
* [DS2480B Serial to 1-Wire Line Driver](https://www.maximintegrated.com/en/products/DS2480B)
* [System.IO.Ports Nuget package](https://www.nuget.org/packages/System.IO.Ports/)
* [Download .NET Core](https://dotnet.microsoft.com/download/dotnet-core)