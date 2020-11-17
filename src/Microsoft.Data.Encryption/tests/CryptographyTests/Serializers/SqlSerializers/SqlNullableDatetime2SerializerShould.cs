using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Sdk;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public class SqlNullableDatetime2SerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableDatetime2SerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<DateTime?> serializer = new SqlNullableDatetime2Serializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<DateTime?> serializer = new SqlNullableDatetime2Serializer();
            DateTime? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableDatetime2SerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTime? plaintext, byte precision)
        {
            Serializer<DateTime?> serializer = new SqlNullableDatetime2Serializer(precision);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime2) { Value = plaintext, Precision = precision });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.DateTime2);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableDatetime2SerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTime? plaintext, byte precision)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.DateTime2) { Value = plaintext, Precision = precision });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.DateTime2);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<DateTime?> serializer = new SqlNullableDatetime2Serializer(precision);
            DateTime? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTime? actualPlaintext = (DateTime?)Database.SelectPlaintext(SqlDbType.DateTime2);

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
            Serializer<DateTime?> serializer = new SqlNullableDatetime2Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        [Theory]
        [SqlNullableDatetime2SerializerInvalidSizeData]
        public void ThrowWhenConstructorSizeOutOfRange(int precision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNullableDatetime2Serializer(precision));
        }

        [Theory]
        [SqlNullableDatetime2SerializerInvalidSizeData]
        public void ThrowWhenPropertySizeOutOfRange(int precision)
        {
            SqlNullableDatetime2Serializer serializer = new SqlNullableDatetime2Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Precision = precision);
        }

        public class SqlNullableDatetime2SerializerInvalidSizeDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -1 };
                yield return new object[] { 8 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlNullableDatetime2SerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 7 };
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
