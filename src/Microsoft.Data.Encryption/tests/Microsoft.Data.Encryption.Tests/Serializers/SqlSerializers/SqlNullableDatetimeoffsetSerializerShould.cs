using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

using static Microsoft.Data.Encryption.TestUtilities.DataTestUtility;
using System.Data;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public class SqlNullableDatetimeoffsetSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableDatetimeoffsetSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<DateTimeOffset?> serializer = new SqlNullableDatetimeoffsetSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<DateTimeOffset?> serializer = new SqlNullableDatetimeoffsetSerializer();
            DateTimeOffset? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableDatetimeoffsetSerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTimeOffset? plaintext, byte scale)
        {
            Serializer<DateTimeOffset?> serializer = new SqlNullableDatetimeoffsetSerializer(scale);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTimeOffset) { Value = plaintext, Scale = scale });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.DateTimeOffset);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableDatetimeoffsetSerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTimeOffset? plaintext, byte scale)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTimeOffset) { Value = plaintext, Scale = scale });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.DateTimeOffset);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<DateTimeOffset?> serializer = new SqlNullableDatetimeoffsetSerializer(scale);
            DateTimeOffset? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTimeOffset? actualPlaintext = (DateTimeOffset?)Database.SelectPlaintext(SqlDbType.DateTimeOffset);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
        public void ShouldThrowIfDeserializingLessThanTenBytes(byte[] data)
        {
            Serializer<DateTimeOffset?> serializer = new SqlNullableDatetimeoffsetSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        [Theory]
        [SqlNullableDatetimeoffsetSerializerInvalidSizeData]
        public void ThrowWhenConstructorSizeOutOfRange(int scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNullableDatetimeoffsetSerializer(scale));
        }

        [Theory]
        [SqlNullableDatetimeoffsetSerializerInvalidSizeData]
        public void ThrowWhenPropertySizeOutOfRange(int scale)
        {
            SqlNullableDatetimeoffsetSerializer serializer = new SqlNullableDatetimeoffsetSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Scale = scale);
        }

        public class SqlNullableDatetimeoffsetSerializerInvalidSizeDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -1 };
                yield return new object[] { 8 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlNullableDatetimeoffsetSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 7 };
                yield return new object[] { DateTimeOffset.Parse("0001-01-01 00:00:00.0000000 -08:00"), 7 };
                yield return new object[] { DateTimeOffset.Parse("0001-01-01 00:00:00.0000000"), 7 };
                yield return new object[] { DateTimeOffset.Parse("1900-01-01 00:00:00.0005555 -08:00"), 7 };
                yield return new object[] { DateTimeOffset.Parse("1900-01-01 00:00:00.0005555 +08:00"), 7 };
                yield return new object[] { DateTimeOffset.Parse("9999-12-31 15:59:59.9999999 -08:00"), 7 };

                yield return new object[] { DateTimeOffset.Parse("0001-01-01 00:00:00.0000000 -08:00"), 3 };
                yield return new object[] { DateTimeOffset.Parse("0001-01-01 00:00:00.0000000"), 3 };
                yield return new object[] { DateTimeOffset.Parse("9999-12-31 15:59:59.9999999 -08:00"), 3 };
                yield return new object[] { DateTimeOffset.Parse("1900-01-01 00:00:00.0005555 -08:00"), 3 };
                yield return new object[] { DateTimeOffset.Parse("1900-01-01 00:00:00.0005555 +08:00"), 3 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
