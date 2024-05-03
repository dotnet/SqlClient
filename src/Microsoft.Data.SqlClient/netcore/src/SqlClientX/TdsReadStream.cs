using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX
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

        internal TdsReadStream(Stream underlyingStream, int bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE)
        {
            Buffer = new byte[bufferSize];
            this._UnderlyingStream = underlyingStream;
        }

        internal void UpdateBuffersize(int bufferSize)
        {
            Buffer = new byte[bufferSize];
        }

        internal void UpdateStream(Stream stream)
        {
            _UnderlyingStream = stream;
        }

        public override bool CanRead => throw new NotImplementedException();

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
            // Do we have any data?
            throw new NotImplementedException();
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
                if ((PacketDataLeft == 0) || (ReadBufferDataLength == ReadBufferOffset))
                    PrepareBuffer();

                // We can only read the minimum of what is left in the packet, what is left in the buffer, and what we need to fill
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
                if ((PacketDataLeft == 0) || (ReadBufferDataLength == ReadBufferOffset))
                    await PrepareBufferAsync();

                // We can only read the minimum of what is left in the packet, what is left in the buffer, and what we need to fill
                // If we have the length available, then we read it, else we will read either the data in packet, or the 
                // data in buffer, whichever is smaller.
                // If the data spans multiple packets, then we will go ahead and read those packets.
                int lengthToCopy = Math.Min(Math.Min(PacketDataLeft, ReadBufferDataLength - ReadBufferOffset), lengthToFill);
                var copyFrom = new ReadOnlyMemory<byte>(Buffer, ReadBufferOffset, lengthToCopy);
                copyFrom.CopyTo(buffer.Slice(totalRead, lengthToFill));
                totalRead += lengthToCopy;
                lengthToFill -= lengthToCopy;
                ReadBufferOffset += lengthToCopy;
                PacketDataLeft -= lengthToCopy;
            }

            return totalRead;
        }

        public override int ReadByte()
        {
            var oneByteArray = new byte[1];
            int r = Read(oneByteArray);
            return r == 0 ? -1 : oneByteArray[0];
        }

        private void PrepareBuffer()
        {
            // Either we have read all the data from the packet, and we have data left in the buffer
            if (PacketDataLeft == 0 && (ReadBufferDataLength > ReadBufferOffset))
            {
                ProcessHeader();
            }

            if (ReadBufferOffset == ReadBufferDataLength)
            {
                // We have read all the data from the buffer, so we need to read more data from the stream
                if (PacketDataLeft > 0)
                {
                    ReadBufferDataLength = _UnderlyingStream.Read(Buffer);
                }
                else if (PacketDataLeft == 0)
                {
                    ReadBufferDataLength = _UnderlyingStream.Read(Buffer);
                    ProcessHeader();

                    if (ReadBufferDataLength == ReadBufferOffset)
                    {
                        ReadBufferDataLength = _UnderlyingStream.Read(Buffer);
                    }
                }
            }
        }

        private async ValueTask PrepareBufferAsync()
        {
            // Either we have read all the data from the packet, and we have data left in the buffer
            if (PacketDataLeft == 0 && (ReadBufferDataLength > ReadBufferOffset))
            {
                await ProcessHeaderAsync();
            }

            if (ReadBufferOffset == ReadBufferDataLength)
            {
                // We have read all the data from the buffer, so we need to read more data from the stream
                if (PacketDataLeft > 0)
                {
                    ReadBufferDataLength = await _UnderlyingStream.ReadAsync(Buffer).ConfigureAwait(false);
                }
                else if (PacketDataLeft == 0)
                {
                    ReadBufferDataLength = await _UnderlyingStream.ReadAsync(Buffer).ConfigureAwait(false);
                    await ProcessHeaderAsync().ConfigureAwait(false);

                    if (ReadBufferDataLength == ReadBufferOffset)
                    {
                        ReadBufferDataLength = await _UnderlyingStream.ReadAsync(Buffer).ConfigureAwait(false);
                    }
                }
            }
        }

        private void ProcessHeader()
        {
            if (ReadBufferDataLength - ReadBufferOffset < 8)
            {
                ReadBufferDataLength += _UnderlyingStream.ReadAtLeast(Buffer.AsSpan(ReadBufferOffset), 8 - (ReadBufferDataLength - ReadBufferOffset));
            }

            PacketType = Buffer[ReadBufferOffset];
            PacketStatus = Buffer[ReadBufferOffset + 1];
            PacketDataLeft = (Buffer[ReadBufferOffset + TdsEnums.HEADER_LEN_FIELD_OFFSET] << 8
                | Buffer[ReadBufferOffset + TdsEnums.HEADER_LEN_FIELD_OFFSET + 1]) - TdsEnums.HEADER_LEN;
            // Ignore SPID and Window

            ReadBufferOffset += TdsEnums.HEADER_LEN;
        }

        private async ValueTask ProcessHeaderAsync()
        {
            if (ReadBufferDataLength - ReadBufferOffset < 8)
            {
                ReadBufferDataLength += await _UnderlyingStream.ReadAtLeastAsync(Buffer.AsMemory(ReadBufferOffset), 8 - (ReadBufferDataLength - ReadBufferOffset));
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
    }
}
