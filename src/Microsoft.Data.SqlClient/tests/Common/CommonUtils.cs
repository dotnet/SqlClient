// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;

namespace Microsoft.Data.SqlClient.Tests.Common;

public static class CommonUtils
{
    // Ensures the base connection string ends with ';' before appending a new keyword.
    public static string EnsureSeparator(string connectionString)
        => string.IsNullOrEmpty(connectionString) || connectionString.EndsWith(';') ? connectionString : connectionString + ';';

    // Returns randomly generated characters of specified length.
    public static SecureString GenerateRandomSecureString(int length = 10)
    {
        SecureString secureString = new();
        for (int i = 0; i < length; i++)
        {
            secureString.AppendChar((char)Random.Shared.Next(33, 126));
        }
        secureString.MakeReadOnly();
        return secureString;
    }

    // Returns randomly generated characters of specified length with a prefix.
    public static string GenerateRandomCharacters(string prefix, int length = 11)
    {
        string path = Path.GetRandomFileName();
        path = path.Replace(".", ""); // Remove period.
        return string.Concat(prefix, path.AsSpan(0, length));
    }

    public static string GenerateObjectName()
    {
        return string.Format("TEST_{0}{1}{2}", Environment.GetEnvironmentVariable("ComputerName"), Environment.TickCount, Guid.NewGuid()).Replace('-', '_');
    }

}