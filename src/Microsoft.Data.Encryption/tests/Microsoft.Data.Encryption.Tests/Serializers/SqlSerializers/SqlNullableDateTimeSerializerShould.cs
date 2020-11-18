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
    public class SqlNullableDateTimeSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableDateTimeSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<DateTime?> serializer = new SqlNullableDatetimeSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<DateTime?> serializer = new SqlNullableDatetimeSerializer();
            DateTime? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableDatetimeSerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTime? plaintext)
        {
            Serializer<DateTime?> serializer = new SqlNullableDatetimeSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.DateTime);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableDatetimeSerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTime? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.DateTime);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<DateTime?> serializer = new SqlNullableDatetimeSerializer();
            DateTime? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTime? actualPlaintext = (DateTime?)Database.SelectPlaintext(SqlDbType.DateTime);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlNullableDatetimeSerializerInvalidData]
        public void ThrowWhenValueOutOfRange(DateTime? plaintext)
        {
            Serializer<DateTime?> serializer = new SqlNullableDatetimeSerializer();
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
            Serializer<DateTime?> serializer = new SqlNullableDatetimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableDatetimeSerializerInvalidDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { DateTime.MinValue };
                yield return new object[] { DateTime.Parse("1752-12-31 23:59:59.9999999") };
            }
        }

        public class SqlNullableDatetimeSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { DateTime.Parse("1753-01-01 00:00:00.000") };
                yield return new object[] { DateTime.Parse("1900-01-01 00:00:00.000") };
                yield return new object[] { DateTime.Parse("9999-12-31 23:59:59.997") };
                yield return new object[] { DateTime.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
