using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.Streams
{
    internal static class TdsWriteStreamExtensions
    {
        internal static async ValueTask WriteIntAsync(this TdsWriteStream stream, 
            int integerValue,
            bool isAsync,
            CancellationToken ct)
        {
            AssertSpace<int>(stream);
            int size = sizeof(int);
            byte[] tmp = ArrayPool<byte>.Shared.Rent(size);
            BinaryPrimitives.TryWriteInt32LittleEndian(tmp.AsSpan()[..size], integerValue);
            if (isAsync)
            {
                await stream.WriteAsync(tmp.AsMemory()[..size], ct).ConfigureAwait(false);
            }
            else
            {
                stream.Write(tmp.AsSpan()[..size]);
            }
            ArrayPool<byte>.Shared.Return(tmp);
        }

        internal static async ValueTask WriteShortAsync(
            this TdsWriteStream stream, 
            int value,
            bool isAsync,
            CancellationToken ct)
        {
            AssertSpace<short>(stream);
            int size = sizeof(short);
            byte[] tmp = ArrayPool<byte>.Shared.Rent(size);
            BinaryPrimitives.TryWriteInt16LittleEndian(tmp.AsSpan()[..size], (short)value);
            if (isAsync)
            {
                await stream.WriteAsync(tmp.AsMemory()[..size], ct).ConfigureAwait(false);
            }
            else
            {
                stream.Write(tmp.AsSpan()[..size]);
            }
            ArrayPool<byte>.Shared.Return(tmp);
        }

        internal static async ValueTask WriteStringAsync(this TdsWriteStream stream, string s,
            bool isAsync,
            CancellationToken ct)
        {
            int cBytes = TdsConstants.CharSize * s.Length;
            byte[] tmp = ArrayPool<byte>.Shared.Rent(cBytes);
            CopyStringToBytes(s, 0, tmp, 0, s.Length);
            if (isAsync)
            { 
                await stream.WriteAsync(tmp.AsMemory()[..cBytes], ct).ConfigureAwait(false);
            }
            else
            {
                stream.Write(tmp.AsSpan(0, cBytes));
            }
            ArrayPool<byte>.Shared.Return(tmp);
        }

        internal static async ValueTask WriteLongAsync(this TdsWriteStream stream,
            long v,
            bool isAsync,
            CancellationToken ct)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(sizeof(long));
            BinaryPrimitives.TryWriteInt64LittleEndian(rented.AsSpan()[..sizeof(long)], v);
            if (isAsync)
            {
                await stream.WriteAsync(rented.AsMemory()[..sizeof(long)],
                    ct).ConfigureAwait(false);
            }
            else
            {
                stream.Write(rented.AsSpan()[..sizeof(long)]);
            }
            ArrayPool<byte>.Shared.Return(rented);
        }

        internal static async ValueTask WriteArrayAsync(this TdsWriteStream stream, bool isAsync, byte[] bytes, CancellationToken ct)
        {
            if (isAsync)
            {
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            }
            else
            {
                stream.Write(bytes);
            }
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
