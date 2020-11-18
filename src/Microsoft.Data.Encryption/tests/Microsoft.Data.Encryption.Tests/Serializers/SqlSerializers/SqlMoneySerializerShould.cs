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
    public sealed class SqlMoneySerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlMoneySerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlMoneySerializerTestData]
        public void SerializeTheSameAsSqlServer(decimal plaintext)
        {
            SqlMoneySerializer serializer = new SqlMoneySerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Money) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Money);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlMoneySerializerTestData]
        public void DeserializeTheSameAsSqlServer(decimal plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Money) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Money);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlMoneySerializer serializer = new SqlMoneySerializer();
            decimal expectedPlaintext = serializer.Deserialize(plaintextBytes);
            decimal actualPlaintext = (decimal)Database.SelectPlaintext(SqlDbType.Money);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlMoneySerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(decimal plaintext)
        {
            SqlMoneySerializer serializer = new SqlMoneySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlMoneySerializer serializer = new SqlMoneySerializer();
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
            SqlMoneySerializer serializer = new SqlMoneySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlMoneySerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { decimal.MinValue };
                yield return new object[] { -922337203685477.5809M };
                yield return new object[] { 922337203685477.5808M };
                yield return new object[] { decimal.MaxValue };
            }
        }

        public class SqlMoneySerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { -922337203685477.5808M };
                yield return new object[] { -1.00M };
                yield return new object[] { 0.00M };
                yield return new object[] { 1.00M };
                yield return new object[] { 922337203685477.5807M };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
