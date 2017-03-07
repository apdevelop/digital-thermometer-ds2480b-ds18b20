using System;

namespace DigitalThermometer.Hardware
{
    public interface ISerialPortConnection
    {
        void OpenPort(string serialPortName, int baudRate);

        void ClosePort(bool self = false);

        void SetDtr(bool value);

        void SetRts(bool value);

        void TransmitData(byte[] data);

        event Action<byte[]> OnDataReceived;
    }
}
