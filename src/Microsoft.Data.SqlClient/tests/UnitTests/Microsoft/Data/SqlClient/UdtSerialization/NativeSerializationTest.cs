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
public sealed class NativeSerializationTest : IDisposable
{
    private readonly MemoryStream _stream;

    /// <summary>
    /// Initializes the MemoryStream used for all tests in this class.
    /// </summary>
    public NativeSerializationTest()
    {
        _stream = new MemoryStream();
    }

    void IDisposable.Dispose()
    {
        _stream.Dispose();
    }

    /// <summary>
    /// Provides a collection of test data representing non-null primitive type values and their corresponding
    /// serialized byte arrays.
    /// </summary>
    /// <see cref="Serialize_PrimitiveType_Roundtrips"/>
    public static TheoryData<object, byte[]> SerializedNonNullPrimitiveTypeValues() =>
        new()
        {
            {
                new BoolWrapperStruct { Field1 = true },
                new byte[] { 0x01 }
            },
            {
                new ByteWrapperStruct { Field1 = 0x20 },
                new byte[] { 0x20 }
            },
            {
                new SByteWrapperStruct { Field1 = -0x1 },
                new byte[] { 0x7F }
            },
            {
                new UShortWrapperStruct { Field1 = 0x8000 },
                new byte[] { 0x80, 0x00 }
            },
            {
                new ShortWrapperStruct { Field1 = 0x1234 },
                new byte[] { 0x92, 0x34 }
            },
            {
                new UIntWrapperStruct { Field1 = 0xFFFFFFFF },
                new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }
            },
            {
                new IntWrapperStruct { Field1 = -0x12345678 },
                new byte[] { 0x6D, 0xCB, 0xA9, 0x88 }
            },
            {
                new ULongWrapperStruct { Field1 = ulong.MaxValue },
                new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }
            },
            {
                new LongWrapperStruct { Field1 = long.MinValue },
                new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            },
            {
                new FloatWrapperStruct { Field1 = -0 },
                new byte[] { 0x80, 0x00, 0x00, 0x00 }
            },
            {
                new DoubleWrapperStruct { Field1 = Math.PI },
                new byte[] { 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18 }
            },
            {
                new SqlByteWrapperStruct { Field1 = 0x20 },
                new byte[] { 0x01, 0x20 }
            },
            {
                new SqlInt16WrapperStruct { Field1 = 0x1234 },
                new byte[] { 0x01, 0x92, 0x34 }
            },
            {
                new SqlInt32WrapperStruct { Field1 = -0x12345678 },
                new byte[] { 0x01, 0x6D, 0xCB, 0xA9, 0x88 }
            },
            {
                new SqlInt64WrapperStruct { Field1 = long.MinValue },
                new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            },
            {
                new SqlBooleanWrapperStruct { Field1 = false },
                new byte[] { 0x01 }
            },
            {
                new SqlSingleWrapperStruct { Field1 = -1 },
                new byte[] { 0x01, 0x40, 0x7F, 0xFF, 0xFF }
            },
            {
                new SqlDoubleWrapperStruct { Field1 = -Math.PI },
                new byte[] { 0x01, 0x3F, 0xF6, 0xDE, 0x04, 0xAB, 0xBB, 0xD2, 0xE7 }
            },
            {
                new SqlDateTimeWrapperStruct { Field1 = new DateTime(2000, 1, 1, 12, 34, 56, 500) },
                new byte[] { 0x01, 0x80, 0x00, 0x8E, 0xAC, 0x80, 0xCF, 0x59, 0xD6 }
            },
            {
                new SqlMoneyWrapperStruct { Field1 = 1.10m },
                new byte[] { 0x01, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A, 0xF8 }
            }
        };

    /// <summary>
    /// Provides a collection of test data representing serialized values of nested non-null primitive types.
    /// </summary>
    /// <see cref="Serialize_NestedPrimitiveType_Roundtrips"/>
    public static TheoryData<object, byte[]> SerializedNestedNonNullPrimitiveTypeValues() =>
        new()
        {
            {
                new NestedBoolWrapperStruct { Field1 = true, Field2 = new BoolWrapperStruct { Field1 = false } },
                new byte[]
                {
                    // Field1
                    0x01,
                    // Field2
                    0x00
                }
            },
            {
                new NestedByteWrapperStruct { Field1 = 0x20, Field2 = new ByteWrapperStruct { Field1 = 0x30 } },
                new byte[]
                {
                    // Field1
                    0x20,
                    // Field2
                    0x30
                }
            },
            {
                new NestedSByteWrapperStruct { Field1 = -0x01, Field2 = new SByteWrapperStruct { Field1 = 0x01 } },
                new byte[]
                {
                    // Field1
                    0x7F,
                    // Field2
                    0x81
                }
            },
            {
                new NestedUShortWrapperStruct { Field1 = 0x8000, Field2 = new UShortWrapperStruct { Field1 = 0x8014 } },
                new byte[]
                {
                    // Field1
                    0x80, 0x00,
                    // Field2.Field1
                    0x80, 0x14
                }
            },
            {
                new NestedShortWrapperStruct { Field1 = 0x1234, Field2 = new ShortWrapperStruct { Field1 = 0x4321 } },
                new byte[]
                {
                    // Field1
                    0x92, 0x34,
                    // Field2.Field1
                    0xC3, 0x21
                }
            },
            {
                new NestedUIntWrapperStruct { Field1 = 0xFFFFFFFF, Field2 = new UIntWrapperStruct { Field1 = 0x00000000 } },
                new byte[]
                {
                    // Field1
                    0xFF, 0xFF, 0xFF, 0xFF,
                    // Field2.Field1
                    0x00, 0x00, 0x00, 0x00
                }
            },
            {
                new NestedIntWrapperStruct { Field1 = -0x12345678, Field2 = new IntWrapperStruct { Field1 = 0x12345678 } },
                new byte[]
                {
                    /// Field1
                    0x6D, 0xCB, 0xA9, 0x88,
                    // Field2.Field1
                    0x92, 0x34, 0x56, 0x78
                }
            },
            {
                new NestedULongWrapperStruct { Field1 = ulong.MaxValue, Field2 = new ULongWrapperStruct { Field1 = long.MaxValue } },
                new byte[]
                {
                    // Field1
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                    // Field2.Field1
                    0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                }
            },
            {
                new NestedLongWrapperStruct { Field1 = long.MinValue, Field2 = new LongWrapperStruct { Field1 = long.MaxValue } },
                new byte[]
                {
                    // Field1
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    // Field2.Field1
                    0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                }
            },
            {
                new NestedFloatWrapperStruct { Field1 = -0, Field2 = new FloatWrapperStruct { Field1 = +0 } },
                new byte[]
                {
                    // Field1
                    0x80, 0x00, 0x00, 0x00,
                    // Field2.Field1
                    0x80, 0x00, 0x00, 0x00
                }
            },
            {
                new NestedDoubleWrapperStruct { Field1 = Math.PI, Field2 = new DoubleWrapperStruct { Field1 = Math.PI } },
                new byte[]
                {
                    // Field1
                    0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18,
                    // Field2.Field1
                    0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18
                }
            },
            {
                new NestedSqlByteWrapperStruct { Field1 = 0x20, Field2 = new SqlByteWrapperStruct { Field1 = 0x30 } },
                new byte[]
                {
                    // Field1
                    0x01, 0x20,
                    // Field2.Field1
                    0x01, 0x30
                }
            },
            {
                new NestedSqlInt16WrapperStruct { Field1 = 0x1234, Field2 = new SqlInt16WrapperStruct { Field1 = 0x4321 } },
                new byte[]
                {
                    // Field1
                    0x01, 0x92, 0x34,
                    // Field2.Field1
                    0x01, 0xC3, 0x21
                }
            },
            {
                new NestedSqlInt32WrapperStruct { Field1 = -0x12345678, Field2 = new SqlInt32WrapperStruct { Field1 = 0x12345678 } },
                new byte[]
                {
                    // Field1
                    0x01, 0x6D, 0xCB, 0xA9, 0x88,
                    // Field2.Field1
                    0x01, 0x92, 0x34, 0x56, 0x78
                }
            },
            {
                new NestedSqlInt64WrapperStruct { Field1 = long.MinValue, Field2 = new SqlInt64WrapperStruct { Field1 = long.MaxValue } },
                new byte[]
                {
                    // Field1
                    0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    // Field2.Field1
                    0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
                }
            },
            {
                new NestedSqlBooleanWrapperStruct { Field1 = false, Field2 = new SqlBooleanWrapperStruct { Field1 = true } },
                new byte[]
                {
                    // Field1
                    0x01,
                    // Field2.Field1
                    0x02
                }
            },
            {
                new NestedSqlSingleWrapperStruct { Field1 = -0, Field2 = new SqlSingleWrapperStruct { Field1 = +0 } },
                new byte[]
                {
                    // Field1
                    0x01, 0x80, 0x00, 0x00, 0x00,
                    // Field2.Field1
                    0x01, 0x80, 0x00, 0x00, 0x00
                }
            },
            {
                new NestedSqlDoubleWrapperStruct { Field1 = Math.PI, Field2 = new SqlDoubleWrapperStruct { Field1 = Math.PI } },
                new byte[]
                {
                    // Field1
                    0x01, 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18,
                    // Field2.Field1
                    0x01, 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18
                }
            },
            {
                new NestedSqlDateTimeWrapperStruct { Field1 = new DateTime(2000, 1, 1, 12, 34, 56, 500), Field2 = new SqlDateTimeWrapperStruct { Field1 = new DateTime(2000, 1, 1) } },
                new byte[]
                {
                    // Field1
                    0x01, 0x80, 0x00, 0x8E, 0xAC, 0x80, 0xCF, 0x59, 0xD6,
                    // Field2.Field1
                    0x01, 0x80, 0x00, 0x8E, 0xAC, 0x80, 0x00, 0x00, 0x00
                }
            },
            {
                new NestedSqlMoneyWrapperStruct { Field1 = 1.10m, Field2 = new SqlMoneyWrapperStruct { Field1 = -2.55m } },
                new byte[]
                {
                    // Field1
                    0x01, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A, 0xF8,
                    // Field2.Field1
                    0x01, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x9C, 0x64
                }
            }
        };

    /// <summary>
    /// Provides a collection of test data representing serialized null values for various primitive types.
    /// </summary>
    /// <see cref="Serialize_NullPrimitiveType_Roundtrips"/>
    public static TheoryData<object, byte[]> SerializedNullPrimitiveTypeValues() =>
        new()
        {
            {
                new SqlByteWrapperStruct { Field1 = SqlByte.Null },
                new byte[] { 0x00, 0x00 }
            },
            {
                new SqlInt16WrapperStruct { Field1 = SqlInt16.Null },
                new byte[] { 0x00, 0x80, 0x00 }
            },
            {
                new SqlInt32WrapperStruct { Field1 = SqlInt32.Null },
                new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00 }
            },
            {
                new SqlInt64WrapperStruct { Field1 = SqlInt64.Null },
                new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            },
            {
                new SqlBooleanWrapperStruct { Field1 = SqlBoolean.Null },
                new byte[] { 0x00 }
            },
            {
                new SqlSingleWrapperStruct { Field1 = SqlSingle.Null },
                new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00 }
            },
            {
                new SqlDoubleWrapperStruct { Field1 = SqlDouble.Null },
                new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            },
            {
                new SqlDateTimeWrapperStruct { Field1 = SqlDateTime.Null },
                new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00 }
            },
            {
                new SqlMoneyWrapperStruct { Field1 = SqlMoney.Null },
                new byte[] { 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
            }
        };

    /// <summary>
    /// Attempts to serialize various structs containing non-null primitive types.
    /// Verifies that the method does not throw, that serialized byte output is correct, and that the value round-trips.
    /// </summary>
    /// <param name="primitive">Primitive to serialize and to compare against.</param>
    /// <param name="expectedValue">Expected byte output.</param>
    [Theory]
    [MemberData(
        nameof(SerializedNonNullPrimitiveTypeValues),
        DisableDiscoveryEnumeration = true)]
    public void Serialize_PrimitiveType_Roundtrips(object primitive, byte[] expectedValue) =>
        RoundtripType(primitive, expectedValue);

    /// <summary>
    /// Attempts to serialize a nested struct hierarchy containing non-null primitive types.
    /// Verifies that the method does not throw, that serialized byte output is correct, and that the value round-trips.
    /// </summary>
    /// <param name="primitive">Primitive to serialize and to compare against.</param>
    /// <param name="expectedValue">Expected byte output.</param>
    [Theory]
    [MemberData(
        nameof(SerializedNestedNonNullPrimitiveTypeValues),
        DisableDiscoveryEnumeration = true)]
    public void Serialize_NestedPrimitiveType_Roundtrips(object primitive, byte[] expectedValue) =>
        RoundtripType(primitive, expectedValue);

    /// <summary>
    /// Attempts to serialize various structs containing null-valued primitive types.
    /// Verifies that the method does not throw, that serialized byte output is correct, and that the value round-trips.
    /// </summary>
    /// <param name="primitive">Primitive to serialize and to compare against.</param>
    /// <param name="expectedValue">Expected byte output.</param>
    [Theory]
    [MemberData(
        nameof(SerializedNullPrimitiveTypeValues),
        DisableDiscoveryEnumeration = true)]
    public void Serialize_NullPrimitiveType_Roundtrips(object primitive, byte[] expectedValue) =>
        RoundtripType(primitive, expectedValue);

    /// <summary>
    /// Attempts to serialize an instance of a class.
    /// </summary>
    /// <seealso cref="Serialize_NestedClass_Throws"/>
    [Fact]
    public void Serialize_TopLevelClass_Succeeds()
    {
        NestedBoolWrapperClass validWrapper = new()
        {
            Field1 = true,
            Field2 = new BoolWrapperStruct() { Field1 = true }
        };

        SerializationHelperSql9.Serialize(_stream, validWrapper);
    }

    /// <summary>
    /// Attempts to serialize a field referring to an instance of a class.
    /// Verifies that this fails, and that Native format serialization only operates with primitive types and value types containing these.
    /// </summary>
    /// <seealso cref="Serialize_TopLevelClass_Succeeds"/>
    [Fact]
    public void Serialize_NestedClass_Throws()
    {
        InvalidNestedBoolWrapperClass invalidWrapper = new()
        {
            Field1 = true,
            Field2 = new BoolWrapperClass() { Field1 = true }
        };

        var ex = Assert.Throws<Exception>(() => SerializationHelperSql9.Serialize(_stream, invalidWrapper));
        string expectedException = StringsHelper.GetString(Strings.SQL_CannotCreateNormalizer, invalidWrapper.Field2.GetType().FullName);

        Assert.Equal(expectedException, ex.Message);
    }

    /// <summary>
    /// Attempts to serialize a struct containing non-primitive value types.
    /// Verifies that this fails.
    /// </summary>
    [Fact]
    public void Serialize_NonPrimitiveType_Throws()
    {
        InvalidIntPtrAndByteWrapperStruct invalidWrapper = new()
        {
            Field1 = 1,
            Field2 = IntPtr.Zero
        };

        var ex = Assert.Throws<Exception>(() => SerializationHelperSql9.Serialize(_stream, invalidWrapper));
        string expectedException = StringsHelper.GetString(Strings.SQL_CannotCreateNormalizer, invalidWrapper.Field2.GetType().FullName);

        Assert.Equal(expectedException, ex.Message);
    }

    /// <summary>
    /// Serializes an object, verifies the value and the size of the object, then roundtrips it and verifies the result is identical.
    /// </summary>
    /// <param name="inputValue">Object to serialize.</param>
    /// <param name="expectedValue">Expected serialization output.</param>
    private void RoundtripType(object inputValue, byte[] expectedValue)
    {
        int typeSize = SerializationHelperSql9.SizeInBytes(inputValue.GetType());
        int objectSize = SerializationHelperSql9.SizeInBytes(inputValue);
        int maxTypeSize = SerializationHelperSql9.GetUdtMaxLength(inputValue.GetType());

        SerializationHelperSql9.Serialize(_stream, inputValue);
        _stream.Seek(0, SeekOrigin.Begin);
        object readPrimitive = SerializationHelperSql9.Deserialize(_stream, inputValue.GetType());

        // For native formatting, the type size, the object size and the maximum object size will always be identical
        Assert.Equal(typeSize, objectSize);
        Assert.Equal(expectedValue.Length, typeSize);
        Assert.Equal(typeSize, maxTypeSize);

        Assert.Equal(expectedValue, _stream.ToArray());
        Assert.Equal(inputValue, readPrimitive);
    }
}
