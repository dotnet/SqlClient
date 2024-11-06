#if NETFRAMEWORK

namespace System.IO
{
    // Helpers to read/write Span/Memory<byte> to Stream before netstandard 2.1
    internal static class StreamExtensions
    {
        internal static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = stream.Read(buffer, totalRead, count);
                if (read is 0)
                    throw new EndOfStreamException();

                totalRead += read;
                count -= read;
            }
        }
    }
}
#endif
