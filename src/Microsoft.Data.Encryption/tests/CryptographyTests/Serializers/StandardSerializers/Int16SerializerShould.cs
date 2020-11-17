using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class Int16SerializerShould
    {
        [Theory]
        [InlineData(short.MinValue)]
        [InlineData(-12345)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(12345)]
        [InlineData(short.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(short originalValue)
        {
            Serializer<short> serializer = new Int16Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            short deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<short> serializer = new Int16Serializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        public void ShouldThrowIfDeserializingLessThanTwoBytes(byte[] data)
        {
            Serializer<short> serializer = new Int16Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
