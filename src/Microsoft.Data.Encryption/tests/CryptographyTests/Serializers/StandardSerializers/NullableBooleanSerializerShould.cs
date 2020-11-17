using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class NullableBooleanSerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public void SerializeAndDeserializeToTheSameObject(bool? originalValue)
        {
            Serializer<bool?> serializer = new NullableBooleanSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            bool? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<bool?> serializer = new NullableBooleanSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<bool?> serializer = new NullableBooleanSerializer();
            bool? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Fact]
        public void ThrowIfDeserializingLessThanOneByte()
        {
            Serializer<bool?> serializer = new NullableBooleanSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(new byte[] { }));
        }
    }
}
