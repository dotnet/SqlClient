using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.Data.Encryption.Cryptography.Tests.Serializers.StandardSerializers
{
    public class StringSerializerShould
    {
        [Theory]
        [StringSerializerTestData]
        public void SerializeAndDeserializeToTheSameObject(string originalValue)
        {
            Serializer<string> serializer = new StringSerializer();
            byte[] serializedValue = serializer.Serialize(originalValue);
            string deserializedValue = serializer.Deserialize(serializedValue);

            Assert.Equal(originalValue, deserializedValue);
        }

        [Fact]
        public void ReturnNullIfSerializingNull()
        {
            Serializer<string> serializer = new StringSerializer();
            byte[] actualSerializedValue = serializer.Serialize(null);

            Assert.Null(actualSerializedValue);
        }

        [Fact]
        public void ReturnNullIfDeserializingNull()
        {
            Serializer<string> serializer = new StringSerializer();
            string actualDeserializedValue = serializer.Deserialize(null);

            Assert.Null(actualDeserializedValue);
        }

        public class StringSerializerTestDataAttribute : DataAttribute
        {
            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[] { null };
                yield return new object[] { string.Empty };
                yield return new object[] { "" };
                yield return new object[] { "a" };
                yield return new object[] { "abcdefghijklmnopqrstuvwxyz" };
                yield return new object[] { "12345678-90AB-CDEF-1234-567890ABCDEF" };
                yield return new object[] { new string(Enumerable.Repeat('+', 1000).ToArray()) };
            }
        }
    }
}
