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
    public class SqlNullableIntSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableIntSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<int?> serializer = new SqlNullableIntSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<int?> serializer = new SqlNullableIntSerializer();
            int? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableIntSerializerTestData]
        public void SerializeTheSameAsSqlServer(int plaintext)
        {
            Serializer<int?> serializer = new SqlNullableIntSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Int) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Int);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableIntSerializerTestData]
        public void DeserializeTheSameAsSqlServer(int plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Int) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Int);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<int?> serializer = new SqlNullableIntSerializer();
            int? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            int? actualPlaintext = (int?)Database.SelectPlaintext(SqlDbType.Int);

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
            Serializer<int?> serializer = new SqlNullableIntSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableIntSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { int.MinValue };
                yield return new object[] { -123456789 };
                yield return new object[] { -1 };
                yield return new object[] { 0 };
                yield return new object[] { 1 };
                yield return new object[] { 123456789 };
                yield return new object[] { int.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
