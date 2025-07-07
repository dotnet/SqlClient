
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlTypes;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.Tests;

public class SqlVectorTest
{
    #region Tests

    [Fact]
    public void UnsupportedType()
    {
        Assert.Throws<NotSupportedException>(() => new SqlVector<int>(5));
        Assert.Throws<NotSupportedException>(() => new SqlVector<byte>(5));
        Assert.Throws<NotSupportedException>(() => new SqlVector<double>(5));
    }

    [Fact]
    public void Construct_Length_Negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqlVector<float>(-1));
    }

    [Fact]
    public void Construct_Length()
    {
        // Act
        var vec = new SqlVector<float>(5);
        
        // Assert
        // - SqlVector properties/methods
        Assert.True(vec.IsNull);
        Assert.Equal(5, vec.Length);
        Assert.Equal(28, vec.Size);
        
        // Note that ReadOnlyMemory<> equality checks that both instances point
        // to the same memory.  We want to check memory content equality, so we
        // compare their arrays instead.
        Assert.Equal(new ReadOnlyMemory<float>().ToArray(), vec.Memory.ToArray());

        // - ISqlVector properties/methods
        ISqlVector ivec = vec;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Empty(ivec.VectorPayload);
        
        Assert.Equal(SQLResource.NullString, ivec.GetString());
    }

    [Fact]
    public void Construct_WithLengthZero()
    {
        // Act
        var vec = new SqlVector<float>(0);
        
        // Assert
        // - SqlVector properties/methods
        Assert.True(vec.IsNull);
        Assert.Equal(0, vec.Length);
        Assert.Equal(8, vec.Size);
        
        // Note that ReadOnlyMemory<> equality checks that both instances point
        // to the same memory.  We want to check memory content equality, so we
        // compare their arrays instead.
        Assert.Equal(new ReadOnlyMemory<float>().ToArray(), vec.Memory.ToArray());

        // - ISqlVector properties/methods
        ISqlVector ivec = vec;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Empty(ivec.VectorPayload);
        
        Assert.Equal(SQLResource.NullString, ivec.GetString());
    }

    [Fact]
    public void Construct_Memory_Empty()
    {
        // Act
        SqlVector<float> vec = new SqlVector<float>(new ReadOnlyMemory<float>());
        
        // Assert
        // - SqlVector properties/methods
        Assert.False(vec.IsNull);
        Assert.Equal(0, vec.Length);
        Assert.Equal(8, vec.Size);
        Assert.Equal(new ReadOnlyMemory<float>().ToArray(), vec.Memory.ToArray());

        // - ISqlVector properties/methods
        ISqlVector ivec = vec;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(new byte[] { 0xA9, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, ivec.VectorPayload);
        
        Assert.Equal("[]", ivec.GetString());
    }

    [Fact]
    public void Construct_Memory()
    {
        // Arrange
        float[] data = [1.1f, 2.2f];
        ReadOnlyMemory<float> memory = new ReadOnlyMemory<float>(data);
        
        // Act
        SqlVector<float> vec = new SqlVector<float>(memory);
        
        // Assert
        // - SqlVector methods/properties
        Assert.False(vec.IsNull);
        Assert.Equal(2, vec.Length);
        Assert.Equal(16, vec.Size);
        Assert.Equal(memory.ToArray(), vec.Memory.ToArray());
        Assert.Equal(data, vec.Memory.ToArray());
        
        // - ISqlVector methods/properties
        ISqlVector ivec = vec;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(
            MakeTdsPayload(new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 }, memory),
            ivec.VectorPayload);
        
        #if NETFRAMEWORK
        Assert.Equal("[1.10000002,2.20000005]", ivec.GetString());
        #else
        Assert.Equal("[1.1,2.2]", ivec.GetString());
        #endif
    }

    [Fact]
    public void Construct_Memory_ImplicitConversionFromFloatArray()
    {
        // Arrange
        float[] data = new[] { 3.3f, 4.4f, 5.5f };
        
        // Act
        var vec = new SqlVector<float>(data);
        
        // Assert
        // - SqlVector methods/properties
        Assert.False(vec.IsNull);
        Assert.Equal(3, vec.Length);
        Assert.Equal(20, vec.Size);
        Assert.Equal(new ReadOnlyMemory<float>(data).ToArray(), vec.Memory.ToArray());
        Assert.Equal(data, vec.Memory.ToArray());
        
        // - ISqlVector methods/properties
        ISqlVector ivec = vec;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(
            MakeTdsPayload(new byte[] { 0xA9, 0x01, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00 }, data),
            ivec.VectorPayload);

        #if NETFRAMEWORK
        Assert.Equal("[3.29999995,4.4000001,5.5]", ivec.GetString());
        #else
        Assert.Equal("[3.3,4.4,5.5]", ivec.GetString());
        #endif
    }

    [Fact]
    public void Construct_Bytes()
    {
        // Arrange
        byte[] header = new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 };
        float[] data = new float[] { 6.6f, 7.7f };
        byte[] bytes = MakeTdsPayload(header, data);
        
        // Act
        var vec = new SqlVector<float>(bytes);
        
        // Assert
        // - SqlVector methods/properties
        Assert.False(vec.IsNull);
        Assert.Equal(2, vec.Length);
        Assert.Equal(16, vec.Size);
        Assert.Equal(new ReadOnlyMemory<float>(data).ToArray(), vec.Memory.ToArray());
        Assert.Equal(data, vec.Memory.ToArray());
        
        // - ISqlVector methods/properties
        ISqlVector ivec = vec;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(bytes, ivec.VectorPayload);
        
        #if NETFRAMEWORK
        Assert.Equal("[6.5999999,7.69999981]", ivec.GetString());
        #else
        Assert.Equal("[6.6,7.7]", ivec.GetString());
        #endif
    }

    [Fact]
    public void Construct_Bytes_ShortHeader()
    {
        // Arrange
        var tdsBytes = new byte[] { 0xA9, 0x01, 0x00, 0x00 };
        
        // Act
        Action action = () => _ = new SqlVector<float>(tdsBytes);
        
        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Construct_Bytes_UnknownMagic()
    {
        // Arrange
        var tdsBytes = new byte[] { 0xA8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        
        // Act
        Action action = () => _ = new SqlVector<float>(tdsBytes);
        
        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Construct_Bytes_UnsupportedVersion()
    {
        // Arrange
        var tdsBytes = new byte[] { 0xA9, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        
        // Act
        Action action = () => _ = new SqlVector<float>(tdsBytes);
        
        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Construct_Bytes_TypeMismatch()
    {
        // Arrange
        var tdsBytes = new byte[] { 0xA9, 0x01, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00 };
        
        // Act
        Action action = () => _ = new SqlVector<float>(tdsBytes);
        
        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Construct_Bytes_LengthMismatch()
    {
        // Arrange
        // - The header indicates 2 elements, but the payload has 3 floats.
        var header = new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var floats = new ReadOnlyMemory<float>([1.1f, 2.2f, 3.3f]);
        var bytes = MakeTdsPayload(header, floats);
        
        // Act
        Action action = () => _ = new SqlVector<float>(bytes);
        
        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Null_Property()
    {
        Assert.Null(SqlVector<float>.Null);
    }

    #endregion

    #region Helpers

    private static byte[] MakeTdsPayload(byte[] header, ReadOnlyMemory<float> values)
    {
        int length = header.Length + (values.Length * sizeof(float));
        byte[] payload = new byte[length];
        header.CopyTo(payload, 0);
        for (int i = 0; i < values.Length; i++)
        {
            var offset = header.Length + (i * sizeof(float));
            BitConverter.GetBytes(values.Span[i]).CopyTo(payload, offset);
        }
        return payload;
    }
    
    #endregion
}
