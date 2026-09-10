// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Connection;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies isolation-level reset error handling against the in-process TDS server.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public sealed class IsolationLevelResetTests
{
    /// <summary>
    /// Ensures a successful reset batch leaves the physical connection usable.
    /// </summary>
    [Fact]
    public void ResetSessionIsolationLevel_Success_KeepsConnectionUsable()
    {
        using TdsServer server = new(new TdsServerArguments());
        server.Start();
        using SqlConnection connection = OpenConnection(server);

        SqlConnectionInternal internalConnection = (SqlConnectionInternal)connection.InnerConnection;
        internalConnection.ResetSessionIsolationLevel();

        Assert.False(internalConnection.IsConnectionDoomed);
    }

    /// <summary>
    /// Ensures a server rejection dooms the physical connection and propagates the original error,
    /// rather than handing out a session whose isolation level was not reset.
    /// </summary>
    [Fact]
    public void ResetSessionIsolationLevel_ServerRejects_DoomsConnection()
    {
        const uint errorNumber = 50000;
        using TransientTdsErrorTdsServer server = new(
            new TransientTdsErrorTdsServerArguments
            {
                ErrorClass = 16
            });
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        server.SetErrorBehavior(true, errorNumber);

        SqlConnectionInternal internalConnection = (SqlConnectionInternal)connection.InnerConnection;
        SqlException exception = Assert.Throws<SqlException>(
            internalConnection.ResetSessionIsolationLevel);

        Assert.Equal((int)errorNumber, exception.Number);
        Assert.True(internalConnection.IsConnectionDoomed);
    }

    /// <summary>
    /// Ensures an already-doomed connection does not issue a reset batch.
    /// </summary>
    [Fact]
    public void ResetSessionIsolationLevel_ConnectionAlreadyDoomed_SkipsReset()
    {
        using TransientTdsErrorTdsServer server = new(
            new TransientTdsErrorTdsServerArguments
            {
                ErrorClass = 16
            });
        server.Start();
        using SqlConnection connection = OpenConnection(server);
        server.SetErrorBehavior(true, errorNumber: 50000);

        SqlConnectionInternal internalConnection = (SqlConnectionInternal)connection.InnerConnection;
        internalConnection.DoomThisConnection();
        internalConnection.ResetSessionIsolationLevel();

        Assert.True(internalConnection.IsConnectionDoomed);
    }

    /// <summary>
    /// Opens a non-pooled connection to the supplied in-process TDS server.
    /// </summary>
    private static SqlConnection OpenConnection(GenericTdsServer<TdsServerArguments> server)
    {
        SqlConnection connection = new(
            new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            }.ConnectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Opens a non-pooled connection to the supplied error-injecting TDS server.
    /// </summary>
    private static SqlConnection OpenConnection(
        GenericTdsServer<TransientTdsErrorTdsServerArguments> server)
    {
        SqlConnection connection = new(
            new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            }.ConnectionString);
        connection.Open();
        return connection;
    }
}
