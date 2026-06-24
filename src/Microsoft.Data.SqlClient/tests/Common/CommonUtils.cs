// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;

namespace Microsoft.Data.SqlClient.Tests.Common;

public static class CommonUtils
{
    // Ensures the base connection string ends with ';' before appending a new keyword.
    public static string EnsureSeparator(string connectionString)
        => string.IsNullOrEmpty(connectionString) || connectionString.EndsWith(";", StringComparison.Ordinal) ? connectionString : connectionString + ';';

    // Returns randomly generated characters of specified length.
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

    // Returns randomly generated characters of specified length with a prefix.
    public static string GenerateRandomCharacters(string prefix, int length = 11)
    {
        string path = Path.GetRandomFileName();
        path = path.Replace(".", ""); // Remove period.
        // Clamp length to available characters to avoid ArgumentOutOfRangeException.
        return string.Concat(prefix, path.Substring(0, Math.Min(length, path.Length)));
    }

    public static string GenerateObjectName()
    {
        return string.Format("TEST_{0}{1}{2}", Environment.GetEnvironmentVariable("ComputerName"), Environment.TickCount, Guid.NewGuid()).Replace('-', '_');
    }

}
