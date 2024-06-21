// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// This class provides helper methods for writing bytes using <see cref="TdsStream"/>
    /// It extends <see cref="TdsBufferAlloc"/> that manages allocations of bytes buffer for better memory management.
    /// </summary>
    internal class TdsWriter : IAsyncDisposable, IDisposable
    {
        private readonly TdsBufferAlloc _alloc;

        /// <summary>
        /// Instantiate TdsWriter with a TdsWriteStream
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public TdsWriter()
        {
            _alloc = new TdsBufferAlloc();
        }

        #region Public APIs

        /// <summary>
        /// Writes short value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteShortAsync(TdsStream stream, short value, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(_alloc.GetBuffer(len), value);
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes int value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteIntAsync(TdsStream stream, int value, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(_alloc.GetBuffer(len), value);
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes unsigned short value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedShortAsync(TdsStream stream, ushort value, bool isAsync, CancellationToken ct)
            => WriteShortAsync(stream, (short)value, isAsync, ct);

        /// <summary>
        /// Writes unsigned int value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedIntAsync(TdsStream stream, uint value, bool isAsync, CancellationToken ct)
            => WriteIntAsync(stream, (int)value, isAsync, ct);

        /// <summary>
        /// Writes long value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteLongAsync(TdsStream stream, long value, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(long);
            BinaryPrimitives.WriteInt64LittleEndian(_alloc.GetBuffer(len), value);
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes unsigned long value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedLongAsync(TdsStream stream, ulong value, bool isAsync, CancellationToken ct)
            => WriteLongAsync(stream, (long)value, isAsync, ct);

        /// <summary>
        /// Writes float value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteFloatAsync(TdsStream stream, float value, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(!float.IsInfinity(value) && !float.IsNaN(value), "Float value is out of range.");

            var len = sizeof(float);
            BinaryPrimitives.WriteInt32LittleEndian(_alloc.GetBuffer(len), BitConverter.SingleToInt32Bits(value));
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes double value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteDoubleAsync(TdsStream stream, double value, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(!double.IsInfinity(value) && !double.IsNaN(value), "Double value is out of range.");

            var len = sizeof(double);
            BinaryPrimitives.WriteInt64LittleEndian(_alloc.GetBuffer(len), BitConverter.DoubleToInt64Bits(value));
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes partial long value to out buffer, as little-endian. This should be ideally used for writing date, 
        /// time and related data types. The length specified should be between 0 to 8 bytes supported by long.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="value">Value to write</param>
        /// <param name="length">Length to fill</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WritePartialLongAsync(TdsStream stream, long value, int length, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(length >= 0, "Length should not be negative");
            Debug.Assert(length <= 8, "Length specified is longer than the size of a long");

            var len = sizeof(long);
            var buffer = _alloc.GetBuffer(len);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = (byte)((value >> (i * 8)) & 0xFF);
            }
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, length, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes bytes directly to _stream
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="data">Bytes of data</param>
        /// <param name="start">Offset of starting position</param>
        /// <param name="length">Length of data</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns></returns>
        public async static ValueTask WriteBytesAsync(TdsStream stream, byte[] data, int start, int length, bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            if (isAsync)
            {
                await stream.WriteAsync(data.AsMemory(start, length), ct).ConfigureAwait(false);
            }
            else
            {
                stream.Write(data.AsSpan(start, length));
            }
        }

        public byte[] SerializeDecimal(decimal value)
        {
            int[] decimalBits = decimal.GetBits(value);
            byte[] bytes = _alloc.GetBuffer(17);
            int current = 0;

            /*
             Returns a binary representation of a Decimal. The return value is an integer
             array with four elements. Elements 0, 1, and 2 contain the low, middle, and
             high 32 bits of the 96-bit integer part of the Decimal. Element 3 contains
             the scale factor and sign of the Decimal: bits 0-15 (the lower word) are
             unused; bits 16-23 contain a value between 0 and 28, indicating the power of
             10 to divide the 96-bit integer part by to produce the Decimal value; bits 24-
             30 are unused; and finally bit 31 indicates the sign of the Decimal value, 0
             meaning positive and 1 meaning negative.

             SQLDECIMAL/SQLNUMERIC has a byte stream of:
             struct {
                 BYTE sign; // 1 if positive, 0 if negative
                 BYTE data[]; // 16 bits = int [4]
             }

             For TDS 7.0 and above, there are always 17 bytes of data
            */

            // write the sign (note that COM and SQL are opposite)
            if (0x80000000 == (decimalBits[3] & 0x80000000))
                bytes[current++] = 0;
            else
                bytes[current++] = 1;

            BinaryPrimitives.TryWriteInt32LittleEndian(bytes.AsSpan(current, 4), decimalBits[0]);
            current += 4;
            BinaryPrimitives.TryWriteInt32LittleEndian(bytes.AsSpan(current, 4), decimalBits[1]);
            current += 4;
            BinaryPrimitives.TryWriteInt32LittleEndian(bytes.AsSpan(current, 4), decimalBits[2]);
            current += 4;
            BinaryPrimitives.TryWriteInt32LittleEndian(bytes.AsSpan(current, 4), 0);

            return bytes;
        }

        /// <summary>
        /// Dispose Tds Writer
        /// </summary>
        public void Dispose()
        {
            _alloc.Dispose();
        }

        /// <summary>
        /// Disposes Tds Writer and returns a completed <see cref="ValueTask"/>
        /// </summary>
        /// <returns>Completed ValueTask</returns>
        public async ValueTask DisposeAsync()
        {
            await _alloc.DisposeAsync().ConfigureAwait(false);
        }
        #endregion
    }
}
