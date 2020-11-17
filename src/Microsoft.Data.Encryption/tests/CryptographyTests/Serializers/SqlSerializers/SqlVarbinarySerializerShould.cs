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
    public sealed class SqlVarbinarySerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlVarbinarySerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<byte[]> serializer = new SqlVarbinarySerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<byte[]> serializer = new SqlVarbinarySerializer();
            byte[] actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlVarbinarySerializerTestData]
        public void SerializeTheSameAsSqlServer(byte[] plaintext, int size)
        {
            SqlVarbinarySerializer serializer = new SqlVarbinarySerializer(size);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.VarBinary, size) { Value = plaintext});
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.VarBinary);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlVarbinarySerializerTestData]
        public void DeserializeTheSameAsSqlServer(byte[] plaintext, int size)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.VarBinary, size) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.VarBinary);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlVarbinarySerializer serializer = new SqlVarbinarySerializer(size);
            byte[] expectedPlaintext = serializer.Deserialize(plaintextBytes);
            byte[] actualPlaintext = (byte[])Database.SelectPlaintext(SqlDbType.VarBinary);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-2)]
        [InlineData(0)]
        [InlineData(8001)]
        [InlineData(int.MaxValue)]
        public void ThrowWhenSizeOutOfRange(int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlVarbinarySerializer(size));
        }

        public class SqlVarbinarySerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 10 };
                yield return new object[] { new byte[] { 0 }, 10 };
                yield return new object[] { new byte[] { 255 }, 10 };
                yield return new object[] { new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 10 };
                yield return new object[] { new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10 };
                yield return new object[] { new byte[] { 255, 255, 255, 255, 255, 0, 0, 0, 0, 0 }, 10 };
                yield return new object[] { new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }, 10 };
                yield return new object[] { new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 10 };
                yield return new object[] { new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }, 10 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
