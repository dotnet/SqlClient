using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Data.SqlClient;
using System;
using System.Data;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlVarcharSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlVarcharSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<string> serializer = new SqlVarcharSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<string> serializer = new SqlVarcharSerializer();
            string actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlVarcharSerializerTestData]
        public void SerializeTheSameAsSqlServer(string plaintext, int size, int codepage)
        {
            SqlVarcharSerializer serializer = new SqlVarcharSerializer(size, codepage);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.VarChar, size) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.VarChar);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlVarcharSerializerTestData]
        public void DeserializeTheSameAsSqlServer(string plaintext, int size, int codepage)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.VarChar, size) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.VarChar);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlVarcharSerializer serializer = new SqlVarcharSerializer(size, codepage);
            string expectedPlaintext = serializer.Deserialize(plaintextBytes);
            string actualPlaintext = (string)Database.SelectPlaintext(SqlDbType.VarChar);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(0)]
        [InlineData(8001)]
        [InlineData(int.MaxValue)]
        public void ThrowWhenSizeOutOfRange(int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlVarcharSerializer(size));
        }

        public class SqlVarcharSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 50, 1252 };
                yield return new object[] { "aaaaaaaaaabbbbbbbbbbccccccccccddddddddddeeeeeeeeeeffffffffff", 50, 1252 };
                yield return new object[] { "aaaaaaaaaabbbbbbbbbbccccccccccddddddddddeeeeeeeeee", 50, 1252 };
                yield return new object[] { "abcdefghij", 50, 1252 };
                yield return new object[] { "ナンセンス 1杯1杯", 50, 1252 };
                yield return new object[] { "a", 50, 1252 };
                yield return new object[] { "          ", 50, 1252 };
                yield return new object[] { null, 50, 65001 };
                yield return new object[] { "aaaaaaaaaabbbbbbbbbbccccccccccddddddddddeeeeeeeeeeffffffffff", 50, 65001 };
                yield return new object[] { "aaaaaaaaaabbbbbbbbbbccccccccccddddddddddeeeeeeeeee", 50, 65001 };
                yield return new object[] { "abcdefghij", 50, 65001 };
                // TODO - Must change database level collation in addition to column collation for this to pass.
                // yield return new object[] { "ナンセンス 1杯1杯", 50, 65001 };
                yield return new object[] { "a", 50, 65001 };
                yield return new object[] { "          ", 50, 65001 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
