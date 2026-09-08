// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.UnloadableLibrary;

public sealed class EntryPoint
{
    public string ConnectionString { get; }

    public EntryPoint(string connectionString)
    {
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

        ConnectionString = connectionString;
    }

    public void GetDate()
    {
        using SqlConnection conn = new(ConnectionString);
        using SqlCommand cmd = new("SELECT GETDATE()", conn);

        conn.Open();
        cmd.ExecuteScalar();
    }

    public async Task GetDateAsync()
    {
        using SqlConnection conn = new(ConnectionString);
        using SqlCommand cmd = new("SELECT GETDATE()", conn);

        await conn.OpenAsync().ConfigureAwait(false);
        await cmd.ExecuteScalarAsync().ConfigureAwait(false);
    }
}
