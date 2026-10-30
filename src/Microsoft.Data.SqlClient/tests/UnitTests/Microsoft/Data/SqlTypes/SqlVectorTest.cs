// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlTypes.UnitTests;

/// <summary>
/// Verifies vector construction, payload validation, and conversions without a SQL Server.
/// </summary>
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

    /// <summary>
    /// Reads a matching float32 payload through the strict factory without changing its bytes.
    /// </summary>
    [Fact]
    public void FromTdsPayload_StrictMatchingType()
    {
        float[] data = new float[] { 6.6f, 7.7f };
        var bytes =
            MakeTdsPayload(
                new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 },
                data);
        
        var vec = SqlVector<float>.FromTdsPayload(bytes, allowWidening: false);
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

    /// <summary>
    /// Rejects a truncated header in the strict payload path.
    /// </summary>
    [Fact]
    public void FromTdsPayload_StrictShortHeader()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            SqlVector<float>.FromTdsPayload(
                new byte[] { 0xA9, 0x01, 0x00, 0x00 }, allowWidening: false);
        });
    }

    /// <summary>
    /// Rejects an unknown payload marker in the strict payload path.
    /// </summary>
    [Fact]
    public void FromTdsPayload_StrictUnknownMagic()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            SqlVector<float>.FromTdsPayload(
                new byte[] { 0xA8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                allowWidening: false);
        });
    }

    /// <summary>
    /// Rejects an unsupported payload version in the strict payload path.
    /// </summary>
    [Fact]
    public void FromTdsPayload_StrictUnsupportedVersion()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            SqlVector<float>.FromTdsPayload(
                new byte[] { 0xA9, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                allowWidening: false);
        });
    }

    /// <summary>
    /// Rejects a different base type rather than widening it when strict matching is requested.
    /// </summary>
    /// <param name="elementType">A supported or unknown base type different from float32.</param>
    [Theory]
    [InlineData((byte)0x01)]
    [InlineData((byte)0x03)]
    public void FromTdsPayload_StrictTypeMismatch(byte elementType)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            SqlVector<float>.FromTdsPayload(
                new byte[] { 0xA9, 0x01, 0x01, 0x00, elementType, 0x00, 0x00, 0x00, 0x00, 0x3C },
                allowWidening: false);
        });
    }

    /// <summary>
    /// Rejects payload bytes whose length disagrees with the declared dimension count.
    /// </summary>
    [Fact]
    public void FromTdsPayload_StrictLengthMismatch()
    {
        // The header indicates 2 elements, but the payload has 3 floats.
        var header = new byte[] { 0xA9, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var bytes = MakeTdsPayload(
            header,
            new ReadOnlyMemory<float>(new float[] { 1.1f, 2.2f, 3.3f }));
        
        Assert.Throws<ArgumentException>(() =>
        {
            SqlVector<float>.FromTdsPayload(bytes, allowWidening: false);
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

    /// <summary>
    /// Verifies that a float16 vector built from memory reports the float16 base type and a
    /// two byte element size, and writes that base type into the payload header. The header
    /// is what tells the server how to read the elements, so it must match the type argument.
    /// </summary>
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

    /// <summary>
    /// Verifies that a null float16 vector still carries its base type, element size, and
    /// dimension count, which the driver needs to describe the parameter to the server even
    /// though it sends no elements.
    /// </summary>
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

    /// <summary>
    /// Verifies that the dimension limit is derived from the element size rather than fixed:
    /// a float16 vector holds 3996 elements before the payload exceeds the maximum size,
    /// twice the float32 limit, and one more is rejected.
    /// </summary>
    [Fact]
    public void Float16_Construct_Length_Exceeds_8000()
    {
        // A float16 vector holds twice as many elements as a float32 one before the
        // payload exceeds the maximum size of a TDS packet.
        SqlVector<Half>.CreateNull(3996);

        Assert.Throws<ArgumentOutOfRangeException>(() => SqlVector<Half>.CreateNull(3997));
    }

    /// <summary>
    /// Verifies that a float16 vector renders its exact element values. Serialising the
    /// elements as <c>Half</c> would instead emit the shortest string which round trips to
    /// the same <c>Half</c>, rendering 65504 as "65500" and losing the value the column
    /// actually holds.
    /// </summary>
    [Fact]
    public void Float16_GetString_RendersExactValues()
    {
        // Serialising the elements as Half would instead produce the shortest string which
        // round trips to the same Half, which renders 65504 as "65500".
        var vec = new SqlVector<Half>(new[] { (Half)65504f, (Half)1.5f });

        Assert.Equal("[65504,1.5]", vec.GetString());
    }

    /// <summary>
    /// Verifies that a value both base types represent exactly renders identically, so a
    /// caller reading through the string path cannot tell the two apart from the rendering
    /// alone. This is why the column schema exposes the base type separately.
    /// </summary>
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

    /// <summary>
    /// Verifies that a float32 vector renders as a JSON array, the form the server accepts
    /// as a vector literal and the form callers receive from the string read paths.
    /// </summary>
    [Fact]
    public void Float32_GetString_RendersJson()
    {
        Assert.Equal("[1.5,2.5]", new SqlVector<float>(new[] { 1.5f, 2.5f }).GetString());
    }

    /// <summary>
    /// Verifies that a null vector renders as the null string rather than an empty JSON
    /// array, so a null is not mistaken for a zero length vector.
    /// </summary>
    [Fact]
    public void GetString_Null_RendersNullString()
    {
        Assert.Equal(SQLMessage.NullString(), SqlVector<float>.CreateNull(3).GetString());
    }

    #endregion

    #region Widening Read Tests

    /// <summary>
    /// Verifies that a float16 payload is widened when read as <c>SqlVector&lt;float&gt;</c>,
    /// which is how a float16 column is read on frameworks without <c>System.Half</c>. The
    /// result reports float32, so a vector's base type stays determined by its element type
    /// rather than by the payload it came from.
    /// </summary>
    [Fact]
    public void FromTdsPayload_WidensFloat16ToFloat32()
    {
        // This is how a float16 column is read on frameworks without System.Half.
        byte[] payload = MakeFloat16Payload(new ushort[] { 0x3E00, 0x4100, 0x4300 });

        var vec = SqlVector<float>.FromTdsPayload(payload);

        Assert.Equal(3, vec.Length);
        Assert.Equal(new[] { 1.5f, 2.5f, 3.5f }, vec.Memory.ToArray());

        // The widened vector reports float32, so its base type continues to be determined
        // by its element type alone rather than by the payload it was read from.
        Assert.Equal(0x00, ((ISqlVector)vec).ElementType);
    }

    /// <summary>
    /// Verifies that a payload whose base type already matches the requested element type is
    /// read as it is, confirming the widening path above is taken only when it is needed.
    /// </summary>
    [Fact]
    public void FromTdsPayload_MatchingElementType_ReadsDirectly()
    {
        byte[] payload = ((ISqlVector)new SqlVector<float>(new[] { 1.5f, 2.5f })).VectorPayload;

        var vec = SqlVector<float>.FromTdsPayload(payload);

        Assert.Equal(new[] { 1.5f, 2.5f }, vec.Memory.ToArray());
    }

    #if NET

    /// <summary>
    /// Verifies that reading a float32 payload as <c>SqlVector&lt;Half&gt;</c> throws rather
    /// than narrowing. Narrowing loses information, so it is never performed implicitly on a
    /// read, unlike the widening case above.
    /// </summary>
    [Fact]
    public void FromTdsPayload_NarrowingIsRejected()
    {
        // Narrowing loses information, so it is never performed implicitly on a read.
        byte[] payload = ((ISqlVector)new SqlVector<float>(new[] { 1.5f })).VectorPayload;

        Assert.Throws<NotSupportedException>(() => SqlVector<Half>.FromTdsPayload(payload));
    }

    #endif

    /// <summary>
    /// Verifies that a float16 column wider than the float32 element limit can still be
    /// read as <c>SqlVector&lt;float&gt;</c>. A float16 vector may declare up to 3996
    /// dimensions, while 1998 is the most that can be sent as float32, and that limit
    /// governs what a caller constructs rather than what the server sends. Rejecting these
    /// would make such a column unreadable on .NET Framework by any means, since every read
    /// path there widens to float32.
    /// </summary>
    [Theory]
    [InlineData(1999)]
    [InlineData(2000)]
    [InlineData(3996)]
    public void FromTdsPayload_WidensBeyondTheFloat32ElementLimit(int elementCount)
    {
        byte[] payload = MakeFloat16Payload(new ushort[elementCount]);

        var vec = SqlVector<float>.FromTdsPayload(payload);

        Assert.Equal(elementCount, vec.Length);
    }

    /// <summary>
    /// Verifies the same for a null value, which is materialised from the column's declared
    /// dimension count rather than from a payload. A null row must not fail where a
    /// populated row in the same column succeeds.
    /// </summary>
    [Theory]
    [InlineData(1999)]
    [InlineData(3996)]
    public void CreateNullFromServer_AllowsWideFloat16Columns(int elementCount)
    {
        var vec = SqlVector<float>.CreateNullFromServer(elementCount);

        Assert.True(vec.IsNull);
        Assert.Equal(elementCount, vec.Length);

        // The public entry point still holds a caller to what can be sent as float32.
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlVector<float>.CreateNull(elementCount));
    }

    #endregion

    /// <summary>
    /// Keeps JSON coercion within the established float32 payload limit; float16 text
    /// writes must not bypass that limit by sharing an unchecked intermediate constructor.
    /// </summary>
    [Theory]
    [InlineData(1998)]
    [InlineData(1999)]
    public void JsonCoercionHonorsFloat32DimensionLimit(int dimensions)
    {
        string json = "[" + string.Join(",", new int[dimensions]) + "]";
        MetaType type = MetaType.GetMetaTypeFromSqlDbType(SqlDbTypeExtensions.Vector, false);
        if (dimensions > 1998)
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                SqlParameter.CoerceValue(json, type, out _, out _, false));
            Assert.IsType<ArgumentOutOfRangeException>(error.InnerException);
        }
        else
        {
            byte[] payload = Assert.IsType<byte[]>(
                SqlParameter.CoerceValue(json, type, out bool dataFeed, out bool changed, false));
            Assert.False(dataFeed);
            Assert.True(changed);
            Assert.Equal(8000, payload.Length);
            Assert.Equal((byte)MetaType.SqlVectorElementType.Float32, payload[4]);
        }
    }

    /// <summary>
    /// Keeps the vector-owned JSON parser on the existing float32 representation.
    /// </summary>
    [Fact]
    public void ParseFloat32Json()
    {
        SqlVector<float> vector = SqlVector<float>.Parse("[1.5,2.5]");
        Assert.Equal(new[] { 1.5f, 2.5f }, vector.Memory.ToArray());
        Assert.Equal((byte)MetaType.SqlVectorElementType.Float32, ((ISqlVector)vector).ElementType);
    }

    /// <summary>
    /// Preserves the existing runtime's direct float32 JSON rounding rather than introducing
    /// the distinct double-to-float32 parsing used by server-side float16 JSON conversion.
    /// </summary>
    [Fact]
    public void ParseFloat32JsonPreservesExistingRounding()
    {
        const string Json = "[1.0004883408546447753906250001]";
        float expected = JsonSerializer.Deserialize(
            Json, SqlClientJsonSerializerContext.Default.SingleArray)![0];
        float actual = SqlVector<float>.Parse(Json).Memory.Span[0];
        Assert.Equal(
            BitConverter.ToInt32(BitConverter.GetBytes(expected), 0),
            BitConverter.ToInt32(BitConverter.GetBytes(actual), 0));
    }

    /// <summary>
    /// Preserves the existing invalid-JSON wrapper and the original JSON exception.
    /// </summary>
    [Fact]
    public void ParseFloat32JsonPreservesErrorWrapping()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => SqlVector<float>.Parse("[not-json]"));
        Assert.IsType<JsonException>(exception.InnerException);
    }

    /// <summary>
    /// Keeps JSON null's existing empty-memory constructor behavior.
    /// </summary>
    [Fact]
    public void ParseFloat32JsonNullPreservesConstructorBehavior()
    {
        SqlVector<float> expected = new(ReadOnlyMemory<float>.Empty);
        SqlVector<float> actual = SqlVector<float>.Parse("null");
        Assert.Equal(expected.IsNull, actual.IsNull);
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(((ISqlVector)expected).VectorPayload, ((ISqlVector)actual).VectorPayload);
    }

    #if NET
    /// <summary>
    /// Does not introduce a client-side float16 JSON conversion through the new parser.
    /// </summary>
    [Fact]
    public void ParseFloat16JsonIsNotIntroduced()
    {
        Assert.Throws<NotSupportedException>(() => SqlVector<Half>.Parse("[1.5]"));
    }
    #endif

    /// <summary>
    /// Keeps the bulk-reader fallback from changing ordinary text parameter coercion.
    /// </summary>
    /// <param name="destination">The text parameter type.</param>
    [Theory]
    [InlineData(SqlDbType.VarChar)]
    [InlineData(SqlDbType.NVarChar)]
    [InlineData(SqlDbTypeExtensions.Json)]
    public void TextParameterCoercionDoesNotSerializeVectors(SqlDbType destination)
    {
        MetaType type = MetaType.GetMetaTypeFromSqlDbType(destination, false);
        Assert.Throws<InvalidCastException>(() => SqlParameter.CoerceValue(
            new SqlVector<float>(new[] { 1.5f }), type, out _, out _, false));
        #if NET
        Assert.Throws<InvalidCastException>(() => SqlParameter.CoerceValue(
            new SqlVector<Half>(new[] { (Half)1.5f }), type, out _, out _, false));
        #endif
    }

    #region Helpers

    /// <summary>
    /// Builds a float32 vector payload from a header and its elements.
    /// </summary>
    /// <param name="header">The payload header, which may be deliberately malformed.</param>
    /// <param name="values">The elements to append after the header.</param>
    /// <returns>The assembled payload.</returns>
    private byte[] MakeTdsPayload(byte[] header, ReadOnlyMemory<float> values) =>
        MakeTdsPayloadStatic(header, values);

    /// <summary>
    /// The static form of <see cref="MakeTdsPayload"/>, for callers which have no instance.
    /// </summary>
    /// <param name="header">The payload header, which may be deliberately malformed.</param>
    /// <param name="values">The elements to append after the header.</param>
    /// <returns>The assembled payload.</returns>
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
    /// <param name="values">The known binary16 element encodings.</param>
    /// <returns>The assembled float16 payload.</returns>
    private static byte[] MakeFloat16Payload(ushort[] values)
    {
        byte[] payload = new byte[TdsEnums.VECTOR_HEADER_SIZE + (values.Length * 2)];

        payload[0] = 0xA9;
        payload[1] = 0x01;
        BitConverter.GetBytes((ushort)values.Length).CopyTo(payload, 2);
        payload[4] = (byte)MetaType.SqlVectorElementType.Float16;

        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i])
                .CopyTo(payload, TdsEnums.VECTOR_HEADER_SIZE + (i * 2));
        }

        return payload;
    }

    #endregion
}
