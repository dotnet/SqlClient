// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    [Collection(SimulatedServerTestCollection.Name)]
    public class SqlVariantRpcParameterTests
    {
        [Theory]
        [InlineData(true, 4500, 0, 0, false)]
        [InlineData(true, 4500, 0, 0, true)]
        [InlineData(true, 4500, 0, 1, false)]
        [InlineData(true, 4500, 0, 1, true)]
        [InlineData(true, 4500, 0, 4500, false)]
        [InlineData(true, 4500, 0, 4500, true)]
        [InlineData(true, 4500, 0, 4501, false)]
        [InlineData(true, 4500, 0, 4501, true)]
        [InlineData(true, 4001, 0, 1, false)]
        [InlineData(true, 8000, 0, 1, false)]
        [InlineData(true, 4500, 8000, 1, false)]
        [InlineData(true, 4500, 8000, 1, true)]
        [InlineData(false, 4000, 0, 6, false)]
        [InlineData(false, 4000, 3999, 6, false)]
        public async Task Rpc_AnsiValueWithOffset_WritesExactLengthsAndFollowingParameter(
            bool variant, int count, int size, int offset, bool sqlString)
        {
            using TdsServer server = new();
            server.Start();
            using SqlConnection connection = new(new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false
            }.ConnectionString);
            connection.Open();
            TdsParser parser = connection.Parser;
            typeof(TdsParser).GetField("_defaultEncoding",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parser, Encoding.UTF8);
            typeof(TdsParser).GetField("_defaultCollation",
                BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parser,
                    new SqlCollation(1033 | TdsEnums.UTF8_IN_TDSCOLLATION, 0));
            TdsParserStateObject state = (TdsParserStateObject)typeof(TdsParser)
                .GetField("_physicalStateObj", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(parser)!;
            state.SetPacketSize(32768);
            int start = state._outBytesUsed;

            string text = variant ? "A" + new string('b', count - 2) + "Z"
                : "prefix" + new string('\u00e9', count);
            using SqlCommand command = new("SELECT @p, @marker", connection);
            SqlParameter parameter = command.Parameters.Add("@p",
                variant ? SqlDbType.Variant : SqlDbType.VarChar, size);
            parameter.Value = sqlString ? new SqlString(text) : (object)text;
            parameter.Offset = offset;
            const int Marker = 0x12345678;
            command.Parameters.Add("@marker", SqlDbType.Int).Value = Marker;
            MethodInfo serialize = typeof(TdsParser).GetMethod("TDSExecuteRPCAddParameter",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            // Capture actual RPC output; the second parameter detects incorrect framing.
            for (int i = 0; i < command.Parameters.Count; i++)
            {
                SqlParameter current = command.Parameters[i];
                current.Validate(i, false);
                MetaType metaType = current.ValidateTypeLengths(parser);
                Task? pending = (Task?)serialize.Invoke(parser, new object[]
                {
                    state, current, metaType, (byte)0, command, false
                });
                if (pending != null)
                {
                    await pending;
                }
            }

            string expected = variant ? text : new string('\u00e9', size == 0 ? 4000 : 3999);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            using MemoryStream bytes = new(state._outBuff, start, state._outBytesUsed - start);
            using BinaryReader reader = new(bytes);
            int nameCharacters = reader.ReadByte();
            Assert.Equal("@p", Encoding.Unicode.GetString(reader.ReadBytes(nameCharacters * 2)));
            Assert.Equal(0, reader.ReadByte()); // parameter status
            if (variant)
            {
                Assert.Equal(TdsEnums.SQLVARIANT, reader.ReadByte());
                // Variant headers contain two type bytes and seven varchar property bytes.
                int expectedVariantLength = expectedBytes.Length + 2 + 7;
                Assert.Equal(expectedVariantLength, reader.ReadInt32()); // maximum length
                Assert.Equal(expectedVariantLength, reader.ReadInt32()); // actual length
                Assert.Equal(TdsEnums.SQLBIGVARCHAR, reader.ReadByte());
                Assert.Equal(7, reader.ReadByte()); // property-byte count
            }
            else
            {
                Assert.Equal(TdsEnums.SQLBIGVARCHAR, reader.ReadByte());
                Assert.Equal(expectedBytes.Length, reader.ReadUInt16()); // maximum length
            }
            Assert.Equal(1033 | TdsEnums.UTF8_IN_TDSCOLLATION, reader.ReadUInt32());
            Assert.Equal(0, reader.ReadByte()); // sort ID
            Assert.Equal(expectedBytes.Length, reader.ReadUInt16()); // data length
            Assert.Equal(expectedBytes, reader.ReadBytes(expectedBytes.Length));

            nameCharacters = reader.ReadByte();
            Assert.Equal("@marker",
                Encoding.Unicode.GetString(reader.ReadBytes(nameCharacters * 2)));
            Assert.Equal(0, reader.ReadByte());
            Assert.Equal(TdsEnums.SQLINTN, reader.ReadByte());
            Assert.Equal(4, reader.ReadByte()); // maximum length
            Assert.Equal(4, reader.ReadByte()); // actual length
            Assert.Equal(Marker, reader.ReadInt32());
            Assert.Equal(bytes.Length, bytes.Position);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Variant_OverMaximumLengthWithOffset_RejectsCompleteValue(bool sqlString)
        {
            string text = new('a', 8001);
            SqlParameter parameter = new("@p", SqlDbType.Variant)
            {
                Value = sqlString ? new SqlString(text) : (object)text,
                Offset = 1
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            {
                parameter.Validate(0, false);
                parameter.GetActualSize();
            });

            Assert.Equal(SQL.ParameterInvalidVariant("@p").Message, exception.Message);
        }
    }
}
