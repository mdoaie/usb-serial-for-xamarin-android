/* Copyright 2011-2013 Google Inc.
 * Copyright 2013 mike wakerly <opensource@hoho.com>
 * Copyright 2015 Yasuyuki Hamada <yasuyuki_hamada@agri-info-design.com>
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301,
 * USA.
 *
 * Project home page: https://github.com/ysykhmd/usb-serial-for-xamarin-android
 * 
 * This project is based on usb-serial-for-android and ported for Xamarin.Android.
 * Original project home page: https://github.com/mik3y/usb-serial-for-android
 */

using System;
using System.Threading;
using Android.Hardware.Usb;

#if UseSmartThreadPool
using Amib.Threading;
#endif

namespace Aid.UsbSerial
{
    public abstract class UsbSerialPort
    {
        public const int DEFAULT_INTERNAL_READ_BUFFER_SIZE = 16 * 1024;
        public const int DEFAULT_TEMP_READ_BUFFER_SIZE = 16 * 1024;
        public const int DEFAULT_READ_BUFFER_SIZE = 16 * 1024;
        public const int DEFAULT_WRITE_BUFFER_SIZE = 16 * 1024;
        public const int DefaultBaudrate = 9600;
        public const int DefaultDataBits = 8;
        public const Parity DefaultParity = Parity.None;
        public const StopBits DefaultStopBits = StopBits.One;

        public event EventHandler<DataReceivedEventArgs> DataReceivedEventLinser;

        protected int mPortNumber;

        // non-null when open()
        protected UsbDeviceConnection Connection { get; set; }

        protected Object mInternalReadBufferLock = new Object();
        protected Object mReadBufferLock = new Object();
        protected Object mWriteBufferLock = new Object();

        /** Internal read buffer.  Guarded by {@link #mReadBufferLock}. */
        protected byte[] mInternalReadBuffer;
        protected byte[] mTempReadBuffer;
        protected byte[] mReadBuffer;
        protected int mReadBufferWriteCursor;
        protected int mReadBufferReadCursor;

        /** Internal write buffer.  Guarded by {@link #mWriteBufferLock}. */
        protected byte[] mWriteBuffer;

        private int mDataBits;

        private volatile bool _ContinueUpdating;
        public bool IsOpened { get; protected set; }
        public int Baudrate { get; set; }
        public int DataBits
        {
            get { return mDataBits; }
            set
            {
                if (value < 5 || 8 < value)
                    throw new ArgumentOutOfRangeException();
                mDataBits = value;
            }
        }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }

#if UseSmartThreadPool
        public SmartThreadPool ThreadPool { get; set; }
#endif

#if UseSmartThreadPool
        public UsbSerialPort(UsbManager manager, UsbDevice device, int portNumber, SmartThreadPool threadPool)
#else
        public UsbSerialPort(UsbManager manager, UsbDevice device, int portNumber)
#endif
        {
            Baudrate = DefaultBaudrate;
            DataBits = DefaultDataBits;
            Parity = DefaultParity;
            StopBits = DefaultStopBits;

            UsbManager = manager;
            UsbDevice = device;
            mPortNumber = portNumber;

            mInternalReadBuffer = new byte[DEFAULT_INTERNAL_READ_BUFFER_SIZE];
            mTempReadBuffer = new byte[DEFAULT_TEMP_READ_BUFFER_SIZE];
            mReadBuffer = new byte[DEFAULT_READ_BUFFER_SIZE];
            mReadBufferReadCursor = 0;
            mReadBufferWriteCursor = 0;
            mWriteBuffer = new byte[DEFAULT_WRITE_BUFFER_SIZE];

#if UseSmartThreadPool
            ThreadPool  = threadPool;
#endif
        }

        public override string ToString()
        {
            return string.Format("<{0} device_name={1} device_id={2} port_number={3}>", this.GetType().Name, UsbDevice.DeviceName, UsbDevice.DeviceId, mPortNumber);
        }

        public UsbManager UsbManager
        {
            get; private set;
        }

        /**
         * Returns the currently-bound USB device.
         *
         * @return the device
         */
        public UsbDevice UsbDevice
        {
            get; private set;
        }

        /**
         * Sets the size of the internal buffer used to exchange data with the USB
         * stack for read operations.  Most users should not need to change this.
         *
         * @param bufferSize the size in bytes
         */
        public void SetReadBufferSize(int bufferSize)
        {
            if (bufferSize == mInternalReadBuffer.Length)
            {
                return;
            }
            lock (mInternalReadBufferLock)
            {
                mInternalReadBuffer = new byte[bufferSize];
            }
        }

        /**
         * Sets the size of the internal buffer used to exchange data with the USB
         * stack for write operations.  Most users should not need to change this.
         *
         * @param bufferSize the size in bytes
         */
        public void SetWriteBufferSize(int bufferSize)
        {
            lock (mWriteBufferLock)
            {
                if (bufferSize == mWriteBuffer.Length)
                {
                    return;
                }
                mWriteBuffer = new byte[bufferSize];
            }
        }

        // Members of IUsbSerialPort

        public int PortNumber
        {
            get { return mPortNumber; }
        }

        /**
         * Returns the device serial number
         *  @return serial number
         */
        public string Serial
        {
            get { return Connection.Serial; }
        }


        public abstract void Open();

        public abstract void Close();


