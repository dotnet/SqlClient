using System.Buffers;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    internal static class BufferWriterExtensions
    {
        internal static long GetBytes(this Encoding encoding, string str, IBufferWriter<byte> bufferWriter)
        {
            var count = encoding.GetByteCount(str);
            var array = ArrayPool<byte>.Shared.Rent(count);

            try
            {
                encoding.GetBytes(str, 0, str.Length, array, 0);
                bufferWriter.Write(array);
                return count;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }
}
