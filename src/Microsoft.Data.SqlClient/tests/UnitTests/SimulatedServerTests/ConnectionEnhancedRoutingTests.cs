// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Tests connection routing using the enhanced routing feature extension and envchange token
/// </summary>
[Collection("SimulatedServerTests")]
public class ConnectionEnhancedRoutingTests
{
    /// <summary>
    /// Tests that a connection is routed to the target server when enhanced routing is enabled.
    /// Uses Theory to test both sync and async code paths.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoutedConnection(bool useAsync)
    {
        // Arrange
        using TestRoutingServers servers = new(FeatureExtensionBehavior.Enabled);

        bool clientProvidedCorrectDatabase = false;
        servers.TargetServer.OnLogin7Validated = loginToken =>
        {
            clientProvidedCorrectDatabase = servers.RoutingDatabaseName == loginToken.Database;
        };

        // Act
        using SqlConnection connection = new(servers.ConnectionString);
        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal($"localhost,{servers.TargetServer.EndPoint.Port}", ((SqlConnectionInternal)connection.InnerConnection).RoutingDestination);
        Assert.Equal(servers.RoutingDatabaseName, connection.Database);
        Assert.True(clientProvidedCorrectDatabase);

        Assert.Equal(1, servers.Router.PreLoginCount);
        Assert.Equal(1, servers.TargetServer.PreLoginCount);
    }

    /// <summary>
    /// Tests that a connection is NOT routed when the server does not acknowledge the enhanced routing feature
    /// or has it disabled. Covers both DoNotAcknowledge and Disabled behaviors.
    /// </summary>
    [Theory]
    [InlineData(FeatureExtensionBehavior.DoNotAcknowledge)]
    [InlineData(FeatureExtensionBehavior.Disabled)]
    public void ServerDoesNotRoute(FeatureExtensionBehavior behavior)
    {
        // Arrange
        using TestRoutingServers servers = new(behavior);

        // Act
        using SqlConnection connection = new(servers.ConnectionString);
        connection.Open();

        // Assert
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Null(((SqlConnectionInternal)connection.InnerConnection).RoutingDestination);
        Assert.Equal("master", connection.Database);

        Assert.Equal(1, servers.Router.PreLoginCount);
        Assert.Equal(0, servers.TargetServer.PreLoginCount);
    }

    /// <summary>
    /// Helper class that encapsulates the setup of a routing TDS server and target TDS server
    /// for enhanced routing tests.
    /// </summary>
    private sealed class TestRoutingServers : IDisposable
    {
        public TdsServer TargetServer { get; }
        public RoutingTdsServer Router { get; }
        public string RoutingDatabaseName { get; }
        public string ConnectionString { get; }

        public TestRoutingServers(FeatureExtensionBehavior enhancedRoutingBehavior)
        {
            RoutingDatabaseName = Guid.NewGuid().ToString();

            TargetServer = new TdsServer(new());
            TargetServer.Start();

            Router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)TargetServer.EndPoint.Port,
                    RoutingDatabaseName = RoutingDatabaseName,
                    RequireReadOnly = false
                });
            Router.Start();
            Router.EnhancedRoutingBehavior = enhancedRoutingBehavior;

            ConnectionString = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{Router.EndPoint.Port}",
                Encrypt = false,
                ConnectTimeout = 10000
            }.ConnectionString;
        }

        public void Dispose()
        {
            Router?.Dispose();
            TargetServer?.Dispose();
        }
    }
}
