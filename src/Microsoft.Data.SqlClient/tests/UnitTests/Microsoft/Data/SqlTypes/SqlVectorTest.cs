// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;
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
    public void Construct_Length_Exceeds_8000()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlVector<float>.CreateNull(1999));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlVector<float>.CreateNull(int.MaxValue / 2));
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
        Assert.Equal(SQLMessage.NullString(), vec.GetString());

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
        Assert.Equal(SQLMessage.NullString(), vec.GetString());

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

    #region Float16 Tests

    #if NET

    [Fact]
    public void Float16_Construct_Memory()
    {
        Half[] data = { (Half)1.5f, (Half)2.5f, (Half)3.5f };
        var vec = new SqlVector<Half>(data);

        Assert.False(vec.IsNull);
        Assert.Equal(3, vec.Length);
        Assert.Equal(data, vec.Memory.ToArray());

        var ivec = vec as ISqlVector;
        Assert.Equal(0x01, ivec.ElementType);
        Assert.Equal(0x02, ivec.ElementSize);
        Assert.Equal(TdsEnums.VECTOR_HEADER_SIZE + (3 * 2), ivec.Size);

        // The base type is written into the header, and each element occupies two bytes.
        Assert.Equal(0x01, ivec.VectorPayload[4]);
        Assert.Equal(TdsEnums.VECTOR_HEADER_SIZE + (3 * 2), ivec.VectorPayload.Length);
    }

    [Fact]
    public void Float16_Construct_Length()
    {
        var vec = SqlVector<Half>.CreateNull(5);

        Assert.True(vec.IsNull);
        Assert.Equal(5, vec.Length);
        Assert.Equal(SQLMessage.NullString(), vec.GetString());

        var ivec = vec as ISqlVector;
        Assert.Equal(0x01, ivec.ElementType);
        Assert.Equal(0x02, ivec.ElementSize);
        Assert.Equal(TdsEnums.VECTOR_HEADER_SIZE + (5 * 2), ivec.Size);
    }

    [Fact]
    public void Float16_Construct_Length_Exceeds_8000()
    {
        // A float16 vector holds twice as many elements as a float32 one before the
        // payload exceeds the maximum size of a TDS packet.
        SqlVector<Half>.CreateNull(3996);

        Assert.Throws<ArgumentOutOfRangeException>(() => SqlVector<Half>.CreateNull(3997));
    }

    [Fact]
    public void Float16_GetString_RendersExactValues()
    {
        // Serialising the elements as Half would instead produce the shortest string which
        // round trips to the same Half, which renders 65504 as "65500".
        var vec = new SqlVector<Half>(new[] { (Half)65504f, (Half)1.5f });

        Assert.Equal("[65504,1.5]", vec.GetString());
    }

    [Fact]
    public void Float16_GetString_MatchesFloat32Rendering()
    {
        // A value which both base types represent exactly renders identically, so callers
        // cannot tell the two apart from the rendering alone.
        Assert.Equal(
            new SqlVector<float>(new[] { 1.5f, 2.5f }).GetString(),
            new SqlVector<Half>(new[] { (Half)1.5f, (Half)2.5f }).GetString());
    }

    #endif

    [Fact]
    public void Float32_GetString_RendersJson()
    {
        Assert.Equal("[1.5,2.5]", new SqlVector<float>(new[] { 1.5f, 2.5f }).GetString());
    }

    [Fact]
    public void GetString_Null_RendersNullString()
    {
        Assert.Equal(SQLMessage.NullString(), SqlVector<float>.CreateNull(3).GetString());
    }

    #endregion

    #region Payload Conversion Tests

    [Fact]
    public void ConvertPayload_Float32ToFloat16()
    {
        byte[] source = ((ISqlVector)new SqlVector<float>(new[] { 1.5f, 2.5f, 3.5f })).VectorPayload;

        byte[] converted = SqlVectorPayload.ConvertElementType(
            source,
            (byte)MetaType.SqlVectorElementType.Float16);

        Assert.Equal(0x01, converted[4]);
        Assert.Equal(TdsEnums.VECTOR_HEADER_SIZE + (3 * 2), converted.Length);

        // Reading the converted payload back yields the original values, because all three
        // are exactly representable in binary16.
        byte[] roundTripped = SqlVectorPayload.ConvertElementType(
            converted,
            (byte)MetaType.SqlVectorElementType.Float32);

        Assert.Equal(new[] { 1.5f, 2.5f, 3.5f }, new SqlVector<float>(roundTripped).Memory.ToArray());
    }

    [Fact]
    public void ConvertPayload_Float16ToFloat32_WidensExactly()
    {
        // Built directly rather than through SqlVector<Half>, so that the test also runs on
        // frameworks without System.Half.
        byte[] source = MakeFloat16Payload(new[] { 1.5f, 2.5f, 3.5f });

        byte[] converted = SqlVectorPayload.ConvertElementType(
            source,
            (byte)MetaType.SqlVectorElementType.Float32);

        Assert.Equal(0x00, converted[4]);
        Assert.Equal(new[] { 1.5f, 2.5f, 3.5f }, new SqlVector<float>(converted).Memory.ToArray());
    }

    [Fact]
    public void ConvertPayload_SameElementType_ReturnsInput()
    {
        byte[] source = ((ISqlVector)new SqlVector<float>(new[] { 1.5f })).VectorPayload;

        Assert.Same(
            source,
            SqlVectorPayload.ConvertElementType(source, (byte)MetaType.SqlVectorElementType.Float32));
    }

    [Fact]
    public void ConvertPayload_NarrowingRounds()
    {
        byte[] source = ((ISqlVector)new SqlVector<float>(new[] { 1.1f })).VectorPayload;

        byte[] narrowed = SqlVectorPayload.ConvertElementType(
            source,
            (byte)MetaType.SqlVectorElementType.Float16);

        byte[] widened = SqlVectorPayload.ConvertElementType(
            narrowed,
            (byte)MetaType.SqlVectorElementType.Float32);

        Assert.Equal(1.0996094f, new SqlVector<float>(widened).Memory.Span[0]);
    }

    [Fact]
    public void ConvertPayload_ShortHeader_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SqlVectorPayload.ConvertElementType(
                new byte[] { 0xA9, 0x01 },
                (byte)MetaType.SqlVectorElementType.Float16));
    }

    [Fact]
    public void ConvertPayload_LengthMismatch_Throws()
    {
        // The header declares two elements, but only one is present.
        byte[] source = MakeTdsPayloadStatic(
            new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 },
            new[] { 1.5f });

        Assert.Throws<ArgumentException>(() =>
            SqlVectorPayload.ConvertElementType(
                source,
                (byte)MetaType.SqlVectorElementType.Float16));
    }

    [Fact]
    public void ConvertPayload_UnsupportedElementType_Throws()
    {
        byte[] source = ((ISqlVector)new SqlVector<float>(new[] { 1.5f })).VectorPayload;

        Assert.Throws<NotSupportedException>(() =>
            SqlVectorPayload.ConvertElementType(source, 0x7F));
    }

    #endregion

    #region Widening Read Tests

    [Fact]
    public void FromTdsPayload_WidensFloat16ToFloat32()
    {
        // This is how a float16 column is read on frameworks without System.Half.
        byte[] payload = MakeFloat16Payload(new[] { 1.5f, 2.5f, 3.5f });

        var vec = SqlVector<float>.FromTdsPayload(payload);

        Assert.Equal(3, vec.Length);
        Assert.Equal(new[] { 1.5f, 2.5f, 3.5f }, vec.Memory.ToArray());

        // The widened vector reports float32, so its base type continues to be determined
        // by its element type alone rather than by the payload it was read from.
        Assert.Equal(0x00, ((ISqlVector)vec).ElementType);
    }

    [Fact]
    public void FromTdsPayload_MatchingElementType_ReadsDirectly()
    {
        byte[] payload = ((ISqlVector)new SqlVector<float>(new[] { 1.5f, 2.5f })).VectorPayload;

        var vec = SqlVector<float>.FromTdsPayload(payload);

        Assert.Equal(new[] { 1.5f, 2.5f }, vec.Memory.ToArray());
    }

    #if NET

    [Fact]
    public void FromTdsPayload_NarrowingIsRejected()
    {
        // Narrowing loses information, so it is never performed implicitly on a read.
        byte[] payload = ((ISqlVector)new SqlVector<float>(new[] { 1.5f })).VectorPayload;

        Assert.Throws<NotSupportedException>(() => SqlVector<Half>.FromTdsPayload(payload));
    }

    #endif

    #endregion

    #region Helpers

    private byte[] MakeTdsPayload(byte[] header, ReadOnlyMemory<float> values) =>
        MakeTdsPayloadStatic(header, values);

    private static byte[] MakeTdsPayloadStatic(byte[] header, ReadOnlyMemory<float> values)
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

    /// <summary>
    /// Builds a float16 vector payload without using <c>System.Half</c>, so that tests
    /// which need one can also run on .NET Framework.
    /// </summary>
    private static byte[] MakeFloat16Payload(float[] values)
    {
        byte[] payload = new byte[TdsEnums.VECTOR_HEADER_SIZE + (values.Length * 2)];

        payload[0] = 0xA9;
        payload[1] = 0x01;
        BitConverter.GetBytes((ushort)values.Length).CopyTo(payload, 2);
        payload[4] = (byte)MetaType.SqlVectorElementType.Float16;

        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(Float16Converter.FromSingle(values[i]))
                .CopyTo(payload, TdsEnums.VECTOR_HEADER_SIZE + (i * 2));
        }

        return payload;
    }

    #endregion
}
