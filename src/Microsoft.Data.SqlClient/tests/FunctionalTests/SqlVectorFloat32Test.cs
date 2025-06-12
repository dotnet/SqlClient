

using System;
using System.Reflection;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlVectorFloat32Test
    {
        [Fact]
        public void Constructor_WithValidLength_ShouldSetLength()
        {
            var vec = new SqlVectorFloat32(5);
            Assert.Equal(5, vec.Length);
            Assert.True(vec.IsNull);
        }

        [Fact]
        public void Constructor_WithNegativeLength_ShouldThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SqlVectorFloat32(-1));
        }

        [Fact]
        public void Constructor_WithEmptyValues_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => new SqlVectorFloat32(values: Array.Empty<float>()));
        }

        [Fact]
        public void Constructor_WithValues_ShouldSetProperties()
        {
            float[] data = new float[] { 1.1f, 2.2f };
            var vec = new SqlVectorFloat32(values: data);
            Assert.Equal(2, vec.Length);
            Assert.False(vec.IsNull);
            Assert.Equal(data, vec.Values);
        }

        [Fact]
        public void Constructor_WithReadOnlyMem_ShouldSetProperties()
        {
            ReadOnlyMemory<float> data = new float[] { 1.1f, 2.2f, 3.3f };
            var vec = new SqlVectorFloat32(values: data);
            Assert.Equal(3, vec.Length);
            Assert.False(vec.IsNull);
            Assert.Equal(data.ToArray(), vec.Values);
        }

        [Fact]
        public void ToString_ShouldReturnJsonString()
        {
            float[] data = new float[] { 3.14f };
            var vec = new SqlVectorFloat32(values: data);
            string json = vec.ToString();
            Assert.Contains("3.14", json);
        }

        [Fact]
        public void ToString_ShouldReturnNullString()
        {
            var vec = new SqlVectorFloat32(0);
            string nullStr = vec.ToString();
            Assert.Contains("NULL", nullStr);
        }

        [Fact]
        public void IsNull_WithLengthCtor_ShouldBeTrue()
        {
            var vec = new SqlVectorFloat32(0);
            Assert.True(vec.IsNull);
        }

        [Fact]
        public void Null_Property_ShouldReturnNull()
        {
            SqlVectorFloat32 vec = SqlVectorFloat32.Null;
            Assert.Null(vec);
        }

        [Fact]
        public void Values_WhenNull_ShouldReturnEmptyArray()
        {
            var vec = new SqlVectorFloat32(0);
            Assert.Empty(vec.Values);
        }
    }
}
