// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Microsoft.Data.SqlTypes;

/// <summary>
/// Operations on the TDS representation of a vector which do not depend on the type its
/// elements are surfaced as.
/// </summary>
/// <remarks>
/// Refer to TDS section 2.2.5.5.7 for the layout of a vector.
/// </remarks>
internal static class SqlVectorPayload
{
    #region Constants

    internal const byte HeaderMagicNumber = 0xA9;
    internal const byte HeaderVersion = 0x01;

    // Offsets of the fields within the vector header.
    internal const int LengthOffset = 2;
    internal const int ElementTypeOffset = 4;

    /// <summary>
    /// The exponent field of a binary16 value, which is all ones for an infinity or a NaN.
    /// </summary>
    private const ushort Binary16ExponentMask = 0x7C00;

    #endregion

    #region Methods

    /// <summary>
    /// Validates the fields of a vector header which do not depend on its base type.
    /// </summary>
    internal static void ThrowIfHeaderInvalid(byte[] tdsBytes)
    {
        if (tdsBytes.Length < TdsEnums.VECTOR_HEADER_SIZE ||
            tdsBytes[0] != HeaderMagicNumber ||
            tdsBytes[1] != HeaderVersion)
        {
            throw ADP.InvalidVectorHeader();
        }
    }

    /// <summary>
    /// Rewrites a TDS vector payload so that its elements use the requested base type,
    /// returning the original payload when it already does.
    /// </summary>
    /// <remarks>
    /// This works on every target framework, including those without <c>System.Half</c>,
    /// because it produces a raw payload rather than a strongly typed vector. It is used by
    /// bulk copy, where the base type written to the wire must match the destination
    /// column's: the server reports a mismatch as a column length error rather than
    /// converting the value, because binary16 and binary32 elements differ in size.
    /// </remarks>
    internal static byte[] ConvertElementType(byte[] tdsBytes, byte targetElementType)
    {
        ThrowIfHeaderInvalid(tdsBytes);

        byte sourceElementType = tdsBytes[ElementTypeOffset];

        if (sourceElementType == targetElementType)
        {
            return tdsBytes;
        }

        int length = BinaryPrimitives.ReadUInt16LittleEndian(tdsBytes.AsSpan(LengthOffset));
        int sourceElementSize = MetaType.GetVectorElementSize(sourceElementType);
        int targetElementSize = MetaType.GetVectorElementSize(targetElementType);

        if (tdsBytes.Length != TdsEnums.VECTOR_HEADER_SIZE + (sourceElementSize * length))
        {
            throw ADP.InvalidVectorHeader();
        }

        byte[] result = new byte[TdsEnums.VECTOR_HEADER_SIZE + (targetElementSize * length)];

        result[0] = HeaderMagicNumber;
        result[1] = HeaderVersion;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(LengthOffset), (ushort)length);
        result[ElementTypeOffset] = targetElementType;

        for (int i = 0,
                 sourcePosition = TdsEnums.VECTOR_HEADER_SIZE,
                 targetPosition = TdsEnums.VECTOR_HEADER_SIZE;
             i < length;
             i++, sourcePosition += sourceElementSize, targetPosition += targetElementSize)
        {
            // Every supported base type widens to single precision without loss, so it
            // serves as the common representation for the conversion.
            WriteElement(
                result,
                targetPosition,
                targetElementType,
                ReadElement(tdsBytes, sourcePosition, sourceElementType));
        }

        return result;
    }

    internal static float ReadElement(byte[] payload, int position, byte elementType)
    {
        switch ((MetaType.SqlVectorElementType)elementType)
        {
            case MetaType.SqlVectorElementType.Float32:
                #if NET
                return BinaryPrimitives.ReadSingleLittleEndian(payload.AsSpan(position));
                #else
                return BitConverterCompatible.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(position)));
                #endif

            case MetaType.SqlVectorElementType.Float16:
                return Float16Converter.ToSingle(BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(position)));

            default:
                throw SQL.VectorTypeNotSupported(elementType.ToString());
        }
    }

    internal static void WriteElement(byte[] payload, int position, byte elementType, float value)
    {
        switch ((MetaType.SqlVectorElementType)elementType)
        {
            case MetaType.SqlVectorElementType.Float32:
                #if NET
                BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(position), value);
                #else
                BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(position), BitConverterCompatible.SingleToInt32Bits(value));
                #endif
                break;

            case MetaType.SqlVectorElementType.Float16:
                ushort narrowed = Float16Converter.FromSingle(value);

                // The codec saturates, so a finite input can become an infinity. The server
                // rejects that as a malformed vector rather than as an out of range value,
                // which is a far less useful diagnostic than reporting it here.
                if (!float.IsNaN(value) && !float.IsInfinity(value) &&
                    (narrowed & Binary16ExponentMask) == Binary16ExponentMask)
                {
                    throw ADP.VectorValueOutOfRangeForBaseType(value, "float16");
                }

                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(position), narrowed);
                break;

            default:
                throw SQL.VectorTypeNotSupported(elementType.ToString());
        }
    }

    #endregion
}
