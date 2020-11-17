using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class NullableUInt64SerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(ulong.MinValue)]
        [InlineData(1U)]
        [InlineData(123456789U)]
        [InlineData(ulong.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(ulong? originalValue)
        {
            Serializer<ulong?> serializer = new NullableUInt64Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            ulong? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<ulong?> serializer = new NullableUInt64Serializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<ulong?> serializer = new NullableUInt64Serializer();
            ulong? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<ulong?> serializer = new NullableUInt64Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
