using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.NetCore.UnitTests
{
    public class Dummy
    {
        [Fact]
        public void Test()
        {
            int offset = 0;
            byte[] payload= new byte[] { 1, 2, 3, 4 };
            int payLoadOffset = payload[offset++] << 8 | payload[offset++];
            int payloadLength = payload[offset++] << 8 | payload[offset++];

            Assert.Equal(payLoadOffset, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2)));

            Assert.Equal(payloadLength, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2, 2)));
        }
    }
}
