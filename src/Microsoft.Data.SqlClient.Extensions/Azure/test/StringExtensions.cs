// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/// <summary>
/// Adds the missing IsEmpty() method to string that doesn't waste time on null checks like
/// String.IsNullOrEmpty() does, and has a nice shorter name.
/// </summary>
/// <param name="str">The string to check; must not be null</param>
internal static class StringExtensions
{
    internal static bool IsEmpty(this string str)
    {
        return str.Length == 0;
    }
}
