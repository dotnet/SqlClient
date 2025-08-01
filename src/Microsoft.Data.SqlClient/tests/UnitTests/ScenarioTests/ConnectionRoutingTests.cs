// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
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
            using TransientFaultTdsServer server = new TransientFaultTdsServer(
                new TransientFaultTdsServerArguments()
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
            using TransientFaultTdsServer server = new TransientFaultTdsServer(
                new TransientFaultTdsServerArguments()
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

        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3528")]
        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3527")]
        [Fact]
        public void NetworkErrorAtRoutedLocation_ShouldReturnToGateway()
        {
            // Arrange
            using TransientTimeoutTdsServer server = new TransientTimeoutTdsServer(
                new TransientTimeoutTdsServerArguments()
                {
                    IsEnabledTransientTimeout = true,
                    SleepDuration = TimeSpan.FromMilliseconds(1000),
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

        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3528")]
        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3527")]
        [Fact]
        public void NetworkErrorAtRoutedLocation_RetryDisabled_ShouldFail()
        {
            // Arrange
            using TransientTimeoutTdsServer server = new TransientTimeoutTdsServer(
                new TransientTimeoutTdsServerArguments()
                {
                    IsEnabledTransientTimeout = true,
                    SleepDuration = TimeSpan.FromMilliseconds(1000),
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
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // disable retry
                Encrypt = false,
            };
            using SqlConnection connection = new(builder.ConnectionString);
            //TODO validate exception type
            Assert.Throws<SqlException>(() => connection.Open());
        }

        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3528")]
        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3527")]
        [Fact]
        public void NetworkErrorDuringCommand_ShouldReturnToGateway()
        {
            // Arrange
            using TransientTimeoutTdsServer server = new TransientTimeoutTdsServer(
                new TransientTimeoutTdsServerArguments()
                {
                    IsEnabledTransientTimeout = false,
                    SleepDuration = TimeSpan.FromMilliseconds(1000),
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
                ConnectRetryInterval = 1,
                Encrypt = false,
                CommandTimeout = 5,
                ConnectRetryCount = 1
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

            server.SetTransientTimeoutBehavior(true, TimeSpan.FromMilliseconds(1000));

            SqlCommand command = new SqlCommand("Select 1;", connection);
            command.ExecuteScalar();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{router.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the 
            Assert.Equal(3, router.PreLoginCount);
            Assert.Equal(3, server.PreLoginCount);
        }
    }
}
