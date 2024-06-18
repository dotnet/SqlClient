using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    public class TdsWriteStreamTest
    {
        private readonly byte[] _packetStatuses = new byte[] { TdsEnums.ST_EOM, TdsEnums.ST_BATCH };
    
        [Fact]
        public void WriteStream_WriteSinglePacket()
        {
            using MemoryStream ms = new();
            using TdsWriteStream tdsWriteStream = new(ms);

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            byte[] inputBuffer = new byte[10];
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (byte)i;
            }
            tdsWriteStream.Write(inputBuffer, 0, inputBuffer.Length);

            tdsWriteStream.Flush();

            ms.Position = 0;

            byte[] memoryStreamReadBuffer = new byte[20];
            int lengthRead = ms.Read(memoryStreamReadBuffer, 0, memoryStreamReadBuffer.Length);
            Assert.Equal(inputBuffer.Length + TdsEnums.HEADER_LEN, lengthRead);

            TdsPacketHeader header = new(memoryStreamReadBuffer);

            Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);
            Assert.Equal(inputBuffer.Length + TdsEnums.HEADER_LEN, header.Length);
            Assert.Equal(TdsEnums.ST_EOM, header.Status);

            Assert.Equal(inputBuffer, header.Content);
        }


        [Fact]
        public void WriteStream_WriteMultiplePackets()
        {
            int packetSize = 20;
            using MemoryStream ms = new();
            using TdsWriteStream tdsWriteStream = new(ms);
            tdsWriteStream.SetPacketSize(packetSize);

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            byte[] inputBuffer = new byte[packetSize * 2];
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (byte)i;
            }

            // Multiple packets will be created and flushed.
            tdsWriteStream.Write(inputBuffer.AsSpan());
            tdsWriteStream.Flush();

            ms.Position = 0;
            int matchOffset = 0;
            // Allocate twice the needed buffer.
            byte[] memoryStreamReadBuffer = new byte[inputBuffer.Length * 2];
            TdsPacketHeader? header = null;
            do
            {
                int lengthRead = ms.Read(memoryStreamReadBuffer, 0, packetSize);
                Assert.True(lengthRead >= TdsEnums.HEADER_LEN, "The total length read was less than the header of TDS packet.");
                header = new(memoryStreamReadBuffer);

                Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);

                // Assert the packet type is either ST_EOM or ST_BATCH.
                Assert.Contains(header.Status, _packetStatuses);
                if (header.Status == TdsEnums.ST_BATCH)
                {
                    Assert.Equal(packetSize, header.Length);
                    // When Batch packets are sent out, they are expected to be completely full to the packet size.
                    Assert.Equal(lengthRead, packetSize);
                }

                // Match content 
                int contentLength = lengthRead - 8;
                byte[] expectedData = inputBuffer.AsSpan().Slice(matchOffset, contentLength).ToArray();
                Assert.Equal(expectedData, header.Content);
                // Move the match offset to the next packet.
                matchOffset += contentLength;
            } while (header?.Status != TdsEnums.ST_EOM);
        }

        [Fact]
        public async void WriteStream_WriteMultiplePacketsAsync()
        {
            int packetSize = 20;
            using MemoryStream ms = new();
            using TdsWriteStream tdsWriteStream = new(ms);
            tdsWriteStream.SetPacketSize(packetSize);

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            byte[] inputBuffer = new byte[packetSize*2];
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (byte)i;
            }

            // Multiple packets will be created and flushed.
            await tdsWriteStream.WriteAsync(inputBuffer.AsMemory(), CancellationToken.None);
            await tdsWriteStream.FlushAsync();

            ms.Position = 0;
            int matchOffset = 0;

            // Allocate twice the needed buffer.
            byte[] memoryStreamReadBuffer = new byte[inputBuffer.Length * 2];
            TdsPacketHeader? header = null;
            do
            {
                int lengthRead = ms.Read(memoryStreamReadBuffer, 0, packetSize);
                Assert.True(lengthRead >= TdsEnums.HEADER_LEN, "The total length read was less than the header of TDS packet.");
                header = new(memoryStreamReadBuffer);

                Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);

                // Assert the packet type is either ST_EOM or ST_BATCH.
                Assert.Contains(header.Status, _packetStatuses);
                if (header.Status == TdsEnums.ST_BATCH)
                {
                    Assert.Equal(packetSize, header.Length);
                    // When Batch packets are sent out, they are expected to be completely full to the packet size.
                    Assert.Equal(lengthRead, packetSize);
                }
                // Match content 
                int contentLength = lengthRead - 8;
                byte[] expectedData = inputBuffer.AsSpan().Slice(matchOffset, contentLength).ToArray();
                Assert.Equal(expectedData, header.Content);
                // Move the match offset to the next packet.
                matchOffset += contentLength;
            } while(header?.Status != TdsEnums.ST_EOM);
        }

        [Fact]
        public async void WriteStream_WriteSinglePacketAsync()
        {
            await using MemoryStream ms = new();
            await using TdsWriteStream tdsWriteStream = new(ms);

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            byte[] inputBuffer = new byte[10];
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (byte)i;
            }
            await tdsWriteStream.WriteAsync(inputBuffer, 0, inputBuffer.Length);

            await tdsWriteStream.FlushAsync();

            ms.Position = 0;

            byte[] memoryStreamReadBuffer = new byte[20];
            int lengthRead = ms.Read(memoryStreamReadBuffer, 0, memoryStreamReadBuffer.Length);
            Assert.Equal(inputBuffer.Length + TdsEnums.HEADER_LEN, lengthRead);

            TdsPacketHeader header = new(memoryStreamReadBuffer);

            Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);
            Assert.Equal(inputBuffer.Length + TdsEnums.HEADER_LEN, header.Length);
            Assert.Equal(TdsEnums.ST_EOM, header.Status);
        }

        [Fact]
        public async void WriteStream_WriteByteAsync()
        {
            await using MemoryStream ms = new();
            await using TdsWriteStream tdsWriteStream = new(ms);

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            
            await tdsWriteStream.WriteByteAsync(0x12, isAsync: true, CancellationToken.None).ConfigureAwait(false);

            await tdsWriteStream.FlushAsync();

            ms.Position = 0;

            byte[] memoryStreamReadBuffer = new byte[20];
            int lengthRead = ms.Read(memoryStreamReadBuffer, 0, memoryStreamReadBuffer.Length);
            Assert.Equal(1 + TdsEnums.HEADER_LEN, lengthRead);

            TdsPacketHeader header = new(memoryStreamReadBuffer);

            Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);
            Assert.Equal(1 + TdsEnums.HEADER_LEN, header.Length);
            Assert.Equal(TdsEnums.ST_EOM, header.Status);
        }

        [Fact]
        public void WriteStream_WriteByte()
        {
            using MemoryStream ms = new();
            using TdsWriteStream tdsWriteStream = new(ms);

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;

            tdsWriteStream.WriteByte(0x12);

            tdsWriteStream.Flush();

            ms.Position = 0;

            byte[] memoryStreamReadBuffer = new byte[20];
            int lengthRead = ms.Read(memoryStreamReadBuffer, 0, memoryStreamReadBuffer.Length);
            Assert.Equal(1 + TdsEnums.HEADER_LEN, lengthRead);

            TdsPacketHeader header = new(memoryStreamReadBuffer);

            Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);
            Assert.Equal(1 + TdsEnums.HEADER_LEN, header.Length);
            Assert.Equal(TdsEnums.ST_EOM, header.Status);
        }

        [Fact]
        public void WriteStream_ReplaceStreamTest()
        {
            using MemoryStream msOriginal = new();
            using MemoryStream msReplaced = new();
            using TdsWriteStream tdsWriteStream = new(msOriginal);
            tdsWriteStream.ReplaceUnderlyingStream(msReplaced);

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            byte[] inputBuffer = new byte[10];
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (byte)i;
            }
            tdsWriteStream.Write(inputBuffer, 0, inputBuffer.Length);

            tdsWriteStream.Flush();

            msOriginal.Position = 0;
            msReplaced.Position = 0;

            byte[] buffer = new byte[20];
            int orignalStreamBytesRead = msOriginal.Read(buffer, 0, 20);
            int replacedStreamBytesRead = msReplaced.Read(buffer, 0, 20);

            Assert.Equal(0, orignalStreamBytesRead);
            Assert.True(replacedStreamBytesRead > 0, "The replaced stream had no bytes read.");
        }

        [Fact]
        public void WriteStream_CapabilityTests()
        {
            using MemoryStream msOriginal = new();
            TdsWriteStream tdsWriteStream = new(msOriginal);

            Assert.Throws<NotSupportedException>(() => tdsWriteStream.Length);
            Assert.Throws<NotSupportedException>(() => tdsWriteStream.Position);
            Assert.Throws<NotSupportedException>(() => tdsWriteStream.Position = 1);
            Assert.Throws<NotSupportedException>(() => tdsWriteStream.Seek(0, SeekOrigin.Begin));
            Assert.Throws<NotSupportedException>(() => tdsWriteStream.SetLength(88));
            Assert.Throws<NotSupportedException>(() => tdsWriteStream.Read(new byte[1], 0 , 1));

            Assert.False(tdsWriteStream.CanSeek);
            Assert.False(tdsWriteStream.CanRead);
            Assert.True(tdsWriteStream.CanWrite);
            tdsWriteStream.Dispose();
            Assert.False(tdsWriteStream.CanWrite);
        }


        private class TdsPacketHeader
        {
            public byte PacketType { get; set; }
            public byte Status { get; }
            public short Length { get; }

            public byte[] Content { get;  }

            public TdsPacketHeader(byte[] packet)
            {
                PacketType = packet[0];
                Status = packet[1];
                Length = (short)(packet[2] << 8 | (packet[3]));
                Content = packet.AsSpan().Slice(8, Length - 8).ToArray();
            }
        }
    }
}
