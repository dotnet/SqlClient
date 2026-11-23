// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Verifies primary/partner selection and pool preservation across transport and login SQL errors.
    /// </summary>
    [Collection("SimulatedServerTests")]
    public class ConnectionFailoverTests
    {
        /// <summary>
        /// Executes the selected public open path so both paths share the same scenario assertions.
        /// </summary>
        private static Task OpenConnection(SqlConnection connection, bool useAsync)
        {
            if (useAsync)
            {
                return connection.OpenAsync();
            }
            connection.Open();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Preserves the original physical connection after retrying a login SQL error on the primary.
        /// </summary>
        [Trait("category", "flaky")]
        [Theory]
        [InlineData(40613, false)]
        [InlineData(42108, false)]
        [InlineData(42109, false)]
        [InlineData(40613, true)]
        [InlineData(42108, true)]
        [InlineData(42109, true)]
        public async Task TransientFault_NoFailover_DoesNotClearPool(uint errorCode, bool useAsync)
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
            var failoverDataSource = $"127.0.0.1,{failoverServer.EndPoint.Port}";

            // Errors are off to start to allow the pool to warm up
            using TransientTdsErrorTdsServer initialServer = new(new TransientTdsErrorTdsServerArguments
            {
                FailoverPartner = failoverDataSource
            });
            initialServer.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "127.0.0.1," + initialServer.EndPoint.Port,
                ConnectRetryInterval = 1,
                ConnectTimeout = 30,
                Encrypt = SqlConnectionEncryptOption.Optional,
                InitialCatalog = "test"
            };

            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);
            await OpenConnection(connection, useAsync);
            Guid originalConnectionId = connection.ClientConnectionId;

            // Act
            initialServer.SetErrorBehavior(true, errorCode);
            using SqlConnection secondConnection = new(builder.ConnectionString);
            // Should not trigger a failover, will retry against the same server
            await OpenConnection(secondConnection, useAsync);

            // Request a new connection, should initiate a fresh connection attempt if the pool was cleared.
            connection.Close();
            await OpenConnection(connection, useAsync);

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal(ConnectionState.Open, secondConnection.State);

            Assert.Equal($"127.0.0.1,{initialServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal($"127.0.0.1,{initialServer.EndPoint.Port}", secondConnection.DataSource);

            Assert.Equal(originalConnectionId, connection.ClientConnectionId);

            // 1 for the initial connection, 2 for the second connection
            Assert.Equal(3, initialServer.PreLoginCount);
            // A failover should not be triggered, so prelogin count to the failover server should be 0
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        /// <summary>
        /// Invalidates the primary pool after transport failover, even while its old connection remains usable.
        /// </summary>
        [Trait("category", "flaky")]
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NetworkError_TriggersFailover_ClearsPool(bool useAsync)
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
            var failoverDataSource = $"127.0.0.1,{failoverServer.EndPoint.Port}";

            // Errors are off to start to allow the pool to warm up
            using GatedTdsServer initialServer = new(new TdsServerArguments
            {
                FailoverPartner = failoverDataSource
            });
            initialServer.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "127.0.0.1," + initialServer.EndPoint.Port,
                ConnectRetryInterval = 1,
                ConnectTimeout = 30,
                Encrypt = SqlConnectionEncryptOption.Optional,
                InitialCatalog = "test",
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            };

            // Open the initial connection to warm up the pool and populate failover partner information
            // for the pool group.
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);

            await OpenConnection(connection, useAsync);
            Guid originalConnectionId = connection.ClientConnectionId;
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"127.0.0.1,{initialServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, initialServer.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);

            // Act
            // Should trigger a failover because the initial server is unavailable
            initialServer.WithholdPreLoginResponses();
            using SqlConnection secondConnection = new(builder.ConnectionString);
            await OpenConnection(secondConnection, useAsync);

            // Assert
            Assert.Equal(ConnectionState.Open, secondConnection.State);
            Assert.Equal($"127.0.0.1,{failoverServer.EndPoint.Port}", secondConnection.DataSource);
            Assert.Equal(1, initialServer.Login7Count);

            Assert.Equal(1, failoverServer.PreLoginCount);

            // Act
            // Request a new connection, should initiate a fresh connection attempt if the pool was cleared.
            connection.Close();
            await OpenConnection(connection, useAsync);

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"127.0.0.1,{failoverServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, initialServer.Login7Count);
            Assert.NotEqual(originalConnectionId, connection.ClientConnectionId);
            Assert.NotEqual(secondConnection.ClientConnectionId, connection.ClientConnectionId);
            Assert.Equal(2, failoverServer.Login7Count);
        }

        [Fact]
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
                    FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "127.0.0.1," + server.EndPoint.Port,
                InitialCatalog = "master",// Required for failover partner to work
                ConnectTimeout = 1,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                Encrypt = false,
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            };
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            Action action = () => connection.Open();

            // Assert
            SqlException exception = Assert.Throws<SqlException>(action);
            Assert.Contains("Connection Timeout Expired", exception.Message);

            Assert.Equal(ConnectionState.Closed, connection.State);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Fact]
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
                    FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "127.0.0.1," + server.EndPoint.Port,
                InitialCatalog = "master", // Required for failover partner to work
                ConnectTimeout = 30,
                Encrypt = false,
                Pooling = false, // Disable pooling to ensure a fresh connection attempt is made
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            };
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            connection.Open();

            // Assert
            // On the first connection attempt, no failover partner information is available,
            // so the connection will retry on the same server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"127.0.0.1,{server.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        /// <summary>
        /// Selects the explicit partner after a primary transport failure without outer connect retry.
        /// </summary>
        [Trait("category", "flaky")]
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NetworkError_WithUserProvidedPartner_RetryDisabled_ShouldConnectToFailoverPartner(bool useAsync)
        {
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            using GatedTdsServer server = new(new TdsServerArguments
            {
                FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}",
            });
            server.Start();
            server.WithholdPreLoginResponses();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "127.0.0.1," + server.EndPoint.Port,
                InitialCatalog = "master", // Required for failover partner to work
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}", // User provided failover partner
                Encrypt = false,
                Pooling = false, // Disable pooling to ensure a fresh connection attempt is made on failover
            };
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            await OpenConnection(connection, useAsync);


            // Assert
            // On the first connection attempt, failover partner information is available in the connection string,
            // so the connection will retry on the failover server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"127.0.0.1,{failoverServer.EndPoint.Port}", connection.DataSource);

            Assert.Equal(0, server.Login7Count);
            Assert.Equal(1, failoverServer.Login7Count);
            Assert.True(server.PreLoginBlocked.IsCompleted, "The primary must block a pre-login response.");
        }

        /// <summary>
        /// Selects the explicit partner after a primary transport failure with connect retry enabled.
        /// </summary>
        [Trait("category", "flaky")]
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NetworkError_WithUserProvidedPartner_RetryEnabled_ShouldConnectToFailoverPartner(bool useAsync)
        {
            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            using GatedTdsServer server = new(new TdsServerArguments
            {
                FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}",
            });
            server.Start();
            server.WithholdPreLoginResponses();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "127.0.0.1," + server.EndPoint.Port,
                InitialCatalog = "master", // Required for failover partner to work
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}", // User provided failover partner
                Encrypt = false,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            };
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);
            // Act
            await OpenConnection(connection, useAsync);

            // Assert
            // On the first connection attempt, failover partner information is available in the connection string,
            // so the connection will retry on the failover server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"127.0.0.1,{failoverServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(0, server.Login7Count);
            Assert.Equal(1, failoverServer.Login7Count);
            Assert.True(server.PreLoginBlocked.IsCompleted, "The primary must block a pre-login response.");
        }

        /// <summary>
        /// Verifies login-phase transient SQL errors are retried on the primary endpoint and
        /// do not trigger failover-partner alternation.
        /// </summary>
        [Theory]
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
                    FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"127.0.0.1,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false,
                MultiSubnetFailover = false,
                Pooling = false, // Disable pooling to ensure a fresh connection attempt is made
            };
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            // First login receives the transient token; outer connect retry opens a fresh parser
            // and retries against the same primary endpoint.
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"127.0.0.1,{server.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, server.PreLoginCount);
            // Login-phase errors must NOT trigger failover alternation
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Trait("category", "flaky")]
        [Theory]
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
                DataSource = $"127.0.0.1,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                Encrypt = false
            };
            using SimulatedServerPoolScope pool = new(builder);
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

        /// <summary>
        /// Verifies user-provided failover partner does not change behavior for login-phase
        /// transient SQL errors; retries stay on primary.
        /// </summary>
        [Trait("category", "flaky")]
        [Theory]
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
                DataSource = $"127.0.0.1,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false,
                FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}", // User provided failover partner
            };
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            // Even with a configured partner, this path should use outer connect retry
            // against primary rather than alternation inside LoginWithFailover.
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"127.0.0.1,{server.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, server.PreLoginCount);
            // Login-phase errors must NOT trigger failover alternation
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Trait("category", "flaky")]
        [Theory]
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
                DataSource = $"127.0.0.1,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                ConnectRetryCount = 0, // Disable retry
                Encrypt = false,
                FailoverPartner = $"localhost:{failoverServer.EndPoint.Port}", // User provided failover partner
            };
            using SimulatedServerPoolScope pool = new(builder);
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

            using GatedTdsServer server = new(
                new TdsServerArguments()
                {
                    // Set an invalid failover partner to ensure that the connection fails if the
                    // server provided failover partner is used.
                    FailoverPartner = $"invalidhost",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"127.0.0.1,{server.EndPoint.Port}",
                InitialCatalog = "master",
                Encrypt = false,
                FailoverPartner = $"127.0.0.1,{failoverServer.EndPoint.Port}",
                // Ensure pooling is enabled so that the failover partner information
                // is persisted in the pool group. If pooling is disabled, the server
                // provided failover partner will never be used.
                Pooling = true
            };
            using SimulatedServerPoolScope pool = new(builder);
            using SqlConnection connection = new(builder.ConnectionString);

            // Connect once to the primary to trigger it to send the failover partner
            connection.Open();
            Assert.Equal("invalidhost", (connection.InnerConnection as SqlInternalConnectionTds)!.ServerProvidedFailoverPartner);

            // Close the connection to return it to the pool
            connection.Close();

            // Act
            // Withhold the next pre-login response without racing socket shutdown.
            server.WithholdPreLoginResponses();

            // Clear the pool to ensure the next connection attempt doesn't reuse
            // the pooled connection to the blocked primary server.
            SqlConnection.ClearPool(connection);

            // Opening a new connection will use the failover partner stored in the pool group.
            // This will fail if the server provided failover partner was stored to the pool group.
            using SqlConnection failoverConnection = new(builder.ConnectionString);
            failoverConnection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, failoverConnection.State);

            Assert.Equal($"127.0.0.1,{failoverServer.EndPoint.Port}", failoverConnection.DataSource);
            // 1 for the initial connection
            Assert.True(server.PreLoginBlocked.IsCompleted);
            Assert.Equal(1, server.Login7Count);
            // 1 for the failover connection
            Assert.Equal(1, failoverServer.Login7Count);
        }
    }
}
