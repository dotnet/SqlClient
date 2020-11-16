using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class NullableDecimalSerializerShould
    {
        [Theory]
        [NullableDecimalSerializerTestData]
        public void SerializeAndDeserializeToTheSameObject(decimal? originalValue)
        {
            Serializer<decimal?> serializer = new NullableDecimalSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            decimal? deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<decimal?> serializer = new NullableDecimalSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<decimal?> serializer = new NullableDecimalSerializer();
            decimal? actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })]
        public void ShouldThrowIfDeserializingLessThanSixteenBytes(byte[] data)
        {
            Serializer<decimal?> serializer = new NullableDecimalSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class NullableDecimalSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { decimal.MinValue };
                yield return new object[] { -1797693134862315.7M };
                yield return new object[] { -1.7976931348623157M };
                yield return new object[] { decimal.MinusOne };
                yield return new object[] { decimal.Zero };
                yield return new object[] { decimal.One };
                yield return new object[] { 1.7976931348623157M };
                yield return new object[] { 1797693134862315.7M };
                yield return new object[] { decimal.MaxValue };
            }
        }
    }
}
