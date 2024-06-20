// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    /// <summary>
    /// Unit tests for the TdsWriteStream class.
    /// </summary>
    public class TdsWriteStreamTest
    {
        /// <summary>
        /// The expected packet statuses of a TDS Packet.
        /// </summary>
        private readonly byte[] _packetStatuses = new byte[] { TdsEnums.ST_EOM, TdsEnums.ST_BATCH };
    
        /// <summary>
        /// The test writes a single TDS packet to the stream,
        /// and verifies
        /// 1. Content of the packet
        /// 2. The expected header of the packet.
        /// </summary>
        [Fact]
        public void WriteStream_WriteSinglePacket()
        {
            using MemoryStream ms = new();
            using TdsWriteStream tdsWriteStream = new(ms);

            byte[] tdsDataBuffer = new byte[10];

            // Fill the buffer with some data.
            for (int i = 0; i < tdsDataBuffer.Length; i++)
            {
                tdsDataBuffer[i] = (byte)i;
            }

            // Set the packet header, write data and flush.
            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            tdsWriteStream.Write(tdsDataBuffer, 0, tdsDataBuffer.Length);
            tdsWriteStream.Flush();

            // Position the memory stream to the beginning to make sure we can read the data written to it.
            ms.Position = 0;

            // Assign a buffer to read the data from the memory stream.
            // The buffer size should be Header len more than the data bytes.
            int expectedTdsPacketLength = TdsEnums.HEADER_LEN + tdsDataBuffer.Length;
            byte[] readBufferForMemoryStream = new byte[expectedTdsPacketLength];
            int lengthRead = ms.Read(readBufferForMemoryStream, 0, readBufferForMemoryStream.Length);
            Assert.Equal(expectedTdsPacketLength, lengthRead);

            TdsPacketHeader header = new(readBufferForMemoryStream);

            Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);
            Assert.Equal(expectedTdsPacketLength, header.DataLength);

            // This is the last packet in the stream and hence we verify the status to be ST_EOM.
            Assert.Equal(TdsEnums.ST_EOM, header.Status);

            // Verify the content of the packet is as expected.
            Assert.Equal(tdsDataBuffer, header.Content);
        }

        /// <summary>
        /// Tests the async write of the single packet.
        /// This also exercises the DisposeAsync method.
        /// </summary>
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
            Assert.Equal(inputBuffer.Length + TdsEnums.HEADER_LEN, header.DataLength);
            Assert.Equal(TdsEnums.ST_EOM, header.Status);
        }

        /// <summary>
        /// Sends data which can span multiple packets and 
        /// verifies that that TDS packet packaging along with the content is correct.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void WriteStream_WriteMultiplePacketsAsync(bool isAsync)
        {
            int packetSize = 20;
            using MemoryStream ms = new();
            using TdsWriteStream tdsWriteStream = new(ms);
            tdsWriteStream.SetPacketSize(packetSize);

            byte[] inputBuffer = new byte[packetSize*2];
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (byte)i;
            }

            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            // Multiple packets will be created and flushed.
            if (isAsync)
            {
                await tdsWriteStream.WriteAsync(inputBuffer.AsMemory(), CancellationToken.None);
                await tdsWriteStream.FlushAsync();
            }
            else
            {
                tdsWriteStream.Write(inputBuffer.AsSpan());
                tdsWriteStream.Flush();
            }

            // Position to the beginning of the memory stream to read the data.
            ms.Position = 0;

            // Tracks the offset of data in the incoming buffer for matching the contents in multiple packets.
            int matchOffset = 0;

            // Allocate the buffer with enough space to read the header for multiple packets.
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

                // If the packet is not the last packet, then we validate that the packet is full 
                if (header.Status == TdsEnums.ST_BATCH)
                {
                    Assert.Equal(packetSize, header.DataLength);
                    // When Batch packets are sent out, they are expected to be completely full to the packet size.
                    Assert.Equal(lengthRead, packetSize);
                }
                // Match content 
                int contentLength = lengthRead - TdsEnums.HEADER_LEN;

                Assert.True(contentLength > 0, "The packet content length cannot be less than the header length.");
                byte[] expectedData = inputBuffer.AsSpan().Slice(matchOffset, contentLength).ToArray();
                Assert.Equal(expectedData, header.Content);
                // Move the match offset to the next packet.
                matchOffset += contentLength;
            } while(header?.Status != TdsEnums.ST_EOM);
        }


        /// <summary>
        /// Tests if the write byte works as expected
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void WriteStream_WriteByteAsync(bool isAsync)
        {
            await using MemoryStream ms = new();
            await using TdsWriteStream tdsWriteStream = new(ms);


            // Write a byte to the stream, and flush it. 
            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            if (isAsync)
            { 
                await tdsWriteStream.WriteByteAsync(0x12, isAsync: true, CancellationToken.None).ConfigureAwait(false);
                await tdsWriteStream.FlushAsync();
            }
            else
            {
                tdsWriteStream.WriteByte(0x12);
                tdsWriteStream.Flush();
            }
            ms.Position = 0;

            byte[] memoryStreamReadBuffer = new byte[20];
            int lengthRead = ms.Read(memoryStreamReadBuffer, 0, memoryStreamReadBuffer.Length);

            TdsPacketHeader header = new(memoryStreamReadBuffer);

            // Verify the amount of data from the stream gives us one more than packet header length.
            Assert.Equal(1 + TdsEnums.HEADER_LEN, lengthRead);
            // Verify the packet type is as expected.
            Assert.Equal((byte)TdsStreamPacketType.Login7, header.PacketType);
            // Verify that the packet header has the correct data length.
            Assert.Equal(1 + TdsEnums.HEADER_LEN, header.DataLength);
            // Verify the status of the packet is ST_EOM since its the last packet.
            Assert.Equal(TdsEnums.ST_EOM, header.Status);
        }


        /// <summary>
        /// Replaces the underlying stream of the TdsWriteStream and verifies that the data is written to the new stream.
        /// </summary>
        [Fact]
        public void WriteStream_ReplaceStreamTest()
        {
            using MemoryStream msOriginal = new();
            using MemoryStream msReplaced = new();
            using TdsWriteStream tdsWriteStream = new(msOriginal);
            tdsWriteStream.ReplaceUnderlyingStream(msReplaced);

            byte[] inputBuffer = new byte[10];
            for (int i = 0; i < inputBuffer.Length; i++)
            {
                inputBuffer[i] = (byte)i;
            }

            // Write the data to the stream.
            tdsWriteStream.PacketHeaderType = TdsStreamPacketType.Login7;
            tdsWriteStream.Write(inputBuffer, 0, inputBuffer.Length);
            tdsWriteStream.Flush();

            msOriginal.Position = 0;
            msReplaced.Position = 0;

            byte[] buffer = new byte[20];
            int orignalStreamBytesRead = msOriginal.Read(buffer, 0, 20);
            int replacedStreamBytesRead = msReplaced.Read(buffer, 0, 20);

            // The original stream should not have any bytes read.
            Assert.Equal(0, orignalStreamBytesRead);

            // The replacement of stream should cause the replaced stream to be written to.
            Assert.True(replacedStreamBytesRead > 0, "The replaced stream had no bytes read.");
        }

        /// <summary>
        /// Tests various Get/Set properties of the TdsWriteStream and verifies if they are set correctly
        /// or if they throw the expected exceptions.
        /// </summary>
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

        /// <summary>
        /// Represents the Tds Packet header.
        /// </summary>
        private class TdsPacketHeader
        {
            public byte PacketType { get; set; }
            public byte Status { get; }
            public short DataLength { get; }

            public byte[] Content { get;  }

            public TdsPacketHeader(byte[] packet)
            {
                PacketType = packet[0];
                Status = packet[1];
                DataLength = (short)(packet[2] << 8 | (packet[3]));
                Content = packet.AsSpan().Slice(8, DataLength - 8).ToArray();
            }
        }
    }
}
