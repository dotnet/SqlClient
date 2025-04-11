// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;

namespace Microsoft.Data.SqlClient.Utilities
{
    /// <summary>
    /// This is a collection of general object pools that can be reused as needed. 
    /// </summary>
    internal static class ObjectPools
    {
        private static readonly Lazy<ObjectPool<ArrayBufferWriter<byte>>> s_bufferWriterPool =
            new(() => new ObjectPool<ArrayBufferWriter<byte>>(
                maximumRetained: 20,
                onCreate: () => new ArrayBufferWriter<byte>(),
                onReturned: bufferWriter => bufferWriter.Clear()));

        internal static ObjectPool<ArrayBufferWriter<byte>> BufferWriter => s_bufferWriterPool.Value;
    }

}
