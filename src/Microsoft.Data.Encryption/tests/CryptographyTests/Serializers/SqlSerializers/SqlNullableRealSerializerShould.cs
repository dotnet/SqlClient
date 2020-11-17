using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

using static Microsoft.Data.Encryption.TestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public class SqlNullableRealSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableRealSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<float?> serializer = new SqlNullableRealSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<float?> serializer = new SqlNullableRealSerializer();
            float? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableRealSerializerTestData]
        public void SerializeTheSameAsSqlServer(float? plaintext)
        {
            Serializer<float?> serializer = new SqlNullableRealSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Real) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Real);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableRealSerializerTestData]
        public void DeserializeTheSameAsSqlServer(float? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Real) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Real);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<float?> serializer = new SqlNullableRealSerializer();
            float? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            float? actualPlaintext = (float?)Database.SelectPlaintext(SqlDbType.Real);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(float.PositiveInfinity)]
        public void ThrowWhenValueOutOfRange(float? plaintext)
        {
            Serializer<float?> serializer = new SqlNullableRealSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ShouldThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            Serializer<float?> serializer = new SqlNullableRealSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableRealSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { float.MinValue };
                yield return new object[] { -3.40E+38F };
                yield return new object[] { -1.18E-38F };
                yield return new object[] { -1234.567F };
                yield return new object[] { -1F };
                yield return new object[] { 0F };
                yield return new object[] { 1F };
                yield return new object[] { 1234.567F };
                yield return new object[] { 1.18E-38F };
                yield return new object[] { 3.40E+38F };
                yield return new object[] { float.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
