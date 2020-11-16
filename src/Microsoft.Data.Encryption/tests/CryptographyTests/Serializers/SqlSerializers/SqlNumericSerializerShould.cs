using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlNumericSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNumericSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlNumericSerializerTestData]
        public void SerializeTheSameAsSqlServer(decimal plaintext, byte precision, byte scale)
        {
            SqlNumericSerializer serializer = new SqlNumericSerializer(precision, scale);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Decimal) { Value = plaintext, Precision = precision, Scale = scale });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Decimal);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNumericSerializerTestData]
        public void DeserializeTheSameAsSqlServer(decimal plaintext, byte precision, byte scale)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Decimal) { Value = plaintext, Precision = precision, Scale = scale });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Decimal);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlNumericSerializer serializer = new SqlNumericSerializer(precision, scale);
            decimal expectedPlaintext = serializer.Deserialize(plaintextBytes);
            decimal actualPlaintext = (decimal)Database.SelectPlaintext(SqlDbType.Decimal);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlNumericSerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(decimal plaintext, byte precision, byte scale)
        {
            SqlNumericSerializer serializer = new SqlNumericSerializer(precision, scale);
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Theory]
        [SqlNumericSerializerInvalidPrecisionData]
        public void ThrowWhenConstructorPrecisionOutOfRange(int precision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNumericSerializer(precision));
        }

        [Theory]
        [SqlNumericSerializerInvalidPrecisionData]
        public void ThrowWhenPropertyPrecisionOutOfRange(int precision)
        {
            SqlNumericSerializer serializer = new SqlNumericSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Precision = precision);
        }

        [Theory]
        [SqlNumericSerializerInvalidScaleData]
        public void ThrowWhenConstructorScaleOutOfRange(int precision, int scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNumericSerializer(precision, scale));
        }

        [Theory]
        [SqlNumericSerializerInvalidScaleData]
        public void ThrowWhenPropertyScaleOutOfRange(int precision, int scale)
        {
            SqlNumericSerializer serializer = new SqlNumericSerializer();
            serializer.Precision = precision;
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Scale = scale);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlNumericSerializer serializer = new SqlNumericSerializer();
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
            SqlNumericSerializer serializer = new SqlNumericSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNumericSerializerInvalidPrecisionDataAttribute : DataAttribute
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

        public class SqlNumericSerializerInvalidScaleDataAttribute : DataAttribute
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

        public class SqlNumericSerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { decimal.MinValue, 18, 0 };
                yield return new object[] { -1000000000000000000, 18, 0 };
                yield return new object[] { 1000000000000000000, 18, 0 };
                yield return new object[] { decimal.MaxValue, 18, 0 };
            }
        }

        public class SqlNumericSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { -999999999999999999, 18, 0 };
                yield return new object[] { 999999999999999999, 18, 0 };
                yield return new object[] { 1, 18, 0 };
                yield return new object[] { 0, 18, 0 };
                yield return new object[] { -1, 18, 0 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
