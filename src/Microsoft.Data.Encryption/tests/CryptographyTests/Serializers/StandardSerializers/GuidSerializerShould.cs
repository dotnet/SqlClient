using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.CryptographyTests.Serializers.StandardSerializers
{
    public class GuidSerializerShould
    {
        [Theory]
        [GuidSerializerTestData]
        public void SerializeAndDeserializeToTheSameObject(Guid originalValue)
        {
            Serializer<Guid> serializer = new GuidSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            Guid deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ShouldThrowIfDeserializingNull()
        {
            Serializer<Guid> serializer = new GuidSerializer();
            Assert.Throws<ArgumentNullException>(() => serializer.Deserialize(null));
        }

        [Theory]
        [InlineData(new byte[] { })]
        [InlineData(new byte[] { 0 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 })]
        public void ShouldThrowIfDeserializingLessThanSixteenBytes(byte[] data)
        {
            Serializer<Guid> serializer = new GuidSerializer();
            Assert.Throws<ArgumentOutOfRangeException>(() => serializer.Deserialize(data));
        }

        public class GuidSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { Guid.Empty };
                yield return new object[] { Guid.Parse("11111111-1111-1111-1111-111111111111") };
                yield return new object[] { Guid.Parse("99999999-9999-9999-9999-999999999999") };
                yield return new object[] { Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF") };
                yield return new object[] { Guid.Parse("12345678-90AB-CDEF-1234-567890ABCDEF") };
                yield return new object[] { Guid.NewGuid() };
            }
        }
    }
}
