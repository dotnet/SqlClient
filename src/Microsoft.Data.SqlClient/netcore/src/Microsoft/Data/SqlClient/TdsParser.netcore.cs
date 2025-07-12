// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Buffers.Binary;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class TdsParser
    {
        internal static void FillGuidBytes(Guid guid, Span<byte> buffer) => guid.TryWriteBytes(buffer);

        internal static void FillDoubleBytes(double value, Span<byte> buffer) => BinaryPrimitives.TryWriteInt64LittleEndian(buffer, BitConverter.DoubleToInt64Bits(value));

        internal static void FillFloatBytes(float value, Span<byte> buffer) => BinaryPrimitives.TryWriteInt32LittleEndian(buffer, BitConverterCompatible.SingleToInt32Bits(value));
        
        internal static Guid ConstructGuid(ReadOnlySpan<byte> bytes)
        {
            Debug.Assert(bytes.Length >= 16, "not enough bytes to set guid");
            return new Guid(bytes);
        }
    }
}