        protected void CreateConnection()
        {
            if (UsbManager != null && UsbDevice != null)
            {
                lock (mReadBufferLock)
                {
                    lock (mWriteBufferLock)
                    {
                        Connection = UsbManager.OpenDevice(UsbDevice);
                    }
                }
            }
        }


        protected void CloseConnection()
        {
            if (Connection != null)
            {
                lock (mReadBufferLock)
                {
                    lock (mWriteBufferLock)
                    {
                        Connection.Close();
                        Connection = null;
                    }
                }
            }
        }


        protected void StartUpdating()
        {
#if UseSmartThreadPool
            if (ThreadPool != null)
            {
                ThreadPool.QueueWorkItem(o => DoTasks());
            }
            else
            {
                System.Threading.ThreadPool.QueueUserWorkItem(o => DoTasks());
            }
#else
            ThreadPool.QueueUserWorkItem(o => DoTasks());
#endif
        }


        protected void StopUpdating()
        {
            _ContinueUpdating = false;
        }

#if UseSmartThreadPool
        private object DoTasks()
#else
        /*
         * mReadBuffer は mTempReadBuffer より大きいこと
         */
        private WaitCallback DoTasks()
#endif
        {
            _ContinueUpdating = true;
            while (_ContinueUpdating)
            {
                try
                {
                    int rxlen = ReadInternal(mTempReadBuffer, 0);
                    if (rxlen > 0)
                    {
                        lock (mReadBufferLock)
                        {
                            int remainBufferSize = DEFAULT_READ_BUFFER_SIZE - mReadBufferWriteCursor;

                            if (rxlen > remainBufferSize)
                            {
                                int secondLength;
                                System.Array.Copy(mTempReadBuffer, 0, mReadBuffer, mReadBufferWriteCursor, remainBufferSize);
                                secondLength = rxlen - remainBufferSize;
                                System.Array.Copy(mTempReadBuffer, remainBufferSize, mReadBuffer, 0, secondLength);
                                mReadBufferWriteCursor = secondLength;
                            }
                            else
                            {
                                System.Array.Copy(mTempReadBuffer, 0, mReadBuffer, mReadBufferWriteCursor, rxlen);
                                mReadBufferWriteCursor += rxlen;
                                if (DEFAULT_READ_BUFFER_SIZE == mReadBufferWriteCursor)
                                {
                                    mReadBufferWriteCursor = 0;
                                }
                            }
                        }
                        
                        if (DataReceivedEventLinser != null)
                        {
                            DataReceivedEventLinser(this, new DataReceivedEventArgs(this));
                        }
                    }
                }
                catch (SystemException e)
                {
                    _ContinueUpdating = false;
                    Close();
                }

                Thread.Sleep(1);
            }
            return null;
        }

        public int Read(byte[] dest, int startIndex)
        {
            int firstLength;
            int validDataLength = mReadBufferWriteCursor - mReadBufferReadCursor;

            /*
             * 以下は高速化のために意図的に関数分割していない
             */
            if (mReadBufferWriteCursor < mReadBufferReadCursor)
            {
                validDataLength += DEFAULT_READ_BUFFER_SIZE;
                if (validDataLength > dest.Length)
                {
                    validDataLength = dest.Length;
                }

                if (validDataLength + mReadBufferReadCursor > DEFAULT_READ_BUFFER_SIZE)
                {
                    firstLength = DEFAULT_READ_BUFFER_SIZE - mReadBufferReadCursor;

                    System.Array.Copy(mReadBuffer, mReadBufferReadCursor, dest, startIndex, firstLength);
                    System.Array.Copy(mReadBuffer, 0, dest, startIndex + firstLength, mReadBufferWriteCursor);
                    mReadBufferReadCursor = mReadBufferWriteCursor;
                }
                else
                {
                    System.Array.Copy(mReadBuffer, mReadBufferReadCursor, dest, startIndex, validDataLength);
                    mReadBufferReadCursor += validDataLength;
                    if (DEFAULT_READ_BUFFER_SIZE == mReadBufferReadCursor)
                    {
                        mReadBufferReadCursor = 0;
                    }
                }
            }
            else
            {
                if (validDataLength > dest.Length)
                {
                    validDataLength = dest.Length;
                }

                System.Array.Copy(mReadBuffer, mReadBufferReadCursor, dest, startIndex, validDataLength);
                mReadBufferReadCursor += validDataLength;
                if (DEFAULT_READ_BUFFER_SIZE == mReadBufferReadCursor)
                {
                    mReadBufferReadCursor = 0;
                }
            }

            return validDataLength;
        }

        public void ResetParameters()
        {
            SetParameters(Baudrate, DataBits, StopBits, Parity);
        }

        public void ResetBuffer()
        {
            Object thisLock = new Object();

            lock(thisLock)
            {
                mReadBufferReadCursor = 0;
                mReadBufferWriteCursor = 0;
            }
        }

        protected abstract int ReadInternal(byte[] dest, int timeoutMillis);

        public abstract int Write(byte[] src, int timeoutMillis);

        protected abstract void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity);

        public abstract bool CD { get; }

        public abstract bool Cts { get; }

        public abstract bool Dsr { get; }

        public abstract bool Dtr { get; set; }

        public abstract bool RI { get; }

        public abstract bool Rts { get; set; }

        public virtual bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers)
        {
            return !flushReadBuffers && !flushWriteBuffers;
        }
    }
}

