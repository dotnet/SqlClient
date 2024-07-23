// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// Provides buffer management for reader/writer implementations
    /// </summary>
    internal class TdsBufferManager
    {
        private readonly byte[] _buffer;

        /// <summary>
        /// Default constructor, instantiates buffer with 17 bytes byte array.
        /// </summary>
        public TdsBufferManager()
        {
            // 17: max bytes required for storage in buffer for decimal
            _buffer = new byte[17];
        }

        /// <summary>
        /// Get buffer for reading/writing data
        /// </summary>
        /// <param name="sizeInBytes">Buffer size requested.</param>
        /// <returns>Span for requested bytes</returns>
        internal Memory<byte> GetBuffer(int sizeInBytes) 
            => _buffer.AsMemory(0, sizeInBytes);
    }
}
