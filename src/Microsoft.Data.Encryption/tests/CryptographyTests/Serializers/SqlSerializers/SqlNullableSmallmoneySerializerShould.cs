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
    public class SqlNullableSmallmoneySerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableSmallmoneySerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<decimal?> serializer = new SqlNullableSmallmoneySerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<decimal?> serializer = new SqlNullableSmallmoneySerializer();
            decimal? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableSmallmoneySerializerTestData]
        public void SerializeTheSameAsSqlServer(decimal? plaintext)
        {
            Serializer<decimal?> serializer = new SqlNullableSmallmoneySerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallMoney) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.SmallMoney);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableSmallmoneySerializerTestData]
        public void DeserializeTheSameAsSqlServer(decimal? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallMoney) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.SmallMoney);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<decimal?> serializer = new SqlNullableSmallmoneySerializer();
            decimal? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            decimal? actualPlaintext = (decimal?)Database.SelectPlaintext(SqlDbType.SmallMoney);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlNullableSmallmoneySerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(decimal? plaintext)
        {
            Serializer<decimal?> serializer = new SqlNullableSmallmoneySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<decimal?> serializer = new SqlNullableSmallmoneySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableSmallmoneySerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { decimal.MinValue };
                yield return new object[] { -214748.3649M };
                yield return new object[] { 214748.3648M };
                yield return new object[] { decimal.MaxValue };
            }
        }

        public class SqlNullableSmallmoneySerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { -214748.3648M };
                yield return new object[] { -1.00M };
                yield return new object[] { 0.00M };
                yield return new object[] { 1.00M };
                yield return new object[] { 214748.3647M };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
