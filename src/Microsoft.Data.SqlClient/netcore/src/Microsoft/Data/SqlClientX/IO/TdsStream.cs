// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// A Stream abstraction over the TDS protocol.
    /// The stream can be used to read and write TDS messages on a 
    /// SQL Server physical connection.
    /// The stream is responsible for abstracting away the calls to IO and handling the packet 
    /// header from the consumers of TDS protocol.
    /// The stream responds to the request of the callers, but it doesn't guarantee the correctness of the underlying TDS protocol correctness.
    /// e.g. If the protocol states that there are N bytes that should be read, and the stream is asked to return
    /// N + 1 bytes, then the stream will timeout trying to get N+1 bytes, or it will return N+1 bytes, if the 
    /// N+1 byte is available.
    /// </summary>
    internal class TdsStream : Stream, ITdsWriteStream, ITdsReadStream
    {
        // TODO: Handle Cancellation tokens in all async paths.
        private TdsWriteStream _writeStream;
        private TdsReadStream _readStream;

        /// <inheritdoc />
        public override bool CanRead => _readStream != null && _readStream.CanRead;
        
        /// <inheritdoc />
        public override bool CanSeek => throw new NotSupportedException();

        /// <inheritdoc />
        public override bool CanWrite => _writeStream != null && _writeStream.CanWrite;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <summary>
        /// Indicates if the cancellation is sent to the server.
        /// </summary>
        public virtual bool IsCancellationSent { get; internal set; }

        public TdsStreamPacketType? PacketHeaderType 
        {
            get => _writeStream.PacketHeaderType;
            set => _writeStream.PacketHeaderType = value; 
        }

        /// <inheritdoc />
        public int Spid => _readStream.Spid;

        /// <inheritdoc />
        public byte ReadPacketStatus => _readStream.ReadPacketStatus;

        /// <inheritdoc />
        public byte ReadPacketHeaderType => _readStream.ReadPacketHeaderType;

        /// <inheritdoc />
        public int PacketDataLeft => _readStream.PacketDataLeft;

        /// <summary>
        /// Tds Writer instance that provides managed buffer for writing data to stream.
        /// </summary>
        public TdsWriter TdsWriter { get; private set; }

        /// <summary>
        /// Constructor for instantiating the TdsStream
        /// </summary>
        /// <param name="writeStream">The stream for outgoing TDS packets</param>
        /// <param name="readStream">The stream for reading incoming TDS packets.</param>
        public TdsStream(TdsWriteStream writeStream, TdsReadStream readStream) : base()
        {
            _writeStream = writeStream;
            _readStream = readStream;
            TdsWriter = new TdsWriter(this);
        }

        /// <summary>
        /// Replaces the underlying stream. This is useful while changing between SSL stream and non-SSL stream. 
        /// e.g. Prelogin ends with TLS handshake, which is done on a different stream. Once the handshake is done,
        /// login is sent in the TLS stream. However at the end of the login exchange, the stream may be switched 
        /// back to the non-SSL stream, which is either the pipe stream or the TCP stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void ReplaceUnderlyingStream(Stream stream)
        {
            _writeStream.ReplaceUnderlyingStream(stream);
            _readStream.ReplaceUnderlyingStream(stream);
        }

        /// <summary>
        /// Allows manipulation of the packet size. This should only be used after login ack,
        /// when the client and server exchange the negotiated packet size.
        /// </summary>
        /// <param name="packetSize">The negotiated packet size</param>
        public void SetPacketSize(int packetSize)
        {
            _readStream.SetPacketSize(packetSize);
            _writeStream.SetPacketSize(packetSize);
        }

        /// <inheritdoc />
        public override void Flush() => _writeStream.Flush();


        /// <inheritdoc />
        public override int Read(Span<byte> buffer) => _readStream.Read(buffer);

        /// <inheritdoc />
        public override int Read(
            byte[] buffer, 
            int offset, 
            int count) => _readStream.Read(buffer, offset, count);

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => _readStream.Seek(offset, origin);

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _writeStream.Write(buffer);
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            _writeStream.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken) 
            => await _writeStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);


        /// <inheritdoc />
        public override async Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
         => await _writeStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Called explicitly by the consumers to flush the stream,
        /// which marks the TDS packet as the last packet in the message
        /// and sends it to the server.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async Task FlushAsync(CancellationToken ct)
            => await _writeStream.FlushAsync(ct).ConfigureAwait(false);

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken) => _readStream.ReadAsync(buffer, cancellationToken);

        /// <inheritdoc />
        public virtual ValueTask<byte> ReadByteAsync(bool isAsync, CancellationToken cancellationToken)
            => _readStream.ReadByteAsync(isAsync, cancellationToken);

        /// <inheritdoc />
        public virtual ValueTask<byte> PeekByteAsync(bool isAsync,
            CancellationToken ct) => _readStream.PeekByteAsync(isAsync, ct);

        /// <inheritdoc />
        public virtual ValueTask SkipReadBytesAsync(int skipCount,
            bool isAsync, 
            CancellationToken ct) => _readStream.SkipReadBytesAsync(skipCount, isAsync, ct);

        /// <summary>
        /// Needed to reset the stream.
        /// Useful in some cases for TDS implementation, where we 
        /// dont want to consume all the data in the stream, but 
        /// want to make it available for the next set of operations.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public virtual void Reset()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Queues the TDS cancellation token for the stream.
        /// </summary>
        public virtual void QueueCancellation()
        {
            _writeStream.QueueCancellation();
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await _writeStream.DisposeAsync().ConfigureAwait(false);
            await _readStream.DisposeAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            { 
                _writeStream?.Dispose();
                _readStream?.Dispose();
                _writeStream = null;
                _readStream = null;
                TdsWriter = null;
            }
            base.Dispose(disposing);
        }

        public async ValueTask WriteByteAsync(byte value, bool isAsync, CancellationToken ct)
        {
            await _writeStream.WriteByteAsync(value, isAsync, ct).ConfigureAwait(false);
        }

        /// <inheritdocs />
        public ValueTask WriteStringAsync(string value, bool isAsync, CancellationToken ct) 
            => _writeStream.WriteStringAsync(value, isAsync , ct);
    }
}
