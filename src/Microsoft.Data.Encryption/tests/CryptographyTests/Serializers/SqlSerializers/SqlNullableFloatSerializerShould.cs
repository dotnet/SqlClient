using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public class SqlNullableFloatSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableFloatSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<double?> serializer = new SqlNullableFloatSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<double?> serializer = new SqlNullableFloatSerializer();
            double? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableFloatSerializerTestData]
        public void SerializeTheSameAsSqlServer(double? plaintext)
        {
            Serializer<double?> serializer = new SqlNullableFloatSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Float) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Float);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableFloatSerializerTestData]
        public void DeserializeTheSameAsSqlServer(double? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Float) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Float);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<double?> serializer = new SqlNullableFloatSerializer();
            double? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            double? actualPlaintext = (double?)Database.SelectPlaintext(SqlDbType.Float);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.PositiveInfinity)]
        public void ThrowWhenValueOutOfRange(double? plaintext)
        {
            Serializer<double?> serializer = new SqlNullableFloatSerializer();
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
            Serializer<double?> serializer = new SqlNullableFloatSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableFloatSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { double.MinValue };
                yield return new object[] { -1.79E+308 };
                yield return new object[] { 1.79E+308 };
                yield return new object[] { 0 };
                yield return new object[] { -2.23E-308 };
                yield return new object[] { 2.23E-308 };
                yield return new object[] { double.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
