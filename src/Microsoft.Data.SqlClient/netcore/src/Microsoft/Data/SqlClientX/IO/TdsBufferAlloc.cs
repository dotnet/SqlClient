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
    internal abstract class TdsBufferAlloc
    {
        /// <summary>
        /// Scratch buffer to serialize Short values (2 bytes).
        /// </summary>
        private byte[] _2Buffer;

        /// <summary>
        /// Scratch buffer to serialize Int values (4 bytes).
        /// </summary>
        private byte[] _4Buffer;

        /// <summary>
        /// Scratch buffer to serialize Long values (8 bytes).
        /// </summary>
        private byte[] _8Buffer;

        /// <summary>
        /// Scratch buffer to serialize Decimal values (16 bytes).
        /// </summary>
        private byte[] _16Buffer;

        /// <summary>
        /// Get buffer for reading/writing data
        /// Accepted sizes are: 2, 4, 8, 16
        /// </summary>
        /// <param name="sizeInBytes">2, 4, 8 or 16 bytes of buffer size.</param>
        /// <returns></returns>
        internal byte[] GetBuffer(int sizeInBytes)
        {
            Debug.Assert(sizeInBytes == 2 || sizeInBytes == 4 || sizeInBytes == 8 || sizeInBytes == 16);
            return sizeInBytes switch
            {
                2 => _2Buffer ??= new byte[2],
                4 => _4Buffer ??= new byte[4],
                8 => _8Buffer ??= new byte[8],
                16 => _16Buffer ??= new byte[16],
                _ => throw new ArgumentOutOfRangeException(nameof(sizeInBytes))
            };
        }

        /// <summary>
        /// Destructor to clean up the allocated memory
        /// </summary>
        ~TdsBufferAlloc()
        {
            _2Buffer = null;
            _4Buffer = null;
            _8Buffer = null;
            _16Buffer = null;
        }
    }
}
