using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class BooleanSerializerShould
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SerializeAndDeserializeToTheSameObject(bool originalValue)
        {
            Serializer<bool> serializer = new BooleanSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            bool deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ThrowIfDeserializingNull()
        {
            Serializer<bool> serializer = new BooleanSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Fact]
        public void ThrowIfDeserializingLessThanOneByte()
        {
            Serializer<bool> serializer = new BooleanSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(new byte[] { }));
        }
    }
}
