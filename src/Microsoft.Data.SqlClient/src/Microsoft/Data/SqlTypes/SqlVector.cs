// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Microsoft.Data.SqlTypes;

/// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/SqlVector/*' />
public readonly struct SqlVector<T> : INullable, ISqlVector
where T : unmanaged
{
    #region Constants

    private const byte VecHeaderMagicNo = SqlVectorPayload.HeaderMagicNumber;
    private const byte VecVersionNo = SqlVectorPayload.HeaderVersion;

    // Offsets of the fields within the vector header. Refer to TDS section 2.2.5.5.7.
    private const int VecHeaderLengthOffset = SqlVectorPayload.LengthOffset;
    private const int VecHeaderElementTypeOffset = SqlVectorPayload.ElementTypeOffset;

    #endregion

    #region Fields

    private readonly byte _elementType;
    private readonly byte _elementSize;
    private readonly byte[] _tdsBytes;
    private readonly int _size;

    #endregion

    #region Constructors

    private SqlVector(int length)
    {
        (_elementType, _elementSize, int maxElements) = GetTypeFieldsOrThrow();
        if (length < 0 || length > maxElements)
        {
            throw ADP.InvalidArraySize(nameof(length));
        }

        IsNull = true;

        Length = length;
        _size = TdsEnums.VECTOR_HEADER_SIZE + (_elementSize * Length);

        _tdsBytes = Array.Empty<byte>();
        Memory = new();
    }

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/CreateNull/*' />
    public static SqlVector<T> CreateNull(int length) => new(length);

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/ctor1/*' />
    public SqlVector(ReadOnlyMemory<T> memory)
    {
        (_elementType, _elementSize, int maxElements) = GetTypeFieldsOrThrow();
        if (memory.Length > maxElements)
        {
            throw ADP.InvalidArraySize(nameof(memory));
        }

        IsNull = false;

        Length = memory.Length;
        _size = TdsEnums.VECTOR_HEADER_SIZE + (_elementSize * Length);

        _tdsBytes = MakeTdsBytes(memory);
        Memory = memory;
    }

    internal SqlVector(byte[] tdsBytes)
    {
        (_elementType, _elementSize, _) = GetTypeFieldsOrThrow();

        (Length, _size) = GetCountsOrThrow(tdsBytes);

        IsNull = false;

        _tdsBytes = tdsBytes;
        Memory = new(MakeArray());
    }

    #endregion

    #region Methods

    internal string GetString()
    {
        if (IsNull)
        {
            return SQLMessage.NullString();
        }

        #if NET
        if (typeof(T) == typeof(Half))
        {
            // Widening binary16 to binary32 is exact, so serialising the widened values
            // renders the true value of every element. Serialising Half directly would
            // instead produce the shortest string that round-trips to the same Half,
            // which can misrepresent the value: 65504 would render as "65500". Widening
            // also keeps this rendering identical on .NET Framework, where System.Half
            // is unavailable and float16 vectors are surfaced as single precision.
            ReadOnlySpan<Half> elements = ((ReadOnlyMemory<Half>)(object)Memory).Span;
            float[] widened = new float[elements.Length];

            for (int i = 0; i < elements.Length; i++)
            {
                widened[i] = (float)elements[i];
            }

            return JsonSerializer.Serialize(widened);
        }
        #endif

        return JsonSerializer.Serialize(Memory);
    }

    /// <summary>
    /// Creates a vector from a TDS payload, converting the elements when the payload's
    /// base type differs from <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only widening conversions are performed, because they are always exact. A
    /// <c>float16</c> payload can therefore be read as a <see cref="SqlVector{T}"/> of
    /// <see cref="float"/>, which is the only way for .NET Framework callers to read
    /// such a column in a strongly typed form, as <c>System.Half</c> is unavailable there.
    /// </para>
    /// <para>
    /// The converted vector carries a payload rebuilt for <typeparamref name="T"/>, so
    /// that <typeparamref name="T"/> alone continues to determine the base type used when
    /// the value is sent back to the server.
    /// </para>
    /// </remarks>
    internal static SqlVector<T> FromTdsPayload(byte[] tdsBytes)
    {
        ThrowIfHeaderInvalid(tdsBytes);
        ThrowIfNotConvertibleFrom(tdsBytes[VecHeaderElementTypeOffset]);

        if (tdsBytes[VecHeaderElementTypeOffset] == ElementTypeOf())
        {
            return new SqlVector<T>(tdsBytes);
        }

        return new SqlVector<T>(WidenFloat16Payload(tdsBytes));
    }

    /// <summary>
    /// Throws if a payload with the given base type cannot be surfaced as a vector of
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Whether a payload can be read as a given element type is a property of the column's
    /// base type, so this is checked before a value is examined. A null value would
    /// otherwise appear to succeed where a populated one in the same column fails.
    /// </remarks>
    internal static void ThrowIfNotConvertibleFrom(byte payloadElementType)
    {
        byte targetElementType = ElementTypeOf();

        if (payloadElementType == targetElementType)
        {
            return;
        }

        // Widening is exact, so it is performed implicitly. Narrowing is lossy and never is.
        if (payloadElementType == (byte)MetaType.SqlVectorElementType.Float16 &&
            targetElementType == (byte)MetaType.SqlVectorElementType.Float32)
        {
            return;
        }

        throw SQL.VectorTypeNotSupported(typeof(T).FullName);
    }

    /// <summary>
    /// Validates the fields of a vector header which do not depend on the base type.
    /// </summary>
    private static void ThrowIfHeaderInvalid(byte[] tdsBytes) =>
        SqlVectorPayload.ThrowIfHeaderInvalid(tdsBytes);

    /// <summary>
    /// Returns the vector base type which corresponds to <typeparamref name="T"/>.
    /// </summary>
    private static byte ElementTypeOf()
    {
        (byte elementType, _, _) = GetTypeFieldsOrThrow();

        return elementType;
    }

    /// <summary>
    /// Widens the <c>float16</c> elements of a TDS payload to single precision values.
    /// </summary>
    private static ReadOnlyMemory<T> WidenFloat16Payload(byte[] tdsBytes)
    {
        const int Float16ElementSize = 2;

        int length = BinaryPrimitives.ReadUInt16LittleEndian(tdsBytes.AsSpan(VecHeaderLengthOffset));

        if (tdsBytes.Length != TdsEnums.VECTOR_HEADER_SIZE + (Float16ElementSize * length))
        {
            throw ADP.InvalidVectorHeader();
        }

        float[] widened = new float[length];

        for (int i = 0, currPosition = TdsEnums.VECTOR_HEADER_SIZE; i < length; i++, currPosition += Float16ElementSize)
        {
            widened[i] = Float16Converter.ToSingle(
                BinaryPrimitives.ReadUInt16LittleEndian(tdsBytes.AsSpan(currPosition)));
        }

        // T is known to be float on this path, so the cast through object simply
        // reinterprets the memory's element type.
        return (ReadOnlyMemory<T>)(object)new ReadOnlyMemory<float>(widened);
    }

    #endregion

    #region Properties

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/IsNull/*' />
    public bool IsNull { get; }

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Null/*' />
    public static SqlVector<T>? Null => null;

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Length/*' />
    public int Length { get; }

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVector.xml' path='docs/members[@name="SqlVector"]/Memory/*' />
    public ReadOnlyMemory<T> Memory { get; }

    #endregion

    #region ISqlVector Internal Properties
    byte ISqlVector.ElementType => _elementType;
    byte ISqlVector.ElementSize => _elementSize;
    byte[] ISqlVector.VectorPayload => _tdsBytes;
    int ISqlVector.Size => _size;

    #endregion

    #region Helpers

    private static (byte, byte, int) GetTypeFieldsOrThrow()
    {
        byte elementType;
        byte elementSize;
        int maxSize;

        if (typeof(T) == typeof(float))
        {
            elementType = (byte)MetaType.SqlVectorElementType.Float32;
            elementSize = sizeof(float);
        }
        #if NET
        else if (typeof(T) == typeof(Half))
        {
            elementType = (byte)MetaType.SqlVectorElementType.Float16;
            // sizeof(Half) requires an unsafe context, so the size is stated explicitly.
            elementSize = 2;
        }
        #endif
        else
        {
            throw SQL.VectorTypeNotSupported(typeof(T).FullName);
        }
        // The size of a vector (including its header) must not exceed the maximum size of a TDS packet.
        // Calculate the maximum number of elements to simplify the validation of input sizes in constructors.
        maxSize = (TdsEnums.MAXSIZE - TdsEnums.VECTOR_HEADER_SIZE) / elementSize;

        return (elementType, elementSize, maxSize);
    }

    private byte[] MakeTdsBytes(ReadOnlyMemory<T> values)
    {
        Debug.Assert(Length <= ushort.MaxValue);

        //Refer to TDS section 2.2.5.5.7 for vector header format
        // +------------------------+-----------------+----------------------+------------------+----------------------------+--------------+
        // | Field                  | Size (bytes)    | Example Value         | Description                                                 |
        // +------------------------+-----------------+----------------------+--------------------------------------------------------------+
        // | Layout Format          | 1               | 0xA9                  | Magic number indicating vector layout format                |
        // | Layout Version         | 1               | 0x01                  | Version of the vector format                                |
        // | Number of Dimensions   | 2               | NN                    | Number of vector elements                                   |
        // | Dimension Type         | 1               | 0x00                  | Element type indicator (e.g. 0x00 for float32)              |
        // | Reserved               | 3               | 0x00 0x00 0x00        | Reserved for future use                                     |
        // | Stream of Values       | NN * sizeof(T)  | [element bytes...]    | Raw bytes for vector elements                               |
        // +------------------------+-----------------+----------------------+--------------------------------------------------------------+

        byte[] result = new byte[_size];
        ReadOnlySpan<T> valueSpan = values.Span;

        // Header Bytes
        result[0] = VecHeaderMagicNo;
        result[1] = VecVersionNo;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(VecHeaderLengthOffset), (ushort)Length);
        result[VecHeaderElementTypeOffset] = _elementType;
        result[5] = 0x00;
        result[6] = 0x00;
        result[7] = 0x00;

        // If .NET is running on a little-endian architecture, cast directly to a byte array and proceed.
        // This optimisation relies upon the base type of the vector transporting values in a format and
        // endianness which is identical to the client. This is true for all little-endian clients reading
        // float32-based vectors.
        if (BitConverter.IsLittleEndian)
        {
            ReadOnlySpan<byte> valuesAsBytes = MemoryMarshal.AsBytes(valueSpan);

            valuesAsBytes.CopyTo(result.AsSpan(TdsEnums.VECTOR_HEADER_SIZE));
        }
        else
        {
            if (typeof(T) == typeof(float))
            {
                for (int i = 0, currPosition = TdsEnums.VECTOR_HEADER_SIZE; i < values.Length; i++, currPosition += _elementSize)
                {
                    #if NET
                    BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(currPosition), (float)(object)valueSpan[i]);
                    #else
                    BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(currPosition), BitConverterCompatible.SingleToInt32Bits((float)(object)valueSpan[i]));
                    #endif
                }
            }
            #if NET
            else if (typeof(T) == typeof(Half))
            {
                for (int i = 0, currPosition = TdsEnums.VECTOR_HEADER_SIZE; i < values.Length; i++, currPosition += _elementSize)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(
                        result.AsSpan(currPosition),
                        BitConverter.HalfToUInt16Bits((Half)(object)valueSpan[i]));
                }
            }
            #endif
        }

        return result;
    }

    private (int, int) GetCountsOrThrow(byte[] rawBytes)
    {
        // Validate some of the header fields.
        if (
            // Do we have enough bytes for the header?
            rawBytes.Length < TdsEnums.VECTOR_HEADER_SIZE ||
            // Do we have the expected magic number?
            rawBytes[0] != VecHeaderMagicNo ||
            // Do we support the version?
            rawBytes[1] != VecVersionNo ||
            // Do the vector types match?
            rawBytes[VecHeaderElementTypeOffset] != _elementType)
        {
            // No, so throw.
            throw ADP.InvalidVectorHeader();
        }

        // The vector length is an unsigned 16-bit integer, little-endian.
        int length = BinaryPrimitives.ReadUInt16LittleEndian(rawBytes.AsSpan(VecHeaderLengthOffset));

        // The vector size is the number of bytes required to represent the vector in TDS.
        int size = TdsEnums.VECTOR_HEADER_SIZE + (_elementSize * length);

        // Are there exactly enough bytes for the vector elements?
        if (rawBytes.Length != size)
        {
            // No, so throw.
            throw ADP.InvalidVectorHeader();
        }

        return (length, size);
    }

    private T[] MakeArray()
    {
        if (_tdsBytes.Length == 0)
        {
            return Array.Empty<T>();
        }

        // Allocate array and copy bytes into it
        T[] result = new T[Length];

        // See the comment in MakeTdsBytes for more information on this optimisation.
        if (BitConverter.IsLittleEndian)
        {
            Span<byte> valuesAsBytes = MemoryMarshal.AsBytes(result.AsSpan());

            _tdsBytes.AsSpan(TdsEnums.VECTOR_HEADER_SIZE).CopyTo(valuesAsBytes);
        }
        else
        {
            if (typeof(T) == typeof(float))
            {
                for (int i = 0, currPosition = TdsEnums.VECTOR_HEADER_SIZE; i < Length; i++, currPosition += _elementSize)
                {
                    #if NET
                    result[i] = (T)(object)BinaryPrimitives.ReadSingleLittleEndian(_tdsBytes.AsSpan(currPosition));
                    #else
                    result[i] = (T)(object)BitConverterCompatible.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(_tdsBytes.AsSpan(currPosition)));
                    #endif
                }
            }
            #if NET
            else if (typeof(T) == typeof(Half))
            {
                for (int i = 0, currPosition = TdsEnums.VECTOR_HEADER_SIZE; i < Length; i++, currPosition += _elementSize)
                {
                    result[i] = (T)(object)BitConverter.UInt16BitsToHalf(
                        BinaryPrimitives.ReadUInt16LittleEndian(_tdsBytes.AsSpan(currPosition)));
                }
            }
            #endif
        }

        return result;
    }

    #endregion
}
