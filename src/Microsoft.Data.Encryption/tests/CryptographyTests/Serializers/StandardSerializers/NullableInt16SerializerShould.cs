using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableInt16SerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(short.MinValue)]
        [InlineData((short)-12345)]
        [InlineData((short)-1)]
        [InlineData((short)0)]
        [InlineData((short)1)]
        [InlineData((short)12345)]
        [InlineData(short.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(short? originalValue)
        {
            Serializer<short?> serializer = new NullableInt16Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            short? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<short?> serializer = new NullableInt16Serializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<short?> serializer = new NullableInt16Serializer();
            short? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        public void ThrowIfDeserializingLessThanTwoBytes(byte[] data)
        {
            Serializer<short?> serializer = new NullableInt16Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
