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
    public sealed class SqlIntSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlIntSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlIntSerializerTestData]
        public void SerializeTheSameAsSqlServer(int plaintext)
        {
            SqlIntSerializer serializer = new SqlIntSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Int) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Int);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlIntSerializerTestData]
        public void DeserializeTheSameAsSqlServer(int plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Int) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Int);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlIntSerializer serializer = new SqlIntSerializer();
            int expectedPlaintext = serializer.Deserialize(plaintextBytes);
            int actualPlaintext = (int)Database.SelectPlaintext(SqlDbType.Int);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlIntSerializer serializer = new SqlIntSerializer();
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
            SqlIntSerializer serializer = new SqlIntSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlIntSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -123456789 };
                yield return new object[] { -1 };
                yield return new object[] { 0 };
                yield return new object[] { 1 };
                yield return new object[] { 123456789 };
                yield return new object[] { int.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
