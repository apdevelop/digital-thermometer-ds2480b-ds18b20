using System;
using System.Threading.Tasks;

namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// Basic methods for serial I/O implementation
    /// </summary>
    public interface ISerialConnection
    {
        /// <summary>
        /// Opens connection
        /// </summary>
        /// <returns></returns>
        Task OpenAsync();

        /// <summary>
        /// Closes connection
        /// </summary>
        /// <returns></returns>
        Task CloseAsync();

        /// <summary>
        /// Outputs data to port (to external device)
        /// </summary>
        /// <param name="data">Data to transmit</param>
        /// <returns></returns>
        Task TransmitDataAsync(byte[] data);

        /// <summary>
        /// Data received in port (from external device)
        /// </summary>
        event Action<byte[]> OnDataReceived;
    }
}