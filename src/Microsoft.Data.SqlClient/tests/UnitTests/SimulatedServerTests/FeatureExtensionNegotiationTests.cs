// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.FeatureExtAck;
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

    [Theory]
    [InlineData(FeatureExtensionBehavior.Enabled, (byte[])[1])]
    [InlineData(FeatureExtensionBehavior.Disabled, (byte[])[0])]
    [InlineData(FeatureExtensionBehavior.DoNotAcknowledge, null)]
    public void EnhancedRoutingNegotiationTest(FeatureExtensionBehavior serverBehavior, byte[]? expectedAckData)
    {
        // Arrange
        _server.EnhancedRoutingBehavior = serverBehavior;

        bool clientRequestedFeatureExtension = false;
        _server.OnLogin7Validated = loginToken =>
        {
            var token = loginToken.FeatureExt
                                  .OfType<TDSLogin7GenericOptionToken>()
                                  .FirstOrDefault(t => t.FeatureID == TDSFeatureID.EnhancedRoutingSupport);

            // Test should fail if no UserAgent FE token is found
            Assert.NotNull(token);

            Assert.Equal((byte)TDSFeatureID.EnhancedRoutingSupport, (byte)token.FeatureID);

            clientRequestedFeatureExtension = true;
        };

        bool serverAcknowledgedFeatureExtension = false;
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            var ackToken = responseMessage.OfType<TDSFeatureExtAckToken>()
                           .FirstOrDefault()?
                           .Options
                           .FirstOrDefault(t => t.FeatureID == TDSFeatureID.EnhancedRoutingSupport);
            
            if (ackToken is not null) {
                serverAcknowledgedFeatureExtension = true;
            }

            var ackData = ((TDSFeatureExtAckGenericOption?)ackToken)?.FeatureAckData;
            Assert.Equal(expectedAckData, ackData);
        };

        using SqlConnection sqlConnection = new(_connectionString);

        // Act
        sqlConnection.Open();

        // Assert
        Assert.True(clientRequestedFeatureExtension);
        
        if (serverBehavior == FeatureExtensionBehavior.DoNotAcknowledge)
        {
            // In DoNotAcknowledge mode, server should not acknowledge the feature extension even if client requested it
            Assert.False(serverAcknowledgedFeatureExtension);
        }
        else
        {
            Assert.True(serverAcknowledgedFeatureExtension);
        }

        if (serverBehavior == FeatureExtensionBehavior.Enabled)
        {
            Assert.True(((SqlConnectionInternal)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled);
        }
        else
        {
            Assert.False(((SqlConnectionInternal)sqlConnection.InnerConnection).IsEnhancedRoutingSupportEnabled);
        }
    }
}
