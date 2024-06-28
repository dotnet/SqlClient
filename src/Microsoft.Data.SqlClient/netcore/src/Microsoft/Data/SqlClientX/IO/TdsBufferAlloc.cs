// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// Provides buffer management for reader/writer implementations
    /// </summary>
    internal class TdsBufferAlloc 
    {
        private readonly byte[] _buffer;

        /// <summary>
        /// Default constructor, instantiates buffer with 17 bytes byte array.
        /// </summary>
        public TdsBufferAlloc()
        { 
            // 17: max bytes required for storage in buffer for decimal
            _buffer = new byte[17];
        }

        /// <summary>
        /// Get buffer for reading/writing data
        /// Accepted sizes are: 2, 4, 8, 17
        /// </summary>
        /// <param name="sizeInBytes">2, 4, 8 or 17 bytes of buffer size.</param>
        /// <returns>Span for requested bytes</returns>
        internal Span<byte> GetBuffer(int sizeInBytes)
        {
            Debug.Assert(sizeInBytes == 2 || sizeInBytes == 4 || sizeInBytes == 8 || sizeInBytes == 17);
            return _buffer.AsSpan()[0..sizeInBytes];
        }
    }
}
