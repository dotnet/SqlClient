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
        private readonly TdsStream _stream;

        /// <summary>
        /// Instantiate TdsWriter with a TdsWriteStream
        /// </summary>
        /// <param name="stream">A Tds Writer stream to work with</param>
        /// <exception cref="ArgumentNullException"></exception>
        public TdsWriter(TdsStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        #region Public APIs

        /// <summary>
        /// Writes short value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteShortAsync(short v, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(short);
            BinaryPrimitives.WriteInt16LittleEndian(GetBuffer(len), v);
            await WriteBytesAsync(GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes int value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteIntAsync(int v, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(GetBuffer(len), v);
            await WriteBytesAsync(GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes unsigned short value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedShortAsync(ushort v, bool isAsync, CancellationToken ct) => WriteShortAsync((short)v, isAsync, ct);

        /// <summary>
        /// Writes unsigned int value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedIntAsync(uint v, bool isAsync, CancellationToken ct) => WriteIntAsync((int)v, isAsync, ct);

        /// <summary>
        /// Writes long value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteLongAsync(long v, bool isAsync, CancellationToken ct)
        {
            var len = sizeof(long);
            BinaryPrimitives.WriteInt64LittleEndian(GetBuffer(len), v);
            await WriteBytesAsync(GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes unsigned long value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public ValueTask WriteUnsignedLongAsync(ulong v, bool isAsync, CancellationToken ct) => WriteLongAsync((long)v, isAsync, ct);

        /// <summary>
        /// Writes float value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteFloatAsync(float v, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(!float.IsInfinity(v) && !float.IsNaN(v), "Float value is out of range.");

            var len = sizeof(float);
            BinaryPrimitives.WriteInt32LittleEndian(GetBuffer(len), BitConverter.SingleToInt32Bits(v));
            await WriteBytesAsync(GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes double value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WriteDoubleAsync(double v, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(!double.IsInfinity(v) && !double.IsNaN(v), "Double value is out of range.");

            var len = sizeof(double);
            BinaryPrimitives.WriteInt64LittleEndian(GetBuffer(len), BitConverter.DoubleToInt64Bits(v));
            await WriteBytesAsync(GetBuffer(len), 0, len, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes partial long value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="length">Length to fill</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async ValueTask WritePartialLongAsync(long v, int length, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(length >= 0, "Length should not be negative");
            Debug.Assert(length <= 8, "Length specified is longer than the size of a long");

            var len = sizeof(long);
            var buffer = GetBuffer(len);
            for (int i = 0; i < length; i++)
            {
                buffer[i] = (byte)((v >> (i * 8)) & 0xFF);
            }
            await WriteBytesAsync(GetBuffer(len), 0, length, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes bytes directly to _stream
        /// </summary>
        /// <param name="data">Bytes of data</param>
        /// <param name="start">Offset of starting position</param>
        /// <param name="length">Length of data</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns></returns>
        public async ValueTask WriteBytesAsync(byte[] data, int start, int length, bool isAsync, CancellationToken ct)
        {
            if (isAsync)
            {
                await _stream.WriteAsync(data.AsMemory(start, length), ct).ConfigureAwait(false);
            }
            else
            {
                // Throw operation canceled exception before write.
                ct.ThrowIfCancellationRequested();

                _stream.Write(data.AsSpan(start, length));
            }
        }

        #endregion
    }
}
