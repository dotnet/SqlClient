// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed partial class SslOverTdsStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_encapsulate)
            {
                return _stream.Read(buffer, offset, count);
            }

            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.Read |SNI|INFO|SCOPE> reading encapsulated bytes"))
            {
                if (_packetBytes > 0)
                {
                    // there are queued bytes from a previous packet available
                    // work out how many of the remaining bytes we can consume
                    int wantedCount = Math.Min(count, _packetBytes);
                    int readCount = _stream.Read(buffer, offset, wantedCount);
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
                    Array.Clear(headerBytes, 0, headerBytes.Length);

                    // fetch the packet header to determine how long the packet is
                    int headerBytesRead = 0;
                    do
                    {
                        int headerBytesReadIteration = _stream.Read(headerBytes, headerBytesRead, (TdsEnums.HEADER_LEN - headerBytesRead));
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
                    int packetBytesRead = _stream.Read(buffer, offset, Math.Min(count, _packetBytes));
                    _packetBytes -= packetBytesRead;
                    return packetBytesRead;
                }
            }

        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_encapsulate)
            {
                return await _stream.ReadAsync(buffer, offset, count, cancellationToken);
            }

            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.ReadAsync |SNI|INFO|SCOPE> reading encapsulated bytes"))
            {
                if (_packetBytes > 0)
                {
                    // there are queued bytes from a previous packet available
                    // work out how many of the remaining bytes we can consume
                    int wantedCount = Math.Min(count, _packetBytes);
                    int readCount = await _stream.ReadAsync(buffer, offset, wantedCount, cancellationToken);
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
                    Array.Clear(headerBytes, 0, headerBytes.Length);

                    // fetch the packet header to determine how long the packet is
                    int headerBytesRead = 0;
                    do
                    {
                        int headerBytesReadIteration = await _stream.ReadAsync(headerBytes, headerBytesRead, (TdsEnums.HEADER_LEN - headerBytesRead), cancellationToken);
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
                    int packetBytesRead = await _stream.ReadAsync(buffer, offset, Math.Min(count, _packetBytes), cancellationToken);
                    _packetBytes -= packetBytesRead;
                    return packetBytesRead;
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // During the SSL negotiation phase, SSL is tunnelled over TDS packet type 0x12. After
            // negotiation, the underlying socket only sees SSL frames.
            if (!_encapsulate)
            {
                _stream.Write(buffer, offset, count);
                _stream.Flush();
                return;
            }

            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.Write |SNI|INFO|SCOPE> writing encapsulated bytes"))
            {
                int remainingBytes = count;
                int dataOffset = offset;
                byte[] packetBuffer = null;
                while (remainingBytes > 0)
                {
                    int dataLength = Math.Min(PACKET_SIZE_WITHOUT_HEADER, remainingBytes);
                    int packetLength = TdsEnums.HEADER_LEN + dataLength;
                    remainingBytes -= dataLength;

                    if (packetBuffer == null)
                    {
                        packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                    }
                    else if (packetBuffer.Length < packetLength)
                    {
                        ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                        packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                    }

                    SetupPreLoginPacketHeader(packetBuffer, dataLength, remainingBytes);

                    Array.Copy(buffer, dataOffset, packetBuffer, TdsEnums.HEADER_LEN, dataLength);

                    _stream.Write(packetBuffer, 0, packetLength);
                    _stream.Flush();

                    dataOffset += dataLength;
                }
                if (packetBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                }
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_encapsulate)
            {
                await _stream.WriteAsync(buffer, offset, count).ConfigureAwait(false);
                Task flushTask = _stream.FlushAsync();
                if (flushTask.Status == TaskStatus.RanToCompletion)
                {
                    await flushTask.ConfigureAwait(false);
                }
                return;
            }

            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.WriteAsync |SNI|INFO|SCOPE> writing encapsulated bytes"))
            {
                int remainingBytes = count;
                int dataOffset = offset;
                byte[] packetBuffer = null;
                while (remainingBytes > 0)
                {
                    int dataLength = Math.Min(PACKET_SIZE_WITHOUT_HEADER, remainingBytes);
                    int packetLength = TdsEnums.HEADER_LEN + dataLength;
                    remainingBytes -= dataLength;

                    if (packetBuffer == null)
                    {
                        packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                    }
                    else if (packetBuffer.Length < packetLength)
                    {
                        ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                        packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                    }

                    SetupPreLoginPacketHeader(packetBuffer, dataLength, remainingBytes);

                    Array.Copy(buffer, dataOffset, packetBuffer, TdsEnums.HEADER_LEN, dataLength);

                    await _stream.WriteAsync(packetBuffer, 0, packetLength, cancellationToken).ConfigureAwait(false);
                    await _stream.FlushAsync().ConfigureAwait(false);

                    dataOffset += dataLength;
                }
                if (packetBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(packetBuffer, clearArray: true);
                }
            }
        }
    }
}
