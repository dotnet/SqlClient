// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    // This is a collection of general object pools that can be reused as needed.
    internal static class SqlObjectPools
    {
        private static SqlObjectPool<ArrayBufferWriter<byte>> _bufferWriter;

        internal static SqlObjectPool<ArrayBufferWriter<byte>> BufferWriter
        {
            get
            {
                if (_bufferWriter is null)
                {
                    // This is a shared pool that will retain the last 20 writers to be reused. If more than 20 are requested at a time,
                    // they will not be retained when returned to the pool.
                    Interlocked.CompareExchange(ref _bufferWriter, new(20, () => new(), a => a.Clear()), null);
                }

                return _bufferWriter;
            }
        }
    }

}
