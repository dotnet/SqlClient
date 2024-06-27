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
    internal class TdsWriter : TdsBufferAlloc
    {
        private readonly TdsStream _tdsStream;

        /// <summary>
        /// Instantiate TdsWriter with TdsStream
        /// </summary>
        /// <param name="stream">Tds Stream instance to work with.</param>
        public TdsWriter(TdsStream stream)
        {
            _tdsStream = stream;
        }

        #region Public APIs

        /// <summary>
        /// Writes short value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteShortAsync(short value, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(GetBuffer(len), value);
            return WriteBytesAsync(GetBuffer(len).ToArray(), isAsync, ct);
        }

        /// <summary>
        /// Writes int value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteIntAsync(int value, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(GetBuffer(len), value);
            return WriteBytesAsync(GetBuffer(len).ToArray(), isAsync, ct);
        }

        /// <summary>
        /// Writes unsigned short value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteUnsignedShortAsync(ushort value, bool isAsync, CancellationToken ct)
            => WriteShortAsync((short)value, isAsync, ct);

        /// <summary>
        /// Writes unsigned int value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteUnsignedIntAsync(uint value, bool isAsync, CancellationToken ct)
            => WriteIntAsync((int)value, isAsync, ct);

        /// <summary>
        /// Writes long value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteLongAsync(long value, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(long);
            BinaryPrimitives.WriteInt64LittleEndian(GetBuffer(len), value);
            return WriteBytesAsync(GetBuffer(len).ToArray(), isAsync, ct);
        }

        /// <summary>
        /// Writes unsigned long value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteUnsignedLongAsync(ulong value, bool isAsync, CancellationToken ct)
            => WriteLongAsync((long)value, isAsync, ct);

        /// <summary>
        /// Writes float value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteFloatAsync(float value, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(!float.IsInfinity(value) && !float.IsNaN(value), "Float value is out of range.");

            var len = sizeof(float);
            BinaryPrimitives.WriteInt32LittleEndian(GetBuffer(len), BitConverter.SingleToInt32Bits(value));
            return WriteBytesAsync(GetBuffer(len).ToArray(), isAsync, ct);
        }

        /// <summary>
        /// Writes double value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteDoubleAsync(double value, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(!double.IsInfinity(value) && !double.IsNaN(value), "Double value is out of range.");

            var len = sizeof(double);
            BinaryPrimitives.WriteInt64LittleEndian(GetBuffer(len), BitConverter.DoubleToInt64Bits(value));
            return WriteBytesAsync(GetBuffer(len).ToArray(), isAsync, ct);
        }

        /// <summary>
        /// Writes partial long value to out buffer, as little-endian. This should be ideally used for writing date, 
        /// time and related data types. The length specified should be between 0 to 8 bytes supported by long.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="length">Length to fill.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WritePartialLongAsync(long value, int length, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(length >= 0, "Length should not be negative");
            Debug.Assert(length <= 8, "Length specified is longer than the size of a long");

            var len = sizeof(long);
            Span<byte> buffer = GetBuffer(len);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = (byte)((value >> (i * 8)) & 0xFF);
            }
            return WriteBytesAsync(GetBuffer(len).ToArray(), isAsync, ct);
        }

        /// <summary>
        /// Writes bytes directly to TdsSteam associated with writer.
        /// </summary>
        /// <param name="data">Data in byte array.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        public async ValueTask WriteBytesAsync(byte[] data, bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            if (isAsync)
            {
                await _tdsStream.WriteAsync(data.AsMemory(), ct).ConfigureAwait(false);
            }
            else
            {
                _tdsStream.Write(data.AsSpan());
            }
        }

        /// <summary>
        /// Serializes .NET Decimal to `SqlDecimal` represented by byte arrays for TDS writing.
        /// </summary>
        /// <param name="value">Decimal value,</param>
        /// <returns>Span of bytes for the TDS decimal type.</returns>
        public Span<byte> SerializeDecimal(decimal value)
        {
            int[] decimalBits = decimal.GetBits(value);
            Span<byte> buffer = GetBuffer(17);
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
            buffer[current++] = (byte)((((uint)decimalBits[3]) ^ 0x80000000) >> 31);

            BinaryPrimitives.TryWriteInt32LittleEndian(buffer[current..(4 + current)], decimalBits[0]);
            current += 4;
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer[current..(4 + current)], decimalBits[1]);
            current += 4;
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer[current..(4 + current)], decimalBits[2]);
            current += 4;
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer[current..(4 + current)], 0);

            return buffer;
        }
        #endregion
    }
}
