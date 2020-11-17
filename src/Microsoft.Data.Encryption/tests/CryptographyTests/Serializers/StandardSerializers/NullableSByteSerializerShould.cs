using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableSByteSerializerShould
    {
        [Theory]
        [InlineData(null)]
        [InlineData(sbyte.MinValue)]
        [InlineData((sbyte)-100)]
        [InlineData((sbyte)-1)]
        [InlineData((sbyte)0)]
        [InlineData((sbyte)1)]
        [InlineData((sbyte)100)]
        [InlineData(sbyte.MaxValue)]
        public void SerializeAndDeserializeToTheSameObject(sbyte? originalValue)
        {
            Serializer<sbyte?> serializer = new NullableSByteSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            sbyte? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<sbyte?> serializer = new NullableSByteSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<sbyte?> serializer = new NullableSByteSerializer();
            sbyte? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Fact]
        public void ThrowIfDeserializingLessThanOneByte()
        {
            Serializer<sbyte?> serializer = new NullableSByteSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(new byte[] { }));
        }
    }
}
