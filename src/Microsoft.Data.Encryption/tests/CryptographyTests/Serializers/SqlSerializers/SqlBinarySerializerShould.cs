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
    public sealed class SqlBinarySerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlBinarySerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<byte[]> serializer = new SqlBinarySerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<byte[]> serializer = new SqlBinarySerializer();
            byte[] actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlBinarySerializerTestData]
        public void SerializeTheSameAsSqlServer(byte[] plaintext, int length)
        {
            Serializer<byte[]> serializer = new SqlBinarySerializer(length);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Binary, length) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Binary);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlBinarySerializerTestData]
        public void DeserializeTheSameAsSqlServer(byte[] plaintext, int length)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Binary, length) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Binary);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<byte[]> serializer = new SqlBinarySerializer(length);
            byte[] expectedPlaintext = serializer.Deserialize(plaintextBytes);
            byte[] actualPlaintext = (byte[])Database.SelectPlaintext(SqlDbType.Binary);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlBinarySerializerInvalidSizeData]
        public void ThrowWhenConstructorSizeOutOfRange(int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlBinarySerializer(size));
        }

        [Theory]
        [SqlBinarySerializerInvalidSizeData]
        public void ThrowWhenPropertySizeOutOfRange(int size)
        {
            SqlBinarySerializer serializer = new SqlBinarySerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Size = size);
        }

        public class SqlBinarySerializerInvalidSizeDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -1 };
                yield return new object[] { 0 };
                yield return new object[] { 8001 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlBinarySerializerTestDataAttribute : DataAttribute
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
