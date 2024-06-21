// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using Microsoft.Data.SqlClientX.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.IO
{
    public class TdsWriterTests
    {
        [Fact]
        public void TestSerializeDecimal_SerializesSuccessfully()
        {
            TdsWriter writer = new();
            var data = new decimal(8.5);
            var expectedSign = true;

            // Serialize decimal to byte array for Sql Server needs
            var resultBytes = writer.SerializeDecimal(data);
            
            // Assert format is correct
            Assert.Equal(17, resultBytes.Length);

            // Deserialize decimal and compare bytes
            var sign = (resultBytes[0] & 0x80000000) != 0x80000000;
            Assert.Equal(expectedSign, sign);

            // Convert bytes to decimal bits
            var bits = new int[4];
            for (int n = 0; n < 4; n++)
            {
                bits[n] = BinaryPrimitives.ReadInt32LittleEndian(resultBytes.AsSpan(n * 4 + 1, 4));
            }

            // Assert: 4th integer should always be set to 0
            Assert.True(0 == bits[3], "4th integer not set to 0");

            // Scale is not compared as in Sql Decimal, scale is set via metadata.
            // We set expected scale here to ensure data is converted properly.
            var newValue = new decimal(bits[0], bits[1], bits[2], !sign, 1); 
            Assert.Equal(data, newValue);
        }
    }
}
