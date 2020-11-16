using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableDateTimeSerializerShould
    {
        [Theory]
        [NullableDatetimeSerializerTestData]
        public void SerializeAndDeserializeToTheSameObject(DateTime? originalValue)
        {
            Serializer<DateTime?> serializer = new NullableDateTimeSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            DateTime? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<DateTime?> serializer = new NullableDateTimeSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<DateTime?> serializer = new NullableDateTimeSerializer();
            DateTime? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<DateTime?> serializer = new NullableDateTimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class NullableDatetimeSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { DateTime.MinValue };
                yield return new object[] { DateTime.Parse("1753-01-01 00:00:00.000") };
                yield return new object[] { DateTime.Parse("1900-01-01 00:00:00.000") };
                yield return new object[] { DateTime.Parse("9999-12-31 23:59:59.997") };
                yield return new object[] { DateTime.MaxValue };
            }
        }
    }
}
