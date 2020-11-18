using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class NullableDateTimeOffsetSerializerShould
    {
        [Theory]
        [NullableDatetimeOffsetSerializerTestData]
        public void SerializeAndDeserializeToTheSameObject(DateTimeOffset? originalValue)
        {
            Serializer<DateTimeOffset?> serializer = new NullableDateTimeOffsetSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            DateTimeOffset? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<DateTimeOffset?> serializer = new NullableDateTimeOffsetSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<DateTimeOffset?> serializer = new NullableDateTimeOffsetSerializer();
            DateTimeOffset? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })]
        public void ShouldThrowIfDeserializingLessThanSixteenBytes(byte[] data)
        {
            Serializer<DateTimeOffset?> serializer = new NullableDateTimeOffsetSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class NullableDatetimeOffsetSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { DateTimeOffset.MinValue };
                yield return new object[] { DateTimeOffset.Parse("01/01/0001 -11:59") };
                yield return new object[] { DateTimeOffset.Parse("01/01/0001 11:59") };
                yield return new object[] { DateTimeOffset.Parse("07/20/2020 -6:00") };
                yield return new object[] { DateTimeOffset.UnixEpoch };
                yield return new object[] { DateTimeOffset.Parse("12/31/9999 -11:59") };
                yield return new object[] { DateTimeOffset.Parse("12/31/9999 11:59") };
                yield return new object[] { DateTimeOffset.MaxValue };
            }
        }
    }
}
