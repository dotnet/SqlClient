using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using System;
using Microsoft.Data.SqlClient;
using System.Data;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlNcharSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNcharSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<string> serializer = new SqlNcharSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<string> serializer = new SqlNcharSerializer();
            string actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNcharSerializerTestData]
        public void SerializeTheSameAsSqlServer(string plaintext, int size)
        {
            Serializer<string> serializer = new SqlNcharSerializer(size);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.NChar, size) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.NChar);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNcharSerializerTestData]
        public void DeserializeTheSameAsSqlServer(string plaintext, int size)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.NChar, size) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.NChar);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<string> serializer = new SqlNcharSerializer(size);
            string expectedPlaintext = serializer.Deserialize(plaintextBytes);
            string actualPlaintext = (string)Database.SelectPlaintext(SqlDbType.NChar);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(4001)]
        [InlineData(int.MaxValue)]
        public void ThrowWhenSizeOutOfRange(int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNcharSerializer(size));
        }

        public class SqlNcharSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 10 };
                yield return new object[] { "0000000000123456789", 10 };
                yield return new object[] { "0000000000", 10 };
                yield return new object[] { "abcdefghij", 10 };
                yield return new object[] { "a         ", 10 };
                yield return new object[] { "          ", 10 };
                yield return new object[] { "", 10 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
