using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using System;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using System.Data;
using Microsoft.Data.SqlClient;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlDateSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlDateSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlDateSerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTime plaintext)
        {
            SqlDateSerializer serializer = new SqlDateSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Date) { Value = plaintext});
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Date);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlDateSerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTime plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Date) { Value = plaintext});
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Date);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlDateSerializer serializer = new SqlDateSerializer();
            DateTime expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTime actualPlaintext = (DateTime)Database.SelectPlaintext(SqlDbType.Date);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlDateSerializer serializer = new SqlDateSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        public void ShouldThrowIfDeserializingLessThanTwoBytes(byte[] data)
        {
            SqlDateSerializer serializer = new SqlDateSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlDateSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { DateTime.MinValue };
                yield return new object[] { new DateTime(0001, 01, 01) };
                yield return new object[] { new DateTime(1900, 01, 01) };
                yield return new object[] { new DateTime(9999, 12, 31) };
                yield return new object[] { DateTime.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
