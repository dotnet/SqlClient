// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Buffers;
using System.Text;

namespace Microsoft.Data.SqlClient.Utilities
{
    internal static class BufferWriterExtensions
    {
        internal static long GetBytes(this Encoding encoding, string str, IBufferWriter<byte> bufferWriter)
        {
            var count = encoding.GetByteCount(str);
            var array = ArrayPool<byte>.Shared.Rent(count);

            try
            {
                var length = encoding.GetBytes(str, 0, str.Length, array, 0);
                bufferWriter.Write(array.AsSpan(0, length));
                return length;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }
}

#endif
