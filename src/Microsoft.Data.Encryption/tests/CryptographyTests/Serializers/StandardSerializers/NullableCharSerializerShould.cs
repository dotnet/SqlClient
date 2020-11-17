using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableCharSerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(char.MinValue)]
        [InlineData('\n')]
        [InlineData('a')]
        [InlineData('\u006A')]
        [InlineData('\x0f6A')]
        [InlineData('Z')]
        [InlineData(char.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(char? originalValue)
        {
            Serializer<char?> serializer = new NullableCharSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            char? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<char?> serializer = new NullableCharSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<char?> serializer = new NullableCharSerializer();
            char? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 127 })]
        public void ThrowIfDeserializingLessThanTwoBytes(byte[] data)
        {
            Serializer<char?> serializer = new NullableCharSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
