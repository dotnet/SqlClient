using Microsoft.Data.Encryption.Cryptography.Serializers;
using static Microsoft.Data.Encryption.TestUtilities.DataTestUtility;
using Xunit;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using System;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlBitSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlBitSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }
    
        [Theory]
        [SqlBitSerializerTestData]
        public void SerializeTheSameAsSqlServer(bool plaintext)
        {
            SqlBitSerializer serializer = new SqlBitSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Bit) { Value = plaintext});
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Bit);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlBitSerializerTestData]
        public void DeserializeTheSameAsSqlServer(bool plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Bit) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Bit);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlBitSerializer serializer = new SqlBitSerializer();
            bool expectedPlaintext = serializer.Deserialize(plaintextBytes);
            bool actualPlaintext = (bool)Database.SelectPlaintext(SqlDbType.Bit);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlBitSerializer serializer = new SqlBitSerializer();
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
            SqlBitSerializer serializer = new SqlBitSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }


        public class SqlBitSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { false };
                yield return new object[] { true };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
