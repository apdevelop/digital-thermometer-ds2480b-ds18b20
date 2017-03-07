# Digital Thermometer using DS2480B 1-Wire bus driver with DS18B20 digital temperature sensors
Windows desktop application with basic hardware support implementation in separate assembly.

### Technologies
Developed using MS Visual Studio 2013, C# .NET 4.0, WPF for UI. Code separation according to MVVM pattern; unit tests are also included. 

### Getting started with demo application
* Connect any 1-Wire adapter, based on DS2480B chip to USB or Serial port on PC, connect one or several DS18B20 temperature sensors to 1-Wire port (in normal power mode, not parasite mode)
* Run application, select serial port from list, press 'Measure' button.

### Known issues and limitations
* Using .NET 4.0 without modern async/await programming model.
* Demo application runs on Windows only, due to WPF.

### Screenshot
![Demo screenshot](https://github.com/apdevelop/digital-thermometer-ds2480b-ds18b20/blob/master/Docs/DigitalThermometerScreenshot.png)

### References
* [DS18B20 Programmable Resolution 1-Wire Digital Thermometer](https://www.maximintegrated.com/en/products/analog/sensors-and-sensor-interface/DS18B20.html)
* [DS2480B Serial to 1-Wire Line Driver](https://www.maximintegrated.com/en/products/interface/controllers-expanders/DS2480B.html)