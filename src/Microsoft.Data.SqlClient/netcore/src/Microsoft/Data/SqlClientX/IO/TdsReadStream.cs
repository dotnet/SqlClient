// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// A stream which handles reading TDS messages from the wire, and extracting the data from the 
    /// packets in the message packets.
    /// </summary>
    internal class TdsReadStream : Stream, ITdsReadStream
    {
        #region Private Fields
        
        private Stream _underlyingStream;
        private byte[] _readBuffer;

        private int _readIndex { get; set; } = 0;

        private int _readBufferDataEnd { get; set; } = 0;

        private int _packetDataLeft { get; set; } = 0;

        private int _packetHeaderDataLength { get; set; } = 0;

        private byte _packetHeaderType { get; set; } = 0;

        private byte _packetStatus { get; set; }

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
        public override long Position 
        {
            get => throw new NotImplementedException(); 
            set => throw new NotImplementedException(); 
        }

        #endregion

        #region Public Methods

        public override async ValueTask DisposeAsync()
        {
            await _underlyingStream.DisposeAsync();
            _underlyingStream = null;
            _readBuffer = null;
        }

        /// <inheritdoc />
        public override void Flush() => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<byte> PeekByteAsync(bool isAsync, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            int lengthToFill = buffer.Length;
            int totalRead = 0;
            while (lengthToFill > 0)
            {
                if (_packetDataLeft == 0 || _readBufferDataEnd == _readIndex)
                    _ = PrepareBufferAsync(isAsync: false, CancellationToken.None);

                // We can only read the minimum of what is left in the packet,
                // what is left in the buffer, and what we need to fill
                // If we have the length available, then we read it, else we will read either the data in packet, or the 
                // data in buffer, whichever is smaller.
                // If the data spans multiple packets, then we will go ahead and read those packets.
                int lengthToCopy = Math.Min(Math.Min(_packetDataLeft, _readBufferDataEnd - _readIndex), lengthToFill);
                var copyFrom = new ReadOnlySpan<byte>(_readBuffer, _readIndex, lengthToCopy);
                copyFrom.CopyTo(buffer.Slice(totalRead, lengthToFill));
                totalRead += lengthToCopy;
                lengthToFill -= lengthToCopy;
                _readIndex += lengthToCopy;
                _packetDataLeft -= lengthToCopy;
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
                if (_packetDataLeft == 0 || _readBufferDataEnd <= _readIndex)
                    await PrepareBufferAsync(isAsync: true, cancellationToken).ConfigureAwait(false);

                // We can only read the minimum of what is left in the packet, what is left in the buffer, and what we need to fill
                // If we have the length available, then we read it, else we will read either the data in packet, or the 
                // data in buffer, whichever is smaller.
                // If the data spans multiple packets, then we will go ahead and read those packets from the network.
                int lengthToCopy = Math.Min(Math.Min(_packetDataLeft, _readBufferDataEnd - _readIndex), lengthToFill);
                var copyFrom = new ReadOnlyMemory<byte>(_readBuffer, _readIndex, lengthToCopy);
                copyFrom.CopyTo(buffer.Slice(totalRead, lengthToFill));
                totalRead += lengthToCopy;
                lengthToFill -= lengthToCopy;
                _readIndex += lengthToCopy;
                _packetDataLeft -= lengthToCopy;
            }
            return totalRead;
        }
    
        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
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
            int totalRead = 0;
            while (lengthLeftToSkip > 0)
            {
                if (_packetDataLeft == 0 || _readBufferDataEnd == _readIndex)
                    await PrepareBufferAsync(isAsync, ct).ConfigureAwait(false);

                // We can only read the minimum of
                // 1. what is left in the packet and
                // 2. What is left in the buffer and
                // 3.what we need to fill
                // If we have the skip length available in the buffer, then we read it,
                // else we will read either the data in packet, or the 
                // data in buffer, whichever is smaller.
                // If the data spans multiple packets, then we will go ahead and read those packets and skip.
                int skippableMinLength = Math.Min(Math.Min(_packetDataLeft, _readBufferDataEnd - _readIndex), lengthLeftToSkip);
                totalRead += skippableMinLength;
                lengthLeftToSkip -= skippableMinLength;

                // Advance buffer pointer by skippableMinLength
                _readIndex += skippableMinLength;

                // Indicate that we have read the data from the packet.
                _packetDataLeft -= skippableMinLength;
            }
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private methods

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
            // Either we have read all the data from the packet as stated in the header
            // and we have data left in the buffer.
            // In this case we need to process header. Note that we may have a partially read header here
            // but ProcessHeaderAsync will take care of that.
            if (_packetDataLeft == 0 && _readBufferDataEnd > _readIndex)
            {
                await ProcessHeaderAsync(isAsync, ct).ConfigureAwait(false);
            }

            // There is no data left in the buffer.
            // 1. We need to read more data from the transport because nothing is left in the buffer.
            // Then get the header, and then read the packet data from the network.
            
            if (_readIndex == _readBufferDataEnd)
            {
                // 1.1. If we have left over data indicated in the packet header, then we simply need to get data from the network.
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

                    // Reset the offset to 0, since we read new packet.
                    _readIndex = 0;
                    await ProcessHeaderAsync(isAsync, ct).ConfigureAwait(false);

                    // 1.3. After processing the packet header, there is a possibility that the transport read didn't
                    // // return any more data for the packet. In that case, post another read to have packet data ready..
                    if (_readBufferDataEnd == _readIndex && _packetDataLeft > 0)
                    {
                        _readBufferDataEnd = isAsync ?
                            await _underlyingStream.ReadAsync(_readBuffer, ct).ConfigureAwait(false) :
                            _underlyingStream.Read(_readBuffer);
                    }
                }
            }
        }

        /// <summary>
        /// Processes the header of the packet, and extracts the data from the header.
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async ValueTask ProcessHeaderAsync(bool isAsync, CancellationToken ct)
        {
            int headerDataAvailable = _readBufferDataEnd - _readIndex;
            int bytesNeededToCompleteHeader = TdsEnums.HEADER_LEN - headerDataAvailable;

            // We have less than the header length available in the buffer, so we need to read more data, to atleast complete 
            // the header. After this the header data length can be used to determine, the amount of data in the packet.
            if (headerDataAvailable < TdsEnums.HEADER_LEN)
            {
                // It is possible there may not be space available at the end of the buffer to fill in the header.
                // This is rare but definitely possible.
                if (_readBufferDataEnd + bytesNeededToCompleteHeader > _readBuffer.Length)
                {
                    // We need to reset the buffer, but copying the partial header to the beginning of the buffer, and repositioning
                    // the read buffer pointers.
                    Buffer.BlockCopy(_readBuffer, _readIndex, _readBuffer, 0, headerDataAvailable);
                    _readBufferDataEnd = headerDataAvailable;
                    _readIndex = 0;
                }

                while (bytesNeededToCompleteHeader > 0)
                {
                    int bytesRead = isAsync ? 
                        await _underlyingStream.ReadAsync(_readBuffer.AsMemory(_readBufferDataEnd), ct).ConfigureAwait(false) 
                            : _underlyingStream.Read(_readBuffer, _readBufferDataEnd, bytesNeededToCompleteHeader);
                    // Reduce the number of bytes needed
                    bytesNeededToCompleteHeader -= bytesRead;
                    _readBufferDataEnd += bytesRead;
                }
            }

            _packetHeaderType = _readBuffer[_readIndex];
            _packetStatus = _readBuffer[_readIndex + 1];
            // TODO: Use Binary primitives to read the data.
            _packetDataLeft = (_readBuffer[_readIndex + TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8
                | _readBuffer[_readIndex + TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - TdsEnums.HEADER_LEN;
            // Ignore SPID and Window

            _readIndex += TdsEnums.HEADER_LEN;
        }

        #endregion
    }
}
