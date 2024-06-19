// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.IO
{
    internal class TdsWriter
    {
        private readonly TdsWriteStream _writeStream;

        // Header length constants
        private const int OutputHeaderLen = TdsEnums.HEADER_LEN;

        // Out buffer variables
        private readonly byte[] _outBuff; // internal write buffer - initialize on login
        private int _outBytesUsed = OutputHeaderLen; // number of bytes used in internal write buffer - initialize past header

        /// <summary>
        /// Instantiate TdsWriter with a TdsWriteStream
        /// </summary>
        /// <param name="stream">A Tds Writer stream to work with</param>
        /// <exception cref="ArgumentNullException"></exception>
        public TdsWriter(TdsWriteStream stream)
        {
            _writeStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _outBuff = new byte[OutputHeaderLen]; // Adjust the buffer size as needed
        }

        #region Public APIs

        /// <summary>
        /// Writes short value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async Task WriteShortAsync(short v, bool isAsync, CancellationToken ct)
        {
            await EnsureBufferSpaceAsync(2, isAsync, ct).ConfigureAwait(false);
            BinaryPrimitives.WriteInt16LittleEndian(GetWritableSpan(2), v);
            _outBytesUsed += 2;
        }

        /// <summary>
        /// Writes int value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async Task WriteIntAsync(int v, bool isAsync, CancellationToken ct)
        {
            await EnsureBufferSpaceAsync(4, isAsync, ct).ConfigureAwait(false);
            BinaryPrimitives.WriteInt32LittleEndian(GetWritableSpan(4), v);
            _outBytesUsed += 4;
        }

        /// <summary>
        /// Writes unsigned short value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public Task WriteUnsignedShortAsync(ushort v, bool isAsync, CancellationToken ct) => WriteShortAsync((short)v, isAsync, ct);

        /// <summary>
        /// Writes unsigned int value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public Task WriteUnsignedIntAsync(uint v, bool isAsync, CancellationToken ct) => WriteIntAsync((int)v, isAsync, ct);

        /// <summary>
        /// Writes float value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async Task WriteFloatAsync(float v, bool isAsync, CancellationToken ct)
        {
            if (float.IsInfinity(v) || float.IsNaN(v))
            {
                throw new ArgumentOutOfRangeException(nameof(v), "Float value is out of range.");
            }
            await EnsureBufferSpaceAsync(4, isAsync, ct).ConfigureAwait(false);
            BinaryPrimitives.WriteInt32LittleEndian(GetWritableSpan(4), BitConverter.SingleToInt32Bits(v));
            _outBytesUsed += 4;
        }

        /// <summary>
        /// Writes long value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async Task WriteLongAsync(long v, bool isAsync, CancellationToken ct)
        {
            await EnsureBufferSpaceAsync(8, isAsync, ct).ConfigureAwait(false);
            BinaryPrimitives.WriteInt64LittleEndian(GetWritableSpan(8), v);
            _outBytesUsed += 8;
        }

        /// <summary>
        /// Writes unsigned long value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public Task WriteUnsignedLongAsync(ulong v, bool isAsync, CancellationToken ct) => WriteLongAsync((long)v, isAsync, ct);

        /// <summary>
        /// Writes double value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async Task WriteDoubleAsync(double v, bool isAsync, CancellationToken ct)
        {
            if (double.IsInfinity(v) || double.IsNaN(v))
            {
                throw new ArgumentOutOfRangeException(nameof(v), "Double value is out of range.");
            }
            await EnsureBufferSpaceAsync(8, isAsync, ct).ConfigureAwait(false);
            BinaryPrimitives.WriteInt64LittleEndian(GetWritableSpan(8), BitConverter.DoubleToInt64Bits(v));
            _outBytesUsed += 8;
        }

        /// <summary>
        /// Writes partial long value to out buffer
        /// </summary>
        /// <param name="v">Value to write</param>
        /// <param name="length">Length to fill</param>
        /// <param name="isAsync">Is Async caller method</param>
        /// <param name="ct">Cancellation token</param>
        public async Task WritePartialLongAsync(long v, int length, bool isAsync, CancellationToken ct)
        {
            if (length > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length specified is longer than the size of a long");
            }
            else if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length should not be negative");
            }

            await EnsureBufferSpaceAsync(length, isAsync, ct).ConfigureAwait(false);
            Memory<byte> memory = GetWritableMemory(length);
            for (int i = 0; i < length; i++)
            {
                memory.Span[i] = (byte)((v >> (i * 8)) & 0xFF);
            }
            _outBytesUsed += length;
        }
        #endregion

        #region Private helpers
        private async Task EnsureBufferSpaceAsync(int requiredBytes, bool isAsync, CancellationToken ct)
        {
            if ((_outBytesUsed + requiredBytes) > _outBuff.Length)
            {
                if (isAsync)
                {
                    await _writeStream.WriteAsync(_outBuff.AsMemory(0, _outBytesUsed), ct).ConfigureAwait(false);
                }
                else
                {
                    // Throw operation canceled exception before write.
                    ct.ThrowIfCancellationRequested();

                    _writeStream.Write(_outBuff, 0, _outBytesUsed);
                }
                _outBytesUsed = OutputHeaderLen; // Reset past header
            }
        }

        private Span<byte> GetWritableSpan(int length) => _outBuff.AsSpan(_outBytesUsed, length);

        private Memory<byte> GetWritableMemory(int length) => _outBuff.AsMemory(_outBytesUsed, length);
        #endregion
    }
}
