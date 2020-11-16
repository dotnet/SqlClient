using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class ByteSerializerShould
    {
        [Theory]
        [InlineData(byte.MinValue)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(byte.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(byte originalValue)
        {
            Serializer<byte> serializer = new ByteSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            byte deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ThrowIfDeserializingNull()
        {
            Serializer<byte> serializer = new ByteSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Fact]
        public void ThrowIfDeserializingLessThanOneByte()
        {
            Serializer<byte> serializer = new ByteSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(new byte[] { }));
        }
    }
}
