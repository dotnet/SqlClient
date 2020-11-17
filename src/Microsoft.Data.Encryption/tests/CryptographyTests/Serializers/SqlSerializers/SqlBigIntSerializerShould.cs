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
    public sealed class SqlBigIntSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlBigIntSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory] 
        [SqlBigIntSerializerTestData]
        public void SerializeTheSameAsSqlServer(long plaintext)
        {
            SqlBigintSerializer serializer = new SqlBigintSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.BigInt) { Value = plaintext});
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.BigInt);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlBigIntSerializerTestData]
        public void DeserializeTheSameAsSqlServer(long plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.BigInt) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.BigInt);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlBigintSerializer serializer = new SqlBigintSerializer();
            long expectedPlaintext = serializer.Deserialize(plaintextBytes);
            long actualPlaintext = (long)Database.SelectPlaintext(SqlDbType.BigInt);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlBigintSerializer serializer = new SqlBigintSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7})]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            SqlBigintSerializer serializer = new SqlBigintSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlBigIntSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { long.MaxValue };
                yield return new object[] { 9876543210 };
                yield return new object[] { 1 };
                yield return new object[] { 0 };
                yield return new object[] { -1 };
                yield return new object[] { -9876543210 };
                yield return new object[] { long.MinValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
