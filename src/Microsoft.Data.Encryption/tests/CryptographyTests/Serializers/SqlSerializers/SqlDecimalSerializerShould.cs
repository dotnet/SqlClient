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
    public sealed class SqlDecimalSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlDecimalSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlDecimalSerializerTestData]
        public void SerializeTheSameAsSqlServer(decimal plaintext, byte precision, byte scale)
        {
            SqlDecimalSerializer serializer = new SqlDecimalSerializer(precision, scale);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Decimal) { Value = plaintext, Precision = precision, Scale = scale });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Decimal);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlDecimalSerializerTestData]
        public void DeserializeTheSameAsSqlServer(decimal plaintext, byte precision, byte scale)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Decimal) { Value = plaintext, Precision = precision, Scale = scale });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Decimal);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlDecimalSerializer serializer = new SqlDecimalSerializer(precision, scale);
            decimal expectedPlaintext = serializer.Deserialize(plaintextBytes);
            decimal actualPlaintext = (decimal)Database.SelectPlaintext(SqlDbType.Decimal);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlDecimalSerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(decimal plaintext, byte precision, byte scale)
        {
            SqlDecimalSerializer serializer = new SqlDecimalSerializer(precision, scale);
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Theory]
        [SqlDecimalSerializerInvalidPrecisionData]
        public void ThrowWhenConstructorPrecisionOutOfRange(int precision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlDecimalSerializer(precision));
        }

        [Theory]
        [SqlDecimalSerializerInvalidPrecisionData]
        public void ThrowWhenPropertyPrecisionOutOfRange(int precision)
        {
            SqlDecimalSerializer serializer = new SqlDecimalSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Precision = precision);
        }

        [Theory]
        [SqlDecimalSerializerInvalidScaleData]
        public void ThrowWhenConstructorScaleOutOfRange(int precision, int scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlDecimalSerializer(precision, scale));
        }

        [Theory]
        [SqlDecimalSerializerInvalidScaleData]
        public void ThrowWhenPropertyScaleOutOfRange(int precision, int scale)
        {
            SqlDecimalSerializer serializer = new SqlDecimalSerializer
            {
                Precision = precision
            };
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Scale = scale);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlDecimalSerializer serializer = new SqlDecimalSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
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
            SqlDecimalSerializer serializer = new SqlDecimalSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlDecimalSerializerInvalidPrecisionDataAttribute : DataAttribute
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

        public class SqlDecimalSerializerInvalidScaleDataAttribute : DataAttribute
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

        public class SqlDecimalSerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { decimal.MinValue, 18, 0 };
                yield return new object[] { -1000000000000000000M, 18, 0 };
                yield return new object[] { 1000000000000000000M, 18, 0 };
                yield return new object[] { decimal.MaxValue, 18, 0 };
            }
        }

        public class SqlDecimalSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
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
