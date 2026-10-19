// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Exercises the windowless WAM diagnostic through real Azure SQL login handshakes.
/// </summary>
[Collection("SqlAuthenticationProvider")]
public class WindowlessInteractiveConnectionTests
{
    /// <summary>
    /// Both connection-opening APIs surface the actionable WAM error when no parent window
    /// is supplied. A unique login hint prevents reuse of a silently cached account.
    /// </summary>
    [ConditionalTheory(typeof(Config), nameof(Config.OnWindows), nameof(Config.IsAzureSqlConnectionString))]
    [Trait("category", "interactive")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Open_MissingParentWindow_ExplainsSupportedAlternatives(bool async)
    {
        var builder = new SqlConnectionStringBuilder(Config.TCPConnectionString)
        {
            Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive,
            IntegratedSecurity = false,
            UserID = $"windowless-{Guid.NewGuid():N}@example.invalid",
            Pooling = false,
        };
        builder.Remove("Password");

        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindowFunc(() => IntPtr.Zero);
        SqlAuthenticationProvider? original =
            SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive);
        Assert.NotNull(original);

        // Cached MSAL clients capture the window callback of the provider that created them,
        // and their cache key does not include the provider. Clear the cache so this test uses
        // its zero-window callback and does not leave that callback behind for later tests.
        ActiveDirectoryAuthenticationProvider.ClearUserTokenCache();

        try
        {
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, provider);
            using var connection = new SqlConnection(builder.ConnectionString);

            SqlException exception = async
                ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                : Assert.Throws<SqlException>(() => connection.Open());

            Assert.Contains("window_handle_required", exception.Message);
            Assert.Contains("parent window handle", exception.Message);
            Assert.Contains("SetParentActivityOrWindowFunc", exception.Message);
            Assert.Contains("Active Directory Default", exception.Message);
            Assert.Contains("ApplicationClientId", exception.Message);
            Assert.Contains("UseWamBroker", exception.Message);
        }
        finally
        {
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, original!);
            ActiveDirectoryAuthenticationProvider.ClearUserTokenCache();
        }
    }
}
