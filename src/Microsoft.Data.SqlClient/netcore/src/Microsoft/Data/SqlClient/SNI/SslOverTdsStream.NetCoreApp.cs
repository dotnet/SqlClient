// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed partial class SslOverTdsStream
    {
        public override int Read(byte[] buffer, int offset, int count)
            => ReadInternal(new Memory<byte>(buffer, offset, count), default, async: false).GetAwaiter().GetResult();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => ReadInternal(new Memory<byte>(buffer, offset, count), token, async: true).AsTask();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            => ReadInternal(buffer, cancellationToken, async: true);

        public override void Write(byte[] buffer, int offset, int count)
            => WriteInternal(new ReadOnlyMemory<byte>(buffer, offset, count), default, async: true).GetAwaiter().GetResult();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => WriteInternal(new ReadOnlyMemory<byte>(buffer, offset, count), token, async: true).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            => WriteInternal(buffer, cancellationToken, async: true);

        /// <summary>
        /// Read Internal is called synchronosly when async is false
        /// </summary>
        private async ValueTask<int> ReadInternal(Memory<byte> buffer, CancellationToken token, bool async)
        {
            int readBytes = 0;
            int count = buffer.Length;
            byte[] packetData = new byte[count < TdsEnums.HEADER_LEN ? TdsEnums.HEADER_LEN : count];

            if (_encapsulate)
            {
                if (_packetBytes == 0)
                {
                    // Account for split packets
                    while (readBytes < TdsEnums.HEADER_LEN)
                    {
                        readBytes += async ?
                            await _stream.ReadAsync(packetData, readBytes, TdsEnums.HEADER_LEN - readBytes, token).ConfigureAwait(false) :
                            _stream.Read(packetData, readBytes, TdsEnums.HEADER_LEN - readBytes);
                    }

                    _packetBytes = (packetData[TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8) | packetData[TdsEnums.HEADER_LEN_FIELD_OFFSET + 1];
                    _packetBytes -= TdsEnums.HEADER_LEN;
                }

                if (count > _packetBytes)
                {
                    count = _packetBytes;
                }
            }

            readBytes = async ?
                await _stream.ReadAsync(new Memory<byte>(packetData, 0, count), token).ConfigureAwait(false) :
                _stream.Read(packetData.AsSpan(0, count));

            if (_encapsulate)
            {
                _packetBytes -= readBytes;
            }

            packetData.AsSpan(0, readBytes).CopyTo(buffer.Span);
            return readBytes;
        }

        /// <summary>
        /// The internal write method calls Sync APIs when Async flag is false
        /// </summary>
        private async ValueTask WriteInternal(ReadOnlyMemory<byte> buffer, CancellationToken token, bool async)
        {
            int count = buffer.Length;
            int currentOffset = 0;

            while (count > 0)
            {
                int currentCount;
                // During the SSL negotiation phase, SSL is tunnelled over TDS packet type 0x12. After
                // negotiation, the underlying socket only sees SSL frames.
                //
                if (_encapsulate)
                {
                    if (count > PACKET_SIZE_WITHOUT_HEADER)
                    {
                        currentCount = PACKET_SIZE_WITHOUT_HEADER;
                    }
                    else
                    {
                        currentCount = count;
                    }

                    count -= currentCount;

                    // Prepend buffer data with TDS prelogin header
                    byte[] combinedBuffer = new byte[TdsEnums.HEADER_LEN + currentCount];

                    // We can only send 4088 bytes in one packet. Header[1] is set to 1 if this is a 
                    // partial packet (whether or not count != 0).
                    // 
                    combinedBuffer[0] = PRELOGIN_PACKET_TYPE;
                    combinedBuffer[1] = (byte)(count > 0 ? 0 : 1);
                    combinedBuffer[2] = (byte)((currentCount + TdsEnums.HEADER_LEN) / 0x100);
                    combinedBuffer[3] = (byte)((currentCount + TdsEnums.HEADER_LEN) % 0x100);
                    combinedBuffer[4] = 0;
                    combinedBuffer[5] = 0;
                    combinedBuffer[6] = 0;
                    combinedBuffer[7] = 0;

                    CopyToBuffer(combinedBuffer, buffer.Span.Slice(currentOffset));

                    if (async)
                    {
                        await _stream.WriteAsync(combinedBuffer, 0, combinedBuffer.Length, token).ConfigureAwait(false);
                    }
                    else
                    {
                        _stream.Write(combinedBuffer.AsSpan());
                    }
                }
                else
                {
                    currentCount = count;
                    count = 0;

                    if (async)
                    {
                        await _stream.WriteAsync(buffer.Slice(currentOffset, currentCount), token).ConfigureAwait(false);
                    }
                    else
                    {
                        _stream.Write(buffer.Span.Slice(currentOffset, currentCount));
                    }
                }

                if (async)
                {
                    await _stream.FlushAsync().ConfigureAwait(false);
                }
                else
                {
                    _stream.Flush();
                }

                currentOffset += currentCount;
            }

            void CopyToBuffer(byte[] combinedBuffer, ReadOnlySpan<byte> span)
            {
                for (int i = TdsEnums.HEADER_LEN; i < combinedBuffer.Length; i++)
                {
                    combinedBuffer[i] = span[i - TdsEnums.HEADER_LEN];
                }
            }
        }
    }
}
