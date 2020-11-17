using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class NullableUInt16SerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(ushort.MinValue)]
        [InlineData((ushort)1)]
        [InlineData((ushort)12345)]
        [InlineData(ushort.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(ushort? originalValue)
        {
            Serializer<ushort?> serializer = new NullableUInt16Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            ushort? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<ushort?> serializer = new NullableUInt16Serializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<ushort?> serializer = new NullableUInt16Serializer();
            ushort? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        public void ThrowIfDeserializingLessThanTwoBytes(byte[] data)
        {
            Serializer<ushort?> serializer = new NullableUInt16Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
