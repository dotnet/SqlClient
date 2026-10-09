// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.SqlServer.TDS.TransactionManager
{
    /// <summary>Local transaction-manager operations supported by the connectivity test peer.</summary>
    public enum TDSTransactionManagerRequest : ushort
    {
        Begin = 5,
        Commit = 7,
        Rollback = 8
    }

    /// <summary>Parses the TDS 7.4 transaction descriptor header and local BEGIN/COMMIT/ROLLBACK payload.</summary>
    public sealed class TDSTransactionManagerToken : TDSPacketToken
    {
        public ulong Descriptor { get; set; }
        public uint OutstandingRequests { get; set; } = 1;
        public TDSTransactionManagerRequest Request { get; set; }
        public byte IsolationLevel { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>Inflates a complete buffered request; truncated/unsupported headers and transaction operations fail explicitly.</summary>
        public override bool Inflate(Stream source)
        {
            using (var reader = new BinaryReader(source, Encoding.Unicode, leaveOpen: true))
            {
                if (reader.ReadUInt32() != 22 || reader.ReadUInt32() != 18 || reader.ReadUInt16() != 2)
                {
                    throw new NotSupportedException("The local transaction peer requires one transaction-descriptor header.");
                }
                Descriptor = reader.ReadUInt64();
                OutstandingRequests = reader.ReadUInt32();
                Request = (TDSTransactionManagerRequest)reader.ReadUInt16();
                if (Request == TDSTransactionManagerRequest.Begin)
                {
                    IsolationLevel = reader.ReadByte();
                    if (IsolationLevel < 1 || IsolationLevel > 5)
                    {
                        throw new NotSupportedException("Unsupported local transaction isolation level.");
                    }
                }
                else if (Request != TDSTransactionManagerRequest.Commit && Request != TDSTransactionManagerRequest.Rollback)
                {
                    throw new NotSupportedException("The local peer supports only BEGIN, COMMIT, and ROLLBACK.");
                }
                int length = reader.ReadByte();
                if ((length & 1) != 0)
                {
                    throw new InvalidDataException("Transaction names must contain complete UTF-16 code units.");
                }
                byte[] name = reader.ReadBytes(length);
                if (name.Length != length)
                {
                    throw new EndOfStreamException("Transaction name was truncated.");
                }
                Name = Encoding.Unicode.GetString(name);
                if (Request != TDSTransactionManagerRequest.Begin && reader.ReadByte() != 0)
                {
                    throw new NotSupportedException("Chained transaction flags are not supported by the local peer.");
                }
                if (source.Position != source.Length)
                {
                    throw new InvalidDataException("Unexpected trailing transaction-manager payload.");
                }
                return true;
            }
        }

        /// <summary>Serializes the supported request shape for parser round-trip tests.</summary>
        public override void Deflate(Stream destination)
        {
            if (Request != TDSTransactionManagerRequest.Begin &&
                Request != TDSTransactionManagerRequest.Commit && Request != TDSTransactionManagerRequest.Rollback)
            {
                throw new NotSupportedException("Unsupported local transaction request.");
            }
            byte[] name = Encoding.Unicode.GetBytes(Name);
            using (var writer = new BinaryWriter(destination, Encoding.Unicode, leaveOpen: true))
            {
                writer.Write(22u);
                writer.Write(18u);
                writer.Write((ushort)2);
                writer.Write(Descriptor);
                writer.Write(OutstandingRequests);
                writer.Write((ushort)Request);
                if (Request == TDSTransactionManagerRequest.Begin)
                {
                    writer.Write(IsolationLevel);
                }
                writer.Write(checked((byte)name.Length));
                writer.Write(name);
                if (Request != TDSTransactionManagerRequest.Begin)
                {
                    writer.Write((byte)0);
                }
            }
        }
    }
}
