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
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteShortAsync(TdsStream stream, short v, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(_alloc.GetBuffer(len), v);
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes int value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteIntAsync(TdsStream stream, int v, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(_alloc.GetBuffer(len), v);
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes unsigned short value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedShortAsync(TdsStream stream, ushort v, bool isAsync, CancellationToken ct)
            => WriteShortAsync(stream, (short)v, isAsync, ct);

        /// <summary>
        /// Writes unsigned int value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedIntAsync(TdsStream stream, uint v, bool isAsync, CancellationToken ct)
            => WriteIntAsync(stream, (int)v, isAsync, ct);

        /// <summary>
        /// Writes long value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteLongAsync(TdsStream stream, long v, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(long);
            BinaryPrimitives.WriteInt64LittleEndian(_alloc.GetBuffer(len), v);
            await WriteBytesAsync(stream, _alloc.GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes unsigned long value to out buffer, as little-endian.
        /// </summary>
        /// <param name="stream">Tds Stream to work with</param>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedLongAsync(TdsStream stream, ulong v, bool isAsync, CancellationToken ct)
            => WriteLongAsync(stream, (long)v, isAsync, ct);

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
