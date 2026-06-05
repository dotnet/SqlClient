// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    [Collection("SimulatedServerTests")]
    public class ConnectionFailoverTests
    {
        //TODO parameterize for transient errors
        [Trait("Category", "flaky")]
        [Theory]
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
            Assert.Equal(3, initialServer.PreLoginCount - initialServer.AbandonedPreLoginCount);
            // A failover should not be triggered, so prelogin count to the failover server should be 0
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Trait("Category", "flaky")]
        [Fact]
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
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.EndPoint.Port,
                InitialCatalog = "master", // Required for failover partner to work
                ConnectTimeout = 5,
                Encrypt = false,
                Pooling = false, // Disable pooling to ensure a fresh connection attempt is made
                MultiSubnetFailover = false,
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            connection.Open();

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
                Pooling = false, // Disable pooling to ensure a fresh connection attempt is made on failover
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            connection.Open();


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
#if NETFRAMEWORK
                TransparentNetworkIPResolution = false,
#endif
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
            Assert.Equal(0, server.Login7Count);
            Assert.Equal(1, failoverServer.PreLoginCount - failoverServer.AbandonedPreLoginCount);
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
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false,
                Pooling = false, // Disable pooling to ensure a fresh connection attempt is made
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Act
            // First login receives the transient token; outer connect retry opens a fresh parser
            // and retries against the same primary endpoint.
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, server.PreLoginCount - server.AbandonedPreLoginCount);
            // Login-phase errors must NOT trigger failover alternation
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [Trait("Category", "flaky")]
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

        /// <summary>
        /// Verifies user-provided failover partner does not change behavior for login-phase
        /// transient SQL errors; retries stay on primary.
        /// </summary>
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [Trait("Category", "flaky")]
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
            // Even with a configured partner, this path should use outer connect retry
            // against primary rather than alternation inside LoginWithFailover.
            connection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);

            // Failures should prompt the client to return to the original server, resulting in a login count of 2
            Assert.Equal(2, server.PreLoginCount - server.AbandonedPreLoginCount);
            // Login-phase errors must NOT trigger failover alternation
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Trait("Category", "flaky")]
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [Trait("Category", "flaky")]
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
        [Trait("Category", "flaky")]
        public void TransientFault_IgnoreServerProvidedFailoverPartner_ShouldConnectToUserProvidedPartner()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.IgnoreServerProvidedFailoverPartner = true;

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
                Pooling = true
            };
            SqlConnection connection = new(builder.ConnectionString);

            // Connect once to the primary to trigger it to send the failover partner
            connection.Open();
            Assert.Equal("invalidhost", (connection.InnerConnection as SqlConnectionInternal)!.ServerProvidedFailoverPartner);

            // Close the connection to return it to the pool
            connection.Close();

            // Act
            // Dispose of the server to trigger a failover
            server.Dispose();

            // Clear the pool to ensure the next connection attempt doesn't reuse
            // the pooled connection to the now-disposed primary server.
            SqlConnection.ClearAllPools();

            // Opening a new connection will use the failover partner stored in the pool group.
            // This will fail if the server provided failover partner was stored to the pool group.
            using SqlConnection failoverConnection = new(builder.ConnectionString);
            failoverConnection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, failoverConnection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", failoverConnection.DataSource);
            // 1 for the initial connection
            Assert.Equal(1, server.PreLoginCount - server.AbandonedPreLoginCount);
            // 1 for the failover connection
            Assert.Equal(1, failoverServer.PreLoginCount - failoverServer.AbandonedPreLoginCount);
        }

        /// <summary>
        /// Async parity for primary-only retry behavior on login-phase transient SQL errors.
        /// </summary>
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public async Task TransientFault_Async_ShouldConnectToPrimary_NotFailover(uint errorCode)
        {
            // Async parity for TransientFault_ShouldConnectToPrimary.
            // A transient login-token error must be retried against the primary;
            // the failover partner must never be contacted.

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
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
                Encrypt = false,
                Pooling = false,
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Asserts async open follows the same retry and failover-selection rules as sync.
            await connection.OpenAsync();

            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);
            Assert.Equal(2, server.PreLoginCount - server.AbandonedPreLoginCount);
            // Login-phase errors must NOT trigger failover alternation
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        /// <summary>
        /// Async parity with user-provided partner: login-phase transient SQL errors should
        /// still retry on primary without failover alternation.
        /// </summary>
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        // Quarantined due to intermittent failure:
        //     Assert.Equal() Failure: Strings differ
        //                            ↓ (pos 14)
        //   Expected: "localhost,56862"
        //   Actual:   "localhost,56861"
        [Trait("Category", "flaky")]
        public async Task TransientFault_WithUserProvidedPartner_Async_ShouldConnectToPrimary_NotFailover(uint errorCode)
        {
            // Async parity for TransientFault_WithUserProvidedPartner_ShouldConnectToPrimary.
            // Even with a user-provided failover partner, a login-token error must not
            // cause alternation to the failover server.

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
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
                Encrypt = false,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // Asserts async open with explicit partner still avoids failover alternation.
            await connection.OpenAsync();

            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);
            Assert.Equal(2, server.PreLoginCount - server.AbandonedPreLoginCount);
            // Login-phase errors must NOT trigger failover alternation
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        /// <summary>
        /// Verifies pooled connections are not cleared and failover is not attempted when a
        /// login-phase transient SQL error occurs with a user-provided failover partner.
        /// </summary>
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFault_WithUserProvidedPartner_Pooling_ShouldNotClearPool_NotFailover(uint errorCode)
        {
            // With pooling enabled and a user-provided failover partner, a transient
            // login-token error must not clear the pool and must not contact the failover server.

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            // Start with errors disabled so the pool warms up successfully.
            using TransientTdsErrorTdsServer server = new(
                new TransientTdsErrorTdsServerArguments()
                {
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = SqlConnectionEncryptOption.Optional,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                Pooling = true,
            };

            // Keep one connection open so the next Open() cannot reuse it and must perform login.
            using SqlConnection warmup = new(builder.ConnectionString);
            warmup.Open();

            // Enable the transient error for the next login attempt.
            server.SetErrorBehavior(true, errorCode);

            // ConnectRetryCount > 0 (default 1) so the client retries and succeeds.
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);

            connection.Close();
            warmup.Close();

            // If the pool is not cleared, this open should reuse a pooled connection without a new login.
            using SqlConnection pooledConnection = new(builder.ConnectionString);
            pooledConnection.Open();

            Assert.Equal(ConnectionState.Open, pooledConnection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", pooledConnection.DataSource);

            // 1 warmup login + 1 failed login + 1 retry login.
            Assert.Equal(3, server.PreLoginCount - server.AbandonedPreLoginCount);
            Assert.Equal(3, server.Login7Count);
            // Failover server must never have been contacted.
            Assert.Equal(0, failoverServer.PreLoginCount - failoverServer.AbandonedPreLoginCount);
            Assert.Equal(0, failoverServer.Login7Count);
        }

        /// <summary>
        /// Verifies ConnectRetryCount=0 propagates login-phase transient SQL errors immediately
        /// and never attempts failover alternation.
        /// </summary>
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        public void TransientFault_RetryDisabled_WithUserProvidedPartner_ShouldFail_NotFailover(uint errorCode)
        {
            // When ConnectRetryCount = 0 and the server returns a login-phase token error,
            // the exception must propagate immediately and the failover partner must not be
            // contacted (parser state is not Closed, so the new guard must kick in).

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
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
                ConnectRetryCount = 0,
                Encrypt = false,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
            };
            using SqlConnection connection = new(builder.ConnectionString);

            // No outer connect retry is allowed, so the first transient error should surface.
            SqlException ex = Assert.Throws<SqlException>(() => connection.Open());

            Assert.Equal((int)errorCode, ex.Number);
            Assert.Equal(ConnectionState.Closed, connection.State);
            // The parser was not closed (login-phase error), so the failover alternation branch
            // must not have been entered.
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        /// <summary>
        /// Isolates the parser-state guard by using a non-fatal login error token: without the
        /// guard, LoginWithFailover alternates to partner; with the guard, retry stays on primary.
        /// </summary>
        [Fact]
        public void NonFatalTransientLoginError_WithUserProvidedPartner_ShouldRetryPrimary_NotFailover()
        {
            // This test isolates the parser-state guard added to LoginWithFailover.
            // We emit a transient login error with non-fatal severity so the connection
            // is not automatically doomed/broken by existing breakConnection logic.

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            using TransientTdsErrorTdsServer server = new(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = 40613,
                    // Use non-fatal severity so break/doom logic does not short-circuit the path.
                    ErrorClass = 16,
                    RepeatCount = 1,
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false,
                Pooling = false,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
            };

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{server.EndPoint.Port}", connection.DataSource);
            Assert.Equal(2, server.PreLoginCount - server.AbandonedPreLoginCount);
            Assert.Equal(0, failoverServer.PreLoginCount - failoverServer.AbandonedPreLoginCount);
        }

        /// <summary>
        /// Verifies opt-in legacy behavior: login-phase SQL errors can alternate to the
        /// failover partner when UseLegacyFailoverAlternationOnLoginSqlErrors is enabled.
        /// </summary>
        [Fact]
        public void NonFatalTransientLoginError_WithLegacySwitch_ShouldAlternateToFailoverPartner()
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.UseLegacyFailoverAlternationOnLoginSqlErrors = true;

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            using TransientTdsErrorTdsServer server = new(
                new TransientTdsErrorTdsServerArguments()
                {
                    IsEnabledTransientError = true,
                    Number = 40613,
                    // Keep the login token non-fatal so parser state, not break/doom behavior,
                    // drives this branch decision.
                    ErrorClass = 16,
                    RepeatCount = 1,
                    FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                ConnectTimeout = 30,
                ConnectRetryInterval = 1,
                Encrypt = false,
                Pooling = false,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
            };

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal(1, server.PreLoginCount - server.AbandonedPreLoginCount);
            Assert.Equal(1, failoverServer.PreLoginCount - failoverServer.AbandonedPreLoginCount);
        }
    }
}
