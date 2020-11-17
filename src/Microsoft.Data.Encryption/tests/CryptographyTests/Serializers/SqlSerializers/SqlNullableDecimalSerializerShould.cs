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
    public class SqlNullableDecimalSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableDecimalSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<decimal?> serializer = new SqlNullableDecimalSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<decimal?> serializer = new SqlNullableDecimalSerializer();
            decimal? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableDecimalSerializerTestData]
        public void SerializeTheSameAsSqlServer(decimal? plaintext, byte precision, byte scale)
        {
            Serializer<decimal?> serializer = new SqlNullableDecimalSerializer(precision, scale);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Decimal) { Value = plaintext, Precision = precision, Scale = scale });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Decimal);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableDecimalSerializerTestData]
        public void DeserializeTheSameAsSqlServer(decimal? plaintext, byte precision, byte scale)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Decimal) { Value = plaintext, Precision = precision, Scale = scale });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Decimal);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<decimal?> serializer = new SqlNullableDecimalSerializer(precision, scale);
            decimal? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            decimal? actualPlaintext = (decimal?)Database.SelectPlaintext(SqlDbType.Decimal);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlNullableDecimalSerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(decimal? plaintext, byte precision, byte scale)
        {
            Serializer<decimal?> serializer = new SqlNullableDecimalSerializer(precision, scale);
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Theory]
        [SqlNullableDecimalSerializerInvalidPrecisionData]
        public void ThrowWhenConstructorPrecisionOutOfRange(int precision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNullableDecimalSerializer(precision));
        }

        [Theory]
        [SqlNullableDecimalSerializerInvalidPrecisionData]
        public void ThrowWhenPropertyPrecisionOutOfRange(int precision)
        {
            SqlNullableDecimalSerializer serializer = new SqlNullableDecimalSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Precision = precision);
        }

        [Theory]
        [SqlNullableDecimalSerializerInvalidScaleData]
        public void ThrowWhenConstructorScaleOutOfRange(int precision, int scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNullableDecimalSerializer(precision, scale));
        }

        [Theory]
        [SqlNullableDecimalSerializerInvalidScaleData]
        public void ThrowWhenPropertyScaleOutOfRange(int precision, int scale)
        {
            SqlNullableDecimalSerializer serializer = new SqlNullableDecimalSerializer();
            serializer.Precision = precision;
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Scale = scale);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 })]
        public void ShouldThrowIfDeserializingLessThanSeventeenBytes(byte[] data)
        {
            Serializer<decimal?> serializer = new SqlNullableDecimalSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableDecimalSerializerInvalidPrecisionDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -5 };
                yield return new object[] { 0 };
                yield return new object[] { 39 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlNullableDecimalSerializerInvalidScaleDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { 1, int.MinValue };
                yield return new object[] { 10, -5 };
                yield return new object[] { 11, -1 };
                yield return new object[] { 12, 13 };
                yield return new object[] { 1, 2 };
                yield return new object[] { 13, 14 };
                yield return new object[] { 38, int.MaxValue };
            }
        }

        public class SqlNullableDecimalSerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { decimal.MinValue, 18, 0 };
                yield return new object[] { -1000000000000000000M, 18, 0 };
                yield return new object[] { 1000000000000000000M, 18, 0 };
                yield return new object[] { decimal.MaxValue, 18, 0 };
            }
        }

        public class SqlNullableDecimalSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 18, 0 };
                yield return new object[] { -999999999999999999M, 18, 0 };
                yield return new object[] { 1M, 18, 0 };
                yield return new object[] { 0M, 18, 0 };
                yield return new object[] { -1M, 18, 0 };
                yield return new object[] { 999999999999999999M, 18, 0 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
