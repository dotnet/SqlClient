using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class UInt64SerializerShould
    {
        [Theory]
        [InlineData(ulong.MinValue)]
        [InlineData(1U)]
        [InlineData(123456789U)]
        [InlineData(ulong.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(ulong originalValue)
        {
            Serializer<ulong> serializer = new UInt64Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            ulong deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<ulong> serializer = new UInt64Serializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ShouldThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<ulong> serializer = new UInt64Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
