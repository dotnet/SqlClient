using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using System;
using Microsoft.Data.SqlClient;
using System.Data;

using static Microsoft.Data.Encryption.TestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public class SqlNullableMoneySerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableMoneySerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<decimal?> serializer = new SqlNullableMoneySerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<decimal?> serializer = new SqlNullableMoneySerializer();
            decimal? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableMoneySerializerTestData]
        public void SerializeTheSameAsSqlServer(decimal? plaintext)
        {
            Serializer<decimal?> serializer = new SqlNullableMoneySerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Money) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Money);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableMoneySerializerTestData]
        public void DeserializeTheSameAsSqlServer(decimal? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Money) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Money);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<decimal?> serializer = new SqlNullableMoneySerializer();
            decimal? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            decimal? actualPlaintext = (decimal?)Database.SelectPlaintext(SqlDbType.Money);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlNullableMoneySerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(decimal? plaintext)
        {
            Serializer<decimal?> serializer = new SqlNullableMoneySerializer();
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
            Serializer<decimal?> serializer = new SqlNullableMoneySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableMoneySerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { decimal.MinValue };
                yield return new object[] { -922337203685477.5809M };
                yield return new object[] { 922337203685477.5808M };
                yield return new object[] { decimal.MaxValue };
            }
        }

        public class SqlNullableMoneySerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { -922337203685477.5808M };
                yield return new object[] { -1.00M };
                yield return new object[] { 0.00M };
                yield return new object[] { 1.00M };
                yield return new object[] { 922337203685477.5807M };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
