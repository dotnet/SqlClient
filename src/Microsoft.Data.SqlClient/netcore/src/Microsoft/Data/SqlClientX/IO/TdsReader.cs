// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// This class provides helper methods for reading bytes using <see cref="TdsStream"/>
    /// It extends <see cref="TdsBufferManager"/> that manages allocations of bytes buffer for better memory management.
    /// </summary>
    internal sealed class TdsReader : TdsBufferManager
    {
        private readonly TdsStream _tdsStream;
        private readonly BinaryReader _reader;

        /// <summary>
        /// Instantiate TdsReader with <see cref="TdsStream" />
        /// </summary>
        /// <param name="stream">Tds Stream instance to work with.</param>
        public TdsReader(TdsStream stream)
        {
            _tdsStream = stream;
            _reader = new BinaryReader(stream, Encoding.Unicode);
        }

        #region Public APIs

        /// <summary>
        /// Reads byte from TDS stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="byte"/> value.</returns>
        public async ValueTask<byte> ReadByteAsync(bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
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
        public async ValueTask<char> ReadCharAsync(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadChar();
            }

            var buffer = GetBuffer(sizeof(char));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);

            // Access bytes using Memory<T> APIs
            byte byte1 = buffer.Span[1];
            byte byte0 = buffer.Span[0];

            return (char)((byte1 << 8) + byte0);
        }

        /// <summary>
        /// Reads char array from Tds Stream of defined <paramref name="length"/> asynchronously.
        /// </summary>
        /// <param name="length">Length of array to read.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="char"/> array.</returns>
        public async ValueTask<char[]> ReadCharArrayAsync(int length, bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadChars(length);
            }

            // Calculate byte length for char array
            int byteLength = sizeof(char) * length;

            // Allocate byte array to store the result
            byte[] bytes = new byte[byteLength];

            // Read bytes asynchronously
            await ReadBytesAsync(new ArraySegment<byte>(bytes), isAsync, ct);

            // Convert bytes to char array
            char[] chars = new char[length];
            Encoding.Unicode.GetChars(bytes, chars);
            return chars;
        }

        /// <summary>
        /// Reads short value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="short"/> value.</returns>
        public async ValueTask<short> ReadInt16Async(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadInt16();
            }

            var buffer = GetBuffer(sizeof(short));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadInt16LittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads int value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="int"/> value.</returns>
        public async ValueTask<int> ReadInt32Async(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadInt32();
            }

            var buffer = GetBuffer(sizeof(int));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads long value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="long"/> value.</returns>
        public async ValueTask<long> ReadInt64Async(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadInt64();
            }

            var buffer = GetBuffer(sizeof(long));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads unsigned short value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="ushort"/> value.</returns>
        public async ValueTask<ushort> ReadUInt16Async(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadUInt16();
            }

            var buffer = GetBuffer(sizeof(ushort));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt16LittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads unsigned int value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="uint"/> value.</returns>
        public async ValueTask<uint> ReadUInt32Async(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadUInt32();
            }

            var buffer = GetBuffer(sizeof(uint));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads unsigned long value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="ulong"/> value.</returns>
        public async ValueTask<ulong> ReadUInt64Async(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadUInt64();
            }

            var buffer = GetBuffer(sizeof(ulong));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt64LittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads float value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="float"/> value.</returns>
        public async ValueTask<float> ReadSingleAsync(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadSingle();
            }

            var buffer = GetBuffer(sizeof(float));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadSingleLittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads double value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="double"/> value.</returns>
        public async ValueTask<double> ReadDoubleAsync(bool isAsync, CancellationToken ct)
        {
            if (!isAsync)
            {
                return _reader.ReadDouble();
            }

            var buffer = GetBuffer(sizeof(double));
            await ReadBytesAsync(buffer, isAsync, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadDoubleLittleEndian(buffer.Span);
        }

        /// <summary>
        /// Reads string value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="length">Length of string to read.</param>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="string"/> value.</returns>
        public async ValueTask<string> ReadStringAsync(int length, bool isAsync, CancellationToken ct)
        {
            int byteLength = length * 2; // 2 bytes per char
            int bufferSize = CalculateBufferSize(byteLength);
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            StringBuilder sb = new StringBuilder();
            while (byteLength > 0 && (bytesRead = isAsync
                ? await _tdsStream.ReadAsync(buffer.AsMemory(0, Math.Min(bufferSize, byteLength)), ct).ConfigureAwait(false)
                : _tdsStream.Read(buffer.AsSpan(0, Math.Min(bufferSize, byteLength)))) > 0)
            {
                sb.Append(Encoding.Unicode.GetString(buffer, 0, bytesRead));
                byteLength -= bytesRead;
            }

            return sb.ToString();
        }

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
        /// <summary>
        /// Calculates optimal buffer size for chunk reading based on string length
        /// </summary>
        /// <param name="stringLength">Length of string to read</param>
        /// <returns></returns>
        private static int CalculateBufferSize(int stringLength)
        {
            // Default buffer size
            int defaultBufferSize = 4096; // 4 KB

            // Estimate buffer size based on string length
            if (stringLength < defaultBufferSize)
                return stringLength;           // < 4 KB
            else if (stringLength < 16 * defaultBufferSize)
                return defaultBufferSize;      // 4 KB
            else if (stringLength < 64 * defaultBufferSize)
                return 16 * defaultBufferSize; // 64 KB
            else
                return 64 * defaultBufferSize; // 256 KB
        }

        public async ValueTask<int> ReadBytesAsync(Memory<byte> buffer, bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            return isAsync ?
                await _tdsStream.ReadAsync(buffer, ct).ConfigureAwait(false)
                : _tdsStream.Read(buffer.Span);
        }

        #endregion
    }
}
