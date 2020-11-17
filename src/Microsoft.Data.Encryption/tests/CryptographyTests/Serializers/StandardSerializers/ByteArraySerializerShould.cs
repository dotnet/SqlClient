using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class ByteArraySerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1 })]
        [InlineData(new byte[] { 1, 2 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
        [InlineData(new byte[] { 255 })]
        [InlineData(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 })]
        public void SerializeAndDeserializeToTheSameObject(byte[] originalValue)
        {
            Serializer<byte[]> serializer = new ByteArraySerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            byte[] deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<byte[]> serializer = new ByteArraySerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<byte[]> serializer = new ByteArraySerializer();
            byte[] actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }
    }
}
