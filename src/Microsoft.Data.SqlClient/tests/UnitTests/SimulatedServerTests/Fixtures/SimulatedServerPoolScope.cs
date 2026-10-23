// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Gives a scenario its own pool and clears it before its ephemeral servers are disposed.
/// Declare before the scenario's connections so they are returned before the pool is cleared.
/// </summary>
internal sealed class SimulatedServerPoolScope : IDisposable
{
    private readonly SqlConnection _poolKey;

    /// <summary>
    /// Isolates the builder's pool from other test cases and previous uses of the same TCP port.
    /// </summary>
    public SimulatedServerPoolScope(SqlConnectionStringBuilder builder)
    {
        builder.ApplicationName = Guid.NewGuid().ToString();
        _poolKey = new SqlConnection(builder.ConnectionString);
    }

    /// <summary>
    /// Removes only this scenario's pool, without affecting other simulator clients.
    /// </summary>
    public void Dispose()
    {
        SqlConnection.ClearPool(_poolKey);
        _poolKey.Dispose();
    }
}
