// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// Provides buffer management for reader/writer implementations
    /// </summary>
    internal class TdsBufferAlloc : IDisposable, IAsyncDisposable
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
        /// Scratch buffer to serialize Decimal values (17 bytes).
        /// This is a binary representation of a Decimal. The value is an integer
        /// array with four elements. Elements 0, 1, and 2 contain the low, middle,
        /// and high 32 bits of the 96-bit integer part of the Decimal.
        /// Element 3 contains the scale factor and sign of the Decimal: bits 0-15 (the lower word) are unused;
        /// bits 16-23 contain a value between 0 and 28, indicating the power of 10 to divide
        /// the 96-bit integer part by to produce the Decimal value; bits 24-30 are unused;
        /// and finally bit 31 indicates the sign of the Decimal value, 0 meaning positive and 1 meaning negative.
        ///   SQLDECIMAL/SQLNUMERIC has a byte stream of:
        ///   struct {
        ///     BYTE sign; // 1 if positive, 0 if negative
        ///     BYTE data[];
        ///   }
        /// For TDS 7.0 and above, there are always 17 bytes of data
        /// </summary>
        private byte[] _17Buffer;

        /// <summary>
        /// Get buffer for reading/writing data
        /// Accepted sizes are: 2, 4, 8, 17
        /// </summary>
        /// <param name="sizeInBytes">2, 4, 8 or 17 bytes of buffer size.</param>
        /// <returns></returns>
        internal byte[] GetBuffer(int sizeInBytes)
        {
            Debug.Assert(sizeInBytes == 2 || sizeInBytes == 4 || sizeInBytes == 8 || sizeInBytes == 17);
            return sizeInBytes switch
            {
                2 => _2Buffer ??= new byte[2],
                4 => _4Buffer ??= new byte[4],
                8 => _8Buffer ??= new byte[8],
                17 => _17Buffer ??= new byte[17],
                _ => throw new ArgumentOutOfRangeException(nameof(sizeInBytes))
            };
        }

        /// <summary>
        /// Reset storage buffers to be picked up by GC
        /// </summary>
        public void Dispose()
        {
            _2Buffer = null;
            _4Buffer = null;
            _8Buffer = null;
            _17Buffer = null;
        }

        /// <summary>
        /// Disposes storage buffers and returns a completed <see cref="ValueTask"/>
        /// </summary>
        /// <returns>Completed ValueTask</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Disposes storage buffers and returns a completed <see cref="ValueTask"/>
        /// </summary>
        /// <returns>Completed ValueTask</returns>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
