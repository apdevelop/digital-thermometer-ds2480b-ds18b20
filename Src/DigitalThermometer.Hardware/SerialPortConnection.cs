using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace DigitalThermometer.Hardware
{
    /// <summary>
    /// SerialPort wrapper
    /// </summary>
    public class SerialPortConnection : ISerialPortConnection, IDisposable
    {
        public event Action<byte[]> OnDataReceived;

        private SerialPort serialPort;

        private Thread thread_RxData;

        private Thread thread_TxData;

        public SerialPortConnection()
        {

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
            if (serialPort != null)
            {
                if (serialPort.IsOpen)
                {
                    serialPort.RtsEnable = value;
                }
            }
        }

        /// <summary>
        /// DTR output (pin 4 on DB9 RS232)
        /// </summary>
        /// <param name="value">(default) false = 1 = -U | true = 0 = +U</param>
        public void SetDtr(bool value)
        {
            if (serialPort != null)
            {
                if (serialPort.IsOpen)
                {
                    serialPort.DtrEnable = value;
                }
            }
        }

        public void OpenPort(string portName, int baudRate)
        {
            this.stop = false;
            this.IsConnected = false;

            this.DestroySerialPort();

            this.thread_RxData = null;
            this.thread_TxData = null;

            this.serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = baudRate,
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

        private readonly object portlocker = new object();

        private readonly object rxBufferLocker = new object();

        private readonly AutoResetEvent portByteReceivedWaitHandle = new AutoResetEvent(false);

        private readonly AutoResetEvent rxBufferWaitHandle = new AutoResetEvent(false);

        /// <summary>
        /// Input queue
        /// </summary>
        private readonly Queue<byte> rxBuffer = new Queue<byte>();

        public volatile bool IsConnected = false;

        #region ThreadProc_RxData

        private long rxDataIterations = 0L;

        private long rxDataRxBytes = 0L;

        private long RxDataIterations
        {
            get
            {
                return Interlocked.Read(ref rxDataIterations);
            }
        }

        private long RxDataRxBytes
        {
            get
            {
                return Interlocked.Read(ref rxDataRxBytes);
            }
        }

        #endregion

        private void ThreadProcRxData()
        {
            Interlocked.Exchange(ref rxDataIterations, 0L);
            Interlocked.Exchange(ref rxDataRxBytes, 0L);

            const int InputBufferSize = 1024;

            var inputBuffer = new byte[InputBufferSize];

            while (true)
            {
                Interlocked.Increment(ref rxDataIterations);

                if (stop)
                {
                    break;
                }

                try
                {
                    lock (this.portlocker)
                    {
                        if ((this.serialPort != null) && (this.serialPort.IsOpen))
                        {
                            var availibleBytes = this.serialPort.BytesToRead;
                            if (availibleBytes > 0)
                            {
                                var bytesToRead = Math.Min(availibleBytes, InputBufferSize);
                                var readedBytes = serialPort.Read(inputBuffer, 0, bytesToRead);
                                if (bytesToRead != readedBytes)
                                {
                                    throw new InvalidOperationException("readedBytes != bytesToRead");
                                }

                                Interlocked.Add(ref rxDataRxBytes, readedBytes);
                                lock (this.rxBufferLocker)
                                {
                                    for (var i = 0; i < readedBytes; i++)
                                    {
                                        this.rxBuffer.Enqueue(inputBuffer[i]);
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
                                throw new InvalidOperationException(String.Format("Port <{0}> is in invalid state (serialPort == null)", serialPort.PortName));
                            }
                            else
                            {
                                if (!this.serialPort.IsOpen)
                                {
                                    throw new InvalidOperationException(String.Format("Port <{0}> is in invalid state (serialPort.IsOpen == false)", serialPort.PortName));
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

        public void ClearRxBuffer()
        {
            lock (portlocker)
            {
                if ((serialPort != null) && (serialPort.IsOpen))
                {
                    try
                    {
                        serialPort.DiscardInBuffer();
                    }
                    catch (Exception ex)
                    {

                    }
                }

                lock (this.rxBufferLocker)
                {
                    if (rxBuffer.Count > 0)
                    {
                        rxBuffer.Clear();
                    }
                }
            }
        }

        #region Diagnostic counters

        private long _threadProc_TxData_ThreadId = 0L;

        private uint TxDataThreadId
        {
            get
            {
                return (uint)Interlocked.Read(ref _threadProc_TxData_ThreadId);
            }
        }

        private long txDataIterations = 0L;

        private long txDataTxBytes = 0L;

        private long ThreadProc_TxData_Iterations
        {
            get
            {
                return Interlocked.Read(ref txDataIterations);
            }
        }

        private long ThreadProc_TxData_TxBytes
        {
            get
            {
                return Interlocked.Read(ref txDataTxBytes);
            }
        }

        private int RxBufferCount
        {
            get
            {
                lock (this.rxBufferLocker)
                {
                    return rxBuffer.Count;
                }
            }
        }

        #endregion

        private void ThreadProcTxData()
        {
            Interlocked.Exchange(ref txDataIterations, 0L);
            Interlocked.Exchange(ref txDataTxBytes, 0L);

            while (true)
            {
                Interlocked.Increment(ref txDataIterations);

                if (stop)
                {
                    break;
                }

                if (this.OnDataReceived != null)
                {
                    byte[] buffer = null;

                    lock (this.rxBufferLocker)
                    {
                        if (rxBuffer.Count > 0)
                        {
                            buffer = rxBuffer.ToArray();
                            rxBuffer.Clear();
                        }
                    }

                    if (buffer != null)
                    {
                        this.OnDataReceived(buffer);
                        Interlocked.Increment(ref txDataTxBytes);
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

            lock (portlocker)
            {
                this.DestroySerialPort();
            }
        }

        public void TransmitData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data", "data is null");
            }

            if (data.Length == 0)
            {
                throw new ArgumentException("data", "data.Length = 0");
            }

            try
            {
                lock (portlocker)
                {
                    if ((serialPort != null) && (serialPort.IsOpen))
                    {
                        serialPort.Write(data, 0, data.Length);
                    }
                }
            }
            catch (Exception)
            {
                ClosePort();
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