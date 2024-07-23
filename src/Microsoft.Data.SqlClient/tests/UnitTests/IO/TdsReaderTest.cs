// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.UnitTests.IO.TdsHelpers;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    public class TdsReaderTest : IDisposable
    {
        private readonly TdsWriteStream _tdsWriteStream;
        private readonly MemoryStream _msWrite;

        public TdsReaderTest()
        {
            _msWrite = new MemoryStream();
            _tdsWriteStream = new(_msWrite);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadByteAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            short value = byte.MaxValue;
            byte[] payload = new byte[sizeof(short)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteInt16LittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);
            byte[] readBuffer = new byte[sizeof(short)];

            for (int i = 0; i < payload.Length; i++)
            {
                // Act
                byte readByte = await stream.TdsReader.ReadByteAsync(isAsync, default);

                // Assert
                Assert.Equal(payload[i], readByte);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadShortAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            short value = short.MinValue;
            byte[] payload = new byte[sizeof(short)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteInt16LittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            short readValue = await stream.TdsReader.ReadInt16Async(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadIntAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            int value = int.MinValue;
            byte[] payload = new byte[sizeof(int)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteInt32LittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            int readValue = await stream.TdsReader.ReadInt32Async(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadLongAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            long value = long.MinValue;
            byte[] payload = new byte[sizeof(long)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteInt64LittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            long readValue = await stream.TdsReader.ReadInt64Async(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadUInt16Async_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            ushort value = ushort.MaxValue;
            byte[] payload = new byte[sizeof(ushort)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            ushort readValue = await stream.TdsReader.ReadUInt16Async(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadUIntAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            uint value = int.MaxValue;
            byte[] payload = new byte[sizeof(int)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteUInt32LittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            uint readValue = await stream.TdsReader.ReadUInt32Async(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadULongAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            ulong value = long.MaxValue;
            byte[] payload = new byte[sizeof(long)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteUInt64LittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            ulong readValue = await stream.TdsReader.ReadUInt64Async(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadSingleAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            float value = float.MinValue;
            byte[] payload = new byte[sizeof(float)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteSingleLittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            float readValue = await stream.TdsReader.ReadSingleAsync(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadDoubleAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            double value = double.MaxValue;
            byte[] payload = new byte[sizeof(double)];
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteDoubleLittleEndian(payload, value);
            using TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize);

            // Act
            double readValue = await stream.TdsReader.ReadDoubleAsync(isAsync, CancellationToken.None);

            // Assert
            Assert.Equal(value, readValue);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadCharAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            char value = 'A';
            byte[] payload = new byte[sizeof(char)];
            Encoding.Unicode.GetBytes(new char[] { value }, payload);

            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            using (TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize))
            {
                // Act
                char readChar = await stream.TdsReader.ReadCharAsync(isAsync, default);

                // Assert
                Assert.Equal(value, readChar);
                Assert.True(stream.PacketDataLeft == 0);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async void ReadCharArrayAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            char[] values = { 'A', 'B', 'C' };
            byte[] payload = new byte[sizeof(char) * values.Length];
            Encoding.Unicode.GetBytes(values, payload);

            int negotiatedPacketSize = TdsEnums.HEADER_LEN + payload.Length;
            using (TdsStream stream = GetStreamFromReadableBuffer(payload, negotiatedPacketSize))
            {
                // Act
                char[] readChars = await stream.TdsReader.ReadCharArrayAsync(values.Length, isAsync, default);

                // Assert
                Assert.Equal(values, readChars);
                Assert.True(stream.PacketDataLeft == 0);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadStringAsync_ReadsSuccessfully(bool isAsync)
        {
            // Arrange
            string input = "Hello, World!";
            int negotiatedPacketSize = TdsEnums.HEADER_LEN + input.Length;
            using (TdsStream stream = GetStreamFromReadableBuffer(Encoding.Unicode.GetBytes(input), negotiatedPacketSize))
            {
                TdsReader reader = stream.TdsReader;
                int length = input.Length;

                // Act
                string result = await reader.ReadStringAsync(length, isAsync, CancellationToken.None);

                // Assert
                Assert.Equal(input, result);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _msWrite.Dispose();
            _tdsWriteStream.Dispose();
        }

        private TdsStream GetStreamFromReadableBuffer(byte[] buffer, int packetSize)
            => new(_tdsWriteStream, new(SplittableStream.FromMessage(
                new(packetSize, buffer, TdsEnums.MT_PRELOGIN, TdsUtils.GenerateSpid()))));
    }
}
