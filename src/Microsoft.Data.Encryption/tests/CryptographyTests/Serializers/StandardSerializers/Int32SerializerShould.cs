using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class Int32SerializerShould
    {
        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(-123456789)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(123456789)]
        [InlineData(int.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(int originalValue)
        {
            Serializer<int> serializer = new Int32Serializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            int deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<int> serializer = new Int32Serializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        public void ShouldThrowIfDeserializingLessThanFourBytes(byte[] data)
        {
            Serializer<int> serializer = new Int32Serializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }
    }
}
