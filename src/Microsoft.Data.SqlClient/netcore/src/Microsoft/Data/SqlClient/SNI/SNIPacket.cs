// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

 // #define TRACE_HISTORY // this is used for advanced debugging when you need to trace the entire lifetime of a single packet, be very careful with it

using System;
using System.Buffers;
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
        private static readonly Action<Task<int>, object> s_readCallback = ReadFromStreamAsyncContinuation;
        private int _dataLength; // the length of the data in the data segment, advanced by Append-ing data, does not include smux header length
        private int _dataCapacity; // the total capacity requested, if the array is rented this may be less than the _data.Length, does not include smux header length
        private int _dataOffset; // the start point of the data in the data segment, advanced by Take-ing data
        private int _headerLength; // the amount of space at the start of the array reserved for the smux header, this is zeroed in SetHeader
                                   // _headerOffset is not needed because it is always 0
        private byte[] _data;
        private SNIAsyncCallback _asyncIOCompletionCallback;
#if DEBUG
        internal readonly int _id;  // in debug mode every packet is assigned a unique id so that the entire lifetime can be tracked when debugging
        /// refcount = 0 means that a packet should only exist in the pool
        /// refcount = 1 means that a packet is active
        /// refcount > 1 means that a packet has been reused in some way and is a serious error
        internal int _refCount;
        internal readonly SNIHandle _owner; // used in debug builds to check that packets are being returned to the correct pool
        internal string _traceTag; // used in debug builds to assist tracing what steps the packet has been through

#if TRACE_HISTORY
        [DebuggerDisplay("{Action.ToString(),nq}")]
        internal struct History
        {
            public enum Direction
            {
                Rent = 0,
                Return = 1,
            }

            public Direction Action;
            public int RefCount;
            public string Stack;
        }
      
        internal List<History> _history = null;
#endif

        /// <summary>
        /// uses the packet refcount in debug mode to identify if the packet is considered active
        /// it is an error to use a packet which is not active in any function outside the pool implementation
        /// </summary>
        public bool IsActive => _refCount == 1;

        public SNIPacket(SNIHandle owner, int id)
            : this()
        {
#if TRACE_HISTORY
            _history = new List<History>();
#endif
            _id = id;
            _owner = owner;
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} instantiated,", args0: _owner?.ConnectionId, args1: _id);
        }

        // the finalizer is only included in debug builds and is used to ensure that all packets are correctly recycled
        // it is not an error if a packet is dropped but it is undesirable so all efforts should be made to make sure we
        // do not drop them for the GC to pick up
        ~SNIPacket()
        {
            if (_data != null)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.ERR, "Finalizer called for unreleased SNIPacket, Connection Id {0}, Packet Id {1}, _refCount {2}, DataLeft {3}, tag {4}", args0: _owner?.ConnectionId, args1: _id, args2: _refCount, args3: DataLeft, args4: _traceTag);
            }
        }

