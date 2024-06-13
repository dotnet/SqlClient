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
    internal class TdsStream : Stream, ITdsWriteStream
    {
        private readonly TdsWriteStream _writeStream;

        /// <inheritdoc />
        public override bool CanRead => throw new NotImplementedException();
        
        /// <inheritdoc />
        public override bool CanSeek => throw new NotImplementedException();

        /// <inheritdoc />
        public override bool CanWrite => throw new NotImplementedException();

        /// <inheritdoc />
        public override long Length => throw new NotImplementedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Indicates if the cancellation is sent to the server.
        /// </summary>
        public virtual bool IsCancellationSent { get; internal set; }

        public TdsStreamPacketType PacketHeaderType { get => _writeStream.PacketHeaderType; set => _writeStream.PacketHeaderType = value; }

        public TdsStream(Stream underLyingStream, TdsWriteStream writeStream) : base()
        {
            _writeStream = writeStream;
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
            // TODO: do this for the read stream as well.
        }

        /// <summary>
        /// Allows manipulation of the packet size. This should only be used after login ack,
        /// when the client and server exchange the negotiated packet size.
        /// </summary>
        /// <param name="packetSize">The negotiated packet size</param>
        /// <exception cref="NotImplementedException"></exception>
        public void SetPacketSize(int packetSize)
        {
            _writeStream.SetPacketSize(packetSize);
            // TODO: Do this for the read stream as well.
        }

        /// <inheritdoc />
        public override void Flush() => _writeStream.Flush();


        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override int Read(
            byte[] buffer, 
            int offset, 
            int count)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
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
         => await _writeStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Called explicitly by the consumers to flush the stream,
        /// which marks the TDS packet as the last packet in the message
        /// and sends it to the server.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override async Task FlushAsync(CancellationToken ct)
            => await _writeStream.FlushAsync(ct);

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Peeks the next byte in the stream, without consuming it.
        /// The next call to read will return the same byte but it 
        /// will consume it.
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual ValueTask<byte> PeekByteAsync(bool isAsync,
            CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A convenience method to skip the bytes in the stream,
        /// by allowing buffer manipulation, instead of making the consumer
        /// allocate buffers to read and discard the bytes.
        /// </summary>
        /// <param name="skipCount">Number of bytes to skip</param>
        /// <param name="isAsync">If the method should be called Asynchronously.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual ValueTask SkipReadBytesAsync(int skipCount,
            bool isAsync, 
            CancellationToken ct)
        {
            throw new NotImplementedException();
        }

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
        /// <exception cref="NotImplementedException"></exception>
        public virtual void QueueCancellation()
        {
            _writeStream.QueueCancellation();
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await _writeStream.DisposeAsync();
            // TODO: For Read Stream as well.
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            throw new NotImplementedException();
        }

        public async ValueTask WriteByteAsync(byte value, bool isAsync, CancellationToken ct)
        {
            await _writeStream.WriteByteAsync(value, isAsync, ct).ConfigureAwait(false);
        }
    }
}
