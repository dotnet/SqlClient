using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class CharSerializerShould
    {
        [Theory]
        [InlineData(char.MinValue)]
        [InlineData('\n')]
        [InlineData('a')]
        [InlineData('\u006A')]
        [InlineData('\x0f6A')]
        [InlineData('Z')]
        [InlineData(char.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(char originalValue)
        {
            Serializer<char> serializer = new CharSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            char deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ThrowIfDeserializingNull()
        {
            Serializer<char> serializer = new CharSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 127 })]
        public void ThrowIfDeserializingLessThanTwoBytes(byte[] data)
        {
            Serializer<char> serializer = new CharSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
