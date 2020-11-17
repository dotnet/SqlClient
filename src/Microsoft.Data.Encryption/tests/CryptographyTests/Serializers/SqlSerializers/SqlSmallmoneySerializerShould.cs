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
    public sealed class SqlSmallmoneySerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlSmallmoneySerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlSmallmoneySerializerTestData]
        public void SerializeTheSameAsSqlServer(decimal plaintext)
        {
            SqlSmallmoneySerializer serializer = new SqlSmallmoneySerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallMoney) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.SmallMoney);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlSmallmoneySerializerTestData]
        public void DeserializeTheSameAsSqlServer(decimal plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.SmallMoney) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.SmallMoney);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlSmallmoneySerializer serializer = new SqlSmallmoneySerializer();
            decimal expectedPlaintext = serializer.Deserialize(plaintextBytes);
            decimal actualPlaintext = (decimal)Database.SelectPlaintext(SqlDbType.SmallMoney);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlSmallmoneySerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(decimal plaintext)
        {
            SqlSmallmoneySerializer serializer = new SqlSmallmoneySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlSmallmoneySerializer serializer = new SqlSmallmoneySerializer();
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
            SqlSmallmoneySerializer serializer = new SqlSmallmoneySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlSmallmoneySerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { decimal.MinValue };
                yield return new object[] { -214748.3649M };
                yield return new object[] { 214748.3648M };
                yield return new object[] { decimal.MaxValue };
            }
        }

        public class SqlSmallmoneySerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { -214748.3648M };
                yield return new object[] { -1.00M };
                yield return new object[] { 0.00M };
                yield return new object[] { 1.00M };
                yield return new object[] { 214748.3647M };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
