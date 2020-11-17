using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Data.SqlClient;
using System;
using System.Data;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.SqlSerializers
{
    [Collection("Database collection")]
    public sealed class SqlCharSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlCharSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            SqlCharSerializer serializer = new SqlCharSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            SqlCharSerializer serializer = new SqlCharSerializer();
            string actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlCharSerializerTestData]
        public void SerializeTheSameAsSqlServer(string plaintext, int size, int codepage)
        {
            SqlCharSerializer serializer = new SqlCharSerializer(size, codepage);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] actualCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Char, size) { Value = plaintext });
            byte[] expectedCiphertext = Database.SelectCiphertext(SqlDbType.Char);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlCharSerializerTestData]
        public void DeserializeTheSameAsSqlServer(string plaintext, int size, int codepage)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Char, size) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Char);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlCharSerializer serializer = new SqlCharSerializer(size, codepage);
            string expectedPlaintext = serializer.Deserialize(plaintextBytes);
            string actualPlaintext = (string)Database.SelectPlaintext(SqlDbType.Char);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlCharSerializerInvalidSizeData]
        public void ThrowWhenConstructorSizeOutOfRange(int size)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlCharSerializer(size));
        }

        [Theory]
        [SqlCharSerializerInvalidSizeData]
        public void ThrowWhenPropertySizeOutOfRange(int size)
        {
            SqlCharSerializer serializer = new SqlCharSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Size = size);
        }

        public class SqlCharSerializerInvalidSizeDataAttribute : DataAttribute
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

        public class SqlCharSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { "0000000000123456789", 10, 1252 };
                yield return new object[] { "0000000000", 10, 1252 };
                yield return new object[] { "abcdefghij", 10, 1252 };
                yield return new object[] { "ナンセンス 1杯1杯", 10, 1252 };
                yield return new object[] { "a         ", 10, 1252 };
                yield return new object[] { "a", 10, 1252 };
                yield return new object[] { "          ", 10, 1252 };
                yield return new object[] { "", 10, 1252 };
                yield return new object[] { "0000000000123456789", 10, 65001 };
                yield return new object[] { "0000000000", 10, 65001 };
                // TODO - Must change database level collation in addition to column collation for this to pass.
                // yield return new object[] { "ナンセンス 1杯1杯", 10, 65001 };
                yield return new object[] { "abcdefghij", 10, 65001 };
                yield return new object[] { "a         ", 10, 65001 };
                yield return new object[] { "a", 10, 65001 };
                yield return new object[] { "          ", 10, 65001 };
                yield return new object[] { "", 10, 65001 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
