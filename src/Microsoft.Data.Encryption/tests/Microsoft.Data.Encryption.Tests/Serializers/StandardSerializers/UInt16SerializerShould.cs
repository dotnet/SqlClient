using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class UInt16SerializerShould
    {
        [Theory]
        [InlineData(ushort.MinValue)]
        [InlineData(1)]
        [InlineData(123)]
        [InlineData(12345)]
        [InlineData(ushort.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(ushort originalValue)
        {
            Serializer<ushort> serializer = new UInt16Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            ushort deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<ushort> serializer = new UInt16Serializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        public void ShouldThrowIfDeserializingLessThanTwoBytes(byte[] data)
        {
            Serializer<ushort> serializer = new UInt16Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
