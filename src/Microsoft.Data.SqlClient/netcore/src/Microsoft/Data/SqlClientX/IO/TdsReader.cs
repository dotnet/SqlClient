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
        /// Reads bytes asynchronously for the length of buffer.
        /// Recommended to be used by Tds Parser: 
        ///     Read entire buffer for the expected data, and then break down data from packets to avoid multiple async calls.
        /// </summary>
        /// <param name="buffer">Buffer to use for reading bytes.</param>
        /// <param name="isAsync">Whether the call should be made asynchronously or synchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        public ValueTask<int> ReadBytesAsync(Memory<byte> buffer, bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            return isAsync
                ? _tdsStream.ReadAsync(buffer, ct)
                : new ValueTask<int>(_tdsStream.Read(buffer.Span));
        }

        /// <summary>
        /// Reads byte from TDS stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="byte"/> value.</returns>
        public ValueTask<byte> ReadByteAsync(bool isAsync, CancellationToken ct)
        {
            // Throw operation canceled exception before write.
            ct.ThrowIfCancellationRequested();

            return isAsync
                ? _tdsStream.ReadByteAsync(isAsync, ct)
                : new ValueTask<byte>((byte)_tdsStream.ReadByte()); // UNSAFE - TODO Expose ReadByte from TdsStream that returns 'byte'.
        }

        /// <summary>
        /// Reads next char from TDS stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="char"/> value.</returns>
        public ValueTask<char> ReadCharAsync(bool isAsync, CancellationToken ct)
            => isAsync
            ? ReadCharInternalAsync(ct)
            : new ValueTask<char>(_reader.ReadChar());

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
        public ValueTask<short> ReadInt16Async(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadInt16InternalAsync(ct)
                : new ValueTask<short>(_reader.ReadInt16());

        /// <summary>
        /// Reads int value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="int"/> value.</returns>
        public ValueTask<int> ReadInt32Async(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadInt32InternalAsync(ct)
                : new ValueTask<int>(_reader.ReadInt32());

        /// <summary>
        /// Reads long value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="long"/> value.</returns>
        public ValueTask<long> ReadInt64Async(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadInt64InternalAsync(ct)
                : new ValueTask<long>(_reader.ReadInt64());

        /// <summary>
        /// Reads unsigned short value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="ushort"/> value.</returns>
        public ValueTask<ushort> ReadUInt16Async(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadUInt16InternalAsync(ct)
                : new ValueTask<ushort>(_reader.ReadUInt16());

        /// <summary>
        /// Reads unsigned int value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="uint"/> value.</returns>
        public ValueTask<uint> ReadUInt32Async(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadUInt32InternalAsync(ct)
                : new ValueTask<uint>(_reader.ReadUInt32());

        /// <summary>
        /// Reads unsigned long value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="ulong"/> value.</returns>
        public ValueTask<ulong> ReadUInt64Async(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadUInt64InternalAsync(ct)
                : new ValueTask<ulong>(_reader.ReadUInt64());

        /// <summary>
        /// Reads float value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="float"/> value.</returns>
        public ValueTask<float> ReadSingleAsync(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadSingleInternalAsync(ct)
                : new ValueTask<float>(_reader.ReadSingle());

        /// <summary>
        /// Reads double value from Tds Stream asynchronously.
        /// </summary>
        /// <param name="isAsync">Whether caller method is executing asynchronously.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Async <see cref="ValueTask"/> that returns a <see cref="double"/> value.</returns>
        public ValueTask<double> ReadDoubleAsync(bool isAsync, CancellationToken ct)
            => isAsync
                ? ReadDoubleInternalAsync(ct)
                : new ValueTask<double>(_reader.ReadDouble());

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
            byte[] buffer = new byte[byteLength];

            int bytesRead = isAsync
                ? await _tdsStream.ReadAsync(buffer.AsMemory(0, byteLength), ct).ConfigureAwait(false)
                : _tdsStream.Read(buffer.AsSpan(0, byteLength));

            return Encoding.Unicode.GetString(buffer, 0, bytesRead);
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

        private ValueTask<char> ReadCharInternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(char));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<char>(doWork())
                : new ValueTask<char>(task.AsTask().ContinueWith((task) => doWork()));

            char doWork()
            {
                byte byte1 = buffer.Span[1];
                byte byte0 = buffer.Span[0];

                return (char)((byte1 << 8) + byte0);
            }
        }

        private ValueTask<short> ReadInt16InternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(short));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<short>(doWork())
                : new ValueTask<short>(task.AsTask().ContinueWith((task) => doWork()));

            short doWork() => BinaryPrimitives.ReadInt16LittleEndian(buffer.Span);
        }

        private ValueTask<int> ReadInt32InternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(int));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<int>(doWork())
                : new ValueTask<int>(task.AsTask().ContinueWith((task) => doWork()));

            int doWork() => BinaryPrimitives.ReadInt32LittleEndian(buffer.Span);
        }

        private ValueTask<long> ReadInt64InternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(long));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<long>(doWork())
                : new ValueTask<long>(task.AsTask().ContinueWith((task) => doWork()));

            long doWork() => BinaryPrimitives.ReadInt64LittleEndian(buffer.Span);
        }

        private ValueTask<ushort> ReadUInt16InternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(ushort));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<ushort>(doWork())
                : new ValueTask<ushort>(task.AsTask().ContinueWith((task) => doWork()));

            ushort doWork() => BinaryPrimitives.ReadUInt16LittleEndian(buffer.Span);
        }

        private ValueTask<uint> ReadUInt32InternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(uint));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<uint>(doWork())
                : new ValueTask<uint>(task.AsTask().ContinueWith((task) => doWork()));

            uint doWork() => BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span);
        }

        private ValueTask<ulong> ReadUInt64InternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(ulong));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<ulong>(doWork())
                : new ValueTask<ulong>(task.AsTask().ContinueWith((task) => doWork()));

            ulong doWork() => BinaryPrimitives.ReadUInt64LittleEndian(buffer.Span);
        }

        private ValueTask<float> ReadSingleInternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(float));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<float>(doWork())
                : new ValueTask<float>(task.AsTask().ContinueWith((task) => doWork()));

            float doWork() => BinaryPrimitives.ReadSingleLittleEndian(buffer.Span);
        }

        private ValueTask<double> ReadDoubleInternalAsync(CancellationToken ct)
        {
            Memory<byte> buffer = GetBuffer(sizeof(double));
            ValueTask<int> task = ReadBytesAsync(buffer, true, ct);

            return task.IsCompleted
                ? new ValueTask<double>(doWork())
                : new ValueTask<double>(task.AsTask().ContinueWith((task) => doWork()));

            double doWork() => BinaryPrimitives.ReadDoubleLittleEndian(buffer.Span);
        }

        #endregion
    }
}
