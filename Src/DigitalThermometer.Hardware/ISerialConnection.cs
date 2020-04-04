using System;

namespace DigitalThermometer.Hardware
{
    public interface ISerialConnection
    {
        void OpenPort();

        void ClosePort(bool self = false);

        void TransmitData(byte[] data);

        event Action<byte[]> OnDataReceived;
    }
}