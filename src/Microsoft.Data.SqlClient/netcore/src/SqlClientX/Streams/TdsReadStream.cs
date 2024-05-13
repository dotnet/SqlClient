using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.Streams
{
    internal class TdsReadStream : Stream
    {
        private byte[] Buffer;
        private Stream _UnderlyingStream;

        internal int ReadBufferOffset { get; private set; } = 0;

        internal int ReadBufferDataLength { get; private set; } = 0;

        internal int PacketDataLeft { get; private set; } = 0;

        internal int PacketHeaderDataLength { get; private set; } = 0;

        internal byte PacketType { get; private set; } = 0;
        public byte PacketStatus { get; private set; }

        public ScratchPad Scratchpad { get; } = new ScratchPad();

        internal TdsReadStream(Stream underlyingStream, int bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE)
        {
            Buffer = new byte[bufferSize];
            _UnderlyingStream = underlyingStream;
        }

        internal void UpdateBuffersize(int bufferSize)
        {
            Buffer = new byte[bufferSize];
        }

        internal void UpdateStream(Stream stream)
        {
            _UnderlyingStream = stream;
        }

        public override bool CanRead => true; // throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public async ValueTask SkipBytesAsync(int skipCount, bool isAsync, CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(skipCount);
            int lengthToFill = skipCount;
            int totalRead = 0;
            while (lengthToFill > 0)
            {
                if (PacketDataLeft == 0 || ReadBufferDataLength == ReadBufferOffset)
                    await PrepareBufferAsync(isAsync, ct).ConfigureAwait(false);

                // We can only read the minimum of what is left in the packet, what is left in the buffer, and what we need to fill
                // If we have the length available, then we read it, else we will read either the data in packet, or the 
                // data in buffer, whichever is smaller.
                // If the data spans multiple packets, then we will go ahead and read those packets.
                int lengthToCopy = Math.Min(Math.Min(PacketDataLeft, ReadBufferDataLength - ReadBufferOffset), lengthToFill);
                var copyFrom = new ReadOnlyMemory<byte>(Buffer, ReadBufferOffset, lengthToCopy);
                copyFrom.CopyTo(buffer.AsMemory().Slice(totalRead, lengthToFill));
                totalRead += lengthToCopy;
                lengthToFill -= lengthToCopy;
                ReadBufferOffset += lengthToCopy;
                PacketDataLeft -= lengthToCopy;
            }
            ArrayPool<byte>.Shared.Return(buffer);
        }

        /// <summary>
        /// Make sure to pass in a buffer that is expected and not more.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public override int Read(Span<byte> buffer)
        {
            int lengthToFill = buffer.Length;
            int totalRead = 0;
            while (lengthToFill > 0)
            {
                if (PacketDataLeft == 0 || ReadBufferDataLength == ReadBufferOffset)
                    _ = PrepareBufferAsync(isAsync: false, CancellationToken.None);

                // We can only read the minimum of what is left in the packet,
                // what is left in the buffer, and what we need to fill
                // If we have the length available, then we read it, else we will read either the data in packet, or the 
                // data in buffer, whichever is smaller.
                // If the data spans multiple packets, then we will go ahead and read those packets.
                int lengthToCopy = Math.Min(Math.Min(PacketDataLeft, ReadBufferDataLength - ReadBufferOffset), lengthToFill);
                var copyFrom = new ReadOnlySpan<byte>(Buffer, ReadBufferOffset, lengthToCopy);
                copyFrom.CopyTo(buffer.Slice(totalRead, lengthToFill));
                totalRead += lengthToCopy;
                lengthToFill -= lengthToCopy;
                ReadBufferOffset += lengthToCopy;
                PacketDataLeft -= lengthToCopy;
            }
            return totalRead;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int lengthToFill = buffer.Length;
            int totalRead = 0;
            while (lengthToFill > 0)
            {
                if (ReadBufferOffset > ReadBufferDataLength)
                {
                    throw new Exception($" 1 Invalid state with {ReadBufferOffset} offset being more than the data lenght" +
                        $" {ReadBufferDataLength}");
                }

                if (PacketDataLeft == 0 || ReadBufferDataLength <= ReadBufferOffset)
                    await PrepareBufferAsync(isAsync: true, cancellationToken).ConfigureAwait(false);

                if (ReadBufferOffset > ReadBufferDataLength)
                {
                    throw new Exception($"2 Invalid state with {ReadBufferOffset} offset being more than the data lenght" +
                        $" {ReadBufferDataLength}");
                }

                // We can only read the minimum of what is left in the packet, what is left in the buffer, and what we need to fill
                // If we have the length available, then we read it, else we will read either the data in packet, or the 
                // data in buffer, whichever is smaller.
                // If the data spans multiple packets, then we will go ahead and read those packets.
                int lengthToCopy = Math.Min(Math.Min(PacketDataLeft, ReadBufferDataLength - ReadBufferOffset), lengthToFill);
                try
                { 
                    var copyFrom = new ReadOnlyMemory<byte>(Buffer, ReadBufferOffset, lengthToCopy);
                    copyFrom.CopyTo(buffer.Slice(totalRead, lengthToFill));
                    totalRead += lengthToCopy;
                    lengthToFill -= lengthToCopy;
                    ReadBufferOffset += lengthToCopy;
                    PacketDataLeft -= lengthToCopy;

                    if (ReadBufferOffset > ReadBufferDataLength)
                    {
                        throw new Exception($"3 Invalid state with {ReadBufferOffset} offset being more than the data lenght" +
                            $" {ReadBufferDataLength}");
                    }

                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine(ReadBufferOffset + " " + ReadBufferDataLength + " " + PacketDataLeft
                        + " " + lengthToFill);
                    Console.WriteLine(lengthToCopy);
                    throw;
                }
                
            }

            return totalRead;
        }

        public override int ReadByte()
        {
            var oneByteArray = new byte[1];
            int r = Read(oneByteArray);
            return r == 0 ? -1 : oneByteArray[0];
        }


        private async ValueTask PrepareBufferAsync(bool isAsync, CancellationToken ct)
        {
            // Either we have read all the data from the packet, and we have data left in the buffer
            if (PacketDataLeft == 0 && ReadBufferDataLength > ReadBufferOffset)
            {
                await ProcessHeaderAsync(isAsync, ct).ConfigureAwait(false);
            }

            if (ReadBufferOffset == ReadBufferDataLength)
            {
                // We have read all the data from the buffer, so we need to read more data from the stream
                if (PacketDataLeft > 0)
                {
                    ReadBufferDataLength = isAsync ?
                        await _UnderlyingStream.ReadAsync(Buffer, ct).ConfigureAwait(false) :
                        _UnderlyingStream.Read(Buffer);

                    ReadBufferOffset = 0;
                }
                else if (PacketDataLeft == 0)
                {
                    ReadBufferDataLength = isAsync ?
                        await _UnderlyingStream.ReadAsync(Buffer, ct).ConfigureAwait(false) :
                        _UnderlyingStream.Read(Buffer);

                    // Reset the offset to 0, since we read new data.
                    ReadBufferOffset = 0;
                    await ProcessHeaderAsync(isAsync, ct).ConfigureAwait(false);

                    if (ReadBufferDataLength == ReadBufferOffset)
                    {
                        ReadBufferDataLength = isAsync ?
                            await _UnderlyingStream.ReadAsync(Buffer, ct).ConfigureAwait(false) :
                            _UnderlyingStream.Read(Buffer);
                    }
                }
            }
        }

        private async ValueTask ProcessHeaderAsync(bool isAsync, CancellationToken ct)
        {
            if (ReadBufferDataLength - ReadBufferOffset < 8)
            {
                ReadBufferDataLength += isAsync ? await _UnderlyingStream.ReadAtLeastAsync(
                    Buffer.AsMemory(ReadBufferOffset), 
                    8 - (ReadBufferDataLength - ReadBufferOffset), 
                    cancellationToken: ct).ConfigureAwait(false)
                    : _UnderlyingStream.ReadAtLeast(Buffer.AsSpan(ReadBufferOffset),
                    8 - (ReadBufferDataLength - ReadBufferOffset));
            }

            PacketType = Buffer[ReadBufferOffset];
            PacketStatus = Buffer[ReadBufferOffset + 1];
            PacketDataLeft = (Buffer[ReadBufferOffset + TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8
                | Buffer[ReadBufferOffset + TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - TdsEnums.HEADER_LEN;
            // Ignore SPID and Window

            ReadBufferOffset += TdsEnums.HEADER_LEN;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        internal void ResetPacket()
        {
            ReadBufferDataLength = 0;
            ReadBufferOffset = 0;
            PacketDataLeft = 0;
        }

        internal async Task<byte> PeekTokenAsync(bool isAsync, CancellationToken ct)
        {
            byte nextToken = await this.ReadByteAsync(isAsync, ct).ConfigureAwait(false);
            this.ReadBufferOffset--;
            this.PacketDataLeft++;
            return nextToken;
        }
    }

    internal class ScratchPad
    {
        internal int[] _decimalBits;

        public ScratchPad() 
        {
            this._decimalBits = new int[4];
        }
    }
}
