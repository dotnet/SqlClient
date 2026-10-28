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
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    [Collection(SimulatedServerTestCollection.Name)]
    public class EncryptedAnsiParameterTests
    {
        [Theory]
        [InlineData(4000, 8000, 0, false, false)]
        [InlineData(4001, 8000, 0, false, false)]
        [InlineData(4001, 4000, 0, false, false)]
        [InlineData(4001, 8000, 1, false, false)]
        [InlineData(4000, 0, 1, true, false)]
        [InlineData(4001, 0, 1, true, false)]
        [InlineData(4001, 4000, 1, true, false)]
        [InlineData(0, 8000, 0, false, true)]
        [InlineData(0, -1, 0, true, true)]
        public async Task EncryptedRpc_Utf8Parameter_WritesOriginalTypeInfoAndExactCiphertext(
            int count, int size, int offset, bool sqlString, bool isNull)
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

            // Supply an initialized test cipher to exercise encryption without a key store.
            SqlAeadAes256CbcHmac256Algorithm cipher = new(
                new AeadAes256CbcHmac256EncryptionKey(new byte[32]),
                EncryptionType.Deterministic, SqlAeadAes256CbcHmac256Algorithm.CurrentVersion);
            string text = new string('a', offset) + new string('\u00e9', count);
            object value = isNull ? (sqlString ? SqlString.Null : DBNull.Value)
                : sqlString ? new SqlString(text) : (object)text;
            using SqlCommand command = new("SELECT @p", connection);
            SqlParameter parameter = command.Parameters.Add("@p", SqlDbType.VarChar, size);
            parameter.Value = value;
            parameter.Offset = offset;
            parameter.CipherMetadata = new SqlCipherMetadata(new SqlTceCipherInfoEntry(), 0,
                2, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName, 1, 1)
            {
                CipherAlgorithm = cipher,
                EncryptionKeyInfo = new SqlEncryptionKeyInfo()
            };
            parameter.Validate(0, false);
            MetaType metaType = parameter.ValidateTypeLengths(parser);

            // Invoke the actual encrypted RPC writer, including its original TYPE_INFO caller.
            MethodInfo serialize = typeof(TdsParser).GetMethod("TDSExecuteRPCAddParameter",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            Task? pending = (Task?)serialize.Invoke(parser, new object[]
            {
                state, parameter, metaType, TdsEnums.RPC_PARAM_ENCRYPTED, command, false
            });
            if (pending != null)
            {
                await pending;
            }

            using MemoryStream bytes = new(state._outBuff, start, state._outBytesUsed - start);
            using BinaryReader reader = new(bytes);
            int nameCharacters = reader.ReadByte();
            Assert.Equal("@p", Encoding.Unicode.GetString(reader.ReadBytes(nameCharacters * 2)));
            Assert.Equal(TdsEnums.RPC_PARAM_ENCRYPTED, reader.ReadByte());
            Assert.Equal(TdsEnums.SQLBIGVARBINARY, reader.ReadByte());
            Assert.Equal(ushort.MaxValue, reader.ReadUInt16());
            ulong payloadLength = reader.ReadUInt64();
            int transmittedCharacters = size > 0 ? Math.Min(size, count) : count;
            if (isNull)
            {
                Assert.Equal(ulong.MaxValue, payloadLength);
            }
            else
            {
                int chunkLength = reader.ReadInt32();
                Assert.Equal((ulong)chunkLength, payloadLength);
                byte[] encrypted = reader.ReadBytes(chunkLength);
                byte[] plaintext = cipher.DecryptData(encrypted);
                Assert.Equal(Encoding.UTF8.GetBytes(new string('\u00e9', transmittedCharacters)),
                    plaintext);
                Assert.Equal(0, reader.ReadInt32()); // PLP terminator
            }

            Assert.Equal(TdsEnums.SQLBIGVARCHAR, reader.ReadByte());
            int capacity = Math.Max(size, transmittedCharacters * 2);
            ushort expectedLength = size == -1 || capacity > 8000
                ? ushort.MaxValue : (ushort)capacity;
            Assert.Equal(expectedLength, reader.ReadUInt16());
            Assert.Equal(1033 | TdsEnums.UTF8_IN_TDSCOLLATION, reader.ReadUInt32());
            Assert.Equal(0, reader.ReadByte()); // sort ID
            Assert.Equal(parameter.CipherMetadata.CipherAlgorithmId, reader.ReadByte());
            Assert.Equal(parameter.CipherMetadata.EncryptionType, reader.ReadByte());
            Assert.Equal(0, reader.ReadInt32()); // database ID
            Assert.Equal(0, reader.ReadInt32()); // CEK ID
            Assert.Equal(0, reader.ReadInt32()); // CEK version
            Assert.Equal(0UL, reader.ReadUInt64()); // CEK metadata version
            Assert.Equal(1, reader.ReadByte()); // normalization version
            Assert.Equal(bytes.Length, bytes.Position);
        }
    }
}
