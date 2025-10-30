// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    [Collection("SimulatedServerTests")]
    public class ConnectionFailoverTests
    {
        //TODO parameterize for transient errors
        [Theory]
        [Trait("Category", "flaky")]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFault_NoFailover_DoesNotClearPool(uint errorCode)
        {
            // When connecting to a server with a configured failover partner,
            // transient errors returned during the login ack should not clear the connection pool.

            // Arrange
            using TdsServer failoverServer = new(new TdsServerArguments
            {
                // Doesn't need to point to a real endpoint, just needs a value specified
                FailoverPartner = "localhost,1234"
            });
            failoverServer.Start();
            var failoverDataSource = $"localhost,{failoverServer.EndPoint.Port}";

            // Errors are off to start to allow the pool to warm up
            using TransientTdsErrorTdsServer initialServer = new(new TransientTdsErrorTdsServerArguments
            {
                FailoverPartner = failoverDataSource
            });
            initialServer.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + initialServer.EndPoint.Port,
                ConnectRetryInterval = 1,
                ConnectTimeout = 30,
                Encrypt = SqlConnectionEncryptOption.Optional,
                InitialCatalog = "test"
            };

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            // Act
            initialServer.SetErrorBehavior(true, errorCode);
            using SqlConnection secondConnection = new(builder.ConnectionString);
            // Should not trigger a failover, will retry against the same server
            secondConnection.Open();

            // Request a new connection, should initiate a fresh connection attempt if the pool was cleared.
            connection.Close();
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(ConnectionState.Open, secondConnection.State);
            Assert.Equal($"localhost,{initialServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal($"localhost,{initialServer.EndPoint.Port}", secondConnection.DataSource);

            // 1 for the initial connection, 2 for the second connection
            Assert.Equal(3, initialServer.PreLoginCount);
            // A failover should not be triggered, so prelogin count to the failover server should be 0
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Fact]
        [Trait("Category", "flaky")]
        public void NetworkError_TriggersFailover_ClearsPool()
        {
            // When connecting to a server with a configured failover partner,
            // network errors returned during prelogin should clear the connection pool.

            // Arrange
            using TdsServer failoverServer = new(new TdsServerArguments
            {
                // Doesn't need to point to a real endpoint, just needs a value specified
                FailoverPartner = "localhost,1234"
            });
            failoverServer.Start();
            var failoverDataSource = $"localhost,{failoverServer.EndPoint.Port}";

            // Errors are off to start to allow the pool to warm up
            using TdsServer initialServer = new(new TdsServerArguments
            {
                FailoverPartner = failoverDataSource
            });
            initialServer.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + initialServer.EndPoint.Port,
                ConnectRetryInterval = 1,
                ConnectTimeout = 30,
                Encrypt = SqlConnectionEncryptOption.Optional,
                InitialCatalog = "test",
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            };

            // Open the initial connection to warm up the pool and populate failover partner information
            // for the pool group.
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{initialServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, initialServer.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);

            // Act
            // Should trigger a failover because the initial server is unavailable
            initialServer.Dispose();
            using SqlConnection secondConnection = new(builder.ConnectionString);
            secondConnection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, secondConnection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", secondConnection.DataSource);
            Assert.Equal(1, initialServer.PreLoginCount);
            Assert.Equal(1, failoverServer.PreLoginCount);


            // Act
            // Request a new connection, should initiate a fresh connection attempt if the pool was cleared.
            connection.Close();
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, initialServer.PreLoginCount);
            Assert.Equal(2, failoverServer.PreLoginCount);
        }

        [Fact]
        [Trait("Category", "flaky")]
        public void NetworkTimeout_ShouldFail()
        {
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientDelayTdsServer server = new(
                new TransientDelayTdsServerArguments()
                {
                    IsEnabledTransientDelay = true,
                    DelayDuration = TimeSpan.FromMilliseconds(2000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.EndPoint.Port,
                InitialCatalog = "master",// Required for failover partner to work
                ConnectTimeout = 1,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry 
                Encrypt = false,
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            var e = Assert.Throws<SqlException>(() => connection.Open());

            // Assert
            Assert.Contains("Connection Timeout Expired", e.Message);
            Assert.Equal(ConnectionState.Closed, connection.State);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Fact]
        [Trait("Category", "flaky")]
        public void NetworkDelay_ShouldConnectToPrimary()
        {
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientDelayTdsServer server = new(
                new TransientDelayTdsServerArguments()
                {
                    IsEnabledTransientDelay = true,
                    DelayDuration = TimeSpan.FromMilliseconds(1000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.EndPoint.Port,
                InitialCatalog = "master",// Required for failover partner to work
                ConnectTimeout = 5,
                Encrypt = false,
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
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
            // On the first connection attempt, no failover partner information is available,
            // so the connection will retry on the same server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Fact]
        [Trait("Category", "flaky")]
        public void NetworkError_WithUserProvidedPartner_RetryDisabled_ShouldConnectToFailoverPartner()
        {
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientDelayTdsServer server = new(
                new TransientDelayTdsServerArguments()
                {
                    IsEnabledTransientDelay = true,
                    DelayDuration = TimeSpan.FromMilliseconds(10000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.EndPoint.Port,
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
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, failoverServer.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);
        }

        [Fact]
        [Trait("Category", "flaky")]
        public void NetworkError_WithUserProvidedPartner_RetryEnabled_ShouldConnectToFailoverPartner()
        {
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Arrange
            using TransientDelayTdsServer server = new(
                new TransientDelayTdsServerArguments()
                {
                    IsEnabledTransientDelay = true,
                    DelayDuration = TimeSpan.FromMilliseconds(10000),
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.EndPoint.Port,
                InitialCatalog = "master", // Required for failover partner to work
                ConnectTimeout = 5,
                ConnectRetryInterval = 1,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}", // User provided failover partner
                Encrypt = false,
            };
            using SqlConnection connection = new(builder.ConnectionString);
            // Act
            connection.Open();

            // Assert
            // On the first connection attempt, failover partner information is available in the connection string,
            // so the connection will retry on the failover server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(1, failoverServer.PreLoginCount);
        }

        [Theory]
        [Trait("Category", "flaky")]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFault_ShouldConnectToPrimary(uint errorCode)
        {
            // Arrange
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            using TransientTdsErrorTdsServer server = new(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, server.PreLoginCount);
        }

        [Theory]
        [Trait("Category", "flaky")]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFault_RetryDisabled_ShouldFail(uint errorCode)
        {
            // Arrange
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost:1234",
                });
            failoverServer.Start();

            using TransientTdsErrorTdsServer server = new(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                    FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
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

        [Theory]
        [Trait("Category", "flaky")]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFault_WithUserProvidedPartner_ShouldConnectToPrimary(uint errorCode)
        {
            // Arrange
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost:1234",
                });
            failoverServer.Start();

            using TransientTdsErrorTdsServer server = new(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                    FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false,
                FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}", // User provided failover partner
            };
            using SqlConnection connection = new(builder.ConnectionString);
                
            // Act
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, server.PreLoginCount);
        }

        [Theory]
        [Trait("Category", "flaky")]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFault_WithUserProvidedPartner_RetryDisabled_ShouldFail(uint errorCode)
        {
            // Arrange
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost:1234",
                });
            failoverServer.Start();

            using TransientTdsErrorTdsServer server = new(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = errorCode,
                    FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                Encrypt = false,
                FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}", // User provided failover partner
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

        [Fact]
        public void TransientFault_IgnoreServerProvidedFailoverPartner_ShouldConnectToUserProvidedPartner()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.IgnoreServerProvidedFailoverPartnerField = LocalAppContextSwitchesHelper.Tristate.True;

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            using TdsServer server = new(
                new TdsServerArguments()
                {
                    // Set an invalid failover partner to ensure that the connection fails if the
                    // server provided failover partner is used.
                    FailoverPartner = $"invalidhost",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                Encrypt = false,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                // Ensure pooling is enabled so that the failover partner information
                // is persisted in the pool group. If pooling is disabled, the server
                // provided failover partner will never be used.
                Pooling = true,
                MinPoolSize = 1
            };
            SqlConnection connection = new(builder.ConnectionString);

            // Connect once to the primary to trigger it to send the failover partner
            connection.Open();
            Assert.Equal("invalidhost", (connection.InnerConnection as SqlInternalConnectionTds)!.ServerProvidedFailoverPartner);

            // Close the connection to return it to the pool
            connection.Close();

            // Act
            // Dispose of the server to trigger a failover
            server.Dispose();

            // Opening a new connection will use the failover partner stored in the pool group.
            // This will fail if the server provided failover partner was stored to the pool group.
            using SqlConnection failoverConnection = new(builder.ConnectionString);

            // Clear the pool to ensure a new physical connection is created
            // Pool group info such as failover partner will still be retained
            SqlConnection.ClearPool(connection);
            failoverConnection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, failoverConnection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", failoverConnection.DataSource);
            // 1 for the initial connection
            Assert.Equal(1, server.PreLoginCount);
            // 1 for the failover connection
            Assert.Equal(1, failoverServer.PreLoginCount);
        }
    }
}
