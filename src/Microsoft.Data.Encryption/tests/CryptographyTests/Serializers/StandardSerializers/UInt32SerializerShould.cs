using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class UInt32SerializerShould
    {
        [Theory]
        [InlineData(uint.MinValue)]
        [InlineData(1U)]
        [InlineData(123456789U)]
        [InlineData(uint.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(uint originalValue)
        {
            Serializer<uint> serializer = new UInt32Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            uint deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<uint> serializer = new UInt32Serializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ShouldThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            Serializer<uint> serializer = new UInt32Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
