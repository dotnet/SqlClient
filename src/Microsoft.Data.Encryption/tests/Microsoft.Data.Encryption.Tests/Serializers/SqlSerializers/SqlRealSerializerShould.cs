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
    public sealed class SqlRealSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlRealSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlRealSerializerTestData]
        public void SerializeTheSameAsSqlServer(float plaintext)
        {
            SqlRealSerializer serializer = new SqlRealSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Real) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Real);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlRealSerializerTestData]
        public void DeserializeTheSameAsSqlServer(float plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Real) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Real);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlRealSerializer serializer = new SqlRealSerializer();
            float expectedPlaintext = serializer.Deserialize(plaintextBytes);
            float actualPlaintext = (float)Database.SelectPlaintext(SqlDbType.Real);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(float.PositiveInfinity)]
        public void ThrowWhenValueOutOfRange(float plaintext)
        {
            SqlRealSerializer serializer = new SqlRealSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlRealSerializer serializer = new SqlRealSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ShouldThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            SqlRealSerializer serializer = new SqlRealSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlRealSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { float.MinValue };
                yield return new object[] { -3.40E+38 };
                yield return new object[] { -1.18E-38 };
                yield return new object[] { -1234.567 };
                yield return new object[] { -1 };
                yield return new object[] { 0 };
                yield return new object[] { 1 };
                yield return new object[] { 1234.567 };
                yield return new object[] { 1.18E-38 };
                yield return new object[] { 3.40E+38 };
                yield return new object[] { float.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }

    }
}
