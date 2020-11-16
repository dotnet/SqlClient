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
    public class SqlNullableTinyIntSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableTinyIntSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<byte?> serializer = new SqlNullableTinyintSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<byte?> serializer = new SqlNullableTinyintSerializer();
            byte? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableTinyintSerializerTestData]
        public void SerializeTheSameAsSqlServer(byte? plaintext)
        {
            Serializer<byte?> serializer = new SqlNullableTinyintSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.TinyInt) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.TinyInt);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableTinyintSerializerTestData]
        public void DeserializeTheSameAsSqlServer(byte? plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.TinyInt) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.TinyInt);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<byte?> serializer = new SqlNullableTinyintSerializer();
            byte? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            byte? actualPlaintext = (byte?)Database.SelectPlaintext(SqlDbType.TinyInt);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<byte?> serializer = new SqlNullableTinyintSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlNullableTinyintSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { byte.MinValue };
                yield return new object[] { (byte)1 };
                yield return new object[] { (byte)2 };
                yield return new object[] { (byte)8 };
                yield return new object[] { (byte)16 };
                yield return new object[] { (byte)32 };
                yield return new object[] { (byte)64 };
                yield return new object[] { (byte)128 };
                yield return new object[] { byte.MaxValue };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
