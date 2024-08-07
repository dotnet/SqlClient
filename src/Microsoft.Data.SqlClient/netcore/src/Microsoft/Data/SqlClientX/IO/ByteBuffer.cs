// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// Buffer to handle low-level byte operations.
    /// </summary>
    internal class ByteBuffer : IEnumerable<byte>, IEquatable<ByteBuffer>
    {

        /// <summary>
        /// Empty <see cref="ByteBuffer"/>.
        /// </summary>
        public static readonly ByteBuffer Empty = new ByteBuffer(0);

        private readonly byte[] buffer;

        /// <summary>
        /// The length of this ByteBuffer.
        /// </summary>
        public int Length => buffer.Length;

        /// <summary>
        /// Element at the specified index. 
        /// </summary>
        /// <param name="index">Index of the element.</param>
        /// <returns>Byte value at the specified index.</returns>
        public byte this[int index]
        {
            get => buffer[index];
            set => buffer[index] = value;
        }

        /// <summary>
        /// Create a new empty ByteBuffer with a specific length.
        /// </summary>
        /// <param name="length">The length of the new ByteBuffer.</param>
        public ByteBuffer(int length)
        {
            buffer = new byte[length];
        }

        /// <summary>
        /// Creates a new ByteBuffer from a copy of an array.
        /// </summary>
        /// <param name="buffer">The array to copy.</param>
        /// <param name="startIndex">The start index of the source array.</param>
        /// <param name="length">The amount of bytes to copy.</param>
        public ByteBuffer(byte[] buffer, int startIndex, int length)
        {
            this.buffer = new byte[length];
            Array.Copy(buffer, startIndex, this.buffer, 0, length);
        }

        /// <summary>
        /// Creates a new ByteBuffer with a backing buffer.
        /// </summary>
        /// <param name="buffer">The backing buffer. This buffer will be used directly, a copy of the elements will not be made.</param>
        public ByteBuffer(byte[] buffer)
        {
            this.buffer = buffer;
        }

        /// <summary>
        /// Creates a new ByteBuffer from an enumerable of byte arrays.
        /// </summary>
        /// <param name="buffers">Enumerable of byte arrays. The new ByteBuffer will contains the concatenation of all provided byte arrays.</param>
        public ByteBuffer(IEnumerable<byte[]> buffers)
        {
            buffer = buffers.SelectMany((b) => b).ToArray();
        }

        /// <summary>
        /// Creates a new ByteBuffer from an enumerable of ByteBuffers.
        /// </summary>
        /// <param name="buffers">Enumerable of byte buffers. The new ByteBuffer will contains the concatenation of all provided byte buffers.</param>
        public ByteBuffer(IEnumerable<ByteBuffer> buffers)
        {
            buffer = buffers.SelectMany((b) => b.buffer).ToArray();
        }

        #region Private Methods

        private void EnsureBoundaries(int index)
        {
            if (index < 0 || index >= Length)
            {
                throw new IndexOutOfRangeException($"Index '{index}' is outside buffer boundaries");
            }
        }

        private void EnsureBoundaries(int index, int size)
        {
            if (index < 0 || index + size - 1 >= Length)
            {
                throw new IndexOutOfRangeException($"Range '[{index},{index + size - 1}]' is outside buffer boundaries");
            }
        }

        #endregion

        #region Read Methods

        /// <summary>
        /// Reads an Int8 from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public sbyte ReadInt8(int offset = 0)
        {
            EnsureBoundaries(offset);
            return (sbyte)buffer[offset];
        }

        /// <summary>
        /// Reads an UInt8 from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public byte ReadUInt8(int offset = 0)
        {
            EnsureBoundaries(offset);
            return buffer[offset];
        }

        /// <summary>
        /// Reads an Int16 in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public short ReadInt16LE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(short));
            return (short)(buffer[offset] | buffer[offset + 1] << 8);
        }

        /// <summary>
        /// Reads an Int16 in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public short ReadInt16BE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(short));
            return (short)(buffer[offset + 1] | buffer[offset] << 8);
        }

        /// <summary>
        /// Reads an UInt16 in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public ushort ReadUInt16LE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ushort));
            return (ushort)(buffer[offset] | buffer[offset + 1] << 8);
        }

        /// <summary>
        /// Reads an UInt16 in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public ushort ReadUInt16BE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ushort));
            return (ushort)(buffer[offset + 1] | buffer[offset] << 8);
        }

        /// <summary>
        /// Reads an Int32 in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public int ReadInt32LE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(int));
            return buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24;
        }

        /// <summary>
        /// Reads an Int32 in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public int ReadInt32BE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(int));
            return buffer[offset + 3] | buffer[offset + 2] << 8 | buffer[offset + 1] << 16 | buffer[offset] << 24;
        }

        /// <summary>
        /// Reads an UInt32 in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public uint ReadUInt32LE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(uint));
            return (uint)(buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24);
        }

        /// <summary>
        /// Reads an UInt32 in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public uint ReadUInt32BE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(uint));
            return (uint)(buffer[offset + 3] | buffer[offset + 2] << 8 | buffer[offset + 1] << 16 | buffer[offset] << 24);
        }

        /// <summary>
        /// Reads an Int64 in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public long ReadInt64LE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(long));

            uint lo = (uint)(buffer[offset + 0] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24);
            uint hi = (uint)(buffer[offset + 4] | buffer[offset + 5] << 8 | buffer[offset + 6] << 16 | buffer[offset + 7] << 24);
            return (long)(ulong)hi << 32 | lo;
        }

        /// <summary>
        /// Reads an Int64 in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public long ReadInt64BE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(long));

            uint hi = (uint)(buffer[offset + 0] << 24 | buffer[offset + 1] << 16 | buffer[offset + 2] << 8 | buffer[offset + 3]);
            uint lo = (uint)(buffer[offset + 4] << 24 | buffer[offset + 5] << 16 | buffer[offset + 6] << 8 | buffer[offset + 7]);
            return (long)(lo | (ulong)hi << 32);
        }

        /// <summary>
        /// Reads an UInt64 in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public ulong ReadUInt64LE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ulong));
            return (ulong)ReadInt64LE(offset);
        }

        /// <summary>
        /// Reads an UInt64 in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public ulong ReadUInt64BE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ulong));
            return (ulong)ReadInt64BE(offset);
        }

        /// <summary>
        /// Reads a Float in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public float ReadFloatLE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(float));
            return BitConverter.ToSingle(buffer, offset);
        }

        /// <summary>
        /// Reads a Float in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public float ReadFloatBE(int offset = 0)
        {
            byte[] reversedBuffer = new byte[sizeof(float)];

            reversedBuffer[0] = buffer[offset + 3];
            reversedBuffer[1] = buffer[offset + 2];
            reversedBuffer[2] = buffer[offset + 1];
            reversedBuffer[3] = buffer[offset];

            return BitConverter.ToSingle(reversedBuffer, offset);
        }

        /// <summary>
        /// Reads a Double in Little Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public double ReadDoubleLE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(double));
            return BitConverter.ToDouble(buffer, offset);
        }

        /// <summary>
        /// Reads a Double in Big Endian format from the buffer.
        /// </summary>
        /// <param name="offset">The read offset.</param>
        /// <returns>Value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the read would pass the buffer boundaries.</exception>
        public double ReadDoubleBE(int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(double));

            byte[] reversedBuffer = new byte[sizeof(double)];

            reversedBuffer[0] = buffer[offset + 7];
            reversedBuffer[1] = buffer[offset + 6];
            reversedBuffer[2] = buffer[offset + 5];
            reversedBuffer[3] = buffer[offset + 4];
            reversedBuffer[4] = buffer[offset + 3];
            reversedBuffer[5] = buffer[offset + 2];
            reversedBuffer[6] = buffer[offset + 1];
            reversedBuffer[7] = buffer[offset];

            return BitConverter.ToDouble(reversedBuffer, offset);
        }

        #endregion

        #region Write Methods

        /// <summary>
        /// Writes the data from another <see cref="ByteBuffer"/> to this buffer.
        /// </summary>
        /// <param name="byteBuffer">The other <see cref="ByteBuffer"/>.</param>
        /// <param name="offset">The destination offset.</param>
        /// <returns>The last offset of the write.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write range is outside the buffer range.</exception>
        public int Write(ByteBuffer byteBuffer, int offset = 0)
        {
            EnsureBoundaries(offset, byteBuffer.Length);
            Array.Copy(byteBuffer.buffer, 0, buffer, offset, byteBuffer.Length);
            return offset + byteBuffer.Length;
        }

        /// <summary>
        /// Writes the data from another <see cref="ByteBuffer"/> to this buffer.
        /// </summary>
        /// <param name="byteBuffer">The other <see cref="ByteBuffer"/>.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <returns>The last offset of the write.</returns>
        /// <exception cref="IndexOutOfRangeException">If the copy range is outside the buffers ranges.</exception>
        public int Write(ByteBuffer byteBuffer, int destinationOffset, int sourceOffset)
        {
            EnsureBoundaries(destinationOffset, byteBuffer.Length - sourceOffset);
            Array.Copy(byteBuffer.buffer, sourceOffset, buffer, destinationOffset, byteBuffer.Length - sourceOffset);
            return destinationOffset + byteBuffer.Length - sourceOffset;
        }

        /// <summary>
        /// Writes the data from another <see cref="ByteBuffer"/> to this buffer.
        /// </summary>
        /// <param name="byteBuffer">The other <see cref="ByteBuffer"/>.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="sourceOffset">The source offset.</param>
        /// <param name="length">The amount of bytes to copy.</param>
        /// <returns>The last offset of the write.</returns>
        /// <exception cref="IndexOutOfRangeException">If the copy range is outside the buffers ranges.</exception>
        public int Write(ByteBuffer byteBuffer, int destinationOffset, int sourceOffset, int length)
        {
            byteBuffer.EnsureBoundaries(sourceOffset, length);
            EnsureBoundaries(destinationOffset, length);

            Array.Copy(byteBuffer.buffer, sourceOffset, buffer, destinationOffset, length);
            return destinationOffset + length;
        }

        /// <summary>
        /// Writes an Int8 to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteInt8(sbyte value, int offset = 0)
        {
            EnsureBoundaries(offset);
            buffer[offset] = (byte)value;

            return offset + sizeof(byte);
        }

        /// <summary>
        /// Writes an UInt8 to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteUInt8(byte value, int offset = 0)
        {
            EnsureBoundaries(offset);
            buffer[offset] = value;

            return offset + sizeof(byte);
        }

        /// <summary>
        /// Writes an Int16 in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteInt16LE(short value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(short));
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);

            return offset + sizeof(short);
        }

        /// <summary>
        /// Writes an Int16 in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteInt16BE(short value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(short));
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;

            return offset + sizeof(short);
        }

        /// <summary>
        /// Writes an UInt16 in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteUInt16LE(ushort value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ushort));
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);

            return offset + sizeof(ushort);
        }

        /// <summary>
        /// Writes an UInt16 in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteUInt16BE(ushort value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ushort));
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;

            return offset + sizeof(ushort);
        }

        /// <summary>
        /// Writes an Int32 in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteInt32LE(int value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(int));
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);

            return offset + sizeof(int);
        }

        /// <summary>
        /// Writes an Int32 in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteInt32BE(int value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(int));
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;

            return offset + sizeof(int);
        }

        /// <summary>
        /// Writes an UInt32 in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteUInt32LE(uint value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(uint));
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);

            return offset + sizeof(uint);
        }

        /// <summary>
        /// Writes an UInt32 in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteUInt32BE(uint value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(uint));
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;

            return offset + sizeof(uint);
        }

        /// <summary>
        /// Writes an Int64 in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteInt64LE(long value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(long));
            buffer[offset + 0] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
            return offset + sizeof(long);
        }

        /// <summary>
        /// Writes an Int64 in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteInt64BE(long value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(long));
            buffer[offset + 0] = (byte)(value >> 56);
            buffer[offset + 1] = (byte)(value >> 48);
            buffer[offset + 2] = (byte)(value >> 40);
            buffer[offset + 3] = (byte)(value >> 32);
            buffer[offset + 4] = (byte)(value >> 24);
            buffer[offset + 5] = (byte)(value >> 16);
            buffer[offset + 6] = (byte)(value >> 8);
            buffer[offset + 7] = (byte)value;
            return offset + sizeof(long);
        }

        /// <summary>
        /// Writes an UInt64 in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteUInt64LE(ulong value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ulong));
            buffer[offset + 0] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
            return offset + sizeof(ulong);
        }

        /// <summary>
        /// Writes an UInt64 in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteUInt64BE(ulong value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(ulong));
            buffer[offset + 0] = (byte)(value >> 56);
            buffer[offset + 1] = (byte)(value >> 48);
            buffer[offset + 2] = (byte)(value >> 40);
            buffer[offset + 3] = (byte)(value >> 32);
            buffer[offset + 4] = (byte)(value >> 24);
            buffer[offset + 5] = (byte)(value >> 16);
            buffer[offset + 6] = (byte)(value >> 8);
            buffer[offset + 7] = (byte)value;
            return offset + sizeof(ulong);
        }

        /// <summary>
        /// Writes a Float in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteFloatLE(float value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(float));

            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, sizeof(float));

            return offset + sizeof(float);
        }

        /// <summary>
        /// Writes a Float in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteFloatBE(float value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(float));

            byte[] bytes = BitConverter.GetBytes(value);

            buffer[offset] = bytes[3];
            buffer[offset + 1] = bytes[2];
            buffer[offset + 2] = bytes[1];
            buffer[offset + 3] = bytes[0];

            return offset + sizeof(float);
        }

        /// <summary>
        /// Writes a Double in Little Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteDoubleLE(double value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(double));

            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, sizeof(double));

            return offset + sizeof(double);
        }

        /// <summary>
        /// Writes a Double in Big Endian format to the buffer.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <param name="offset">The offset to which the value is written to.</param>
        /// <returns>The offset of last byte of the written value.</returns>
        /// <exception cref="IndexOutOfRangeException">If the write operation operates outside the buffer range.</exception>
        public int WriteDoubleBE(double value, int offset = 0)
        {
            EnsureBoundaries(offset, sizeof(double));

            byte[] bytes = BitConverter.GetBytes(value);

            buffer[offset] = bytes[7];
            buffer[offset + 1] = bytes[6];
            buffer[offset + 2] = bytes[5];
            buffer[offset + 3] = bytes[4];
            buffer[offset + 4] = bytes[3];
            buffer[offset + 5] = bytes[2];
            buffer[offset + 6] = bytes[1];
            buffer[offset + 7] = bytes[0];

            return offset + sizeof(double);
        }

        #endregion

        /// <summary>
        /// Slices this buffer and returns the sliced part as a copy.
        /// </summary>
        /// <param name="startIndex">The start index of the new buffer.</param>
        /// <returns>The sliced part of the buffer as a copy.</returns>
        public ByteBuffer Slice(int startIndex)
        {
            EnsureBoundaries(startIndex);
            return Slice(startIndex, Length - startIndex);
        }

        /// <summary>
        /// Slices this buffer and returns the sliced part as a copy.
        /// </summary>
        /// <param name="startIndex">The start index of the new buffer.</param>
        /// <param name="length">The amount of item in the slice.</param>
        /// <returns>The sliced part of the buffer as a copy.</returns>
        public ByteBuffer Slice(int startIndex, int length)
        {
            EnsureBoundaries(startIndex, length);
            return new ByteBuffer(buffer, startIndex, length);
        }

        /// <summary>
        /// Concats a buffer with this one and returns the result as a new buffer.
        /// </summary>
        /// <param name="byteBuffer">The buffer to concat with the end of this buffer.</param>
        /// <returns>The new buffer from the concatenation.</returns>
        public ByteBuffer Concat(ByteBuffer byteBuffer)
        {
            return Concat(byteBuffer.buffer);
        }

        /// <summary>
        /// Concats a byte array with this buffer and returns the result as a new buffer.
        /// </summary>
        /// <param name="buffer">The byte array to concat with the end of this buffer.</param>
        /// <returns>The new buffer from the concatenation.</returns>
        public ByteBuffer Concat(byte[] buffer)
        {
            byte[] newArray = new byte[Length + buffer.Length];
            Array.Copy(this.buffer, 0, newArray, 0, this.buffer.Length);
            Array.Copy(buffer, 0, newArray, Length, buffer.Length);

            return new ByteBuffer(newArray);
        }

        /// <summary>
        /// Fills the buffer with a value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <param name="startIndex">The start index.</param>
        public void Fill(byte value, int startIndex = 0) => Fill(value, startIndex, Length - startIndex);

        /// <summary>
        /// Fills the buffer with a value.
        /// </summary>
        /// <param name="value">The value to use.</param>
        /// <param name="startIndex">The start index.</param>
        /// <param name="count">The number of bytes to fill.</param>
        public void Fill(byte value, int startIndex, int count)
        {
            EnsureBoundaries(startIndex, count);

            for (int i = 0; i < count; i++)
            {
                buffer[startIndex + i] = value;
            }
        }

        /// <summary>
        /// Copies the data from this buffer to an array.
        /// </summary>
        /// <param name="destination">The destination array.</param>
        /// <param name="destinationOffset">The destination offset.</param>
        /// <param name="count">The number of bytes to copy.</param>
        public void CopyTo(byte[] destination, int destinationOffset, int count)
        {
            Array.Copy(buffer, 0, destination, destinationOffset, count);
        }

        /// <summary>
        /// Copies the data from this buffer into a stream.
        /// </summary>
        /// <param name="stream">The stream to copy the data to.</param>
        public void CopyTo(Stream stream)
        {
            stream.Write(buffer, 0, Length);
        }

        /// <summary>
        /// Copies the data from this buffer into a stream.
        /// </summary>
        /// <param name="stream">The stream to copy the data to.</param>
        /// <returns>Awaitable task.</returns>
        public Task CopyToAsync(Stream stream)
        {
            return stream.WriteAsync(buffer, 0, Length);
        }

        /// <summary>
        /// Creates a new array with the data from this buffer.
        /// </summary>
        /// <returns>New array with the data of the buffer.</returns>
        public byte[] ToArray()
        {
            byte[] newArray = new byte[Length];
            Array.Copy(buffer, 0, newArray, 0, Length);
            return newArray;
        }

        /// <summary>
        /// Gets an array segment from this buffer.
        /// </summary>
        /// <returns>Array segment.</returns>
        public ArraySegment<byte> ToArraySegment()
        {
            return new ArraySegment<byte>(buffer);
        }

        /// <summary>
        /// Gets an Enumerator to iterate through the elements of this buffer.
        /// </summary>
        /// <returns>Enumerator with the elements of this buffer.</returns>
        public IEnumerator<byte> GetEnumerator()
        {
            foreach (var b in buffer)
            {
                yield return b;
            }
        }

        /// <summary>
        /// Gets an Enumerator to iterate through the elements of this buffer.
        /// </summary>
        /// <returns>Enumerator with the elements of this buffer.</returns>
        IEnumerator IEnumerable.GetEnumerator() => buffer.GetEnumerator();

        /// <summary>
        /// Returns a human readable string representation of this object.
        /// </summary>
        /// <returns>Human readable string representation of this object.</returns>
        public override string ToString()
        {
            return $"Buffer ({buffer.Length})[{string.Join(", ", buffer.Select(b => "0x" + b.ToString("X2")))}]";
        }

        /// <summary>
        /// Indicates if the ByteBuffer is equal to another.
        /// </summary>
        /// <param name="other">The other ByteBuffer to compare.</param>
        /// <returns>True if both ByteBuffer are equals, False otherwise.</returns>
        public bool Equals(ByteBuffer other)
        {
            return other is object && buffer.SequenceEqual(other.buffer);
        }

        /// <summary>
        /// Indicates if the ByteBuffer is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the ByteBuffer is equal to the other object, False otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is ByteBuffer buffer && Equals(buffer);
        }

        /// <summary>
        /// Gets the hashcode.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode()
        {
            return 143091379 + EqualityComparer<byte[]>.Default.GetHashCode(buffer);
        }

        /// <summary>
        /// Compares two ByteBuffer.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if the operands are equal, False otherwise.</returns>
        public static bool operator ==(ByteBuffer left, ByteBuffer right)
        {
            return left is null ? right is null : left.Equals(right);
        }

        /// <summary>
        /// Compares two ByteBuffer.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if the operands are not equal, False otherwise.</returns>
        public static bool operator !=(ByteBuffer left, ByteBuffer right)
        {
            return !(left == right);
        }
    }
}
