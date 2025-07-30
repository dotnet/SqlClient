// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.ScenarioTests
{
    public class ConnectionRoutingFailoverTests
    {
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFaultAtRoutedLocation_ShouldReturnToGateway(uint errorCode)
        {
            // Arrange
            using TdsServer failoverServer = new TdsServer(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost:1234",
                });
            failoverServer.Start();

            using TransientFaultTdsServer server = new TransientFaultTdsServer(
                new TransientFaultTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                    FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}",
                });
            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                    RequireReadOnly = false,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{router.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false
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
            using TdsServer failoverServer = new TdsServer(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost:1234",
                });
            failoverServer.Start();

            using TransientFaultTdsServer server = new TransientFaultTdsServer(
                new TransientFaultTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                    FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}",
                });
            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                    RequireReadOnly = false,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{router.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                Encrypt = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                // Act
                connection.Open();
            }
            catch (SqlException e)
            {
                Assert.Equal((int)errorCode, e.Number);
                return;
            }

            Assert.Fail();
        }

        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3528")]
        [Fact]
        public void NetworkErrorAtRoutedLocation_ShouldConnectToPrimary()
        {
            using TdsServer failoverServer = new TdsServer(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientTimeoutTdsServer server = new TransientTimeoutTdsServer(
                new TransientTimeoutTdsServerArguments()
                {
                    IsEnabledTransientTimeout = true,
                    SleepDuration = TimeSpan.FromMilliseconds(1000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                    RequireReadOnly = false,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                InitialCatalog = "master",// Required for failover partner to work
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
            // On the first connection attempt, no failover partner information is available,
            // so the connection will retry on the same server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(2, router.PreLoginCount);
            Assert.Equal(2, server.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3527")]
        [Fact]
        public void NetworkErrorAtRoutedLocation_RetryDisabled_ShouldFail()
        {
            using TdsServer failoverServer = new TdsServer(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientTimeoutTdsServer server = new TransientTimeoutTdsServer(
                new TransientTimeoutTdsServerArguments()
                {
                    IsEnabledTransientTimeout = true,
                    SleepDuration = TimeSpan.FromMilliseconds(1000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                    RequireReadOnly = false,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                InitialCatalog = "master",// Required for failover partner to work
                ConnectTimeout = 5,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                Encrypt = false,
            };
            using SqlConnection connection = new(builder.ConnectionString);
            // Act

            // currently doesn't go back to gateway or evaluate retry count
            Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            // On the first connection attempt, no failover partner information is available in the pool group,
            // so the connection will retry on the same server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(1, router.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Fact]
        public void NetworkErrorAtRoutedLocation_WithUserProvidedPartner_RetryDisabled_ShouldConnectToFailoverPartner()
        {
            using TdsServer failoverServer = new TdsServer(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientTimeoutTdsServer server = new TransientTimeoutTdsServer(
                new TransientTimeoutTdsServerArguments()
                {
                    IsEnabledTransientTimeout = true,
                    SleepDuration = TimeSpan.FromMilliseconds(1000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                    RequireReadOnly = false,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                InitialCatalog = "master", // Required for failover partner to work
                ConnectTimeout = 5,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}", // User provided failover partner
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
            // On the first connection attempt, failover partner information is available in the connection string,
            // so the connection will retry on the failover server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(1, router.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(1, failoverServer.PreLoginCount);
        }

        [Fact]
        public void NetworkErrorAtRoutedLocation_WithUserProvidedPartner_RetryEnabled_ShouldConnectToFailoverPartner()
        {
            using TdsServer failoverServer = new TdsServer(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientTimeoutTdsServer server = new TransientTimeoutTdsServer(
                new TransientTimeoutTdsServerArguments()
                {
                    IsEnabledTransientTimeout = true,
                    SleepDuration = TimeSpan.FromMilliseconds(1000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            using RoutingTdsServer router = new RoutingTdsServer(
                new RoutingTdsServerArguments()
                {
                    RoutingTCPHost = "localhost",
                    RoutingTCPPort = (ushort)server.EndPoint.Port,
                    RequireReadOnly = false,
                });
            router.Start();

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost," + router.EndPoint.Port,
                InitialCatalog = "master", // Required for failover partner to work
                ConnectTimeout = 5,
                ConnectRetryInterval = 1,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}", // User provided failover partner
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
            // On the first connection attempt, failover partner information is available in the connection string,
            // so the connection will retry on the failover server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(1, router.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(1, failoverServer.PreLoginCount);
        }
    }
}
