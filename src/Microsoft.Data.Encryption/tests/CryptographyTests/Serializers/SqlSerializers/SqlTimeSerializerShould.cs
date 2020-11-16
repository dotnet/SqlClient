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
    public sealed class SqlTimeSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlTimeSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Theory]
        [SqlTimeSerializerTestData]
        public void SerializeTheSameAsSqlServer(TimeSpan plaintext, byte scale)
        {
            SqlTimeSerializer serializer = new SqlTimeSerializer(scale);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Time) { Value = plaintext, Scale = scale });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Time);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlTimeSerializerTestData]
        public void DeserializeTheSameAsSqlServer(TimeSpan plaintext, byte scale)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Time) { Value = plaintext, Scale = scale });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Time);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            SqlTimeSerializer serializer = new SqlTimeSerializer(scale);
            TimeSpan expectedPlaintext = serializer.Deserialize(plaintextBytes);
            TimeSpan actualPlaintext = (TimeSpan)Database.SelectPlaintext(SqlDbType.Time);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlTimeSerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(TimeSpan plaintext, byte scale)
        {
            SqlTimeSerializer serializer = new SqlTimeSerializer(scale);
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            SqlTimeSerializer serializer = new SqlTimeSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        public void ShouldThrowIfDeserializingLessThanFiveBytes(byte[] data)
        {
            SqlTimeSerializer serializer = new SqlTimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        [Theory]
        [SqlTimeSerializerInvalidScaleData]
        public void ThrowWhenConstructorScaleOutOfRange(int scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlTimeSerializer(scale));
        }

        [Theory]
        [SqlTimeSerializerInvalidScaleData]
        public void ThrowWhenPropertyScaleOutOfRange(int scale)
        {
            SqlTimeSerializer serializer = new SqlTimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Scale = scale);
        }

        public class SqlTimeSerializerInvalidScaleDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -1 };
                yield return new object[] { 8 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlTimeSerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { TimeSpan.MinValue, 7 };
                yield return new object[] { TimeSpan.Parse("-1:23:59:59.9999999"), 7 };
                yield return new object[] { TimeSpan.Parse("1:00:00:00.0000000"), 7 };
                yield return new object[] { TimeSpan.MaxValue, 7 };
            }
        }

        public class SqlTimeSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { TimeSpan.Parse("00:00:00.0000000"), 7 };
                yield return new object[] { TimeSpan.Parse("12:12:12.1212121"), 7 };
                yield return new object[] { TimeSpan.Parse("23:59:59.9999999"), 7 };

                yield return new object[] { TimeSpan.Parse("00:00:00.0000000"), 5 };
                yield return new object[] { TimeSpan.Parse("12:12:12.1212121"), 4 };
                yield return new object[] { TimeSpan.Parse("23:59:59.9999999"), 3 };
            }
        }

        public void Dispose()
        {
            Database.DeleteAllDataFromTable();
        }
    }
}
