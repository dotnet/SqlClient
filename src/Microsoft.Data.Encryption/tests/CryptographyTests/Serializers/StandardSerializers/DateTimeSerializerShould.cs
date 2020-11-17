using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class DateTimeSerializerShould
    {
        [Theory]
        [DatetimeSerializerTestData]
        public void SerializeAndDeserializeToTheSameObject(DateTime originalValue)
        {
            Serializer<DateTime> serializer = new DateTimeSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            DateTime deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<DateTime> serializer = new DateTimeSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<DateTime> serializer = new DateTimeSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class DatetimeSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { DateTime.MinValue };
                yield return new object[] { DateTime.Parse("1753-01-01 00:00:00.000") };
                yield return new object[] { DateTime.Parse("1900-01-01 00:00:00.000") };
                yield return new object[] { DateTime.Parse("9999-12-31 23:59:59.997") };
                yield return new object[] { DateTime.MaxValue };
                yield return new object[] { DateTime.Parse("2020-09-25T21:01:10.1522754-07:00") };
                yield return new object[] { DateTime.Now };
                yield return new object[] { DateTime.UtcNow };
            }
        }
    }
}
