using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using DigitalThermometer.Hardware;

namespace DigitalThermometer.App.Utils
{
    /// <summary>
    /// SerialPort wrapper
    /// </summary>
    public class SerialPortConnection : ISerialConnection
    {
        private readonly string serialPortName;

        private readonly int baudRate;

        private SerialPort serialPort;

        private Timer pollingTimer;

        private readonly SemaphoreSlim pollingTimerMutex = new SemaphoreSlim(1, 1);

        private volatile bool stopPending = false;

        private readonly SemaphoreSlim portMutex = new SemaphoreSlim(1, 1);

        private readonly SemaphoreSlim rxBufferMutex = new SemaphoreSlim(1, 1);

        public event Action<byte[]> OnDataReceived;

        private readonly List<byte> transmitQueue = new List<byte>();

        private readonly SemaphoreSlim transmitQueueMutex = new SemaphoreSlim(1, 1);

        private const int PollingPeriod = 10;

        /// <summary>
        /// Receive buffer
        /// </summary>
        private readonly Queue<byte> receiveBuffer = new Queue<byte>();

        public SerialPortConnection(string serialPortName, int baudRate)
        {
            this.serialPortName = serialPortName;
            this.baudRate = baudRate;
        }

        private void DestroySerialPort()
        {
            if (this.serialPort != null)
            {
                try
                {
                    if (this.serialPort.IsOpen)
                    {
                        this.serialPort.Close();
                    }
                }
                catch
                {

                }
                finally
                {
                    this.serialPort.Dispose();
                    this.serialPort = null;
                }
            }
        }

        /// <summary>
        /// RTS output (pin 7 on DB9 RS232)
        /// </summary>
        /// <param name="value">(default) false = 1 = -U | true = 0 = +U</param>
        public void SetRts(bool value)
        {
            if (this.serialPort != null)
            {
                if (this.serialPort.IsOpen)
                {
                    this.serialPort.RtsEnable = value;
                }
            }
        }

        /// <summary>
        /// DTR output (pin 4 on DB9 RS232)
        /// </summary>
        /// <param name="value">(default) false = 1 = -U | true = 0 = +U</param>
        public void SetDtr(bool value)
        {
            if (this.serialPort != null)
            {
                if (this.serialPort.IsOpen)
                {
                    this.serialPort.DtrEnable = value;
                }
            }
        }

        public void OpenPort()
        {
            this.stopPending = false;

            this.DestroySerialPort();

            this.serialPort = new SerialPort
            {
                PortName = this.serialPortName,
                BaudRate = this.baudRate,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                WriteBufferSize = 1024,
                WriteTimeout = 3000,
                ReadTimeout = 3000,
                ReceivedBytesThreshold = 1,
                DiscardNull = false,
            };

            this.serialPort.DataReceived += async (s, e) => await SerialPortDataReceivedAsync(s, e);

            this.serialPort.Open();

            this.pollingTimer = new Timer(async (e) =>
            {
                if (await this.pollingTimerMutex.WaitAsync(0))
                {
                    try
                    {
                        if (this.stopPending)
                        {
                            this.pollingTimer.Change(Timeout.Infinite, 0);
                        }
                        else
                        {
                            await this.PerformPollingAsync();
                        }
                    }
                    finally
                    {
                        this.pollingTimerMutex.Release();
                    }
                }
            }, null, PollingPeriod, PollingPeriod);
        }

        async Task SerialPortDataReceivedAsync(object sender, SerialDataReceivedEventArgs e)
        {
            if (await this.pollingTimerMutex.WaitAsync(0))
            {
                try
                {
                    if (!this.stopPending)
                    {
                        await this.PerformPollingAsync();
                    }
                }
                finally
                {
                    this.pollingTimerMutex.Release();
                }
            }
        }

        private async Task TransmitAsync()
        {
            try
            {
                await this.portMutex.WaitAsync();
                try
                {
                    if ((this.serialPort != null) && this.serialPort.IsOpen)
                    {
                        byte[] data = null;

                        await this.transmitQueueMutex.WaitAsync();
                        try
                        {
                            if (this.transmitQueue.Count > 0)
                            {
                                data = this.transmitQueue.ToArray();
                                this.transmitQueue.Clear();
                            }
                        }
                        finally
                        {
                            this.transmitQueueMutex.Release();
                        }

                        if (data != null)
                        {
                            this.serialPort.Write(data, 0, data.Length);
                        }
                    }
                }
                finally
                {
                    this.portMutex.Release();
                }
            }
            catch (Exception)
            {
                await this.ClosePortAsync();
            }
        }

        private async Task PerformPollingAsync()
        {
            await this.TransmitAsync();
            await this.ReceiveAsync();
            await this.DataReceivedEventDriverAsync();
        }

        byte[] inputBuffer = new byte[4096];

        private async Task ReceiveAsync()
        {
            try
            {
                await this.portMutex.WaitAsync();
                try
                {
                    if ((this.serialPort != null) && this.serialPort.IsOpen)
                    {
                        var availibleBytes = this.serialPort.BytesToRead;
                        if (availibleBytes > 0)
                        {
                            var bytesToRead = Math.Min(availibleBytes, inputBuffer.Length);
                            var readedBytes = this.serialPort.Read(inputBuffer, 0, bytesToRead);
                            if (bytesToRead != readedBytes)
                            {
                                throw new InvalidOperationException("readedBytes != bytesToRead");
                            }

                            await this.rxBufferMutex.WaitAsync();
                            try
                            {
                                for (var i = 0; i < readedBytes; i++)
                                {
                                    this.receiveBuffer.Enqueue(inputBuffer[i]);
                                }
                            }
                            finally
                            {
                                this.rxBufferMutex.Release();
                            }

                            await Task.Delay(1);
                        }
                    }
                    else
                    {
                        if (this.serialPort == null)
                        {
                            throw new InvalidOperationException($"Port <{serialPort.PortName}> is in invalid state (serialPort == null)");
                        }
                        else
                        {
                            if (!this.serialPort.IsOpen)
                            {
                                throw new InvalidOperationException($"Port <{serialPort.PortName}> is in invalid state (serialPort.IsOpen == false)");
                            }
                        }
                    }
                }
                finally
                {
                    this.portMutex.Release();
                }
            }
            catch (Exception)
            {
                await this.ClosePortAsync();
                return;
            }
        }

        public async Task ClosePortAsync()
        {
            this.stopPending = true;

            await this.portMutex.WaitAsync();
            try
            {
                this.DestroySerialPort();
            }
            finally
            {
                this.portMutex.Release();
            }
        }

        public async Task TransmitDataAsync(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "data is null");
            }

            if (data.Length == 0)
            {
                throw new ArgumentException("data.Length = 0", nameof(data));
            }

            // TX queue for non-blocking calling TransmitDataAsync
            await this.transmitQueueMutex.WaitAsync();
            try
            {
                this.transmitQueue.AddRange(data);
            }
            finally
            {
                this.transmitQueueMutex.Release();
            }
        }

        private async Task DataReceivedEventDriverAsync()
        {
            if (this.OnDataReceived != null) // TODO: another way to read data instead of callback event
            {
                byte[] buffer = null;

                await this.rxBufferMutex.WaitAsync();
                try
                {
                    if (this.receiveBuffer.Count > 0)
                    {
                        buffer = this.receiveBuffer.ToArray();
                        this.receiveBuffer.Clear();
                    }
                }
                finally
                {
                    this.rxBufferMutex.Release();
                }

                if (buffer != null)
                {
                    this.OnDataReceived(buffer);
                }
            }
        }
    }
}