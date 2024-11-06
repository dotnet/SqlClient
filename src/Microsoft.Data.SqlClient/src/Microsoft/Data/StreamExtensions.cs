#if NETFRAMEWORK

namespace System.IO
{
    // Helpers to read/write Span/Memory<byte> to Stream before netstandard 2.1
    internal static class StreamExtensions
    {
        internal static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            var totalRead = 0;
            do
            {
                var read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read is 0)
                    throw new EndOfStreamException();

                totalRead += read;
            } while (totalRead < count);
        }
    }
}
#endif
