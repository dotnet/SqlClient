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
    public class SqlNullableDateSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableDateSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<DateTime?> serializer = new SqlNullableDateSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<DateTime?> serializer = new SqlNullableDateSerializer();
            DateTime? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableDateSerializerTestData]
        public void SerializeTheSameAsSqlServer(DateTime? plaintext)
        {
            Serializer<DateTime?> serializer = new SqlNullableDateSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Date) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Date);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableDateSerializerTestData]
        public void DeserializeTheSameAsSqlServer(DateTime? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Date) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Date);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<DateTime?> serializer = new SqlNullableDateSerializer();
            DateTime? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            DateTime? actualPlaintext = (DateTime?)Database.SelectPlaintext(SqlDbType.Date);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        public void ShouldThrowIfDeserializingLessThanThreeBytes(byte[] data)
        {
            Serializer<DateTime?> serializer = new SqlNullableDateSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableDateSerializerTestDataAttribute : DataAttribute
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
