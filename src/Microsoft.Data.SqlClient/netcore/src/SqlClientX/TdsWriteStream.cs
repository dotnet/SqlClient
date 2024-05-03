using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SqlClientX
{
    /// <summary>
    /// This class is meant to Write the stream of data in TDS packets. 
    /// The header offered by the class is the latest header information set on it.
    /// </summary>
    internal class TdsWriteStream : Stream
    {
        private Stream _underlyingStream;

        private byte[] _WriteBuffer;

        // Start at the end of the Tds header
        internal int WriteBufferOffset { get; private set;} = TdsEnums.HEADER_LEN; 
       
        internal byte PacketHeaderType { get; set; } = 0; // This should be set before flushing the buffer.

        internal byte PacketNumber { get; private set; } = 1; // Packets always start with 1.

        public TdsWriteStream(Stream underLyingStream, int bufferSize = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE) : base()
        {
            this._underlyingStream = underLyingStream;
            this._WriteBuffer = new byte[bufferSize];
        }

        /// <summary>
        /// Should only be called after the login negotiation is done.
        /// </summary>
        /// <param name="readBufferSize"></param>
        public void UpdateBufferSize(int readBufferSize)
        {
            _WriteBuffer = new byte[readBufferSize];
        }

        public void UpdateStream(Stream stream)
        {
            _underlyingStream = stream;
        }

        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override async Task FlushAsync(CancellationToken ct)
        {
            await FlushAsync(ct, true).ConfigureAwait(false);
        }

        public override void Flush()
        {
            Flush(true);
        }

        /// <summary>
        /// Use hard flush when we definitely know that the end of packet is reached.
        /// </summary>
        /// <param name="ct"></param>
        /// <param name="hardFlush"></param>
        private async Task FlushAsync(CancellationToken ct, bool hardFlush = false)
        {
            Debug.Assert(PacketHeaderType != 0, "PacketHeaderType is not set. Cannot flush the buffer without setting the packet header type.");
            _WriteBuffer[0] = PacketHeaderType;
            byte status;
            if (hardFlush)
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

            await _underlyingStream.WriteAsync(_WriteBuffer.AsMemory<byte>(), ct).ConfigureAwait(false);

            // Reset the offset since we will start filling up the packet again.
            WriteBufferOffset = TdsEnums.HEADER_LEN;

            // If we are doing a hard flush, then make sure that the data definitely goes out.
            if (hardFlush)
            {
                await this._underlyingStream.FlushAsync(ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Use hard flush when we definitely know that the end of packet is reached.
        /// </summary>
        /// <param name="hardFlush"></param>
        private void Flush(bool hardFlush = false)
        {
            Debug.Assert(PacketHeaderType != 0, "PacketHeaderType is not set. Cannot flush the buffer without setting the packet header type.");
            _WriteBuffer[0] = PacketHeaderType;
            byte status;
            if (hardFlush)
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

            this._underlyingStream.Write(_WriteBuffer.AsSpan<byte>());

            // Reset the offset since we will start filling up the packet again.
            WriteBufferOffset = TdsEnums.HEADER_LEN;

            // If we are doing a hard flush, then make sure that the data definitely goes out.
            if (hardFlush)
            { 
                this._underlyingStream.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
                    Flush(false); // Send to network.
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
            // The buffer may not have enough space. Write what we can and then flush the buffer with a soft flush.
            while (len > 0)
            {
                if (len > _WriteBuffer.Length - WriteBufferOffset)
                {
                    // Only a part of the length fits in the buffer.
                    int bytesToWrite = _WriteBuffer.Length - WriteBufferOffset;
                    buffer.Slice(0, bytesToWrite).CopyTo(_WriteBuffer.AsMemory<byte>(WriteBufferOffset));
                    WriteBufferOffset += bytesToWrite;
                    len -= bytesToWrite;
                    await FlushAsync(cancellationToken, false).ConfigureAwait(false); // Send to network.
                }
                else
                {
                    // The whole length can be added to the buffer.
                    buffer.CopyTo(_WriteBuffer.AsMemory<byte>(WriteBufferOffset));
                    WriteBufferOffset += len;
                    len = 0;
                }
            }

        }


    }
}
