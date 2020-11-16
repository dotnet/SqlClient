using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class TimeSpanSerializerShould
    {
        [Theory]
        [TimeSpanSerializerTestData]
        public void SerializeAndDeserializeToTheSameObject(TimeSpan originalValue)
        {
            Serializer<TimeSpan> serializer = new TimeSpanSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            TimeSpan deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<TimeSpan> serializer = new TimeSpanSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<TimeSpan> serializer = new TimeSpanSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class TimeSpanSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { TimeSpan.MinValue };
                yield return new object[] { new TimeSpan(-1000000000000) };
                yield return new object[] { new TimeSpan(-1) };
                yield return new object[] { new TimeSpan(0) };
                yield return new object[] { new TimeSpan(1) };
                yield return new object[] { new TimeSpan(1000000000000) };
                yield return new object[] { TimeSpan.MaxValue };
                yield return new object[] { TimeSpan.Zero };
            }
        }
    }
}