#endif
        public SNIPacket()
        {
        }

        /// <summary>
        /// Length of data left to process
        /// </summary>
        public int DataLeft => (_dataLength - _dataOffset);

        /// <summary>
        /// Indicates that the packet should be sent out of band bypassing the normal send-receive lock
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

        public bool HasAsyncIOCompletionCallback => _asyncIOCompletionCallback is not null;

        /// <summary>
        /// Set async receive callback
        /// </summary>
        /// <param name="asyncIOCompletionCallback">Completion callback</param>
        public void SetAsyncIOCompletionCallback(SNIAsyncCallback asyncIOCompletionCallback) => _asyncIOCompletionCallback = asyncIOCompletionCallback;

        /// <summary>
        /// Invoke the receive callback
        /// </summary>
        /// <param name="sniErrorCode">SNI error</param>
        public void InvokeAsyncIOCompletionCallback(uint sniErrorCode) => _asyncIOCompletionCallback(this, sniErrorCode);

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
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} allocated with _headerLength {2}, _dataCapacity {3}", args0: _owner?.ConnectionId, args1: _id, args2: _headerLength, args3: _dataCapacity);
#endif
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
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} took data from Packet Id {2} dataSize {3}, _dataLength {4}", args0: _owner?.ConnectionId, args1: _id, args2: packet?._id, args3: dataSize, args4: packet._dataLength);
#endif
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
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} was appended with size {2}, _dataLength {3}", args0: _owner?.ConnectionId, args1: _id, args2: size, args3: _dataLength);
#endif
        }

        /// <summary>
        /// Read data from the packet into the buffer at dataOffset for size and then remove that data from the packet
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
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} took data size {2}, _dataLength {3}, _dataOffset {4}", args0: _owner?.ConnectionId, args1: _id, args2: size, args3: _dataLength, args4: _dataOffset);
#endif
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
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} _dataLength {2} header set to active.", args0: _owner?.ConnectionId, args1: _id, args2: _dataLength);
#endif
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
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} _headerLength {2} and _dataLength {3} released.", args0: _owner?.ConnectionId, args1: _id, args2: _headerLength, args3: _dataLength);
#endif
            _dataLength = 0;
            _dataOffset = 0;
            _headerLength = 0;
            _asyncIOCompletionCallback = null;
            IsOutOfBand = false;
        }

        /// <summary>
        /// Read data from a stream synchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public void ReadFromStream(Stream stream)
        {
            _dataLength = stream.Read(_data, _headerLength, _dataCapacity);
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} _dataLength {2} read from stream.", args0: _owner?.ConnectionId, args1: _id, args2: _dataLength);
#endif
        }

        /// <summary>
        /// Read data from a stream asynchronously
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        public void ReadFromStreamAsync(Stream stream)
        {
            stream.ReadAsync(_data, 0, _dataCapacity, CancellationToken.None)
                .ContinueWith(
                    continuationAction: s_readCallback,
                    state: this,
                    CancellationToken.None,
                    TaskContinuationOptions.DenyChildAttach,
                    TaskScheduler.Default
                );
        }

        private static void ReadFromStreamAsyncContinuation(Task<int> task, object state)
        {
            SNIPacket packet = (SNIPacket)state;
            bool error = false;
            Exception e = task.Exception?.InnerException;
            if (e != null)
            {
                SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.TCP_PROV, SNICommon.InternalExceptionError, e);
#if DEBUG
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.ERR, "Connection Id {0}, Internal Exception occurred while reading data: {1}", args0: packet._owner?.ConnectionId, args1: e?.Message);
#endif
                error = true;
            }
            else
            {
                packet._dataLength = task.Result;
#if DEBUG
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} _dataLength {2} read from stream.", args0: packet._owner?.ConnectionId, args1: packet._id, args2: packet._dataLength);
#endif
                if (packet._dataLength == 0)
                {
                    SNILoadHandle.SingletonInstance.LastError = new SNIError(SNIProviders.TCP_PROV, 0, SNICommon.ConnTerminatedError, Strings.SNI_ERROR_2);
#if DEBUG
                    SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.ERR, "Connection Id {0}, No data read from stream, connection was terminated.", args0: packet._owner?.ConnectionId);
#endif
                    error = true;
                }
            }

            packet.InvokeAsyncIOCompletionCallback(error ? TdsEnums.SNI_ERROR : TdsEnums.SNI_SUCCESS);
        }

        /// <summary>
        /// Write data to a stream synchronously
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        public void WriteToStream(Stream stream)
        {
            stream.Write(_data, _headerLength, _dataLength);
#if DEBUG
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} _dataLength {2} written to stream.", args0: _owner?.ConnectionId, args1: _id, args2: _dataLength);
#endif
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
#if DEBUG
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.INFO, "Connection Id {0}, Packet Id {1} _dataLength {2} written to stream.", args0: _owner?.ConnectionId, args1: _id, args2: _dataLength);
#endif
            }
            catch (Exception e)
            {
                SNILoadHandle.SingletonInstance.LastError = new SNIError(provider, SNICommon.InternalExceptionError, e);
#if DEBUG
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SNIPacket), EventType.ERR, "Connection Id {0}, Internal Exception occurred while writing data: {1}", args0: _owner?.ConnectionId, args1: e?.Message);
#endif
                status = TdsEnums.SNI_ERROR;
            }
            callback(this, status);
        }
    }
}
