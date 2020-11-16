using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class SbyteSerializerShould
    {
        [Theory]
        [InlineData(sbyte.MinValue)]
        [InlineData(-100)]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(sbyte.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(sbyte originalValue)
        {
            Serializer<sbyte> serializer = new SByteSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            sbyte deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ThrowIfDeserializingNull()
        {
            Serializer<sbyte> serializer = new SByteSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Fact]
        public void ThrowIfDeserializingLessThanOneByte()
        {
            Serializer<sbyte> serializer = new SByteSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(new byte[] { }));
        }
    }
}
