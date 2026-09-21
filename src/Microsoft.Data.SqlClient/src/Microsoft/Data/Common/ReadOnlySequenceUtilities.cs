// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Buffers.Binary;

namespace Microsoft.Data.Common;

internal static class ReadOnlySequenceUtilities
{
    /// <summary>
    /// Reads the next byte from the sequence, advancing its position by one byte.
    /// </summary>
    /// <param name="sequence">The sequence to read and to advance from.</param>
    /// <param name="currSpan">The first span in the sequence. Reassigned if the next byte can only be read from the next span.</param>
    /// <param name="currPos">Current position in the sequence. Advanced by one byte following a successful read.</param>
    /// <param name="value">The <see cref="byte"/> value read from <paramref name="sequence"/>.</param>
    /// <returns><c>true</c> if <paramref name="sequence"/> was long enough to retrieve the next byte, <c>false</c> otherwise.</returns>
    public static bool ReadByte(this ref ReadOnlySequence<byte> sequence, ref ReadOnlySpan<byte> currSpan, ref long currPos, out byte value)
    {
        if (sequence.Length < sizeof(byte))
        {
            value = default;
            return false;
        }

        currPos += sizeof(byte);
        if (currSpan.Length >= sizeof(byte))
        {
            value = currSpan[0];

            sequence = sequence.Slice(sizeof(byte));
            currSpan = currSpan.Slice(sizeof(byte));

            return true;
        }
        else
        {
            Span<byte> buffer = stackalloc byte[sizeof(byte)];

            sequence.Slice(0, sizeof(byte)).CopyTo(buffer);
            value = buffer[0];

            sequence = sequence.Slice(sizeof(byte));
            currSpan = sequence.First.Span;

            return true;
        }
    }

    /// <summary>
    /// Reads the next two bytes from the sequence as a <see cref="ushort"/>, advancing its position by two bytes.
    /// </summary>
    /// <param name="sequence">The sequence to read and to advance from.</param>
    /// <param name="currSpan">The first span in the sequence. Reassigned if the next two bytes can only be read from the next span.</param>
    /// <param name="currPos">Current position in the sequence. Advanced by two bytes following a successful read.</param>
    /// <param name="value">The <see cref="ushort"/> value read from <paramref name="sequence"/></param>
    /// <returns><c>true</c> if <paramref name="sequence"/> was long enough to retrieve the next two bytes, <c>false</c> otherwise.</returns>
    public static bool ReadLittleEndian(this ref ReadOnlySequence<byte> sequence, ref ReadOnlySpan<byte> currSpan, ref long currPos, out ushort value)
    {
        if (sequence.Length < sizeof(ushort))
        {
            value = default;
            return false;
        }

        currPos += sizeof(ushort);
        if (currSpan.Length >= sizeof(ushort))
        {
            value = BinaryPrimitives.ReadUInt16LittleEndian(currSpan);

            sequence = sequence.Slice(sizeof(ushort));
            currSpan = currSpan.Slice(sizeof(ushort));

            return true;
        }
        else
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];

            sequence.Slice(0, sizeof(ushort)).CopyTo(buffer);
            value = BinaryPrimitives.ReadUInt16LittleEndian(buffer);

            sequence = sequence.Slice(sizeof(ushort));
            currSpan = sequence.First.Span;

            return true;
        }
    }
}
