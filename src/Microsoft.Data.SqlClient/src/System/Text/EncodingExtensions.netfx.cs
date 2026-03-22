// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System.Diagnostics;

namespace System.Text;

internal static class EncodingExtensions
{
    public static int GetByteCount(this Encoding encoding, string s, int offset, int count)
    {
        ReadOnlySpan<char> slicedString = s.AsSpan(offset, count);

        // This also implicitly checks for a null string. If the input string is null, slicedString
        // will be default(ReadOnlySpan<char>), which also has a length of null.
        if (slicedString.Length == 0)
        {
            return 0;
        }

        unsafe
        {
            fixed (char* str = slicedString)
            {
                return encoding.GetByteCount(str, slicedString.Length);
            }
        }
    }

    public static byte[] GetBytes(this Encoding encoding, string s, int index, int count)
    {
        ReadOnlySpan<char> slicedString = s.AsSpan(index, count);
        byte[] bytes;
        int bytesWritten;

        // This also implicitly checks for a null string. If the input string is null, slicedString
        // will be default(ReadOnlySpan<char>), which also has a length of null.
        if (slicedString.Length == 0)
        {
            return Array.Empty<byte>();
        }

        unsafe
        {
            fixed (char* str = slicedString)
            {
                int byteCount = encoding.GetByteCount(str, slicedString.Length);

                bytes = new byte[byteCount];

                fixed (byte* destArray = &bytes[0])
                {
                    bytesWritten = encoding.GetBytes(str, slicedString.Length, destArray, bytes.Length);

                    Debug.Assert(bytesWritten == byteCount);
                }
            }
        }

        return bytes;
    }
}

#endif
