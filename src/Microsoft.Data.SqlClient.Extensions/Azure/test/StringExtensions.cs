// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/// <summary>
/// String extensions used by our tests.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Adds the IsEmpty() method to string that doesn't waste time on null checks
    /// like String.IsNullOrEmpty() does, and has a nice short name.
    /// </summary>
    /// <param name="str">The string to check; must not be null.</param>
    /// <returns>True if the string is empty, false otherwise.</returns>
    internal static bool IsEmpty(this string str)
    {
        return str.Length == 0;
    }
}
