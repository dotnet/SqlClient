using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlSmalldatetimeSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlSmalldatetimeSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlSmalldatetimeSerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTime plaintext)
        {
            SqlSmalldatetimeSerializer serializer = new SqlSmalldatetimeSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallDateTime) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.SmallDateTime);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlSmalldatetimeSerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTime plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallDateTime) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.SmallDateTime);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlSmalldatetimeSerializer serializer = new SqlSmalldatetimeSerializer();
            DateTime expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTime actualPlaintext = (DateTime)Database.SelectPlaintext(SqlDbType.SmallDateTime);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlSmalldatetimeSerializerInvalidData]
        public void ThrowWhenValueOutOfRange(DateTime plaintext)
        {
            SqlSmalldatetimeSerializer serializer = new SqlSmalldatetimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlSmalldatetimeSerializer serializer = new SqlSmalldatetimeSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ShouldThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            SqlSmalldatetimeSerializer serializer = new SqlSmalldatetimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlSmalldatetimeSerializerInvalidDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { DateTime.MinValue };
                yield return new object[] { DateTime.Parse("1899-12-31") };
                yield return new object[] { DateTime.Parse("2079-06-07") };
                yield return new object[] { DateTime.MaxValue };
            }
        }

        public class SqlSmalldatetimeSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { DateTime.Parse("1900-01-01 00:00:00") };
                yield return new object[] { DateTime.Parse("2079-06-06 00:00:00") };
                yield return new object[] { DateTime.Parse("2020-07-10 00:00:00") };
                yield return new object[] { DateTime.Parse("1900-01-01") };
                yield return new object[] { DateTime.Parse("2079-06-06") };
                yield return new object[] { DateTime.Parse("2020-07-10") };
                yield return new object[] { DateTime.Parse("1900-01-01 23:59:59") };
                yield return new object[] { DateTime.Parse("2079-06-06 23:59:29") };
                yield return new object[] { DateTime.Parse("2020-07-10 23:59:59") };
                yield return new object[] { DateTime.Parse("1900-01-01 12:12:12") };
                yield return new object[] { DateTime.Parse("2079-06-06 12:12:12") };
                yield return new object[] { DateTime.Parse("2020-07-10 12:12:12") };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
