// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// This class provides helper methods for reading bytes using <see cref="TdsStream"/>
    /// It extends <see cref="TdsBufferManager"/> that manages allocations of bytes buffer for better memory management.
    /// </summary>
    internal class TdsReader : TdsBufferManager
    {
        private readonly TdsStream _tdsStream;

        /// <summary>
        /// Instantiate TdsReader with <see cref="TdsStream" />
        /// </summary>
        /// <param name="stream">Tds Stream instance to work with.</param>
        public TdsReader(TdsStream stream)
        {
            _tdsStream = stream;
        }

        #region Public APIs

        /// <summary>
        /// Reads byte from TDS stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="byte"/> value.</returns>
        public async ValueTask<byte> ReadByteAsync(bool isAsync, CancellationToken ct)
        {// Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            return isAsync ?
                await _tdsStream.ReadByteAsync(isAsync, ct).ConfigureAwait(false)
                : (byte)_tdsStream.ReadByte(); // UNSAFE - TODO Expose ReadByte from TdsStream that returns 'byte'.
        }
        /// <summary>
        /// Reads next char from TDS stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="char"/> value.</returns>
        public ValueTask<char> ReadCharAsync(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(char), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return (char)((buffer[1] << 8) + buffer[0]);
            });

        /// <summary>
        /// Reads char array from Tds Stream of defined <paramref name="length"/> asynchronously.
        /// </summary>
        /// <param name="length">Length of array to read.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="char"/> array.</returns>
        public async ValueTask<char[]> ReadCharArrayAsync(int length, bool isAsync, CancellationToken ct)
        {
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = await ReadCharAsync(isAsync, ct).ConfigureAwait(false);
                if (!BitConverter.IsLittleEndian)
                {
                    result[i] = (char)BinaryPrimitives.ReverseEndianness((ushort)result[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// Reads short value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="short"/> value.</returns>
        public ValueTask<short> ReadInt16Async(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(short), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadInt16LittleEndian(buffer);
            });

        /// <summary>
        /// Reads int value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="int"/> value.</returns>
        public ValueTask<int> ReadInt32Async(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(int), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadInt32LittleEndian(buffer);
            });

        /// <summary>
        /// Reads long value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="long"/> value.</returns>
        public ValueTask<long> ReadInt64Async(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(long), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadInt64LittleEndian(buffer);
            });

        /// <summary>
        /// Reads unsigned short value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="ushort"/> value.</returns>
        public ValueTask<ushort> ReadUInt16Async(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(ushort), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
            });

        /// <summary>
        /// Reads unsigned int value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="uint"/> value.</returns>
        public ValueTask<uint> ReadUInt32Async(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(uint), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            });

        /// <summary>
        /// Reads unsigned long value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="ulong"/> value.</returns>
        public ValueTask<ulong> ReadUInt64Async(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(ulong), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
            });

        /// <summary>
        /// Reads float value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="float"/> value.</returns>
        public ValueTask<float> ReadSingleAsync(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(float), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadSingleLittleEndian(buffer);
            });

        /// <summary>
        /// Reads double value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="double"/> value.</returns>
        public ValueTask<double> ReadDoubleAsync(bool isAsync, CancellationToken ct)
            => DoWithRentedBuffer(sizeof(double), async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
            });

        /// <summary>
        /// Reads string value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="length">Length of string to read.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="string"/> value.</returns>
        public ValueTask<string> ReadStringAsync(int length, bool isAsync, CancellationToken ct)
            // Calculate the number of bytes required (assuming UTF-16 encoding where each character is 2 bytes)
            => DoWithRentedBuffer(length * 2, async buffer =>
            {
                await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
                return System.Text.Encoding.Unicode.GetString(buffer);
            });

        /// <summary>
        /// Reads string value from Tds Stream with defined <paramref name="encoding"/> asynchronously.
        /// </summary>
        /// <param name="length">Length of string to read.</param>
        /// <param name="encoding">String character encoding.</param>
        /// <param name="isPlp">Whether this is PLP data.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="string"/> value.</returns>
        public ValueTask<string> ReadStringWithEncodingAsync(int length, System.Text.Encoding encoding, bool isPlp, bool isAsync, CancellationToken ct)
            // TODO Implement PLP reading support
            => throw new NotImplementedException();

        #endregion

        #region Private helpers

        internal async ValueTask<int> ReadBytesAsync(byte[] buffer, bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            return isAsync ?
                await _tdsStream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)
                : _tdsStream.Read(buffer.AsSpan());
        }

        #endregion
    }
}
