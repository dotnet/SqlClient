// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.UnitTests.IO.TdsHelpers
{
    internal class TdsServerPacket
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
