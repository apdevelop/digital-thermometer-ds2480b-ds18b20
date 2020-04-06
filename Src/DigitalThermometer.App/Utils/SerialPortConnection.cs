using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

using DigitalThermometer.Hardware;

namespace DigitalThermometer.App.Utils
{
    /// <summary>
    /// SerialPort wrapper
    /// </summary>
    public class SerialPortConnection : ISerialConnection, IDisposable
    {
        public event Action<byte[]> OnDataReceived;

        private SerialPort serialPort;

        private Thread thread_RxData;

        private Thread thread_TxData;

        private readonly string serialPortName;

        private readonly int baudRate;

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
                    this.serialPort.DataReceived -= this.SerialPortDataReceived;
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
            this.stop = false;
            this.IsConnected = false;

            this.DestroySerialPort();

            this.thread_RxData = null;
            this.thread_TxData = null;

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

            this.serialPort.DataReceived += SerialPortDataReceived;

            this.serialPort.Open();

            this.thread_RxData = new Thread(ThreadProcRxData)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "thread_RxData",
            };

            this.thread_RxData.Start();

            this.thread_TxData = new Thread(ThreadProcTxData)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "thread_TxData",
            };

            this.thread_TxData.Start();

            this.IsConnected = true;
        }

        void SerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            this.portByteReceivedWaitHandle.Set();
        }

        private volatile bool stop;

        private readonly object portMutex = new object(); // TODO: SemaphoreSlim

        private readonly object rxBufferMutex = new object(); // TODO: SemaphoreSlim

        private readonly AutoResetEvent portByteReceivedWaitHandle = new AutoResetEvent(false);

        private readonly AutoResetEvent rxBufferWaitHandle = new AutoResetEvent(false);

        /// <summary>
        /// Receive buffer
        /// </summary>
        private readonly Queue<byte> receiveBuffer = new Queue<byte>();

        public volatile bool IsConnected = false;

        private void ThreadProcRxData()
        {
            var inputBuffer = new byte[4096];

            while (true)
            {
                if (stop)
                {
                    break;
                }

                try
                {
                    lock (this.portMutex)
                    {
                        if ((this.serialPort != null) && this.serialPort.IsOpen)
                        {
                            var availibleBytes = this.serialPort.BytesToRead;
                            if (availibleBytes > 0)
                            {
                                var bytesToRead = Math.Min(availibleBytes, inputBuffer.Length);
                                var readedBytes = this.serialPort.Read(inputBuffer, 0, bytesToRead); // TODO: async
                                if (bytesToRead != readedBytes)
                                {
                                    throw new InvalidOperationException("readedBytes != bytesToRead");
                                }

                                lock (this.rxBufferMutex)
                                {
                                    for (var i = 0; i < readedBytes; i++)
                                    {
                                        this.receiveBuffer.Enqueue(inputBuffer[i]);
                                    }
                                }

                                this.rxBufferWaitHandle.Set();
                                continue;
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
                }
                catch (Exception)
                {
                    this.ClosePort(true);
                    break;
                }

                this.portByteReceivedWaitHandle.WaitOne(1);
            }

            IsConnected = false;
        }

        private void ThreadProcTxData()
        {
            while (true)
            {
                if (stop)
                {
                    break;
                }

                if (this.OnDataReceived != null)
                {
                    byte[] buffer = null;

                    lock (this.rxBufferMutex)
                    {
                        if (this.receiveBuffer.Count > 0)
                        {
                            buffer = this.receiveBuffer.ToArray();
                            this.receiveBuffer.Clear();
                        }
                    }

                    if (buffer != null)
                    {
                        this.OnDataReceived(buffer);
                    }
                }

                this.rxBufferWaitHandle.WaitOne(1);
            }
        }

        public void ClosePort(bool self = false)
        {
            IsConnected = false;
            stop = true;

            if (thread_TxData != null)
            {
                if (!thread_TxData.Join(1000))
                {

                }

                thread_TxData = null;
            }

            if (!self)
            {
                if (thread_RxData != null)
                {
                    if (!thread_RxData.Join(1000))
                    {

                    }

                    thread_RxData = null;
                }
            }

            lock (this.portMutex)
            {
                this.DestroySerialPort();
            }
        }

        public void TransmitData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "data is null");
            }

            if (data.Length == 0)
            {
                throw new ArgumentException(nameof(data), "data.Length = 0");
            }

            try
            {
                lock (this.portMutex)
                {
                    if ((this.serialPort != null) && (this.serialPort.IsOpen))
                    {
                        this.serialPort.Write(data, 0, data.Length); // TODO: async
                    }
                }
            }
            catch (Exception)
            {
                this.ClosePort();
            }
        }

        public void Dispose()
        {
            this.ClosePort();

            this.portByteReceivedWaitHandle.Close();
            this.rxBufferWaitHandle.Close();
        }
    }
}