// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Login7;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

[Collection("SimulatedServerTests")]
public class FeatureExtensionNegotiationTests : IClassFixture<TdsServerFixture>
{
    private TdsServer _server;
    private string _connectionString;

    public FeatureExtensionNegotiationTests(TdsServerFixture fixture)
    {
        _server = fixture.TdsServer;
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            // Disable connection pooling to avoid recycling a connection that has already negotiated feature extensions
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    [Fact]
    public void EnhancedRouting_EnabledByServer_ShouldBeEnabled()
    {
        // Arrange
        _server.EnhancedRoutingBehavior = FeatureExtensionBehavior.Enabled;

        bool clientFeatureExtensionFound = false;
        _server.OnLogin7Validated = loginToken =>
        {
            var token = loginToken.FeatureExt
                                  .OfType<TDSLogin7GenericOptionToken>()
                                  .FirstOrDefault(t => t.FeatureID == TDSFeatureID.EnhancedRoutingSupport);

            // Test should fail if no UserAgent FE token is found
            Assert.NotNull(token);

            Assert.Equal((byte)TDSFeatureID.EnhancedRoutingSupport, (byte)token.FeatureID);

            clientFeatureExtensionFound = true;
        };

        using SqlConnection sqlConnection = new(_connectionString);

        // Act
        sqlConnection.Open();

        // Assert
        Assert.True(clientFeatureExtensionFound);
        Assert.True(((SqlInternalConnectionTds)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled);
    }

    [Fact]
    public void EnhancedRouting_DisabledByServer_ShouldBeDisabled()
    {
        // Arrange
        _server.EnhancedRoutingBehavior = FeatureExtensionBehavior.Disabled;

        using SqlConnection sqlConnection = new(_connectionString);

        // Act
        sqlConnection.Open();

        // Assert
        Assert.False(((SqlInternalConnectionTds)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled);
    }

    [Fact]
    public void EnhancedRouting_NotAcknowledgedByServer_ShouldBeDisabled()
    {
        // Arrange
        _server.EnhancedRoutingBehavior = FeatureExtensionBehavior.DoNotAcknowledge;

        using SqlConnection sqlConnection = new(_connectionString);

        // Act
        sqlConnection.Open();

        // Assert
        Assert.False(((SqlInternalConnectionTds)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled);
    }
}
