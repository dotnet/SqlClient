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
    public sealed class SqlNvarcharSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNvarcharSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<string> serializer = new SqlNvarcharSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<string> serializer = new SqlNvarcharSerializer();
            string actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNvarcharSerializerTestData]
        public void SerializeTheSameAsSqlServer(string plaintext, int size)
        {
            SqlNvarcharSerializer serializer = new SqlNvarcharSerializer(size);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.NVarChar, size) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.NVarChar);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNvarcharSerializerTestData]
        public void DeserializeTheSameAsSqlServer(string plaintext, int size)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.NVarChar, size) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.NVarChar);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlNvarcharSerializer serializer = new SqlNvarcharSerializer(size);
            string expectedPlaintext = serializer.Deserialize(plaintextBytes);
            string actualPlaintext = (string)Database.SelectPlaintext(SqlDbType.NVarChar);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlNvarcharSerializerInvalidSizeData]
        public void ThrowWhenConstructorSizeOutOfRange(int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNvarcharSerializer(size));
        }

        [Theory]
        [SqlNvarcharSerializerInvalidSizeData]
        public void ThrowWhenPropertySizeOutOfRange(int size)
        {
            SqlNvarcharSerializer serializer = new SqlNvarcharSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Size = size);
        }

        public class SqlNvarcharSerializerInvalidSizeDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -2 };
                yield return new object[] { 0 };
                yield return new object[] { 4001 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlNvarcharSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 50 };
                yield return new object[] { "aaaaaaaaaabbbbbbbbbbccccccccccddddddddddeeeeeeeeeeffffffffff", 50 };
                yield return new object[] { "aaaaaaaaaabbbbbbbbbbccccccccccddddddddddeeeeeeeeee", 50 };
                yield return new object[] { "abcdefghij", 50 };
                yield return new object[] { "a", 50 };
                yield return new object[] { "          ", 50 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
