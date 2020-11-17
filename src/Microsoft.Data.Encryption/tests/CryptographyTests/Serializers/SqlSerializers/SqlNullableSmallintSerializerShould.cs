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
    public class SqlNullableSmallintSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableSmallintSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<short?> serializer = new SqlNullableSmallintSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<short?> serializer = new SqlNullableSmallintSerializer();
            short? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableSmallintSerializerTestData]
        public void SerializeTheSameAsSqlServer(short? plaintext)
        {
            Serializer<short?> serializer = new SqlNullableSmallintSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallInt) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.SmallInt);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableSmallintSerializerTestData]
        public void DeserializeTheSameAsSqlServer(short? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallInt) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.SmallInt);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<short?> serializer = new SqlNullableSmallintSerializer();
            short? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            short? actualPlaintext = (short?)Database.SelectPlaintext(SqlDbType.SmallInt);

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
            Serializer<short?> serializer = new SqlNullableSmallintSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableSmallintSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { short.MinValue };
                yield return new object[] { (short)-12345 };
                yield return new object[] { (short)-1 };
                yield return new object[] { (short)0 };
                yield return new object[] { (short)1 };
                yield return new object[] { (short)12345 };
                yield return new object[] { short.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
