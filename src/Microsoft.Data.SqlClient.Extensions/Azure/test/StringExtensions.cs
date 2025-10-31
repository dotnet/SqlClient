// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Adds the missing Empty() method to string that doesn't waste time on null
// checks like String.IsNullOrEmpty() does, and has a nice short name.
internal static class StringExtensions
{
    internal static bool Empty(this string str)
    {
        return str.Length == 0;
    }
}
