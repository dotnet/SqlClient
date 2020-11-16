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
    public sealed class SqlDatetime2SerializerShould: IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlDatetime2SerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlDatetime2SerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTime plaintext, byte precision)
        {
            SqlDatetime2Serializer serializer = new SqlDatetime2Serializer(precision);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime2) { Value = plaintext, Precision = precision});
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.DateTime2);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlDatetime2SerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTime plaintext, byte precision)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime2) { Value = plaintext, Precision = precision });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.DateTime2);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlDatetime2Serializer serializer = new SqlDatetime2Serializer(precision);
            DateTime expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTime actualPlaintext = (DateTime)Database.SelectPlaintext(SqlDbType.DateTime2);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlDatetime2Serializer serializer = new SqlDatetime2Serializer();
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
            SqlDatetime2Serializer serializer = new SqlDatetime2Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        [Theory]
        [SqlDatetime2SerializerInvalidSizeData]
        public void ThrowWhenConstructorSizeOutOfRange(int precision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlDatetime2Serializer(precision));
        }

        [Theory]
        [SqlDatetime2SerializerInvalidSizeData]
        public void ThrowWhenPropertySizeOutOfRange(int precision)
        {
            SqlDatetime2Serializer serializer = new SqlDatetime2Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Precision = precision);
        }

        public class SqlDatetime2SerializerInvalidSizeDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -1 };
                yield return new object[] { 8 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlDatetime2SerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { DateTime.MinValue, 7 };
                yield return new object[] { DateTime.Parse("1900-01-01 00:00:00.0000000"), 7 };
                yield return new object[] { DateTime.MaxValue, 7 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
