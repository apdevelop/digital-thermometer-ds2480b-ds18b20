using System;
using System.Threading.Tasks;

namespace DigitalThermometer.OneWire
{
    public interface ISerialConnection
    {
        void OpenPort();

        Task ClosePortAsync();

        Task TransmitDataAsync(byte[] data);

        event Action<byte[]> OnDataReceived;
    }
}