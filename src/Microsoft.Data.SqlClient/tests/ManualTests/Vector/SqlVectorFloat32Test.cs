using System;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlVectorFloat32Test
    {
        [Fact]
        public void Constructor_WithLength_InitializesCorrectly()
        {
            var vec = new SqlVectorFloat32(4);
            Assert.Equal(4, vec.Length);
            Assert.True(vec.IsNull); // _rawbytes is empty array => IsNull = true
        }

        [Fact]
        public void Constructor_WithFloatArray_SetsElementCount()
        {
            float[] data = { 1f, 2f, 3f };
            var vec = new SqlVectorFloat32(data);
            Assert.Equal(3, vec.Length);
            Assert.False(vec.IsNull);
        }

        [Fact]
        public void Constructor_WithNullArray_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SqlVectorFloat32(null!));
        }

        [Fact]
        public void ToArray_ReturnsOriginalFloats()
        {
            float[] input = { 1.1f, 2.2f, 3.3f };
            var vec = new SqlVectorFloat32(input);
            var output = vec.ToArray();
            Assert.Equal(input, output);
        }

        [Fact]
        public void ToArray_InvalidRawBytes_Throws()
        {
            var vec = new SqlVectorFloat32(0);
            Assert.Throws<ArgumentException>(() => vec.ToArray());
        }

        [Fact]
        public void ToString_ReturnsJsonString()
        {
            var input = new float[] { 9.9f, 5.5f };
            var vec = new SqlVectorFloat32(input);
            var json = vec.ToString();
            var expected = JsonSerializer.Serialize(input);
            Assert.Equal(expected, json);
        }

        [Fact]
        public void ToString_WhenNull_ReturnsNULL()
        {
            var vec = SqlVectorFloat32.Null;
            Assert.Equal("NULL", vec.ToString());
        }

        [Fact]
        public void IsNull_ReturnsExpected()
        {
            var vecNull = SqlVectorFloat32.Null;
            var vecReal = new SqlVectorFloat32(new float[] { 1f });
            Assert.True(vecNull.IsNull);
            Assert.False(vecReal.IsNull);
        }

        [Fact]
        public void NullProperty_ReturnsZeroVector()
        {
            var vec = SqlVectorFloat32.Null;
            Assert.Equal(0, vec.Length);
        }

        [Fact]
        public void Internal_Constructor_WithValidRawBytes_DeserializesCorrectly()
        {
            float[] data = { 4.4f, 5.5f };
            var original = new SqlVectorFloat32(data);
            var raw = GetISqlVectorProperty<byte[]>(original, "VectorPayload");

            var ctor = typeof(SqlVectorFloat32).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(byte[]) }, null);
            Assert.NotNull(ctor);

            var deserialized = (SqlVectorFloat32)ctor.Invoke(new object[] { raw });
            float[] result = deserialized.ToArray();
            Assert.Equal(data, result);
        }

        [Fact]
        public void Internal_Constructor_WithInvalidRawBytes_Throws()
        {
            var invalidBytes = new byte[] { 0x00, 0x00, 0x00 }; // bad prefix
            var ctor = typeof(SqlVectorFloat32).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(byte[]) }, null);
            Assert.NotNull(ctor);
            var ex = Assert.Throws<TargetInvocationException>(() => ctor.Invoke(new object[] { invalidBytes }));
            Assert.IsType<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public void ISqlVector_ElementCount_IsCorrect()
        {
            var vec = new SqlVectorFloat32(new float[] { 1f, 2f });
            int count = vec.Length;
            Assert.Equal(2, count);
        }

        [Fact]
        public void ISqlVector_ElementSize_IsFloatSize()
        {
            var vec = new SqlVectorFloat32(new float[] { 1f });
            byte elementSize = GetISqlVectorProperty<byte>(vec, "ElementSize");
            Assert.Equal(sizeof(float), elementSize);
        }

        [Fact]
        public void ISqlVector_ElementType_IsFloat32()
        {
            var vec = new SqlVectorFloat32(new float[] { 1f });
            byte elementType = GetISqlVectorProperty<byte>(vec, "ElementType");
            Assert.Equal((byte)0, elementType);
        }

        [Fact]
        public void ISqlVector_VectorPayload_IsCorrectLength()
        {
            var vec = new SqlVectorFloat32(new float[] { 10f, 20f });
            byte[] payload = GetISqlVectorProperty<byte[]>(vec, "VectorPayload");
            Assert.NotNull(payload);
            Assert.True(payload.Length >= 8);
        }

        // === Utility for accessing internal interface ===
        private static T GetISqlVectorProperty<T>(SqlVectorFloat32 instance, string propName)
        {
            var interfaceType = typeof(SqlVectorFloat32).GetInterface("Microsoft.Data.SqlClient.ISqlVector", true);
            Assert.NotNull(interfaceType);

            var prop = interfaceType.GetProperty(propName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(prop);

            return (T)prop.GetValue(instance)!;
        }
    }
}
