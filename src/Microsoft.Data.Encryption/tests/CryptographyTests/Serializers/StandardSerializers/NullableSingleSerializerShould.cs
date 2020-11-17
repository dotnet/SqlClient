using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableSingleSerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(float.MinValue)]
        [InlineData(-12345.6789f)]
        [InlineData(-1.0f)]
        [InlineData(0.0f)]
        [InlineData(1.0f)]
        [InlineData(12345.6789f)]
        [InlineData(float.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(float? originalValue)
        {
            Serializer<float?> serializer = new NullableSingleSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            float? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<float?> serializer = new NullableSingleSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<float?> serializer = new NullableSingleSerializer();
            float? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ShouldThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            Serializer<float?> serializer = new NullableSingleSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
