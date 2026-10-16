// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <Snippet_ConnectionPool_Impersonation>
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Win32.SafeHandles;

public static class ConnectionPoolImpersonationSample
{
    /// <summary>
    /// Opens a pooled integrated-security connection under the supplied Windows token and
    /// returns the authenticated SQL login. Requires Windows and an integrated-security
    /// connection string. The caller retains ownership of the token.
    /// </summary>
    /// <param name="token">The Windows token supplying the intended outbound credentials.</param>
    /// <param name="connectionString">A connection string with integrated security enabled.</param>
    /// <returns>The login that authenticated the SQL connection.</returns>
    public static Task<string> GetLoginAsync(SafeAccessTokenHandle token, string connectionString)
    {
        return WindowsIdentity.RunImpersonated(token, async () =>
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT ORIGINAL_LOGIN()", connection);
            return (string)await command.ExecuteScalarAsync();
        });
    }
}
// </Snippet_ConnectionPool_Impersonation>
