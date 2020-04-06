# Digital Thermometer using DS2480B 1-Wire bus driver with DS18B20 digital temperature sensors
Windows desktop (WPF) application with basic support of 1-Wire devices, implemented as separate assembly.

### Technologies
Developed using MS Visual Studio 2017, C#, .NET Framework 4.5.2, WPF for UI. Application code separation according to MVVM pattern; unit tests are also included. 

### Getting started with demo application
* Connect any 1-Wire adapter, based on DS2480B chip to USB or Serial port on PC, connect one or several DS18B20 temperature sensors to 1-Wire port (in normal power mode, not parasite mode)
* Run application, select serial port from list, press 'Measure' button.

### Known issues and limitations
* Demo application runs on Windows only, due to WPF.

### Screenshot
![Demo screenshot](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerScreenshot.png)

### References
* [DS18B20 Programmable Resolution 1-Wire Digital Thermometer](https://www.maximintegrated.com/en/products/DS18B20)
* [DS2480B Serial to 1-Wire Line Driver](https://www.maximintegrated.com/en/products/DS2480B)