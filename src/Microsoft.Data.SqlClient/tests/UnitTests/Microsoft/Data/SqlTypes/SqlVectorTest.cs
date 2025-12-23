// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlTypes.UnitTests;

public class SqlVectorTest
{
    #region Tests

    [Fact]
    public void UnsupportedType()
    {
        Assert.Throws<NotSupportedException>(() => SqlVector<int>.CreateNull(5));
        Assert.Throws<NotSupportedException>(() => SqlVector<byte>.CreateNull(5));
        Assert.Throws<NotSupportedException>(() => SqlVector<double>.CreateNull(5));
    }

    [Fact]
    public void Construct_Length_Negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlVector<float>.CreateNull(-1));
    }

    [Fact]
    public void Construct_Length()
    {
        var vec = SqlVector<float>.CreateNull(5);
        Assert.True(vec.IsNull);
        Assert.Equal(5, vec.Length);
        // Note that ReadOnlyMemory<> equality checks that both instances point
        // to the same memory.  We want to check memory content equality, so we
        // compare their arrays instead.
        Assert.Equal(new ReadOnlyMemory<float>().ToArray(), vec.Memory.ToArray());
        Assert.Equal(SQLResource.NullString, vec.GetString());

        var ivec = vec as ISqlVector;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(28, ivec.Size);
        Assert.Empty(ivec.VectorPayload);
    }

    [Fact]
    public void Construct_WithLengthZero()
    {
        var vec = SqlVector<float>.CreateNull(0);
        Assert.True(vec.IsNull);
        Assert.Equal(0, vec.Length);
        
        // Note that ReadOnlyMemory<> equality checks that both instances point
        // to the same memory.  We want to check memory content equality, so we
        // compare their arrays instead.
        Assert.Equal(new ReadOnlyMemory<float>().ToArray(), vec.Memory.ToArray());
        Assert.Equal(SQLResource.NullString, vec.GetString());

        var ivec = vec as ISqlVector;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(8, ivec.Size);
        Assert.Empty(ivec.VectorPayload);
    }

    [Fact]
    public void Construct_Memory_Empty()
    {
        SqlVector<float> vec = new(new ReadOnlyMemory<float>());
        Assert.False(vec.IsNull);
        Assert.Equal(0, vec.Length);
        Assert.Equal(new ReadOnlyMemory<float>().ToArray(), vec.Memory.ToArray());
        Assert.Equal("[]", vec.GetString());

        var ivec = vec as ISqlVector;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(8, ivec.Size);
        Assert.Equal(
            new byte[] { 0xA9, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
            ivec.VectorPayload);
    }

    [Fact]
    public void Construct_Memory()
    {
        float[] data = [1.1f, 2.2f];
        ReadOnlyMemory<float> memory = new(data);
        SqlVector<float> vec = new(memory);
        Assert.False(vec.IsNull);
        Assert.Equal(2, vec.Length);
        Assert.Equal(memory.ToArray(), vec.Memory.ToArray());
        Assert.Equal(data, vec.Memory.ToArray());
        #if NETFRAMEWORK
        Assert.Equal("[1.10000002,2.20000005]", vec.GetString());
        #else
        Assert.Equal("[1.1,2.2]", vec.GetString());
        #endif
        var ivec = vec as ISqlVector;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(16, ivec.Size);
        Assert.Equal(
            MakeTdsPayload(
                new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 },
                memory),
            ivec.VectorPayload);
    }

    [Fact]
    public void Construct_Memory_ImplicitConversionFromFloatArray()
    {
        float[] data = new float[] { 3.3f, 4.4f, 5.5f };
        var vec = new SqlVector<float>(data);
        Assert.False(vec.IsNull);
        Assert.Equal(3, vec.Length);
        Assert.Equal(new ReadOnlyMemory<float>(data).ToArray(), vec.Memory.ToArray());
        Assert.Equal(data, vec.Memory.ToArray());
        #if NETFRAMEWORK
        Assert.Equal("[3.29999995,4.4000001,5.5]", vec.GetString());
        #else
        Assert.Equal("[3.3,4.4,5.5]", vec.GetString());
        #endif

        var ivec = vec as ISqlVector;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(20, ivec.Size);
        Assert.Equal(
            MakeTdsPayload(
                new byte[] { 0xA9, 0x01, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00 },
                data),
            ivec.VectorPayload);
    }

    [Fact]
    public void Construct_Bytes()
    {
        float[] data = new float[] { 6.6f, 7.7f };
        var bytes =
            MakeTdsPayload(
                new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 },
                data);
        
        var vec = new SqlVector<float>(bytes);
        Assert.False(vec.IsNull);
        Assert.Equal(2, vec.Length);
        Assert.Equal(new ReadOnlyMemory<float>(data).ToArray(), vec.Memory.ToArray());
        Assert.Equal(data, vec.Memory.ToArray());
        #if NETFRAMEWORK
        Assert.Equal("[6.5999999,7.69999981]", vec.GetString());
        #else
        Assert.Equal("[6.6,7.7]", vec.GetString());
        #endif

        var ivec = vec as ISqlVector;
        Assert.Equal(0x00, ivec.ElementType);
        Assert.Equal(0x04, ivec.ElementSize);
        Assert.Equal(16, ivec.Size);
        Assert.Equal(bytes, ivec.VectorPayload);
    }

    [Fact]
    public void Construct_Bytes_ShortHeader()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            new SqlVector<float>(new byte[] { 0xA9, 0x01, 0x00, 0x00 });
        });
    }

    [Fact]
    public void Construct_Bytes_UnknownMagic()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            new SqlVector<float>(
                new byte[] { 0xA8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        });
    }

    [Fact]
    public void Construct_Bytes_UnsupportedVersion()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            new SqlVector<float>(
                new byte[] { 0xA9, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        });
    }

    [Fact]
    public void Construct_Bytes_TypeMismatch()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            new SqlVector<float>(
                new byte[] { 0xA9, 0x01, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00 });
        });
    }

    [Fact]
    public void Construct_Bytes_LengthMismatch()
    {
        // The header indicates 2 elements, but the payload has 3 floats.
        var header = new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var bytes = MakeTdsPayload(
            header,
            new ReadOnlyMemory<float>(new float[] { 1.1f, 2.2f, 3.3f }));
        
        Assert.Throws<ArgumentException>(() =>
        {
            new SqlVector<float>(bytes);
        });
    }

    [Fact]
    public void Null_Property()
    {
        Assert.Null(SqlVector<float>.Null);
    }

    #endregion

    #region Helpers

    private byte[] MakeTdsPayload(byte[] header, ReadOnlyMemory<float> values)
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
