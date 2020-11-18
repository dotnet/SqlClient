using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class Int64SerializerShould
    {
        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(-123456789)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(123456789)]
        [InlineData(long.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(long originalValue)
        {
            Serializer<long> serializer = new Int64Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            long deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<long> serializer = new Int64Serializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<long> serializer = new Int64Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
