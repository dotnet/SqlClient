using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace simplesqlclient
{
    internal class BufferWriter
    {
        byte[] outBuffer;
        //byte[] inBuffer;
        private Stream transportStream;
        int offset = 8; // We write the data at the offset of the header, and fill in the header later
        
        byte packetCount = 1;

        public BufferWriter(int bufferSize, Stream transportStream)
        {
            outBuffer = new byte[bufferSize];
            //inBuffer = new byte[bufferSize];
            this.transportStream = transportStream;
        }

        internal PacketType PacketType { get; set; }

        internal bool FlushPacket(PacketFlushMode flushMode)
        {
            // Write the header
            outBuffer[0] = (byte)PacketType;
            
            outBuffer[2] = (byte)(offset >> 8);
            outBuffer[3] = (byte)(offset & 0xff);
            outBuffer[4] = 0;
            outBuffer[5] = 0;
            outBuffer[6] = packetCount;
            outBuffer[7] = 0;
            

            switch (flushMode)
            {
                case PacketFlushMode.SOFTFLUSH:
                    outBuffer[1] = (byte)PacketStatus.BATCH;
                    packetCount++;
                    break;
                case PacketFlushMode.HARDFLUSH:
                    outBuffer[1] = (byte)PacketStatus.EOM;
                    ResetBytesAndPacketCount();
                    break;
                default:
                    Debug.Assert(false, "Unknown flush mode");
                    break;
            }
            try
            {
                transportStream.Write(outBuffer, 0, offset);
                ResetBuffer();
            }
            catch (Exception)
            {
                // Handle various exceptions.
                return false;
            }
            return true;
        }

        public void WriteByteArray(Span<byte> data)
        {
            int offsetInInput = 0;
            // If length of the data to be written exceeds the buffer size, write the data to the stream
            do
            { 
                if (data.Length + offset > outBuffer.Length)
                {
                    int remainderInOutBuffer = outBuffer.Length - offset;
                    // Copy partial data from input Span to the output buffer.
                    data.Slice(offsetInInput, remainderInOutBuffer).CopyTo(outBuffer.AsSpan(offset, remainderInOutBuffer));
                    // The offset of the buffer moves ahead to indicate the end of the data written.
                    offset += remainderInOutBuffer;
                    offsetInInput += remainderInOutBuffer;
                    data = data[offsetInInput..];
                    // Since the buffer is full, flush the data to the stream.
                    this.FlushPacket(PacketFlushMode.SOFTFLUSH);
                }
                else
                {
                    data.CopyTo(outBuffer.AsSpan(offset));
                    offset += data.Length;

                    // At this point, we have read all the data from the input
                    // No point resetting data for minor optimizations.
                    // Simply break out of the loop.
                    break;
                }
            } while (data.Length > 0);
        }

        private void ResetBytesAndPacketCount()
        {
            this.packetCount = 0;
        }

        private void ResetBuffer()
        {
            offset = TdsConstants.PACKET_HEADER_SIZE;
        }

        public void WriteByte(byte data)
        {
            // If length of the data to be written exceeds the buffer size, write the data to the stream
            if (1 + offset >= outBuffer.Length)
            {
                this.FlushPacket(PacketFlushMode.SOFTFLUSH);
            }
            // Store the data in the buffer.
            outBuffer[offset++] = data;           
        }

        private void WriteDecimal(decimal value)
        {
            Span<int> decimalBits = stackalloc int[4];
            decimal.GetBits(value, decimalBits);

            /*
             Returns a binary representation of a Decimal. The return value is an integer
             array with four elements. Elements 0, 1, and 2 contain the low, middle, and
             high 32 bits of the 96-bit integer part of the Decimal. Element 3 contains
             the scale factor and sign of the Decimal: bits 0-15 (the lower word) are
             unused; bits 16-23 contain a value between 0 and 28, indicating the power of
             10 to divide the 96-bit integer part by to produce the Decimal value; bits 24-
             30 are unused; and finally bit 31 indicates the sign of the Decimal value, 0
             meaning positive and 1 meaning negative.

             SQLDECIMAL/SQLNUMERIC has a byte stream of:
             struct {
                 BYTE sign; // 1 if positive, 0 if negative
                 BYTE data[];
             }

             For TDS 7.0 and above, there are always 17 bytes of data
            */

            // write the sign (note that COM and SQL are opposite)
            if (0x80000000 == (decimalBits[3] & 0x80000000))
                WriteByte(0);
            else
                WriteByte(1);

            WriteInt(decimalBits[0]);
            WriteInt(decimalBits[1]);
            WriteInt(decimalBits[2]);
            WriteInt(0);
        }

        internal void WriteInt(int integerValue)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer, integerValue);
            if ((offset + 4) > buffer.Length)
            {
                // if all of the int doesn't fit into the buffer
                for (int index = 0; index < sizeof(int); index++)
                {
                    WriteByte(buffer[index]);
                }
            }
            else
            {
                // all of the int fits into the buffer
                buffer.CopyTo(outBuffer.AsSpan(offset, sizeof(int)));
                offset += 4;
            }
        }

        internal void WriteShort(int v)
        {
            if ((offset + 2) > outBuffer.Length)
            {
                // if all of the short doesn't fit into the buffer
                WriteByte((byte)(v & 0xff));
                WriteByte((byte)((v >> 8) & 0xff));
            }
            else
            {
                // all of the short fits into the buffer
                outBuffer[offset] = (byte)(v & 0xff);
                outBuffer[offset + 1] = (byte)((v >> 8) & 0xff);
                offset += 2;
            }
        }

        internal void WriteLong(long v)
        {
            WriteByte((byte)(v & 0xff));
            WriteByte((byte)((v >> 8) & 0xff));
            WriteByte((byte)((v >> 16) & 0xff));
            WriteByte((byte)((v >> 24) & 0xff));
            WriteByte((byte)((v >> 32) & 0xff));
            WriteByte((byte)((v >> 40) & 0xff));
            WriteByte((byte)((v >> 48) & 0xff));
            WriteByte((byte)((v >> 56) & 0xff));
        }

        internal void WriteString(string s)
        {
            int cBytes = TdsConstants.CharSize * s.Length;

            // Perf shortcut: If it fits, write directly to the outBuff
            if (cBytes < (outBuffer.Length - offset))
            {
                CopyStringToBytes(s, 0, outBuffer, offset, s.Length);
                offset += cBytes;
            }
            else
            {
                Span<byte> tmp = stackalloc byte[cBytes];
                CopyStringToBytes(s, 0, tmp.ToArray(), offset, s.Length);
                WriteByteArray(tmp);
            }
        }

        private static void CopyStringToBytes(string source, int sourceOffset, byte[] dest, int destOffset, int charLength)
        {
            Encoding.Unicode.GetBytes(source, sourceOffset, charLength, dest, destOffset);
        }

        private static void CopyStringToBytes(string source, int sourceOffset, Span<byte> dest, int destOffset, int charLength)
        {
            Encoding.Unicode.GetBytes(source.AsSpan(sourceOffset, charLength), dest.Slice(destOffset));
        }

        internal void UpdateStream(Stream sslStream)
        {
            this.transportStream = sslStream;
        }

        
    }


    
}
