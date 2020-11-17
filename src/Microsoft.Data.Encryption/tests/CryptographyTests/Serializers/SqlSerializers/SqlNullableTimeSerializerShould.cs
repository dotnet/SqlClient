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
    public class SqlNullableTimeSerializerShould : IDisposable
    {
        readonly SqlDatabaseFixture Database;

        public SqlNullableTimeSerializerShould(SqlDatabaseFixture fixture)
        {
            Database = fixture;
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<TimeSpan?> serializer = new SqlNullableTimeSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<TimeSpan?> serializer = new SqlNullableTimeSerializer();
            TimeSpan? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [SqlNullableTimeSerializerTestData]
        public void SerializeTheSameAsSqlServer(TimeSpan? plaintext, byte scale)
        {
            Serializer<TimeSpan?> serializer = new SqlNullableTimeSerializer(scale);
            byte[] serializedPlaintext = serializer.Serialize(plaintext);
            byte[] expectedCiphertext = deterministicEncryptionAlgorithm.Encrypt(serializedPlaintext);

            Database.Insert(new SqlParameter("@parameter", SqlDbType.Time) { Value = plaintext, Scale = scale });
            byte[] actualCiphertext = Database.SelectCiphertext(SqlDbType.Time);

            Assert.Equal(expectedCiphertext, actualCiphertext);
        }

        [Theory]
        [SqlNullableTimeSerializerTestData]
        public void DeserializeTheSameAsSqlServer(TimeSpan? plaintext, byte scale)
        {
            Database.Insert(new SqlParameter("@parameter", SqlDbType.Time) { Value = plaintext, Scale = scale });
            byte[] ciphertextBytes = Database.SelectCiphertext(SqlDbType.Time);
            byte[] plaintextBytes = deterministicEncryptionAlgorithm.Decrypt(ciphertextBytes);
            Serializer<TimeSpan?> serializer = new SqlNullableTimeSerializer();
            TimeSpan? expectedPlaintext = serializer.Deserialize(plaintextBytes);
            TimeSpan? actualPlaintext = (TimeSpan?)Database.SelectPlaintext(SqlDbType.Time);

            Assert.Equal(expectedPlaintext, actualPlaintext);
        }

        [Theory]
        [SqlNullableTimeSerializerInvalidTestData]
        public void ThrowWhenValueOutOfRange(TimeSpan? plaintext, byte scale)
        {
            Serializer<TimeSpan?> serializer = new SqlNullableTimeSerializer(scale);
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Serialize(plaintext));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4 })]
        public void ShouldThrowIfDeserializingLessThanFiveBytes(byte[] data)
        {
            Serializer<TimeSpan?> serializer = new SqlNullableTimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        [Theory]
        [SqlNullableTimeSerializerInvalidScaleData]
        public void ThrowWhenConstructorScaleOutOfRange(int scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlNullableTimeSerializer(scale));
        }

        [Theory]
        [SqlNullableTimeSerializerInvalidScaleData]
        public void ThrowWhenPropertyScaleOutOfRange(int scale)
        {
            SqlNullableTimeSerializer serializer = new SqlNullableTimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Scale = scale);
        }

        public class SqlNullableTimeSerializerInvalidScaleDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { int.MinValue };
                yield return new object[] { -1 };
                yield return new object[] { 8 };
                yield return new object[] { int.MaxValue };
            }
        }

        public class SqlNullableTimeSerializerInvalidTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { TimeSpan.MinValue, 7 };
                yield return new object[] { TimeSpan.Parse("-1:23:59:59.9999999"), 7 };
                yield return new object[] { TimeSpan.Parse("1:00:00:00.0000000"), 7 };
                yield return new object[] { TimeSpan.MaxValue, 7 };
            }
        }

        public class SqlNullableTimeSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null, 7 };
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
