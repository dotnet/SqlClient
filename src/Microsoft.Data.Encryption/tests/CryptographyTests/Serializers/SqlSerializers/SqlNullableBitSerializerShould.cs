using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using static Microsoft.Data.Encryption.TestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public class SqlNullableBitSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableBitSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<bool?> serializer = new SqlNullableBitSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<bool?> serializer = new SqlNullableBitSerializer();
            bool? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableBitSerializerTestData]
        public void SerializeTheSameAsSqlServer(bool? plaintext)
        {
            Serializer<bool?> serializer = new SqlNullableBitSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Bit) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Bit);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableBitSerializerTestData]
        public void DeserializeTheSameAsSqlServer(bool? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Bit) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Bit);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<bool?> serializer = new SqlNullableBitSerializer();
            bool? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            bool? actualPlaintext = (bool?)Database.SelectPlaintext(SqlDbType.Bit);

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
            Serializer<bool?> serializer = new SqlNullableBitSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableBitSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { false };
                yield return new object[] { true };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
