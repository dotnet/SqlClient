// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.Common;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.ScenarioTests
{
    public class ConnectionRoutingTests
    {
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFaultAtRoutedLocation_ShouldReturnToGateway(uint errorCode)
        {
            // Arrange
            using TransientTdsErrorTdsServer server = new TransientTdsErrorTdsServer(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                });

            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    //RoutingTCPHost = server.EndPoint.Address.ToString() == IPAddress.Any.ToString() ? IPAddress.Loopback.ToString() : server.EndPoint.Address.ToString(),
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false,
            };
            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                // Act
                connection.Open();
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            // Routing does not update the connection's data source
            Assert.Equal($"localhost,{router.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, router.PreLoginCount);
            Assert.Equal(2, server.PreLoginCount);
        }

        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFaultAtRoutedLocation_RetryDisabled_ShouldFail(uint errorCode)
        {
            // Arrange
            using TransientTdsErrorTdsServer server = new TransientTdsErrorTdsServer(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                });

            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    //RoutingTCPHost = server.EndPoint.Address.ToString() == IPAddress.Any.ToString() ? IPAddress.Loopback.ToString() : server.EndPoint.Address.ToString(),
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                Encrypt = false,
            };
            using SqlConnection connection = new(builder.ConnectionString);
            //TODO validate exception type
            Assert.Throws<SqlException>(() => connection.Open());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NetworkDelayAtRoutedLocation_RetryDisabled_ShouldSucceed(bool multiSubnetFailoverEnabled)
        {
            // Arrange
            using TransientDelayTdsServer server = new TransientDelayTdsServer(
                new TransientDelayTdsServerArguments()
                {
                    IsEnabledTransientDelay = true,
                    DelayDuration = TimeSpan.FromMilliseconds(1000),
                });

            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 5,
                ConnectRetryCount = 0, // disable retry
                Encrypt = false,
                MultiSubnetFailover = multiSubnetFailoverEnabled,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = multiSubnetFailoverEnabled,
#endif
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(1, router.PreLoginCount);
            if (multiSubnetFailoverEnabled)
            {
                Assert.True(server.PreLoginCount > 1);
            }
            else
            {
                Assert.Equal(1, server.PreLoginCount);
            }
        }

        [Fact]
        public void NetworkTimeoutAtRoutedLocation_RetryDisabled_ShouldFail()
        {
            // Arrange
            using TransientDelayTdsServer server = new TransientDelayTdsServer(
                new TransientDelayTdsServerArguments()
                {
                    IsEnabledTransientDelay = true,
                    DelayDuration = TimeSpan.FromMilliseconds(2000),
                });

            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 1,
                ConnectRetryCount = 0, // disable retry
                Encrypt = false,
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false
#endif
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            var e = Assert.Throws<SqlException>(connection.Open);

            // Assert 
            Assert.Equal(ConnectionState.Closed, connection.State);
            Assert.Contains("Connection Timeout Expired", e.Message);
        }

        [Fact]
        public void NetworkErrorAtRoutedLocation_RetryDisabled_ShouldFail()
        {
            // Arrange
            using TransientNetworkErrorTdsServer server = new TransientNetworkErrorTdsServer(
                new TransientNetworkErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true
                });

            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 5,
                ConnectRetryCount = 0, // disable retry
                Encrypt = false
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            var e = Assert.Throws<SqlException>(connection.Open);

            // Assert 
            Assert.Equal(ConnectionState.Closed, connection.State);
            Assert.Contains("An existing connection was forcibly closed by the remote host", e.Message);
            Assert.Equal(1, router.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);
        }

        [Fact]
        public void NetworkErrorAtRoutedLocation_ShouldFail()
        {
            // Arrange
            using TransientNetworkErrorTdsServer server = new TransientNetworkErrorTdsServer(
                new TransientNetworkErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true
                });

            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 5,
                Encrypt = false
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            var e = Assert.Throws<SqlException>(connection.Open);

            // Assert 
            Assert.Equal(ConnectionState.Closed, connection.State);
            Assert.Contains("An existing connection was forcibly closed by the remote host", e.Message);
            Assert.Equal(1, router.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NetworkErrorDuringCommand_ShouldReturnToGateway(bool multiSubnetFailoverEnabled)
        {
            // Arrange
            using TdsServer server = new TdsServer();
            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                ApplicationIntent = ApplicationIntent.ReadOnly,
                ConnectTimeout = 10,
                Encrypt = false,
                CommandTimeout = 10,
                ConnectRetryInterval = 1,
                Pooling = false,
                MultiSubnetFailover = multiSubnetFailoverEnabled,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = multiSubnetFailoverEnabled,
#endif
            };
            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                // Act
                connection.Open();
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            // Routing does not update the connection's data source
            Assert.Equal($"localhost,{router.EndPoint.Port}", connection.DataSource);

            Assert.Equal(1, router.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);

            // Break the connection to force a reconnect
            server.KillAllConnections();

            SqlCommand command = new SqlCommand("Select 1;", connection);
            command.ExecuteScalar();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{router.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the gateway
            Assert.Equal(2, router.PreLoginCount);
            Assert.Equal(2, server.PreLoginCount);
        }
    }
}
