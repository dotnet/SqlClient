using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using System;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using System.Data;
using Microsoft.Data.SqlClient;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlDatetimeSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlDatetimeSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlDatetimeSerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTime plaintext)
        {
            SqlDatetimeSerializer serializer = new SqlDatetimeSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.DateTime);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlDatetimeSerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTime plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.DateTime);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlDatetimeSerializer serializer = new SqlDatetimeSerializer();
            DateTime expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTime actualPlaintext = (DateTime)Database.SelectPlaintext(SqlDbType.DateTime);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlDatetimeSerializerInvalidData]
        public void ThrowWhenValueOutOfRange(DateTime plaintext)
        {
            SqlDatetimeSerializer serializer = new SqlDatetimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        public class SqlDatetimeSerializerInvalidDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { DateTime.MinValue };
                yield return new object[] { DateTime.Parse("1752-12-31 23:59:59.9999999") };
            }
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlDatetimeSerializer serializer = new SqlDatetimeSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            SqlDatetimeSerializer serializer = new SqlDatetimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlDatetimeSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
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
