using System;
using System.Threading.Tasks;

namespace DigitalThermometer.OneWire
{
    /// <summary>
    /// Simple serial I/O implementation
    /// </summary>
    public interface ISerialConnection
    {
        /// <summary>
        /// Opens port
        /// </summary>
        /// <returns></returns>
        Task OpenPortAsync();

        /// <summary>
        /// Closes port
        /// </summary>
        /// <returns></returns>
        Task ClosePortAsync();

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