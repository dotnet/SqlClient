﻿using System;
using System.Buffers;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace simplesqlclient
{
    internal sealed class SslOverTdsStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override void Write(byte[] buffer, int offset, int count)
            => Write(buffer.AsSpan(offset, count));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

        private readonly Stream _stream;

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private bool _encapsulate;

        private int _packetBytes = 0;

        private const int PACKET_SIZE_WITHOUT_HEADER = TdsConstants.DEFAULT_LOGIN_PACKET_SIZE - TdsConstants.PACKET_HEADER_SIZE;


        public SslOverTdsStream(Stream stream, Guid connectionId = default)
        {
            _stream = stream;
            _encapsulate = true;
        }



        public override void Write(ReadOnlySpan<byte> buffer)
        {
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
                        int packetLength = TdsConstants.PACKET_HEADER_SIZE + dataLength;

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

                        Span<byte> data = packetBuffer.AsSpan(TdsConstants.PACKET_HEADER_SIZE, dataLength);
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
        public override int Read(Span<byte> buffer)
        {
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
                    Span<byte> headerBytes = stackalloc byte[TdsConstants.PACKET_HEADER_SIZE];

                    // fetch the packet header to determine how long the packet is
                    int headerBytesRead = 0;
                    do
                    {
                        int headerBytesReadIteration = _stream.Read(headerBytes.Slice(headerBytesRead, TdsConstants.PACKET_HEADER_SIZE - headerBytesRead));
                        if (headerBytesReadIteration == 0)
                        {
                            // 0 means the connection was closed, tell the caller
                            return 0;
                        }
                        headerBytesRead += headerBytesReadIteration;
                    } while (headerBytesRead < TdsConstants.PACKET_HEADER_SIZE);

                    // read the packet data size from the header and store it in case it is needed for a subsequent call
                    _packetBytes = ((headerBytes[2] << 8) | headerBytes[2 + 1]) - TdsConstants.PACKET_HEADER_SIZE;

                    // read as much from the packet as the caller can accept
                    int packetBytesRead = _stream.Read(buffer.Slice(0, Math.Min(buffer.Length, _packetBytes)));
                    _packetBytes -= packetBytesRead;
                    return packetBytesRead;
                }
            }
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
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
                    byte[] headerBytes = ArrayPool<byte>.Shared.Rent(TdsConstants.PACKET_HEADER_SIZE);

                    // fetch the packet header to determine how long the packet is
                    int headerBytesRead = 0;
                    do
                    {
                        int headerBytesReadIteration;
                        {
                            ValueTask<int> headerReadValueTask = _stream.ReadAsync(headerBytes.AsMemory(headerBytesRead, (TdsConstants.PACKET_HEADER_SIZE - headerBytesRead)), cancellationToken);
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
                    } while (headerBytesRead < TdsConstants.PACKET_HEADER_SIZE);

                    // read the packet data size from the header and store it in case it is needed for a subsequent call
                    _packetBytes = ((headerBytes[TdsConstants.PACKET_HEADER_SIZE] << 8) | headerBytes[2 + 1]) - TdsConstants.PACKET_HEADER_SIZE;

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

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
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
                        int packetLength = TdsConstants.PACKET_HEADER_SIZE + dataLength;

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

                        remaining.Span.Slice(0, dataLength).CopyTo(packetBuffer.AsSpan(TdsConstants.PACKET_HEADER_SIZE, dataLength));

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

        private static void SetupPreLoginPacketHeader(byte[] buffer, int dataLength, int remainingLength)
        {
            // We can only send 4088 bytes in one packet. Header[1] is set to 1 if this is a 
            // partial packet (whether or not count != 0).
            buffer[7] = 0; // touch this first for the jit bounds check
            buffer[0] = 0x12;
            buffer[1] = (byte)(remainingLength > 0 ? 0 : 1);
            buffer[2] = (byte)((dataLength + 8) / 0x100);
            buffer[3] = (byte)((dataLength + 8) % 0x100);
            buffer[4] = 0;
            buffer[5] = 0;
            buffer[6] = 0;
        }

        public override void Flush()
        {
            // Can sometimes get Pipe broken errors from flushing a PipeStream.
            // PipeStream.Flush() also doesn't do anything, anyway.
            if (!(_stream is PipeStream))
            {
                _stream.Flush();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public void FinishHandshake()
        {
            _encapsulate = false;
        }
    }
}