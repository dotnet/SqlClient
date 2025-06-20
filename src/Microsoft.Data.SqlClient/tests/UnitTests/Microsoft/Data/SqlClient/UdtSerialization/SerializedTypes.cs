// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.SqlServer.Server;
using System;
using System.Data.SqlTypes;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient.UnitTests.UdtSerialization.SerializedTypes;

// Simple cases: a struct containing one of the designated primitive types
[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct BoolWrapperStruct { public bool Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct ByteWrapperStruct { public byte Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SByteWrapperStruct { public sbyte Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct UShortWrapperStruct { public ushort Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct ShortWrapperStruct { public short Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct UIntWrapperStruct { public uint Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct IntWrapperStruct { public int Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct ULongWrapperStruct { public ulong Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct LongWrapperStruct { public long Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct FloatWrapperStruct { public float Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct DoubleWrapperStruct { public double Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlByteWrapperStruct { public SqlByte Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlInt16WrapperStruct { public SqlInt16 Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlInt32WrapperStruct { public SqlInt32 Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlInt64WrapperStruct { public SqlInt64 Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlBooleanWrapperStruct { public SqlBoolean Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlSingleWrapperStruct { public SqlSingle Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlDoubleWrapperStruct { public SqlDouble Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlDateTimeWrapperStruct { public SqlDateTime Field1; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct SqlMoneyWrapperStruct { public SqlMoney Field1; }


// Success case: a class containing one of the designated primitive types
[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public class BoolWrapperClass { public bool Field1; }

// Success case: a struct containing one designated primitive type and one nested struct
[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedBoolWrapperStruct { public bool Field1; public BoolWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedByteWrapperStruct { public byte Field1; public ByteWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSByteWrapperStruct { public sbyte Field1; public SByteWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedUShortWrapperStruct { public ushort Field1; public UShortWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedShortWrapperStruct { public short Field1; public ShortWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedUIntWrapperStruct { public uint Field1; public UIntWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedIntWrapperStruct { public int Field1; public IntWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedULongWrapperStruct { public ulong Field1; public ULongWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedLongWrapperStruct { public long Field1; public LongWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedFloatWrapperStruct { public float Field1; public FloatWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedDoubleWrapperStruct { public double Field1; public DoubleWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlByteWrapperStruct { public SqlByte Field1; public SqlByteWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlInt16WrapperStruct { public SqlInt16 Field1; public SqlInt16WrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlInt32WrapperStruct { public SqlInt32 Field1; public SqlInt32WrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlInt64WrapperStruct { public SqlInt64 Field1; public SqlInt64WrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlBooleanWrapperStruct { public SqlBoolean Field1; public SqlBooleanWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlSingleWrapperStruct { public SqlSingle Field1; public SqlSingleWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlDoubleWrapperStruct { public SqlDouble Field1; public SqlDoubleWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlDateTimeWrapperStruct { public SqlDateTime Field1; public SqlDateTimeWrapperStruct Field2; }

[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct NestedSqlMoneyWrapperStruct { public SqlMoney Field1; public SqlMoneyWrapperStruct Field2; }


// Success case: a class containing one designated primitive type and a nested struct
[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public class NestedBoolWrapperClass { public bool Field1; public BoolWrapperStruct Field2; }

// Failure case: a struct or a class containing one designated primitive type and a nested class
[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public class InvalidNestedBoolWrapperClass { public bool Field1; public BoolWrapperClass Field2; }

// Failure case: a struct or a class containing a field which is not a designated primitive type
[SqlUserDefinedType(Format.Native)]
[StructLayout(LayoutKind.Sequential)]
public struct InvalidIntPtrAndByteWrapperStruct { public byte Field1; public IntPtr Field2; }

// Success case: a struct or a class implementing IBinarySerialize which would not otherwise be serializable
public interface IFormattingProgress
{
    bool ParameterlessConstructorInvoked { get; }
    bool ReadInvoked { get; }
    bool WriteInvoked { get; }
}

[SqlUserDefinedType(Format.UserDefined, MaxByteSize = 11)]
public struct UserDefinedFormattedStruct : IBinarySerialize, IFormattingProgress, IEquatable<UserDefinedFormattedStruct>
{
    public IntPtr Field1;
    public bool ParameterlessConstructorInvoked { get; }
    public bool ReadInvoked { get; private set; }
    public bool WriteInvoked { get; private set; }

    public UserDefinedFormattedStruct()
    {
        ParameterlessConstructorInvoked = true;
    }

    public UserDefinedFormattedStruct(IntPtr field1)
    {
        Field1 = field1;
    }

    public void Read(BinaryReader r)
    {
        Field1 = IntPtr.Size switch
        {
            sizeof(uint) => (IntPtr)r.ReadUInt32(),
            sizeof(ulong) => (IntPtr)r.ReadUInt64(),
            _ => throw new Exception("Invalid IntPtr size")
        };

        ReadInvoked = true;
    }

    public void Write(BinaryWriter w)
    {
        if (IntPtr.Size == sizeof(uint))
        {
            w.Write((uint)Field1);
        }
        else if (IntPtr.Size == sizeof(ulong))
        {
            w.Write((ulong)Field1);
        }
        else
        {
            throw new Exception("Invalid IntPtr size");
        }

        WriteInvoked = true;
    }

    public bool Equals(UserDefinedFormattedStruct other)
        => other.Field1 == Field1;
}

[SqlUserDefinedType(Format.UserDefined, MaxByteSize = 11)]
public class UserDefinedFormattedClass : IBinarySerialize, IFormattingProgress, IEquatable<UserDefinedFormattedClass>
{
    public IntPtr Field1;
    public bool ParameterlessConstructorInvoked { get; }
    public bool ReadInvoked { get; private set; }
    public bool WriteInvoked { get; private set; }

    public UserDefinedFormattedClass()
    {
        ParameterlessConstructorInvoked = true;
    }

    public UserDefinedFormattedClass(IntPtr field1)
    {
        Field1 = field1;
    }

    public void Read(BinaryReader r)
    {
        Field1 = IntPtr.Size switch
        {
            sizeof(uint) => (IntPtr)r.ReadUInt32(),
            sizeof(ulong) => (IntPtr)r.ReadUInt64(),
            _ => throw new Exception("Invalid IntPtr size")
        };

        ReadInvoked = true;
    }

    public void Write(BinaryWriter w)
    {
        if (IntPtr.Size == sizeof(uint))
        {
            w.Write((uint)Field1);
        }
        else if (IntPtr.Size == sizeof(ulong))
        {
            w.Write((ulong)Field1);
        }
        else
        {
            throw new Exception("Invalid IntPtr size");
        }

        WriteInvoked = true;
    }

    public bool Equals(UserDefinedFormattedClass other)
        => other is not null && other.Field1 == Field1;
}

// Failure cases: type does not have a public constructor, does not implement IBinarySerialize, does not have a SqlUserDefinedType attribute,
// or has a SqlUserDefinedType attribute with a Format of Unknown.

[SqlUserDefinedType(Format.UserDefined)]
public class UserDefinedMissingPublicConstructor : IBinarySerialize
{
    public UserDefinedMissingPublicConstructor(bool _) { }

    public void Read(BinaryReader r) { }

    public void Write(BinaryWriter w) { }
}

[SqlUserDefinedType(Format.UserDefined)]
public class UserDefinedDoesNotImplementIBinarySerialize
{
    public UserDefinedDoesNotImplementIBinarySerialize() { }
}

public class ClassMissingSqlUserDefinedTypeAttribute
{
}

[SqlUserDefinedType(Format.Unknown)]
public class UnknownFormattedClass
{
}
