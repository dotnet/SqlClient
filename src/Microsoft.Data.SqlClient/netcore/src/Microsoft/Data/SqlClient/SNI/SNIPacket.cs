// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// #define TRACE_HISTORY // this is used for advanced debugging when you need to trace the entire lifetime of a single packet, be very careful with it

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI Packet
    /// </summary>
    internal sealed class SNIPacket
    {
        private int _dataLength; // the length of the data in the data segment, advanced by Append-ing data, does not include smux header length
        private int _dataCapacity; // the total capacity requested, if the array is rented this may be less than the _data.Length, does not include smux header length
        private int _dataOffset; // the start point of the data in the data segment, advanced by Take-ing data
        private int _headerLength; // the amount of space at the start of the array reserved for the smux header, this is zeroed in SetHeader
                                   // _headerOffset is not needed because it is always 0
        private byte[] _data;
        private SNIAsyncCallback _completionCallback;
        private readonly Action<Task<int>, object> _readCallback;
#if DEBUG
        internal readonly int _id;  // in debug mode every packet is assigned a unique id so that the entire lifetime can be tracked when debugging
        /// refcount = 0 means that a packet should only exist in the pool
        /// refcount = 1 means that a packet is active
        /// refcount > 1 means that a packet has been reused in some way and is a serious error
        internal int _refCount;
        internal readonly SNIHandle _owner; // used in debug builds to check that packets are being returned to the correct pool
        internal string _traceTag; // used in debug builds to assist tracing what steps the packet has been through

        [DebuggerDisplay("{Action.ToString(),nq}")]
        internal struct History
        {
            public enum Direction
            {
                Rent=0,
                Return=1,
            }

            public Direction Action;
            public int RefCount;
            public string Stack;
        }

        internal List<History> _history = null;

        /// <summary>
        /// uses the packet refcount in debug mode to identify if the packet is considered active
        /// it is an error to use a packet which is not active in any function outside the pool implementation
        /// </summary>
        public bool IsActive => _refCount == 1;

        public SNIPacket(SNIHandle owner,int id)
            : this()
        {
#if TRACE_HISTORY
            _history = new List<Activity>();
#endif
            _id = id;
            _owner = owner;
        }

        // the finalizer is only included in debug builds and is used to ensure that all packets are correctly recycled
        // it is not an error if a packet is dropped but it is undesirable so all efforts should be made to make sure we
        // do not drop them for the GC to pick up
        ~SNIPacket()
        {
            if (_data != null)
            {
                Debug.Fail($@"finalizer called for unreleased SNIPacket, tag: {_traceTag}");
            }
        }

#endif
        public SNIPacket()
        {
            _readCallback = ReadFromStreamAsyncContinuation;
        }

        /// <summary>
        /// Length of data left to process
        /// </summary>
        public int DataLeft => (_dataLength - _dataOffset);

        /// <summary>
        /// Indicates that the packet should be sent out of band bypassing the normal send-recieve lock
        /// </summary>
        public bool IsOutOfBand { get; set; }

        /// <summary>
        /// Length of data
        /// </summary>
        public int Length => _dataLength;

        /// <summary>
        /// Packet validity
        /// </summary>
        public bool IsInvalid => _data is null;

        public int ReservedHeaderSize => _headerLength;

        public bool HasCompletionCallback => !(_completionCallback is null);

        /// <summary>
        /// Set async completion callback
        /// </summary>
        /// <param name="completionCallback">Completion callback</param>
        public void SetCompletionCallback(SNIAsyncCallback completionCallback)
        {
            _completionCallback = completionCallback;
        }

        /// <summary>
        /// Invoke the completion callback 
        /// </summary>
        /// <param name="sniErrorCode">SNI error</param>
        public void InvokeCompletionCallback(uint sniErrorCode)
        {
            _completionCallback(this, sniErrorCode);
        }

        /// <summary>
        /// Allocate space for data
        /// </summary>
        /// <param name="headerLength">Length of packet header</param>
        /// <param name="dataLength">Length of byte array to be allocated</param>
        public void Allocate(int headerLength, int dataLength)
        {
            _data = ArrayPool<byte>.Shared.Rent(headerLength + dataLength);
            _dataCapacity = dataLength;
            _dataLength = 0;
            _dataOffset = 0;
            _headerLength = headerLength;
        }

        /// <summary>
        /// Read packet data into a buffer without removing it from the packet
        /// </summary>
        /// <param name="buffer">Buffer</param>
        /// <param name="dataSize">Number of bytes read from the packet into the buffer</param> 
        public void GetData(byte[] buffer, ref int dataSize)
        {
            Buffer.BlockCopy(_data, _headerLength, buffer, 0, _dataLength);
            dataSize = _dataLength;
        }

        /// <summary>
        /// Take data from another packet
        /// </summary>
        /// <param name="packet">Packet</param>
        /// <param name="size">Data to take</param>
        /// <returns>Amount of data taken</returns>
        public int TakeData(SNIPacket packet, int size)
        {
            int dataSize = TakeData(packet._data, packet._headerLength + packet._dataLength, size);
            packet._dataLength += dataSize;
            return dataSize;
        }

        /// <summary>
        /// Append data
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="size">Size</param>
        public void AppendData(byte[] data, int size)
        {
            Buffer.BlockCopy(data, 0, _data, _headerLength + _dataLength, size);
            _dataLength += size;
        }

        /// <summary>
        /// Read data from the packet into the buffer at dataOffset for zize and then remove that data from the packet
        /// </summary>
        /// <param name="buffer">Buffer</param>
        /// <param name="dataOffset">Data offset to write data at</param>
        /// <param name="size">Number of bytes to read from the packet into the buffer</param> 
        /// <returns></returns>
        public int TakeData(byte[] buffer, int dataOffset, int size)
        {
            if (_dataOffset >= _dataLength)
            {
                return 0;
            }

            if (_dataOffset + size > _dataLength)
            {
                size = _dataLength - _dataOffset;
            }

            Buffer.BlockCopy(_data, _headerLength + _dataOffset, buffer, dataOffset, size);
            _dataOffset += size;
            return size;
        }

        public Span<byte> GetHeaderBuffer(int headerSize)
        {
            Debug.Assert(_dataOffset == 0, "requested packet header buffer from partially consumed packet");
            Debug.Assert(headerSize > 0, "requested packet header buffer of 0 length");
            Debug.Assert(_headerLength == headerSize, "requested packet header of headerSize which is not equal to the _headerSize reservation");
            return _data.AsSpan(0, headerSize);
        }

        public void SetHeaderActive()
        {
            Debug.Assert(_headerLength > 0, "requested to set header active when it is not reserved or is already active");
            _dataCapacity += _headerLength;
            _dataLength += _headerLength;
            _headerLength = 0;
        }

        /// <summary>
        /// Release packet
        /// </summary>
        public void Release()
        {
            if (_data != null)
            {
                Array.Clear(_data, 0, _headerLength + _dataLength);
                ArrayPool<byte>.Shared.Return(_data, clearArray: false);

                _data = null;
                _dataCapacity = 0;
            }
            _dataLength = 0;
            _dataOffset = 0;
            _headerLength = 0;
            _completionCallback = null;
            IsOutOfBand = false;
        }

        /// <summary>
        /// Read data from a stream synchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public void ReadFromStream(Stream stream)
        {
            _dataLength = stream.Read(_data, _headerLength, _dataCapacity);
        }

        /// <summary>
        /// Read data from a stream asynchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <param name="callback">Completion callback</param>
        public void ReadFromStreamAsync(Stream stream, SNIAsyncCallback callback)
        {
            stream.ReadAsync(_data, 0, _dataCapacity, CancellationToken.None)
                .ContinueWith(
                    continuationAction: _readCallback,
                    state: callback,
                    CancellationToken.None,
                    TaskContinuationOptions.DenyChildAttach,
                    TaskScheduler.Default
                );
        }

        private void ReadFromStreamAsyncContinuation(Task<int> t, object state)
        {
            SNIAsyncCallback callback = (SNIAsyncCallback)state;
            bool error = false;
            Exception e = t.Exception?.InnerException;
            if (e != null)
            {
                SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.TCP_PROV, SNICommon.InternalExceptionError, e);
                error = true;
            }
            else
            {
                _dataLength = t.Result;

                if (_dataLength == 0)
                {
                    SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.ConnTerminatedError, string.Empty);
                    error = true;
                }
            }

            callback(this, error ? TdsEnums.SNI_ERROR : TdsEnums.SNI_SUCCESS);
        }

        /// <summary>
        /// Write data to a stream synchronously
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        public void WriteToStream(Stream stream)
        {
            stream.Write(_data, _headerLength, _dataLength);
        }

        /// <summary>
        /// Write data to a stream asynchronously
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        /// <param name="callback">SNI Asynchronous Callback</param>
        /// <param name="provider">SNI provider identifier</param>
        public async void WriteToStreamAsync(Stream stream, SNIAsyncCallback callback, SNIProviders provider)
        {
            uint status = TdsEnums.SNI_SUCCESS;
            try
            {
                await stream.WriteAsync(_data, 0, _dataLength, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                SNILoadHandle.SingletonInstance.LastError = new SNIError(provider, SNICommon.InternalExceptionError, e);
                status = TdsEnums.SNI_ERROR;
            }
            callback(this, status);
        }
    }
}
