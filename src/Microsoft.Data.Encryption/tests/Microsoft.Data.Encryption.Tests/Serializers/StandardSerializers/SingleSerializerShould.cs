using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class SingleSerializerShould
    {
        [Theory]
        [InlineData(float.MinValue)]
        [InlineData(-12345.6789)]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(12345.6789)]
        [InlineData(float.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(float originalValue)
        {
            Serializer<float> serializer = new SingleSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            float deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<float> serializer = new SingleSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ShouldThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            Serializer<float> serializer = new SingleSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
