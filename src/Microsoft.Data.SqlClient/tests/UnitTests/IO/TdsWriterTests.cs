// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    public class TdsWriterTests: IDisposable
    {
        private readonly TdsStream _stream;
        private readonly MemoryStream _msWrite;
        private readonly MemoryStream _msRead;

        public TdsWriterTests() {
            _msWrite = new();
            _msRead = new();
            TdsWriteStream tdsWriteStream = new(_msWrite);
            TdsReadStream tdsReadStream = new(_msRead);

            _stream = new(tdsWriteStream, tdsReadStream);
        }

        [Fact]
        public async Task TestWriteShortAsync_WritesSuccessfully()
        {
            short value = 5;
            await _stream.TdsWriter.WriteShortAsync(value, true, CancellationToken.None);
            ValidateWriteBuffer(BitConverter.GetBytes(value), sizeof(short));
        }

        [Fact]
        public void TestSerializeDecimal_SerializesSuccessfully()
        {
            TdsWriter writer = _stream.TdsWriter;
            var data = new decimal(8.5);
            var expectedSign = true;

            // Serialize decimal to byte array for Sql Server needs
            Span<byte> resultBytes = writer.SerializeDecimal(data);

            // Assert format is correct
            Assert.Equal(17, resultBytes.Length);

            // Deserialize decimal and compare bytes
            var sign = (resultBytes[0] & 0x80000000) != 0x80000000;
            Assert.Equal(expectedSign, sign);

            // Convert bytes to decimal bits
            var bits = new int[4];
            for (int n = 0; n < 4; n++)
            {
                var start = (n * 4 + 1);
                var end = start + 4;
                bits[n] = BinaryPrimitives.ReadInt32LittleEndian(resultBytes[start..end]);
            }

            // Assert: 4th integer should always be set to 0
            Assert.True(0 == bits[3], "4th integer not set to 0");

            // Scale is not compared as in Sql Decimal, scale is set via metadata.
            // We set expected scale here to ensure data is converted properly.
            var newValue = new decimal(bits[0], bits[1], bits[2], !sign, 1);
            Assert.Equal(data, newValue);
        }

        private void ValidateWriteBuffer(byte[] expected, int length)
        {
            byte[] buffer = new byte[length];
            _msWrite.Read(buffer);

            // Assert Stream is now empty
            Assert.Equal(-1, _msWrite.ReadByte());
            Assert.Equal(expected.Length, buffer.Length);
            Assert.Equal(length, buffer.Length);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _msWrite.Dispose();
            _msWrite.Dispose();
            _stream.Dispose();
        }
    }
}
