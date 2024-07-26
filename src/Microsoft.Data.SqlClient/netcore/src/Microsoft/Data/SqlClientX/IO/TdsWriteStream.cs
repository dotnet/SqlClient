// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// A stream abstraction over the TDS protocol which works to write the TDS packet
    /// This stream will take care of wrapping the bytes with the TDS header. 
    /// </summary>
    internal class TdsWriteStream : Stream, ITdsWriteStream
    {
        #region Private Fields

        private Stream _underlyingStream;

        private byte[] _writeBuffer;

        // Start at the end of the Tds header
        private int _writeBufferOffset { get; set; } = TdsEnums.HEADER_LEN;

        // Packets always start with 1.
        private byte _packetNumber { get; set; } = 1;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor for the TDS write stream.
        /// </summary>
        /// <param name="underLyingStream"></param>
        public TdsWriteStream(Stream underLyingStream) : base()
        {
            _underlyingStream = underLyingStream;
            _writeBuffer = new byte[TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
            InitializeHeaderBytesToZero();
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <summary>
        /// Returns true since we can always write to the stream.
        /// </summary>
        public override bool CanWrite => _underlyingStream != null;

        /// <inheritdoc />
        public override long Length
            => throw new NotSupportedException();

        /// <inheritdoc />
        public TdsStreamPacketType? PacketHeaderType { get; set; }

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await _underlyingStream.DisposeAsync().ConfigureAwait(false);
            _underlyingStream = null;
            _writeBuffer = null;
        }

        /// <inheritdoc />
        public override void Flush()
            => FlushPacketAsync(true, isAsync: false, CancellationToken.None).ConfigureAwait(false);

        /// <inheritdoc />
        public override async Task FlushAsync(CancellationToken ct)
            => await FlushPacketAsync(true, true, ct).ConfigureAwait(false);

        /// <inheritdoc />
        public void SetPacketSize(int bufferSize)
        {
            _writeBuffer = new byte[bufferSize];
            InitializeHeaderBytesToZero();
        }

        /// <inheritdoc />
        public void ReplaceUnderlyingStream(Stream stream) => _underlyingStream = stream;

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
            => Write(buffer.AsSpan(offset, count));


        /// <inheritdoc/>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            int len = buffer.Length;
            int start = 0;
            // The buffer may not have enough space. Write what we can and then flush the buffer with a soft flush.
            while (len > 0)
            {
                if (len > _writeBuffer.Length - _writeBufferOffset)
                {
                    // Only a part of the length fits in the buffer.
                    int bytesToWrite = _writeBuffer.Length - _writeBufferOffset;
                    buffer.Slice(start, bytesToWrite).CopyTo(_writeBuffer.AsSpan(_writeBufferOffset));
                    _writeBufferOffset += bytesToWrite;
                    len -= bytesToWrite;
                    start += bytesToWrite;
                    FlushPacketAsync(false, isAsync: false, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    // The whole length can be added to the buffer.
                    buffer.Slice(start).CopyTo(_writeBuffer.AsSpan(_writeBufferOffset));
                    _writeBufferOffset += len;
                    len = 0;
                }
            }
        }

        /// <summary>
        /// An overload for writing a byte to the stream.
        /// Use this in case the caller is always going to write asynchronously. 
        /// Otherwise use the WriteByteAsync because it takes the isAsync flag.
        /// </summary>
        /// <param name="value">The byte value to be written to the stream.</param>
        public override void WriteByte(byte value)
        {
            // If we are already at the end of the buffer, flush the buffer, with a softflush.
            if (_writeBuffer.Length - _writeBufferOffset == 0)
            {
                FlushPacketAsync(false, isAsync: false, CancellationToken.None).ConfigureAwait(false);
            }
            _writeBuffer[_writeBufferOffset++] = value;
        }

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
            if (_writeBuffer.Length - _writeBufferOffset == 0)
            {
                await FlushPacketAsync(false, isAsync, ct).ConfigureAwait(false);
            }
            _writeBuffer[_writeBufferOffset++] = value;
        }

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            int len = buffer.Length;
            int start = 0;
            // The buffer may not have enough space. Write what we can and then flush the buffer with a soft flush, then 
            // save the rest of the data.
            while (len > 0)
            {
                if (len > _writeBuffer.Length - _writeBufferOffset)
                {
                    // Only a part of the length fits in the buffer.
                    // TODO: It might be possible to optimize this by writing directly to the underlying stream.
                    // In that case, we need to first write the header and then write the data packet to the underlying stream,
                    // directly. This needs to be tested.
                    int bytesToWrite = _writeBuffer.Length - _writeBufferOffset;
                    buffer.Slice(start, bytesToWrite).CopyTo(_writeBuffer.AsMemory(_writeBufferOffset));
                    _writeBufferOffset += bytesToWrite;
                    len -= bytesToWrite;
                    start += bytesToWrite;
                    await FlushPacketAsync(false, false, cancellationToken).ConfigureAwait(false); // Send to network.
                }
                else
                {
                    // The whole length can be added to the buffer.
                    buffer.Slice(start).CopyTo(_writeBuffer.AsMemory(_writeBufferOffset));
                    _writeBufferOffset += len;
                    len = 0;
                }
            }
        }

        /// <inheritdoc />
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc />
        public async ValueTask WriteStringAsync(string value, bool isAsync, CancellationToken ct)
        {            
            int byteCount = Encoding.Unicode.GetByteCount(value.AsSpan());
            // There is enough space in the buffer.
            if (byteCount < _writeBuffer.Length - _writeBufferOffset)
            {
                int writtenBytes = Encoding.Unicode.GetBytes(value.AsSpan(), _writeBuffer.AsSpan(_writeBufferOffset));
                _writeBufferOffset += byteCount;
            }
            else
            {
                int spaceLeft = _writeBuffer.Length - _writeBufferOffset;
                Encoding.Unicode.GetBytes(value.AsSpan(0, spaceLeft), _writeBuffer.AsSpan(_writeBufferOffset, spaceLeft));
                await FlushPacketAsync(false, isAsync, ct).ConfigureAwait(false);
                // Write the rest of the string.
                Encoding.Unicode.GetBytes(value.AsSpan(spaceLeft), _writeBuffer.AsSpan(_writeBufferOffset, byteCount - spaceLeft));
            }
        }

        /// <summary>
        /// Queues the TDS cancellation token for the stream.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public virtual void QueueCancellation()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _underlyingStream?.Dispose();
            _underlyingStream = null;
            _writeBuffer = null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Use hard flush when we definitely know that the end of packet is reached.
        /// </summary>
        /// <param name="ct">Cancellation token for async operation.</param>
        /// <param name="isAsync">Bool to indicate async operation</param>
        /// <param name="isEndOfMessage">Whether this is the end of message</param>
        private async Task FlushPacketAsync(bool isEndOfMessage, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(PacketHeaderType != null, "PacketHeaderType is not set. Cannot flush the buffer without setting the packet header type.");
            _writeBuffer[0] = (byte)PacketHeaderType;
            byte status;

            // TODO: Handle cancellation queueing. If there is a cancellation queued up, then send the status with IGNORE bit set.

            if (isEndOfMessage)
            {
                status = TdsEnums.ST_EOM;
            }
            else
            {
                status = TdsEnums.ST_BATCH;
                // We have filled up the buffer. We need to send it out, hence next packet gets an increment.
                _packetNumber++;
            }

            // Set the variables in the header, the rest of the fields should already be set
            // when the buffer was initialized.
            _writeBuffer[1] = status;
            _writeBuffer[2] = (byte)(_writeBufferOffset >> 8);   // Length upper byte
            _writeBuffer[3] = (byte)(_writeBufferOffset & 0xff); // Length lower byte
            _writeBuffer[6] = _packetNumber;

            if (isAsync)
            {
                await _underlyingStream.WriteAsync(_writeBuffer.AsMemory(0, _writeBufferOffset), ct).ConfigureAwait(false);
                await _underlyingStream.FlushAsync(ct).ConfigureAwait(false);
            }
            else
            {
                _underlyingStream.Write(_writeBuffer, 0, _writeBufferOffset);
                _underlyingStream.Flush();
            }

            // A hard flush means that the TDS message is terminated.
            // Reset the packet header type so that the next message sender on the stream
            // will have to set the packet header type.
            if (isEndOfMessage)
            {
                // Reset the packet header type to null.
                PacketHeaderType = null;
            }

            // Reset the offset since we will start filling up the packet again.
            _writeBufferOffset = TdsEnums.HEADER_LEN;
        }

        private void InitializeHeaderBytesToZero()
        {
            _writeBuffer[7] = 0;
            _writeBuffer[1] = 0;
            _writeBuffer[2] = 0;
            _writeBuffer[3] = 0;
            _writeBuffer[6] = 0;
            _writeBuffer[4] = 0;
            _writeBuffer[5] = 0;
        }


        

        #endregion
    }
}
