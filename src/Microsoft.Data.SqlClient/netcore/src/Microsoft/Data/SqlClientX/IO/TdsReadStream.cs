// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// A stream which handles reading TDS messages from the wire, and extracting the data from the 
    /// packets in the message packets.
    /// </summary>
    internal class TdsReadStream : Stream, ITdsReadStream
    {
        // TODO: Handle Cancellation tokens in all async paths.
        #region Private Fields

        private Stream _underlyingStream;

        // The buffer to hold the TDS read data.
        private byte[] _readBuffer;

        // The read pointer inside the buffer.
        private int _readIndex = 0;

        // The end of the data index in the buffer.
        private int _readBufferDataEnd = 0;

        // The number of bytes left in the packet to be consumed.
        // This is not necessarily the number of bytes available in the buffer.
        private int _packetDataLeft = 0;

        #endregion

        #region Constructors

        public TdsReadStream(Stream underlyingStream)
        {
            _readBuffer = new byte[TdsEnums.DEFAULT_LOGIN_PACKET_SIZE];
            _underlyingStream = underlyingStream;
        }

        #endregion

        #region Public Properties
        /// <inheritdoc />
        public override bool CanRead => _underlyingStream != null;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();


        /// <inheritdoc />
        public virtual byte ReadPacketHeaderType { get; private set; }

        /// <inheritdoc />
        public virtual byte ReadPacketStatus { get; private set; }

        /// <inheritdoc />
        public override long Position 
        {
            get => throw new NotSupportedException(); 
            set => throw new NotSupportedException(); 
        }

        /// <inheritdoc />
        public virtual int Spid { get; private set; }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await _underlyingStream.DisposeAsync();
            _underlyingStream = null;
            _readBuffer = null;
        }

        /// <inheritdoc />
        public override void Flush() => throw new NotSupportedException();

        /// <inheritdoc />
        public async ValueTask<byte> PeekByteAsync(bool isAsync, CancellationToken ct)
        {
            // If we have logically finished reading the packet, or if we have 
            // reached the end of the buffer, then we need to position the buffer at the beginning of next
            // packet start.
            await PrepareBufferIfNeeded(isAsync, ct).ConfigureAwait(false);
            return _readBuffer[_readIndex];
        }

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            int lengthToFill = buffer.Length;
            int totalRead = 0;
            while (lengthToFill > 0)
            {
                PrepareBufferIfNeeded(isAsync: false, CancellationToken.None).ConfigureAwait(false);

                int lengthToCopy = MinDataAvailableBeforeRead(lengthToFill);
                ReadOnlySpan<byte> copyFrom = _readBuffer.AsSpan(_readIndex, lengthToCopy);
                copyFrom.CopyTo(buffer.Slice(totalRead, lengthToFill));
                totalRead += lengthToCopy;
                lengthToFill -= lengthToCopy;
                AdvanceBufferOnRead(lengthToCopy);
            }
            return totalRead;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int lengthToFill = buffer.Length;
            int totalRead = 0;
            while (lengthToFill > 0)
            {
                await PrepareBufferIfNeeded(isAsync: true, cancellationToken).ConfigureAwait(false);
                int lengthToCopy = MinDataAvailableBeforeRead(lengthToFill);
                ReadOnlyMemory<byte> copyFrom = new ReadOnlyMemory<byte>(_readBuffer, _readIndex, lengthToCopy);
                copyFrom.CopyTo(buffer.Slice(totalRead, lengthToFill));
                totalRead += lengthToCopy;
                lengthToFill -= lengthToCopy;
                AdvanceBufferOnRead(lengthToCopy);
            }
            return totalRead;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void ReplaceUnderlyingStream(Stream stream) => _underlyingStream = stream;

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public void SetPacketSize(int bufferSize)
        {
            _readBuffer = new byte[bufferSize];
        }

        /// <inheritdoc />
        public async ValueTask SkipReadBytesAsync(int skipCount, bool isAsync, CancellationToken ct)
        {
            int lengthLeftToSkip = skipCount;
            while (lengthLeftToSkip > 0)
            {
                await PrepareBufferIfNeeded(isAsync, ct).ConfigureAwait(false);
                int skippableMinLength = MinDataAvailableBeforeRead(lengthLeftToSkip);
                lengthLeftToSkip -= skippableMinLength;
                AdvanceBufferOnRead(skippableMinLength);
            }
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Advances the buffer read index, and the packet data left count, by the specified length.
        /// </summary>
        /// <param name="length">The length to advance the packet by.</param>
        private void AdvanceBufferOnRead(int length)
        {
            _readIndex += length;
            _packetDataLeft -= length;
        }

        /// <summary>
        /// Computes the minimum byte count available for copying into the buffer.
        /// </summary>
        /// <param name="maxByteCountExpected">The maximum bytes count expected by the caller.</param>
        /// <returns></returns>
        private int MinDataAvailableBeforeRead(int maxByteCountExpected)
        {
            // We can only read the minimum of what is left in the packet,
            // what is left in the buffer, and what we need to fill
            // If we have the max Byte Count available, then we read it
            // else we will read either the data in packet, or the 
            // data in buffer, whichever is smaller.
            // If the data spans multiple packets, then the caller will go ahead and post a network read.
            return Math.Min(Math.Min(_packetDataLeft, _readBufferDataEnd - _readIndex), maxByteCountExpected);
        }

        /// <summary>
        /// Prepares the Read buffer with more data. 
        /// This method is called, when the data from existing buffer is completely read, or there 
        /// is less data available in the buffer, than what is needed to complete the read call.
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async ValueTask PrepareBufferAsync(bool isAsync, CancellationToken ct)
        {
            // We have read all the data from the packet as stated in the header, this means that we have to 
            // process the next packet header.
            if (_packetDataLeft == 0 && _readBufferDataEnd > _readIndex)
            {
                await ProcessHeaderAsync(isAsync, ct).ConfigureAwait(false);
            }

            // There is no data left in the buffer.
            if (_readIndex == _readBufferDataEnd)
            {
                // If we have left over data indicated in the packet header, then we simply need to get data from the network.
                if (_packetDataLeft > 0)
                {
                    _readBufferDataEnd = isAsync ?
                        await _underlyingStream.ReadAsync(_readBuffer, ct).ConfigureAwait(false) :
                        _underlyingStream.Read(_readBuffer);
                    _readIndex = 0;
                }
                // 1.2. There is no data left as indicated by packet header and the buffer is empty.
                else if (_packetDataLeft == 0)
                {
                    _readBufferDataEnd = isAsync ?
                        await _underlyingStream.ReadAsync(_readBuffer, ct).ConfigureAwait(false) :
                        _underlyingStream.Read(_readBuffer);
                    
                    _readIndex = 0;

                    await ProcessHeaderAsync(isAsync, ct).ConfigureAwait(false);

                    // 1.3. After processing the packet header, there is a possibility that the transport read didn't
                    // return any more data for the packet. In that case, post another read to have packet data ready..
                    if (_readBufferDataEnd == _readIndex)
                    {
                        _readBufferDataEnd = isAsync ?
                            await _underlyingStream.ReadAsync(_readBuffer, ct).ConfigureAwait(false) :
                            _underlyingStream.Read(_readBuffer);
                    }
                }
            }
        }

        /// <summary>
        /// If the buffer is empty, or the packet data is completely read, 
        /// then we need to prepare the buffer for the next packet.
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async ValueTask PrepareBufferIfNeeded(bool isAsync, CancellationToken ct)
        {
            // If there we have read through the packet, 
            // or if we have reached the end of the buffer,
            // then we need to position the buffer at the beginning of packet or buffer
            if (_packetDataLeft == 0 || _readBufferDataEnd == _readIndex)
            { 
                await PrepareBufferAsync(isAsync, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Processes the header of the packet, and extracts the data from the header.
        /// If needed, this function will read more data from the network to complete the header.
        /// </summary>
        /// <param name="isAsync">Whether this method is invoked asynchronously.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns></returns>
        private async ValueTask ProcessHeaderAsync(bool isAsync, CancellationToken ct)
        {
            int headerDataAvailable = _readBufferDataEnd - _readIndex;
            int bytesNeededToCompleteHeader = TdsEnums.HEADER_LEN - headerDataAvailable;

            // We have less than the header length available in the buffer, so we need to read more data, to atleast complete 
            // the header.
            if (headerDataAvailable < TdsEnums.HEADER_LEN)
            {
                // We move the header information to the beginning of the buffer.
                Buffer.BlockCopy(_readBuffer, _readIndex, _readBuffer, 0, headerDataAvailable);
                _readBufferDataEnd = headerDataAvailable;
                _readIndex = 0;

                while (bytesNeededToCompleteHeader > 0)
                {
                    int bytesRead = isAsync ? 
                        await _underlyingStream.ReadAsync(_readBuffer.AsMemory(_readBufferDataEnd), ct).ConfigureAwait(false) 
                            : _underlyingStream.Read(_readBuffer.AsSpan(_readBufferDataEnd));
                    // Reduce the number of bytes needed
                    bytesNeededToCompleteHeader -= bytesRead;
                    _readBufferDataEnd += bytesRead;
                }
            }

            ReadPacketHeaderType = _readBuffer[_readIndex];
            ReadPacketStatus = _readBuffer[_readIndex + 1];
            _packetDataLeft = BinaryPrimitives.ReadUInt16BigEndian(_readBuffer.AsSpan(_readIndex + TdsEnums.HEADER_LEN_FIELD_OFFSET, 2)) - TdsEnums.HEADER_LEN;
            Spid = BinaryPrimitives.ReadUInt16BigEndian(_readBuffer.AsSpan(_readIndex + TdsEnums.SPID_OFFSET, 2));
            // Position the read index to the start of the packet data.
            _readIndex += TdsEnums.HEADER_LEN;
        }
        
        #endregion
    }
}
