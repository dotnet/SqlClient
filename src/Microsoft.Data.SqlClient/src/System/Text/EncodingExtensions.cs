// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Text;

internal static class EncodingExtensions
{
    private const byte NullByte = 0x00;
    private static ReadOnlySpan<byte> MultiByteNull => [NullByte, NullByte];

    /// <summary>
    /// Creates a new string from a null-terminated sequence of bytes, decoding them using the specified encoding.
    /// </summary>
    /// <param name="encoding">The encoding to use to decode the bytes.</param>
    /// <param name="nullTerminatedBytes">The null-terminated sequence of bytes to decode.</param>
    /// <returns>The decoded string.</returns>
    public static unsafe string CreateStringFromNullTerminated(this Encoding encoding, ReadOnlySpan<byte> nullTerminatedBytes)
    {
        int preNullBytes = nullTerminatedBytes.IndexOf(MultiByteNull);

        // If the sequence starts with a null terminator, avoid allocating a new zero-length string.
        if (preNullBytes == 0 || nullTerminatedBytes.Length == 0)
        {
            return string.Empty;
        }

        // IndexOf has searched for a multi-byte null terminator. This will work in most circumstances, assuming that
        // every value after the end of the string is zeroed out.
        if (preNullBytes == -1)
        {
            // If the byte sequence is [NullByte], we're in the same position as before. Return an empty string.
            if (nullTerminatedBytes.Length == 1
                && nullTerminatedBytes[0] == NullByte)
            {
                return string.Empty;
            }
            // If the byte sequence is only long enough to contain an encoded string followed by a single null byte,
            // adjust the null index to account for the false positive.
            else if (nullTerminatedBytes.Length > 1
                && nullTerminatedBytes[nullTerminatedBytes.Length - 1] == NullByte)
            {
                preNullBytes = nullTerminatedBytes.Length - 1;
            }
            // Otherwise, there is no null terminator. Use the entire byte array.
            else
            {                
                preNullBytes = nullTerminatedBytes.Length;
            }
        }
        // If we work with unicode encodings and strings containing nothing but ASCII characters, every other byte will
        // be a null byte. In such a case, the last byte of the string will be null. This means that preNullBytes will be
        // one byte too long. Adjust to account for that.
        else if (encoding is UnicodeEncoding)
        {
            if (preNullBytes % 2 != 0)
            {
                // If we have a match, we already know that it'll be less than or equal to [array size - search string length].
                Debug.Assert(preNullBytes + 1 <= nullTerminatedBytes.Length);

                preNullBytes++;
            }
        }

        fixed (byte* pBytes = nullTerminatedBytes)
        {
            return encoding.GetString(pBytes, preNullBytes);
        }
    }
}
