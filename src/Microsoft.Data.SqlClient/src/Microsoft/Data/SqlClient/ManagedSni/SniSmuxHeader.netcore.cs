// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Buffers.Binary;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// SMUX packet header
    /// </summary>
    internal sealed class SniSmuxHeader
    {
        public const int HEADER_LENGTH = 16;

        public byte SMID;
        public byte flags;
        public ushort sessionId;
        public uint length;
        public uint sequenceNumber;
        public uint highwater;

        public void Read(byte[] bytes)
        {
            SMID = bytes[0];
            flags = bytes[1];
            Span<byte> span = bytes.AsSpan();
            sessionId = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2));
            length = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4)) - SniSmuxHeader.HEADER_LENGTH;
            sequenceNumber = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8));
            highwater = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12));
        }

        public void Write(Span<byte> bytes)
        {
            uint value = highwater;
            // access the highest element first to cause the largest range check in the jit, then fill in the rest of the value and carry on as normal
            bytes[15] = (byte)((value >> 24) & 0xff);
            bytes[12] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.highwater).CopyTo(headerBytes, 12);
            bytes[13] = (byte)((value >> 8) & 0xff);
            bytes[14] = (byte)((value >> 16) & 0xff);

            bytes[0] = SMID; // BitConverter.GetBytes(_currentHeader.SMID).CopyTo(headerBytes, 0);
            bytes[1] = flags; // BitConverter.GetBytes(_currentHeader.flags).CopyTo(headerBytes, 1);

            value = sessionId;
            bytes[2] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.sessionId).CopyTo(headerBytes, 2);
            bytes[3] = (byte)((value >> 8) & 0xff);

            value = length;
            bytes[4] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.length).CopyTo(headerBytes, 4);
            bytes[5] = (byte)((value >> 8) & 0xff);
            bytes[6] = (byte)((value >> 16) & 0xff);
            bytes[7] = (byte)((value >> 24) & 0xff);

            value = sequenceNumber;
            bytes[8] = (byte)(value & 0xff); // BitConverter.GetBytes(_currentHeader.sequenceNumber).CopyTo(headerBytes, 8);
            bytes[9] = (byte)((value >> 8) & 0xff);
            bytes[10] = (byte)((value >> 16) & 0xff);
            bytes[11] = (byte)((value >> 24) & 0xff);

        }
    }
}

#endif
