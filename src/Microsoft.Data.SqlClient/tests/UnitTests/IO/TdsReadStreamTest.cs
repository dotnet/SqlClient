// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.IO;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    public partial class TdsReadStreamTest
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadWholeSinglePacket(bool isAsync)
        {
            // Arrange
            int negotiatedPacketSize = 200;
            byte[] payload = new byte[100];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }
            byte messageType = TdsEnums.MT_PRELOGIN;
            int spid = new Random().Next();
            TdsMessage message = new(negotiatedPacketSize, payload, messageType, spid);

            byte[] underlyingStream = message.GetBytes();
            SplittableStream splitStream = new(underlyingStream, 200);

            // Act            
            using TdsReadStream stream = new TdsReadStream(splitStream);
            byte[] readBuffer = new byte[100];

            int readCount = isAsync ? await stream.ReadAsync(readBuffer, 0, 2) :
                                stream.Read(readBuffer, 0, 2);


            Assert.Equal(2, readCount);

            Assert.Equal(payload.AsSpan(0, readCount).ToArray(), readBuffer.AsSpan(0, readCount).ToArray());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadPacketSplit(bool isAsync)
        {
            // Arrange
            int negotiatedPacketSize = 200;
            byte[] payload = new byte[100];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }
            byte messageType = TdsEnums.MT_PRELOGIN;
            int spid = new Random().Next();
            TdsMessage message = new TdsMessage(negotiatedPacketSize, payload, messageType, spid);

            byte[] underlyingStream = message.GetBytes();
            SplittableStream splitStream = new SplittableStream(underlyingStream, 4);

            // Act            
            using TdsReadStream stream = new TdsReadStream(splitStream);
            byte[] readBuffer = new byte[100];

            int readCount = isAsync ? await stream.ReadAsync(readBuffer, 0, 2) :
                                stream.Read(readBuffer, 0, 2);

            Assert.Equal(2, readCount);

            Assert.Equal(payload.AsSpan(0, readCount).ToArray(), readBuffer.AsSpan(0, readCount).ToArray());
        }

        /// <summary>
        /// This test splits the packet sending so that the partial header is 
        /// received in one underlying stream read by the TdsReadStream.
        /// </summary>
        /// <param name="isAsync"></param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadStream_ReadPacketSplitWithPartialHeader(bool isAsync)
        {
            // Arrange
            int negotiatedPacketSize = 200;
            byte[] payload = new byte[500];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }
            byte messageType = TdsEnums.MT_PRELOGIN;
            int spid = new Random().Next();
            TdsMessage message = new TdsMessage(negotiatedPacketSize, payload, messageType, spid);

            byte[] underlyingStream = message.GetBytes();
            SplittableStream splitStream = new SplittableStream(underlyingStream, negotiatedPacketSize + 3);

            // Act            
            using TdsReadStream stream = new TdsReadStream(splitStream);
            byte[] readBuffer = new byte[payload.Length];

            int readCount = isAsync ? await stream.ReadAsync(readBuffer, 0, payload.Length) :
                                stream.Read(readBuffer, 0, payload.Length);


            Assert.Equal(readBuffer.Length, readCount);

            Assert.Equal(payload.AsSpan(0, readCount).ToArray(), readBuffer.AsSpan(0, readCount).ToArray());
        }

        private class TdsMessage
        {
            private int _negotiatedPacketSize;

            private int _spid;
            private byte _messageType;
            private byte[] _data;
            private ArrayList _packets = new ArrayList();

            public TdsMessage(int negotiatedPacketSize, byte[] payload, byte messageType, int spid)
            {
                _data = payload;
                Assert.True(negotiatedPacketSize > TdsEnums.HEADER_LEN, "Negotiated packet size must be greater than header length.");
                _negotiatedPacketSize = negotiatedPacketSize;
                _spid = spid;
                _messageType = messageType;

                CreatePackets();
            }

            public byte[] GetBytes()
            {
                MemoryStream stream = new();
                foreach (TdsServerPacket packet in _packets)
                {
                    byte[] packetBytes = packet.GetBytes();
                    stream.Write(packetBytes.AsSpan());
                }
                return stream.ToArray();
            }

            /// <summary>
            /// Takes the payload and breaks it into packets of the negotiated size, with the packet header populated.
            /// </summary>
            /// <returns></returns>
            private void CreatePackets()
            {
                int offset = 0;

                // From the _data take _negotiatedPacketSize bytes - Header_len and create a packet
                // Do this till all the data is consumed and added to packets.

                while (offset < _data.Length)
                {
                    // The amount of data to be copied into the packet.
                    int maxDataInPacket = _negotiatedPacketSize - TdsEnums.HEADER_LEN;
                    int copyLength = Math.Min(maxDataInPacket, _data.Length - offset);
                    byte[] packetPayload = _data.AsSpan(offset, copyLength).ToArray();
                    offset += copyLength;
                    byte status = offset < _data.Length ? TdsEnums.ST_BATCH : TdsEnums.ST_EOM;
                    TdsServerPacket packet = new TdsServerPacket(_messageType, status, copyLength, _spid, packetPayload);
                    _packets.Add(packet);
                }
            }
        }

        private class TdsServerPacket
        {
            byte PacketHeaderType;
            byte PacketStatus;
            int PacketDataLength;
            int Spid;
            byte[] Content;

            public TdsServerPacket(byte packetHeaderType, byte packetStatus, int packetDataLength, int spid, byte[] content)
            {
                PacketHeaderType = packetHeaderType;
                PacketStatus = packetStatus;
                PacketDataLength = packetDataLength + TdsEnums.HEADER_LEN;
                Spid = spid;
                Content = content;
            }

            /// <summary>
            /// Convert the packet to a byte array with 8 byte header and the content.
            /// Header is 8 bytes long with the following format:
            /// PacketHeaderType (1 byte)
            /// PacketStatus (1 byte)
            /// PacketDataLength in Big Endian format (2 bytes)
            /// SPID in Big Endian format (2 bytes)
            /// Reserved (2 bytes)
            /// </summary>
            /// <returns></returns>
            public byte[] GetBytes()
            {
                byte[] bytes = new byte[8 + Content.Length];
                bytes[0] = PacketHeaderType;
                bytes[1] = PacketStatus;
                bytes[2] = (byte)(PacketDataLength >> 8);
                bytes[3] = (byte)(PacketDataLength & 0xFF);
                bytes[4] = (byte)(Spid >> 8);
                bytes[5] = (byte)(Spid & 0xFF);
                bytes[6] = 0;
                bytes[7] = 0;
                Content.CopyTo(bytes, 8);
                return bytes;
            }
        }
    }
}
