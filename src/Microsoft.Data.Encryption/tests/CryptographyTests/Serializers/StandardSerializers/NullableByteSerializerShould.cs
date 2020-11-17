using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableByteSerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(byte.MinValue)]
        [InlineData((byte)1)]
        [InlineData((byte)100)]
        [InlineData(byte.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(byte? originalValue)
        {
            Serializer<byte?> serializer = new NullableByteSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            byte? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<byte?> serializer = new NullableByteSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<byte?> serializer = new NullableByteSerializer();
            byte? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Fact]
        public void ThrowIfDeserializingLessThanOneByte()
        {
            Serializer<byte?> serializer = new NullableByteSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(new byte[] { }));
        }
    }
}