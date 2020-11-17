using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using System;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Data.SqlClient;
using System.Data;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlDatetimeoffsetSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlDatetimeoffsetSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlDatetimeoffsetSerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTimeOffset plaintext, byte scale)
        {
            SqlDatetimeoffsetSerializer serializer = new SqlDatetimeoffsetSerializer(scale);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTimeOffset) { Value = plaintext, Scale = scale });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.DateTimeOffset);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlDatetimeoffsetSerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTimeOffset plaintext, byte scale)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTimeOffset) { Value = plaintext, Scale = scale });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.DateTimeOffset);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlDatetimeoffsetSerializer serializer = new SqlDatetimeoffsetSerializer(scale);
            DateTimeOffset expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTimeOffset actualPlaintext = (DateTimeOffset)Database.SelectPlaintext(SqlDbType.DateTimeOffset);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlDatetimeoffsetSerializer serializer = new SqlDatetimeoffsetSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
        public void ShouldThrowIfDeserializingLessThanTenBytes(byte[] data)
        {
            SqlDatetimeoffsetSerializer serializer = new SqlDatetimeoffsetSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        [Theory]
        [SqlDatetimeoffsetSerializerInvalidSizeData]
        public void ThrowWhenConstructorSizeOutOfRange(int scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlDatetimeoffsetSerializer(scale));
        }

        [Theory]
        [SqlDatetimeoffsetSerializerInvalidSizeData]
        public void ThrowWhenPropertySizeOutOfRange(int scale)
        {
            SqlDatetimeoffsetSerializer serializer = new SqlDatetimeoffsetSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Scale = scale);
        }

        public class SqlDatetimeoffsetSerializerInvalidSizeDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -1 };
                yield return new object[] { 8 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlDatetimeoffsetSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
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
