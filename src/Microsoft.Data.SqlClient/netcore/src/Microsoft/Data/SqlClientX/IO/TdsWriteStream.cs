using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// A stream abstraction over the TDS protocol which works to write the TDS packet
    /// This stream will take care of wrapping the bytes with the TDS header. 
    /// </summary>
    internal class TdsWriteStream : Stream, ITdsWriteStream
    {
        private Stream _underlyingStream;

        private byte[] _WriteBuffer;

        // Start at the end of the Tds header
        internal int WriteBufferOffset { get; private set; } = TdsEnums.HEADER_LEN;

        // This should be set before flushing the buffer.
        public TdsStreamPacketType PacketHeaderType { get; set; }

        internal byte PacketNumber { get; private set; } = 1; // Packets always start with 1.

        public TdsWriteStream(Stream underLyingStream) : base()
        {
            _underlyingStream = underLyingStream;
            _WriteBuffer = new byte[TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
        }

        /// <summary>
        /// Should only be called after the login negotiation is done.
        /// </summary>
        /// <param name="bufferSize">new buffer size</param>
        public virtual void SetPacketSize(int bufferSize)
        {
            _WriteBuffer = new byte[bufferSize];
        }

        public virtual void ReplaceUnderlyingStream(Stream stream)
        {
            _underlyingStream = stream;
        }

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <summary>
        /// Always returns true since we can always write to the stream.
        /// </summary>
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken ct)
        {
            await FlushAsync(ct, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Use hard flush when we definitely know that the end of packet is reached.
        /// </summary>
        /// <param name="ct">Cancellation token for async operation.</param>
        /// <param name="isAsync">Bool to indicate async operation</param>
        /// <param name="flushMode">Whether this is a hard flush or a softflush</param>
        private async Task FlushAsync(CancellationToken ct, bool isAsync, FlushMode flushMode = FlushMode.SoftFlush)
        {
            Debug.Assert(PacketHeaderType != TdsStreamPacketType.None, "PacketHeaderType is not set. Cannot flush the buffer without setting the packet header type.");
            _WriteBuffer[0] = (byte)PacketHeaderType;
            byte status;

            // TODO: Handle cancellation queueing. If there is a cancellation queued up, then send the status with IGNORE bit set.

            if (flushMode == FlushMode.HardFlush)
            {
                status = TdsEnums.ST_EOM;
            }
            else
            {
                status = TdsEnums.ST_BATCH;
                // We have filled up the buffer. We need to send it out, hence next packet gets an increment.
                PacketNumber++;
            }

            _WriteBuffer[1] = status;
            _WriteBuffer[2] = (byte)(WriteBufferOffset >> 8);   // Length upper byte
            _WriteBuffer[3] = (byte)(WriteBufferOffset & 0xff); // Length lower byte
            _WriteBuffer[6] = PacketNumber;
            _WriteBuffer[4] = 0;
            _WriteBuffer[5] = 0;

            _WriteBuffer[7] = 0;

            if (isAsync)
            {
                await _underlyingStream.WriteAsync(_WriteBuffer, 0, WriteBufferOffset, ct).ConfigureAwait(false);
                await _underlyingStream.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                _underlyingStream.Write(_WriteBuffer, 0, WriteBufferOffset);
                _underlyingStream.Flush();
            }

            // A hard flush means that the TDS message is terminated.
            // Reset the packet header type so that the next message sender on the stream
            // will have to set the packet header type.
            if (flushMode == FlushMode.HardFlush)
            {
                // Reset the packet header type
                PacketHeaderType = TdsStreamPacketType.None;
            }

            // Reset the offset since we will start filling up the packet again.
            WriteBufferOffset = TdsEnums.HEADER_LEN;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// Write operation with the byte buffer is not implemented yet.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        /// <summary>
        /// An overload for writing a byte to the stream.
        /// Use this in case the caller is always going to write asynchronously. 
        /// Otherwise use the WriteByteAsync because it takes the isAsync flag.
        /// </summary>
        /// <param name="value">The byte value to be written to the stream.</param>
        public override void WriteByte(byte value) => WriteByteAsync(value, isAsync: false, ct: CancellationToken.None).AsTask().Wait();

        /// <summary>
        /// An async mechanism to write a byte to the stream.
        /// </summary>
        /// <param name="value">The byte value to be written</param>
        /// <param name="isAsync">Indicates if the operation is Async</param>
        /// <param name="ct">Cancellation token to cancel the async operation.</param>
        /// <returns></returns>
        public virtual async ValueTask WriteByteAsync(byte value, bool isAsync, CancellationToken ct)
        {
            // If we are already at the end of the buffer, flush the buffer, with a softflush.
            if (_WriteBuffer.Length - WriteBufferOffset == 0)
            {
                await FlushAsync(ct, isAsync, FlushMode.SoftFlush).ConfigureAwait(false);
            }
            _WriteBuffer[WriteBufferOffset++] = value;
        }

        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            int len = buffer.Length;
            // The buffer may not have enough space. Write what we can and then flush the buffer with a soft flush.
            while (len > 0)
            {
                if (len > _WriteBuffer.Length - WriteBufferOffset)
                {
                    // Only a part of the length fits in the buffer.
                    int bytesToWrite = _WriteBuffer.Length - WriteBufferOffset;
                    buffer.Slice(0, bytesToWrite).CopyTo(_WriteBuffer.AsSpan(WriteBufferOffset));
                    WriteBufferOffset += bytesToWrite;
                    len -= bytesToWrite;
                    FlushAsync(CancellationToken.None, isAsync: false, FlushMode.SoftFlush).ConfigureAwait(false);
                }
                else
                {
                    // The whole length can be added to the buffer.
                    buffer.CopyTo(_WriteBuffer.AsSpan(WriteBufferOffset));
                    WriteBufferOffset += len;
                    len = 0;
                }
            }
        }

        public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int len = buffer.Length;
            // The buffer may not have enough space. Write what we can and then flush the buffer with a soft flush, then 
            // save the rest of the data.
            while (len > 0)
            {
                if (len > _WriteBuffer.Length - WriteBufferOffset)
                {
                    // Only a part of the length fits in the buffer.
                    int bytesToWrite = _WriteBuffer.Length - WriteBufferOffset;
                    buffer[..bytesToWrite].CopyTo(_WriteBuffer.AsMemory(WriteBufferOffset));
                    WriteBufferOffset += bytesToWrite;
                    len -= bytesToWrite;
                    await FlushAsync(cancellationToken, false).ConfigureAwait(false); // Send to network.
                }
                else
                {
                    // The whole length can be added to the buffer.
                    buffer.CopyTo(_WriteBuffer.AsMemory(WriteBufferOffset));
                    WriteBufferOffset += len;
                    len = 0;
                }
            }
        }

        public override void Flush()
        {
            FlushAsync().Wait();
        }

        /// <summary>
        /// Queues the TDS cancellation token for the stream.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public virtual void QueueCancellation()
        {
            throw new NotImplementedException();
        }

        public override async ValueTask DisposeAsync()
        {
            await _underlyingStream.DisposeAsync();
            _underlyingStream = null;
            _WriteBuffer = null;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _underlyingStream?.Dispose();
            _underlyingStream = null;
            _WriteBuffer = null;
        }

        /// <summary>
        /// Flush modes for the stream.
        /// </summary>
        private enum FlushMode
        {
            HardFlush,
            SoftFlush
        }
    }
}
