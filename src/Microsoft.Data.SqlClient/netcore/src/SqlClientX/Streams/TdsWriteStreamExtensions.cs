using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.Streams
{
    internal static class TdsWriteStreamExtensions
    {
        internal static void WriteInt(this TdsWriteStream stream, int integerValue)
        {
            AssertSpace<int>(stream);
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer, integerValue);
            stream.Write(buffer);
        }

        internal static void WriteShort(this TdsWriteStream stream, short value)
        {
            AssertSpace<short>(stream);
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.TryWriteInt16LittleEndian(buffer, value);
            stream.Write(buffer);
        }

        internal static void WriteShort(this TdsWriteStream stream, int value)
        {
            AssertSpace<short>(stream);
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            BinaryPrimitives.TryWriteInt16LittleEndian(buffer, (short)value);
            stream.Write(buffer);
        }

        internal static void WriteString(this TdsWriteStream stream, string s)
        {
            int cBytes = TdsConstants.CharSize * s.Length;
            byte[] tmp = ArrayPool<byte>.Shared.Rent(cBytes);
            CopyStringToBytes(s, 0, tmp, 0, s.Length);
            stream.Write(tmp.AsSpan(0, cBytes));
            ArrayPool<byte>.Shared.Return(tmp);
        }

        internal static void WriteLong(this TdsWriteStream stream, long v)
        {
            Span<byte> bytes = stackalloc byte[sizeof(long)];
            BinaryPrimitives.TryWriteInt64LittleEndian(bytes, v);
            stream.Write(bytes);
        }

        private static void CopyStringToBytes(string source, int sourceOffset, byte[] dest, int destOffset, int charLength)
        {
            Encoding.Unicode.GetBytes(source, sourceOffset, charLength, dest, destOffset);
        }

        [Conditional("DEBUG")]
        private static unsafe void AssertSpace<T>(TdsWriteStream stream) where T : unmanaged
        {
            int size = sizeof(T);
            if (!stream.HasSpaceLeftFor(size))
            {
                throw new Exception("Not enough space");
            }
        }
    }
}
