// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;

namespace Microsoft.Data.SqlClient.Tests.Common;

/// <summary>
/// Random value helpers for tests.
/// </summary>
public static class TestRandomUtilities
{
    /// <summary>
    /// Generates a read-only random alphanumeric <see cref="SecureString"/>.
    /// </summary>
    /// <param name="length">Number of characters to generate.</param>
    /// <returns>A read-only secure string.</returns>
    public static SecureString GenerateRandomSecureString(int length = 10)
    {
        const string alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        SecureString secureString = new();

        byte[] bytes = new byte[length];
        using (System.Security.Cryptography.RandomNumberGenerator rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        // Map random bytes into alphanumeric characters to avoid
        // connection-string delimiters such as ';' and '='.
        for (int i = 0; i < length; i++)
        {
            secureString.AppendChar(alphanumeric[bytes[i] % alphanumeric.Length]);
        }

        secureString.MakeReadOnly();
        return secureString;
    }

    /// <summary>
    /// Generates random file-name-safe characters with a prefix.
    /// </summary>
    /// <param name="prefix">Prefix to prepend.</param>
    /// <param name="length">Maximum number of random characters to append.</param>
    /// <returns>The prefix followed by random characters.</returns>
    public static string GenerateRandomCharacters(string prefix, int length = 11)
    {
        string path = Path.GetRandomFileName();
        path = path.Replace(".", ""); // Remove period.
        // Clamp length to available characters to avoid ArgumentOutOfRangeException.
        return string.Concat(prefix, path.Substring(0, Math.Min(length, path.Length)));
    }
}
