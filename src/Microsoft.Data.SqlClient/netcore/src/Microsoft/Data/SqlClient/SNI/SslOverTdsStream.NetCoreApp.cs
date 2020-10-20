// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed partial class SslOverTdsStream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override int Read(Span<byte> buffer)
        {
            if (!_encapsulate)
            {
                return _stream.Read(buffer);
            }

            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.Read |SNI|INFO|SCOPE> reading encapsulated bytes"))
            {
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

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
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
            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.ReadAsync |SNI|INFO|SCOPE> reading encapsulated bytes"))
            {
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

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // During the SSL negotiation phase, SSL is tunnelled over TDS packet type 0x12. After
            // negotiation, the underlying socket only sees SSL frames.
            if (!_encapsulate)
            {
                _stream.Write(buffer);
                _stream.Flush();
                return;
            }

            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.Write |SNI|INFO|SCOPE> writing encapsulated bytes"))
            {
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

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
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

            using (SNIEventScope.Create("<sc.SNI.SslOverTdsStream.WriteAsync |SNI|INFO|SCOPE> writing encapsulated bytes"))
            {
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
    }
}
