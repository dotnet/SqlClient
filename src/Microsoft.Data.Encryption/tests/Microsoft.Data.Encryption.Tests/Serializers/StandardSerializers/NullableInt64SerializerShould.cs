using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class NullableInt64SerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(long.MinValue)]
        [InlineData(-123456789)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(123456789)]
        [InlineData(long.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(long? originalValue)
        {
            Serializer<long?> serializer = new NullableInt64Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            long? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<long?> serializer = new NullableInt64Serializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<long?> serializer = new NullableInt64Serializer();
            long? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        public void ThrowIfDeserializingLessThanEightBytes(byte[] data)
        {
            Serializer<long?> serializer = new NullableInt64Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
