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
    /// It extends <see cref="TdsBufferManager"/> that manages allocations of bytes buffer for better memory management.
    /// </summary>
    internal sealed class TdsWriter : TdsBufferManager
    {
        private readonly TdsStream _tdsStream;

        /// <summary>
        /// Instantiate TdsWriter with <see cref="TdsStream" />
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
            var buffer = GetBuffer(sizeof(short));
            BinaryPrimitives.WriteInt16LittleEndian(buffer.Span, value);
            return WriteBytesAsync(buffer, isAsync, ct);
        }

        /// <summary>
        /// Writes the least significant 2 bytes as short value to the stream, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteShortAsync(int value, bool isAsync, CancellationToken ct)
        {
            const int len = sizeof(short);
            short int16Value = (short)(value & 0xFFFF);
            BinaryPrimitives.WriteInt16LittleEndian(GetBuffer(len).Span, int16Value);
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
            var buffer = GetBuffer(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Span, value);
            return WriteBytesAsync(buffer, isAsync, ct);
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
            var buffer = GetBuffer(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(buffer.Span, value);
            return WriteBytesAsync(buffer, isAsync, ct);
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
            Debug.Assert(float.IsFinite(value), "Float value is out of range.");

            var buffer = GetBuffer(sizeof(float));
            BinaryPrimitives.WriteInt64LittleEndian(buffer.Span, BitConverter.SingleToInt32Bits(value));
            return WriteBytesAsync(buffer, isAsync, ct);
        }

        /// <summary>
        /// Writes double value to out buffer, as little-endian.
        /// </summary>
        /// <param name="value">Value to write.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        public ValueTask WriteDoubleAsync(double value, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(double.IsFinite(value), "Double value is out of range.");

            var buffer = GetBuffer(sizeof(double));
            BinaryPrimitives.WriteInt64LittleEndian(buffer.Span, BitConverter.DoubleToInt64Bits(value));
            return WriteBytesAsync(buffer, isAsync, ct);
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

            var buffer = GetBuffer(sizeof(long));
            for (int i = 0; i < length; i++)
            {
                buffer.Span[i] = (byte)((value >> (i * 8)) & 0xFF);
            }
            return WriteBytesAsync(buffer, isAsync, ct);
        }

        /// <summary>
        /// Writes a byte to the stream.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public ValueTask WriteByteAsync(byte value, bool isAsync, CancellationToken ct) => _tdsStream.WriteByteAsync(value, isAsync, ct);

        /// <summary>
        /// Writes bytes directly to TdsSteam associated with writer.
        /// </summary>
        /// <param name="data">Data in byte array.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        public async ValueTask WriteBytesAsync(Memory<byte> data, bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            if (isAsync)
            {
                await _tdsStream.WriteAsync(data, ct).ConfigureAwait(false);
            }
            else
            {
                _tdsStream.Write(data.Span);
            }
        }

        /// <summary>
        /// Serializes .NET Decimal to `SqlDecimal` represented by byte arrays for TDS writing.
        /// </summary>
        /// <param name="value">Decimal value,</param>
        /// <param name="buffer">Buffer</param>
        /// <returns>Span of bytes for the TDS decimal type.</returns>
        public static Span<byte> SerializeDecimal(decimal value, Span<byte> buffer)
        {
            if (buffer.Length < 17)
            {
                return Span<byte>.Empty;
            }

            Span<int> decimalBits =
#if !NET5_0_OR_GREATER
                stackalloc int[4];
                decimal.TryGetBits(value, decimalBits, out int valuesWritten);
#else
                decimal.GetBits(value);
#endif

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
            buffer[0] = (uint)decimalBits[3] != 0u ? (byte)1 : (byte)0;

            BinaryPrimitives.TryWriteInt32LittleEndian(buffer.Slice(1, 4), decimalBits[0]);
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer.Slice(5, 4), decimalBits[1]);
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer.Slice(9, 4), decimalBits[2]);
            BinaryPrimitives.TryWriteInt32LittleEndian(buffer.Slice(13, 4), 0);

            return buffer;
        }
        #endregion
    }
}
