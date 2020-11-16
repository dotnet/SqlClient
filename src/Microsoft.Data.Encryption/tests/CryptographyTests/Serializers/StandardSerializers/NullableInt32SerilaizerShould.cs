using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableInt32SerilaizerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(int.MinValue)]
        [InlineData(-123456789)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(123456789)]
        [InlineData(int.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(int? originalValue)
        {
            Serializer<int?> serializer = new NullableInt32Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            int? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<int?> serializer = new NullableInt32Serializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<int?> serializer = new NullableInt32Serializer();
            int? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            Serializer<int?> serializer = new NullableInt32Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
