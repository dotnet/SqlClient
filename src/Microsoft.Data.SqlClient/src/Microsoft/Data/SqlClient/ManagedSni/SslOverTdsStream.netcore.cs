// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SSL encapsulated over TDS transport. During SSL handshake, SSL packets are
    /// transported in TDS packet type 0x12. Once SSL handshake has completed, SSL
    /// packets are sent transparently.
    /// </summary>
    internal sealed partial class SslOverTdsStream : Stream
    {
        private readonly Stream _stream;
        private Guid _connectionId;

        private int _packetBytes = 0;
        private bool _encapsulate;

        private const int PACKET_SIZE_WITHOUT_HEADER = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE - TdsEnums.HEADER_LEN;
        private const int PRELOGIN_PACKET_TYPE = 0x12;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="stream">Underlying stream</param>
        public SslOverTdsStream(Stream stream) : this(stream, default) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="stream">Underlying stream</param>
        /// <param name="connectionId">Connection Id of parent stream handle</param>
        public SslOverTdsStream(Stream stream, Guid connectionId = default)
        {
            _stream = stream;
            _connectionId = connectionId;
            _encapsulate = true;
        }

        /// <summary>
        /// Finish SSL handshake. Stop encapsulating in TDS.
        /// </summary>
        public void FinishHandshake()
        {
            _encapsulate = false;
            SqlClientEventSource.Log.TrySNITraceEvent(nameof(SslOverTdsStream), EventType.INFO, "Connection Id {0}, Switched from encapsulation to passthrough mode", args0: _connectionId);
        }

        /// <summary>
        /// Set stream length. 
        /// </summary>
        /// <param name="value">Length</param>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// Flush stream
        /// </summary>
        public override void Flush()
        {
            // Can sometimes get Pipe broken errors from flushing a PipeStream.
            // PipeStream.Flush() also doesn't do anything, anyway.
            if (!(_stream is PipeStream))
            {
                _stream.Flush();
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SslOverTdsStream), EventType.INFO, "Connection Id {0}, Flushed stream", args0: _connectionId);
            }
        }

        /// <summary>
        /// Get/set stream position
        /// </summary>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Seek in stream
        /// </summary>
        /// <param name="offset">Offset</param>
        /// <param name="origin">Origin</param>
        /// <returns>Position</returns>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// Check if stream can be read from
        /// </summary>
        public override bool CanRead => _stream.CanRead;

        /// <summary>
        /// Check if stream can be written to
        /// </summary>
        public override bool CanWrite => _stream.CanWrite;

        /// <summary>
        /// Check if stream can seek
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// Get stream length
        /// </summary>
        public override long Length => throw new NotSupportedException();

        #region Public Methods

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            using (SqlClientSNIEventScope.Create(nameof(SslOverTdsStream)))
            {
                if (!_encapsulate)
                {
                    return _stream.Read(buffer);
                }

                if (_packetBytes > 0)
                {
                    // there are queued bytes from a previous packet available
                    // work out how many of the remaining bytes we can consume
                    int wantedCount = Math.Min(buffer.Length, _packetBytes);
                    int readCount = _stream.Read(buffer.Slice(0, wantedCount));
                    if (readCount == 0)
                    {
                        // 0 means the connection was closed, tell the caller
                        return 0;
                    }
                    _packetBytes -= readCount;
                    return readCount;
                }
                else
                {
                    Span<byte> headerBytes = stackalloc byte[TdsEnums.HEADER_LEN];

                    // fetch the packet header to determine how long the packet is
                    int headerBytesRead = 0;
                    do
                    {
                        int headerBytesReadIteration = _stream.Read(headerBytes.Slice(headerBytesRead, TdsEnums.HEADER_LEN - headerBytesRead));
                        if (headerBytesReadIteration == 0)
                        {
                            // 0 means the connection was closed, tell the caller
                            return 0;
                        }
                        headerBytesRead += headerBytesReadIteration;
                    } while (headerBytesRead < TdsEnums.HEADER_LEN);

                    // read the packet data size from the header and store it in case it is needed for a subsequent call
                    _packetBytes = ((headerBytes[TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8) | headerBytes[TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - TdsEnums.HEADER_LEN;

                    // read as much from the packet as the caller can accept
                    int packetBytesRead = _stream.Read(buffer.Slice(0, Math.Min(buffer.Length, _packetBytes)));
                    _packetBytes -= packetBytesRead;
                    return packetBytesRead;
                }
            }
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using (SqlClientSNIEventScope.Create(nameof(SslOverTdsStream)))
            {
                if (!_encapsulate)
                {
                    int read;
                    {
                        ValueTask<int> readValueTask = _stream.ReadAsync(buffer, cancellationToken);
                        if (readValueTask.IsCompletedSuccessfully)
                        {
                            read = readValueTask.Result;
                        }
                        else
                        {
                            read = await readValueTask.ConfigureAwait(false);
                        }
                    }
                    return read;
                }

                if (_packetBytes > 0)
                {
                    // there are queued bytes from a previous packet available
                    // work out how many of the remaining bytes we can consume
                    int wantedCount = Math.Min(buffer.Length, _packetBytes);

                    int readCount;
                    {
                        ValueTask<int> remainderReadValueTask = _stream.ReadAsync(buffer.Slice(0, wantedCount), cancellationToken);
                        if (remainderReadValueTask.IsCompletedSuccessfully)
                        {
                            readCount = remainderReadValueTask.Result;
                        }
                        else
                        {
                            readCount = await remainderReadValueTask.ConfigureAwait(false);
                        }
                    }
                    if (readCount == 0)
                    {
                        // 0 means the connection was closed, tell the caller
                        return 0;
                    }
                    _packetBytes -= readCount;
                    return readCount;
                }
                else
                {
                    byte[] headerBytes = ArrayPool<byte>.Shared.Rent(TdsEnums.HEADER_LEN);

                    // fetch the packet header to determine how long the packet is
                    int headerBytesRead = 0;
                    do
                    {
                        int headerBytesReadIteration;
                        {
                            ValueTask<int> headerReadValueTask = _stream.ReadAsync(headerBytes.AsMemory(headerBytesRead, (TdsEnums.HEADER_LEN - headerBytesRead)), cancellationToken);
                            if (headerReadValueTask.IsCompletedSuccessfully)
                            {
                                headerBytesReadIteration = headerReadValueTask.Result;
                            }
                            else
                            {
                                headerBytesReadIteration = await headerReadValueTask.ConfigureAwait(false);
                            }
                        }
                        if (headerBytesReadIteration == 0)
                        {
                            // 0 means the connection was closed, cleanup the rented array and then tell the caller
                            ArrayPool<byte>.Shared.Return(headerBytes, clearArray: true);
                            return 0;
                        }
                        headerBytesRead += headerBytesReadIteration;
                    } while (headerBytesRead < TdsEnums.HEADER_LEN);

                    // read the packet data size from the header and store it in case it is needed for a subsequent call
                    _packetBytes = ((headerBytes[TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8) | headerBytes[TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - TdsEnums.HEADER_LEN;

                    ArrayPool<byte>.Shared.Return(headerBytes, clearArray: true);

                    // read as much from the packet as the caller can accept
                    int packetBytesRead;
                    {
                        ValueTask<int> packetReadValueTask = _stream.ReadAsync(buffer.Slice(0, Math.Min(buffer.Length, _packetBytes)), cancellationToken);
                        if (packetReadValueTask.IsCompletedSuccessfully)
                        {
                            packetBytesRead = packetReadValueTask.Result;
                        }
                        else
                        {
                            packetBytesRead = await packetReadValueTask.ConfigureAwait(false);
                        }
                    }
                    _packetBytes -= packetBytesRead;
                    return packetBytesRead;
                }
            }
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            using (SqlClientSNIEventScope.Create(nameof(SslOverTdsStream)))
            {
                // During the SSL negotiation phase, SSL is tunnelled over TDS packet type 0x12. After
                // negotiation, the underlying socket only sees SSL frames.
                if (!_encapsulate)
                {
                    _stream.Write(buffer);
                    _stream.Flush();
                    return;
                }

                ReadOnlySpan<byte> remaining = buffer;
                byte[] packetBuffer = null;
                try
                {
                    while (remaining.Length > 0)
                    {
                        int dataLength = Math.Min(PACKET_SIZE_WITHOUT_HEADER, remaining.Length);
                        int packetLength = TdsEnums.HEADER_LEN + dataLength;

                        if (packetBuffer == null)
                        {
                            packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                        }
                        else if (packetBuffer.Length < packetLength)
                        {
                            ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                            packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                        }

                        SetupPreLoginPacketHeader(packetBuffer, dataLength, remaining.Length - dataLength);

                        Span<byte> data = packetBuffer.AsSpan(TdsEnums.HEADER_LEN, dataLength);
                        remaining.Slice(0, dataLength).CopyTo(data);

                        _stream.Write(packetBuffer.AsSpan(0, packetLength));
                        _stream.Flush();

                        remaining = remaining.Slice(dataLength);
                    }
                }
                finally
                {
                    if (packetBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using (SqlClientSNIEventScope.Create(nameof(SslOverTdsStream)))
            {
                if (!_encapsulate)
                {
                    {
                        ValueTask valueTask = _stream.WriteAsync(buffer, cancellationToken);
                        if (!valueTask.IsCompletedSuccessfully)
                        {
                            await valueTask.ConfigureAwait(false);
                        }
                    }
                    Task flushTask = _stream.FlushAsync();
                    if (flushTask.IsCompletedSuccessfully)
                    {
                        await flushTask.ConfigureAwait(false);
                    }
                    return;
                }

                ReadOnlyMemory<byte> remaining = buffer;
                byte[] packetBuffer = null;
                try
                {
                    while (remaining.Length > 0)
                    {
                        int dataLength = Math.Min(PACKET_SIZE_WITHOUT_HEADER, remaining.Length);
                        int packetLength = TdsEnums.HEADER_LEN + dataLength;

                        if (packetBuffer == null)
                        {
                            packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                        }
                        else if (packetBuffer.Length < packetLength)
                        {
                            ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                            packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                        }

                        SetupPreLoginPacketHeader(packetBuffer, dataLength, remaining.Length - dataLength);

                        remaining.Span.Slice(0, dataLength).CopyTo(packetBuffer.AsSpan(TdsEnums.HEADER_LEN, dataLength));

                        {
                            ValueTask packetWriteValueTask = _stream.WriteAsync(new ReadOnlyMemory<byte>(packetBuffer, 0, packetLength), cancellationToken);
                            if (!packetWriteValueTask.IsCompletedSuccessfully)
                            {
                                await packetWriteValueTask.ConfigureAwait(false);
                            }
                        }

                        await _stream.FlushAsync().ConfigureAwait(false);

                        remaining = remaining.Slice(dataLength);
                    }
                }
                finally
                {
                    if (packetBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                    }
                }
            }
        }

        #endregion

        private static void SetupPreLoginPacketHeader(byte[] buffer, int dataLength, int remainingLength)
        {
            // We can only send 4088 bytes in one packet. Header[1] is set to 1 if this is a 
            // partial packet (whether or not count != 0).
            buffer[7] = 0; // touch this first for the jit bounds check
            buffer[0] = PRELOGIN_PACKET_TYPE;
            buffer[1] = (byte)(remainingLength > 0 ? 0 : 1);
            buffer[2] = (byte)((dataLength + TdsEnums.HEADER_LEN) / 0x100);
            buffer[3] = (byte)((dataLength + TdsEnums.HEADER_LEN) % 0x100);
            buffer[4] = 0;
            buffer[5] = 0;
            buffer[6] = 0;
        }
    }
}

#endif
