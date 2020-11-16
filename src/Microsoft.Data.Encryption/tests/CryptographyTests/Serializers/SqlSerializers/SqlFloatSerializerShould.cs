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
    public sealed class SqlFloatSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlFloatSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlFloatSerializerTestData]
        public void SerializeTheSameAsSqlServer(double plaintext)
        {
            SqlFloatSerializer serializer = new SqlFloatSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Float) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Float);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlFloatSerializerTestData]
        public void DeserializeTheSameAsSqlServer(double plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Float) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Float);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlFloatSerializer serializer = new SqlFloatSerializer();
            double expectedPlaintext = serializer.Deserialize(plaintextBytes);
            double actualPlaintext = (double)Database.SelectPlaintext(SqlDbType.Float);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.PositiveInfinity)]
        public void ThrowWhenValueOutOfRange(double plaintext)
        {
            SqlFloatSerializer serializer = new SqlFloatSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlFloatSerializer serializer = new SqlFloatSerializer();
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
            SqlFloatSerializer serializer = new SqlFloatSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlFloatSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
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
