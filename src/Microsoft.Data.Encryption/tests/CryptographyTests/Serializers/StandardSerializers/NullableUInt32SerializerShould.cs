using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableUInt32SerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(uint.MinValue)]
        [InlineData(1U)]
        [InlineData(123456789U)]
        [InlineData(uint.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(uint? originalValue)
        {
            Serializer<uint?> serializer = new NullableUInt32Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            uint? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<uint?> serializer = new NullableUInt32Serializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<uint?> serializer = new NullableUInt32Serializer();
            uint? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            Serializer<uint?> serializer = new NullableUInt32Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
