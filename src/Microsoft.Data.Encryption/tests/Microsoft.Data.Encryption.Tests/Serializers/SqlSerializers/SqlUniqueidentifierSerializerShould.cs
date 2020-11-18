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
    public sealed class SqlUniqueidentifierSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlUniqueidentifierSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlUniqueidentifierSerializerTestData]
        public void SerializeTheSameAsSqlServer(Guid plaintext)
        {
            SqlUniqueidentifierSerializer serializer = new SqlUniqueidentifierSerializer();
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.UniqueIdentifier) { Value = plaintext });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.UniqueIdentifier);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlUniqueidentifierSerializerTestData]
        public void DeserializeTheSameAsSqlServer(Guid plaintext)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.UniqueIdentifier) { Value = plaintext });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.UniqueIdentifier);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlUniqueidentifierSerializer serializer = new SqlUniqueidentifierSerializer();
            Guid expectedPlaintext = serializer.Deserialize(plaintextBytes);
            Guid actualPlaintext = (Guid)Database.SelectPlaintext(SqlDbType.UniqueIdentifier);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlUniqueidentifierSerializer serializer = new SqlUniqueidentifierSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })]
        public void ShouldThrowIfDeserializingLessThanSixteenBytes(byte[] data)
        {
            SqlUniqueidentifierSerializer serializer = new SqlUniqueidentifierSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class SqlUniqueidentifierSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { Guid.Empty };
                yield return new object[] { Guid.Parse("0E984725-C51C-4BF4-9960-E1C80E27ABA0") };
                yield return new object[] { Guid.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF") };
                yield return new object[] { Guid.Parse("11111111-1111-1111-1111-111111111111") };
                yield return new object[] { Guid.Parse("A9A9A9A9-A9A9-A9A9-A9A9-A9A9A9A9A9A9") };
                yield return new object[] { Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF") };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
