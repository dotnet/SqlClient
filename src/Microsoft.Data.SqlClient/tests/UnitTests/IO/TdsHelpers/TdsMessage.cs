// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO.TdsHelpers
{
    /// <summary>
    /// Represents a message in the TDS protocol.
    /// A message consists of a of packets.
    /// </summary>
    internal class TdsMessage
    {
        private int _negotiatedPacketSize;

        public int Spid { get; private set; }

        private byte _messageType;

        public byte[] Payload { get; }

        private ArrayList _packets = new ArrayList();

        public TdsMessage(int negotiatedPacketSize, byte[] payload, byte messageType, int spid)
        {
            Payload = payload;
            Assert.True(negotiatedPacketSize > TdsEnums.HEADER_LEN, "Negotiated packet size must be greater than header length.");
            _negotiatedPacketSize = negotiatedPacketSize;
            Spid = spid;
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

            while (offset < Payload.Length)
            {
                int bytesLeftToPacketize = Payload.Length - offset;
                // The amount of data to be copied into the packet.
                int maxDataInPacket = _negotiatedPacketSize - TdsEnums.HEADER_LEN;
                int copyLength = Math.Min(maxDataInPacket, bytesLeftToPacketize);
                byte[] packetPayload = Payload.AsSpan(offset, copyLength).ToArray();
                offset += copyLength;
                byte status = offset < Payload.Length ? TdsEnums.ST_BATCH : TdsEnums.ST_EOM;
                TdsServerPacket packet = new(_messageType, status, copyLength, Spid, packetPayload);
                _packets.Add(packet);
            }
        }
    }

}
