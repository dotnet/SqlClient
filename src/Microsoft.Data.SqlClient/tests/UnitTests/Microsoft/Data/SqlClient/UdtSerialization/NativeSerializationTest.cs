// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Server;
using Microsoft.Data.SqlClient.UnitTests.UdtSerialization.SerializedTypes;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.UdtSerialization;

/// <summary>
/// Tests the serialization method defined by MS-SSCLRT. Ensures that combinations of primitives and custom types round-trip.
/// </summary>
/// <seealso href="https://learn.microsoft.com/en-us/openspecs/sql_server_protocols/ms-ssclrt/77460aa9-8c2f-4449-a65e-1d649ebd77fa"/>
public class NativeSerializationTest
{
    /// <summary>
    /// Provides a collection of test data representing non-null primitive type values and their corresponding
    /// serialized byte arrays.
    /// </summary>
    /// <see cref="SerializePrimitiveType"/>
    public static IEnumerable<object[]> SerializedNonNullPrimitiveTypeValues()
    {
        yield return [new BoolWrapperStruct { Field1 = true },
            new byte[] { 0x01 }];
        yield return [new ByteWrapperStruct { Field1 = 0x20 },
            new byte[] { 0x20 }];
        yield return [new SByteWrapperStruct { Field1 = -0x1 },
            new byte[] { 0x7F }];
        yield return [new UShortWrapperStruct { Field1 = 0x8000 },
            new byte[] { 0x80, 0x00 }];
        yield return [new ShortWrapperStruct { Field1 = 0x1234 },
            new byte[] { 0x92, 0x34 }];
        yield return [new UIntWrapperStruct { Field1 = 0xFFFFFFFF },
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }];
        yield return [new IntWrapperStruct { Field1 = -0x12345678 },
            new byte[] { 0x6D, 0xCB, 0xA9, 0x88 }];
        yield return [new ULongWrapperStruct { Field1 = ulong.MaxValue },
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }];
        yield return [new LongWrapperStruct { Field1 = long.MinValue },
            new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }];
        yield return [new FloatWrapperStruct { Field1 = -0 },
            new byte[] { 0x80, 0x00, 0x00, 0x00 }];
        yield return [new DoubleWrapperStruct { Field1 = Math.PI },
            new byte[] { 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18 }];
        yield return [new SqlByteWrapperStruct { Field1 = 0x20 },
            new byte[] { 0x01, 0x20 }];
        yield return [new SqlInt16WrapperStruct { Field1 = 0x1234 },
            new byte[] { 0x01, 0x92, 0x34 }];
        yield return [new SqlInt32WrapperStruct { Field1 = -0x12345678 },
            new byte[] { 0x01, 0x6D, 0xCB, 0xA9, 0x88 }];
        yield return [new SqlInt64WrapperStruct { Field1 = long.MinValue },
            new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }];
        yield return [new SqlBooleanWrapperStruct { Field1 = false },
            new byte[] { 0x01 }];
        yield return [new SqlSingleWrapperStruct { Field1 = -1 },
            new byte[] { 0x01, 0x40, 0x7F, 0xFF, 0xFF }];
        yield return [new SqlDoubleWrapperStruct { Field1 = -Math.PI },
            new byte[] { 0x01, 0x3F, 0xF6, 0xDE, 0x04, 0xAB, 0xBB, 0xD2, 0xE7 }];
        yield return [new SqlDateTimeWrapperStruct { Field1 = new DateTime(2000, 1, 1, 12, 34, 56, 500) },
            new byte[] { 0x01, 0x80, 0x00, 0x8E, 0xAC, 0x80, 0xCF, 0x59, 0xD6 }];
        yield return [new SqlMoneyWrapperStruct { Field1 = 1.10m },
            new byte[] { 0x01, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A, 0xF8 }];
    }

    /// <summary>
    /// Provides a collection of test data representing serialized values of nested non-null primitive types.
    /// </summary>
    /// <see cref="SerializeNestedPrimitiveType"/>
    public static IEnumerable<object[]> SerializedNestedNonNullPrimitiveTypeValues()
    {
        yield return [new NestedBoolWrapperStruct { Field1 = true, Field2 = new BoolWrapperStruct { Field1 = false } },
            new byte[] { 0x01,
                0x00 }];
        yield return [new NestedByteWrapperStruct { Field1 = 0x20, Field2 = new ByteWrapperStruct { Field1 = 0x30 } },
            new byte[] { 0x20,
                0x30 }];
        yield return [new NestedSByteWrapperStruct { Field1 = -0x01, Field2 = new SByteWrapperStruct { Field1 = 0x01 } },
            new byte[] { 0x7F,
                0x81 }];
        yield return [new NestedUShortWrapperStruct { Field1 = 0x8000, Field2 = new UShortWrapperStruct { Field1 = 0x8014 } },
            new byte[] { 0x80, 0x00,
                0x80, 0x14 }];
        yield return [new NestedShortWrapperStruct { Field1 = 0x1234, Field2 = new ShortWrapperStruct { Field1 = 0x4321 } },
            new byte[] { 0x92, 0x34,
                0xC3, 0x21 }];
        yield return [new NestedUIntWrapperStruct { Field1 = 0xFFFFFFFF, Field2 = new UIntWrapperStruct { Field1 = 0x00000000 } },
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF,
                0x00, 0x00, 0x00, 0x00 }];
        yield return [new NestedIntWrapperStruct { Field1 = -0x12345678, Field2 = new IntWrapperStruct { Field1 = 0x12345678 } },
            new byte[] { 0x6D, 0xCB, 0xA9, 0x88,
                0x92, 0x34, 0x56, 0x78 }];
        yield return [new NestedULongWrapperStruct { Field1 = ulong.MaxValue, Field2 = new ULongWrapperStruct { Field1 = long.MaxValue } },
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }];
        yield return [new NestedLongWrapperStruct { Field1 = long.MinValue, Field2 = new LongWrapperStruct { Field1 = long.MaxValue } },
            new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }];
        yield return [new NestedFloatWrapperStruct { Field1 = -0, Field2 = new FloatWrapperStruct { Field1 = +0 } },
            new byte[] { 0x80, 0x00, 0x00, 0x00,
                0x80, 0x00, 0x00, 0x00 }];
        yield return [new NestedDoubleWrapperStruct { Field1 = Math.PI, Field2 = new DoubleWrapperStruct { Field1 = Math.PI } },
            new byte[] { 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18,
                0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18 }];
        yield return [new NestedSqlByteWrapperStruct { Field1 = 0x20, Field2 = new SqlByteWrapperStruct { Field1 = 0x30 } },
            new byte[] { 0x01, 0x20,
                0x01, 0x30 }];
        yield return [new NestedSqlInt16WrapperStruct { Field1 = 0x1234, Field2 = new SqlInt16WrapperStruct { Field1 = 0x4321 } },
            new byte[] { 0x01, 0x92, 0x34,
                0x01, 0xC3, 0x21 }];
        yield return [new NestedSqlInt32WrapperStruct { Field1 = -0x12345678, Field2 = new SqlInt32WrapperStruct { Field1 = 0x12345678 } },
            new byte[] { 0x01, 0x6D, 0xCB, 0xA9, 0x88,
                0x01, 0x92, 0x34, 0x56, 0x78 }];
        yield return [new NestedSqlInt64WrapperStruct { Field1 = long.MinValue, Field2 = new SqlInt64WrapperStruct { Field1 = long.MaxValue } },
            new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }];
        yield return [new NestedSqlBooleanWrapperStruct { Field1 = false, Field2 = new SqlBooleanWrapperStruct { Field1 = true } },
            new byte[] { 0x01,
                0x02 }];
        yield return [new NestedSqlSingleWrapperStruct { Field1 = -0, Field2 = new SqlSingleWrapperStruct { Field1 = +0 } },
            new byte[] { 0x01, 0x80, 0x00, 0x00, 0x00,
                0x01, 0x80, 0x00, 0x00, 0x00 }];
        yield return [new NestedSqlDoubleWrapperStruct { Field1 = Math.PI, Field2 = new SqlDoubleWrapperStruct { Field1 = Math.PI } },
            new byte[] { 0x01, 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18,
                0x01, 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18 }];
        yield return [new NestedSqlDateTimeWrapperStruct { Field1 = new DateTime(2000, 1, 1, 12, 34, 56, 500), Field2 = new SqlDateTimeWrapperStruct { Field1 = new DateTime(2000, 1, 1) } },
            new byte[] { 0x01, 0x80, 0x00, 0x8E, 0xAC, 0x80, 0xCF, 0x59, 0xD6,
                0x01, 0x80, 0x00, 0x8E, 0xAC, 0x80, 0x00, 0x00, 0x00 }];
        yield return [new NestedSqlMoneyWrapperStruct { Field1 = 1.10m, Field2 = new SqlMoneyWrapperStruct { Field1 = -2.55m } },
            new byte[] { 0x01, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A, 0xF8,
                0x01, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x9C, 0x64 }];
    }

    /// <summary>
    /// Provides a collection of test data representing serialized null values for various primitive types.
    /// </summary>
    /// <see cref="SerializeNullPrimitiveType"/>
    public static IEnumerable<object[]> SerializedNullPrimitiveTypeValues()
    {
        yield return [new SqlByteWrapperStruct { Field1 = SqlByte.Null },
            new byte[] { 0x00, 0x00 }];
        yield return [new SqlInt16WrapperStruct { Field1 = SqlInt16.Null },
            new byte[] { 0x00, 0x80, 0x00 }];
        yield return [new SqlInt32WrapperStruct { Field1 = SqlInt32.Null },
            new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00 }];
        yield return [new SqlInt64WrapperStruct { Field1 = SqlInt64.Null },
            new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }];
        yield return [new SqlBooleanWrapperStruct { Field1 = SqlBoolean.Null },
            new byte[] { 0x00 }];
        yield return [new SqlSingleWrapperStruct { Field1 = SqlSingle.Null },
            new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00 }];
        yield return [new SqlDoubleWrapperStruct { Field1 = SqlDouble.Null },
            new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }];
        yield return [new SqlDateTimeWrapperStruct { Field1 = SqlDateTime.Null },
            new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00 }];
        yield return [new SqlMoneyWrapperStruct { Field1 = SqlMoney.Null },
            new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }];
    }

    /// <summary>
    /// Attempts to serialize various structs containing non-null primitive types.
    /// Verifies that the method does not throw, that serialized byte output is correct, and that the value round-trips.
    /// </summary>
    /// <param name="primitive">Primitive to serialize and to compare against.</param>
    /// <param name="expectedValue">Expected byte output.</param>
    [Theory]
    [MemberData(nameof(SerializedNonNullPrimitiveTypeValues))]
    public void SerializePrimitiveType(object primitive, byte[] expectedValue)
        => RoundtripType(primitive, expectedValue);

    /// <summary>
    /// Attempts to serialize a nested struct hierarchy containing non-null primitive types.
    /// Verifies that the method does not throw, that serialized byte output is correct, and that the value round-trips.
    /// </summary>
    /// <param name="primitive">Primitive to serialize and to compare against.</param>
    /// <param name="expectedValue">Expected byte output.</param>
    [Theory]
    [MemberData(nameof(SerializedNestedNonNullPrimitiveTypeValues))]
    public void SerializeNestedPrimitiveType(object primitive, byte[] expectedValue)
        => RoundtripType(primitive, expectedValue);

    /// <summary>
    /// Attempts to serialize various structs containing null-valued primitive types.
    /// Verifies that the method does not throw, that serialized byte output is correct, and that the value round-trips.
    /// </summary>
    /// <param name="primitive">Primitive to serialize and to compare against.</param>
    /// <param name="expectedValue">Expected byte output.</param>
    [Theory]
    [MemberData(nameof(SerializedNullPrimitiveTypeValues))]
    public void SerializeNullPrimitiveType(object primitive, byte[] expectedValue)
        => RoundtripType(primitive, expectedValue);

    /// <summary>
    /// Attempts to serialize an instance of a class.
    /// </summary>
    /// <seealso cref="CannotSerializeNestedClass"/>
    [Fact]
    public void CanSerializeTopLevelClass()
    {
        NestedBoolWrapperClass validWrapper = new()
        {
            Field1 = true,
            Field2 = new BoolWrapperStruct() { Field1 = true }
        };
        using MemoryStream stream = new();

        SerializationHelperSql9.Serialize(stream, validWrapper);
    }

    /// <summary>
    /// Attempts to serialize a field referring to an instance of a class.
    /// Verifies that this fails, and that Native format serialization only operates with primitive types and value types containing these.
    /// </summary>
    /// <seealso cref="CanSerializeTopLevelClass"/>
    [Fact]
    public void CannotSerializeNestedClass()
    {
        InvalidNestedBoolWrapperClass invalidWrapper = new()
        {
            Field1 = true,
            Field2 = new BoolWrapperClass() { Field1 = true }
        };
        using MemoryStream stream = new();

        var ex = Assert.Throws<Exception>(() => SerializationHelperSql9.Serialize(stream, invalidWrapper));
        string expectedException = StringsHelper.GetString(Strings.SQL_CannotCreateNormalizer, invalidWrapper.Field2.GetType().FullName);

        Assert.Equal(expectedException, ex.Message);
    }

    /// <summary>
    /// Attempts to serialize a struct containing non-primitive value types.
    /// Verifies that this fails.
    /// </summary>
    [Fact]
    public void CannotSerializeNonPrimitiveType()
    {
        InvalidIntPtrAndByteWrapperStruct invalidWrapper = new()
        {
            Field1 = 1,
            Field2 = IntPtr.Zero
        };
        using MemoryStream stream = new();

        var ex = Assert.Throws<Exception>(() => SerializationHelperSql9.Serialize(stream, invalidWrapper));
        string expectedException = StringsHelper.GetString(Strings.SQL_CannotCreateNormalizer, invalidWrapper.Field2.GetType().FullName);

        Assert.Equal(expectedException, ex.Message);
    }

    /// <summary>
    /// Serializes an object, verifies the value and the size of the object, then roundtrips it and verifies the result is identical.
    /// </summary>
    /// <param name="inputValue">Object to serialize.</param>
    /// <param name="expectedValue">Expected serialization output.</param>
    private static void RoundtripType(object inputValue, byte[] expectedValue)
    {
        using MemoryStream stream = new();
        object readPrimitive;
        int typeSize = SerializationHelperSql9.SizeInBytes(inputValue.GetType());
        int objectSize = SerializationHelperSql9.SizeInBytes(inputValue);
        int maxTypeSize = SerializationHelperSql9.GetUdtMaxLength(inputValue.GetType());

        SerializationHelperSql9.Serialize(stream, inputValue);
        stream.Seek(0, SeekOrigin.Begin);
        readPrimitive = SerializationHelperSql9.Deserialize(stream, inputValue.GetType());

        // For native formatting, the type size, the object size and the maximum object size will always be identical
        Assert.Equal(typeSize, objectSize);
        Assert.Equal(expectedValue.Length, typeSize);
        Assert.Equal(typeSize, maxTypeSize);

        Assert.Equal(expectedValue, stream.ToArray());
        Assert.Equal(inputValue, readPrimitive);
    }
}
