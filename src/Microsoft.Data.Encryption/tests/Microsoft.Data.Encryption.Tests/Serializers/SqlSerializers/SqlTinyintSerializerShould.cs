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
    public sealed class SqlTinyintSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlTinyintSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlTinyintSerializerTestData]
        public void SerializeTheSameAsSqlServer(byte plaintext)
        {
            SqlTinyintSerializer serializer = new SqlTinyintSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.TinyInt) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.TinyInt);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlTinyintSerializerTestData]
        public void DeserializeTheSameAsSqlServer(byte plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.TinyInt) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.TinyInt);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlTinyintSerializer serializer = new SqlTinyintSerializer();
            byte expectedPlaintext = serializer.Deserialize(plaintextBytes);
            byte actualPlaintext = (byte)Database.SelectPlaintext(SqlDbType.TinyInt);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlTinyintSerializer serializer = new SqlTinyintSerializer();
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
            SqlTinyintSerializer serializer = new SqlTinyintSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlTinyintSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { byte.MinValue };
                yield return new object[] { 1 };
                yield return new object[] { 2 };
                yield return new object[] { 8 };
                yield return new object[] { 16 };
                yield return new object[] { 32 };
                yield return new object[] { 64 };
                yield return new object[] { 128 };
                yield return new object[] { byte.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
