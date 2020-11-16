using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public class SqlNullableBigIntSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableBigIntSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<long?> serializer = new SqlNullableBigintSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<long?> serializer = new SqlNullableBigintSerializer();
            long? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableBigIntSerializerTestData]
        public void SerializeTheSameAsSqlServer(long? plaintext)
        {
            Serializer<long?> serializer = new SqlNullableBigintSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.BigInt) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.BigInt);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableBigIntSerializerTestData]
        public void DeserializeTheSameAsSqlServer(long? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.BigInt) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.BigInt);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<long?> serializer = new SqlNullableBigintSerializer();
            long? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            long? actualPlaintext = (long?)Database.SelectPlaintext(SqlDbType.BigInt);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<long?> serializer = new SqlNullableBigintSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableBigIntSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { long.MaxValue };
                yield return new object[] { 9876543210 };
                yield return new object[] { 1 };
                yield return new object[] { 0 };
                yield return new object[] { -1 };
                yield return new object[] { -9876543210 };
                yield return new object[] { long.MinValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
