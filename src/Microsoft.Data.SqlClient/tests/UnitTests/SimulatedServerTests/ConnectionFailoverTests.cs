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
    // TODO: Do we need this collection?  It serializes all tests within it, which we probably don't
    // need since each test uses its own TDS Server with ephemeral listen port.
    [Collection("SimulatedServerTests")]
    public class ConnectionFailoverTests
    {
        //TODO parameterize for transient errors
        [Trait("Category", "flaky")]
        [Theory]
        [InlineData(40613)]
        [InlineData(42108)] // Flaky: [errorcode: 42108] Assert.Equal() Failure: Strings differ | Assert.Equal() Failure: Values differ
        [InlineData(42109)] // Flaky: [errorcode: 42109] Assert.Equal() Failure: Strings differ
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

            /* TODO: Fix flaky Assert.Equal() in this test method;
            Assert.Equal() Failure: Strings differ
                                        ↓ (pos 14)
                Expected: "localhost,51966"
                Actual:   "localhost,51965"
                                        ↑ (pos 14)
            */
            Assert.Equal($"localhost,{initialServer.EndPoint.Port}", connection.DataSource);
            Assert.Equal($"localhost,{initialServer.EndPoint.Port}", secondConnection.DataSource);

            // 1 for the initial connection, 2 for the second connection
            /* TODO: Fix flaky test failure for errorcode 42108:
            Assert.Equal() Failure: Values differ
                Expected: 3
                Actual:   4
            */
            Assert.Equal(3, initialServer.PreLoginCount - initialServer.AbandonedPreLoginCount);
            // A failover should not be triggered, so prelogin count to the failover server should be 0
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Trait("Category", "flaky")] // Assert.Equal() Failure: Values differ | System.ComponentModel.Win32Exception : The wait operation timed out.
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

            /* TODO: Fix flaky test failure:
            Microsoft.Data.SqlClient.SqlException : A network-related or instance-specific error occurred while establishing a connection to SQL Server.
                The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured
                to allow remote connections. (provider: TCP Provider, error: 0 - The wait operation timed out.)
                ---- System.ComponentModel.Win32Exception : The wait operation timed out.
            */
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

            /* TODO: Fix flaky Assert.Equal() in this test method;
             Assert.Equal() Failure: Values differ
                Expected: 1
                Actual:   2
            */
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

        [Trait("Category", "flaky")]
        //     Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NetworkTimeout_ShouldFail [1 s]
        // ##[error]EXEC(0,0): Error Message:
        // EXEC : error Message:  [D:\a\_work\1\s\build.proj]
        //      Assert.Equal() Failure: Values differ
        //   Expected: 1
        //   Actual:   0
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NetworkTimeout_ShouldFail() in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\tests\UnitTests\SimulatedServerTests\ConnectionFailoverTests.cs:line 220
        //      at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
        //      at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
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

        [Trait("Category", "flaky")]
        //     Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NetworkDelay_ShouldConnectToPrimary [4 s]
        // ##[error]EXEC(0,0): Error Message:
        // EXEC : error Message:  [D:\a\_work\1\s\build.proj]
        //      Microsoft.Data.SqlClient.SqlException : Connection Timeout Expired.  The timeout period elapsed during the post-login phase.  The connection could have timed out while waiting for server to complete the login process and respond; Or it could have timed out while attempting to create multiple active connections.  The duration spent while attempting to connect to this server was - [Pre-Login] initialization=0; handshake=17; [Login] initialization=0; authentication=0; [Post-Login] complete=3999;
        //   ---- System.ComponentModel.Win32Exception : The wait operation timed out.
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.Connection.SqlConnectionInternal.OnError(SqlException exception, Boolean breakConnection, Action`1 wrapCloseInAction) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Connection\SqlConnectionInternal.cs:line 1241
        //      at Microsoft.Data.SqlClient.TdsParser.ThrowExceptionAndWarning(TdsParserStateObject stateObj, SqlCommand command, Boolean callerHasConnectionLock, Boolean asyncClose) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParser.cs:line 1721
        //      at Microsoft.Data.SqlClient.TdsParserStateObject.ThrowExceptionAndWarning(Boolean callerHasConnectionLock, Boolean asyncClose) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParserStateObject.cs:line 1243
        //      at Microsoft.Data.SqlClient.TdsParserStateObject.ReadSniError(TdsParserStateObject stateObj, UInt32 error) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParserStateObject.cs:line 4168
        //      at Microsoft.Data.SqlClient.TdsParserStateObject.ReadSniSyncOverAsync() in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParserStateObject.cs:line 3581
        //      at Microsoft.Data.SqlClient.TdsParserStateObject.TryReadNetworkPacket() in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParserStateObject.cs:line 3480
        //      at Microsoft.Data.SqlClient.TdsParserStateObject.TryPrepareBuffer() in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParserStateObject.cs:line 1464
        //      at Microsoft.Data.SqlClient.TdsParserStateObject.TryReadByte(Byte& value) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParserStateObject.cs:line 1780
        //      at Microsoft.Data.SqlClient.TdsParser.TryRun(RunBehavior runBehavior, SqlCommand cmdHandler, SqlDataReader dataStream, BulkCopySimpleResultSet bulkCopyHandler, TdsParserStateObject stateObj, Boolean& dataReady) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParser.cs:line 2456
        //      at Microsoft.Data.SqlClient.TdsParser.Run(RunBehavior runBehavior, SqlCommand cmdHandler, SqlDataReader dataStream, BulkCopySimpleResultSet bulkCopyHandler, TdsParserStateObject stateObj) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\TdsParser.cs:line 2343
        //      at Microsoft.Data.SqlClient.Connection.SqlConnectionInternal.CompleteLogin(Boolean enlistOK) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Connection\SqlConnectionInternal.cs:line 2208
        //      at Microsoft.Data.SqlClient.Connection.SqlConnectionInternal.AttemptOneLogin(ServerInfo serverInfo, String newPassword, SecureString newSecurePassword, TimeoutTimer timeout, Boolean withFailover) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Connection\SqlConnectionInternal.cs:line 2170
        //      at Microsoft.Data.SqlClient.Connection.SqlConnectionInternal.LoginNoFailover(ServerInfo serverInfo, String newPassword, SecureString newSecurePassword, Boolean redirectedUserInstance, SqlConnectionOptions connectionOptions, SqlCredential credential, TimeoutTimer timeout) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Connection\SqlConnectionInternal.cs:line 3214
        //      at Microsoft.Data.SqlClient.Connection.SqlConnectionInternal.OpenLoginEnlist(TimeoutTimer timeout, SqlConnectionOptions connectionOptions, SqlCredential credential, String newPassword, SecureString newSecurePassword, Boolean redirectedUserInstance) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Connection\SqlConnectionInternal.cs:line 3793
        //      at Microsoft.Data.SqlClient.Connection.SqlConnectionInternal..ctor(DbConnectionPoolIdentity identity, SqlConnectionOptions connectionOptions, TimeoutTimer timeout, SqlCredential credential, DbConnectionPoolGroupProviderInfo providerInfo, String newPassword, SecureString newSecurePassword, Boolean redirectedUserInstance, SessionData reconnectSessionData, Boolean applyTransientFaultHandling, String accessToken, IDbConnectionPool pool, Func`3 accessTokenCallback, SspiContextProvider sspiContextProvider) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\Connection\SqlConnectionInternal.cs:line 399
        //      at Microsoft.Data.SqlClient.SqlConnectionFactory.CreateConnection(SqlConnectionOptions options, ConnectionPoolKey poolKey, DbConnectionPoolGroupProviderInfo poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection, TimeoutTimer timeout) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnectionFactory.cs:line 687
        //      at Microsoft.Data.SqlClient.SqlConnectionFactory.CreateNonPooledConnection(DbConnection owningConnection, DbConnectionPoolGroup poolGroup, TimeoutTimer timeout) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnectionFactory.cs:line 124
        //      at Microsoft.Data.SqlClient.SqlConnectionFactory.TryGetConnection(DbConnection owningConnection, TaskCompletionSource`1 retry, DbConnectionInternal oldConnection, TimeoutTimer timeout, Boolean forceNewConnection, DbConnectionInternal& connection) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnectionFactory.cs:line 417
        //      at Microsoft.Data.ProviderBase.DbConnectionInternal.TryOpenConnectionInternal(DbConnection outerConnection, SqlConnectionFactory connectionFactory, TaskCompletionSource`1 retry, Boolean forceNewConnection, TimeoutTimer timeout) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\ProviderBase\DbConnectionInternal.cs:line 940
        //      at Microsoft.Data.ProviderBase.DbConnectionClosed.TryOpenConnection(DbConnection outerConnection, SqlConnectionFactory connectionFactory, TaskCompletionSource`1 retry, TimeoutTimer timeout) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\ProviderBase\DbConnectionClosed.cs:line 69
        //      at Microsoft.Data.SqlClient.SqlConnection.TryOpenInner(TaskCompletionSource`1 retry, Boolean forceNewConnection) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnection.cs:line 2287
        //      at Microsoft.Data.SqlClient.SqlConnection.TryOpen(TaskCompletionSource`1 retry, Boolean forceNewConnection, SqlConnectionOverrides overrides) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnection.cs:line 2245
        //      at Microsoft.Data.SqlClient.SqlConnection.Open(SqlConnectionOverrides overrides) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnection.cs:line 1619
        //      at Microsoft.Data.SqlClient.SqlConnection.Open() in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\src\Microsoft\Data\SqlClient\SqlConnection.cs:line 1596
        //      at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NetworkDelay_ShouldConnectToPrimary() in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\tests\UnitTests\SimulatedServerTests\ConnectionFailoverTests.cs:line 260
        //      at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
        //      at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)
        //      at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
        //   ----- Inner Stack Trace -----
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
        [Trait("Category", "flaky")] // Assert.Equal() Failure: Values differ | System.ComponentModel.Win32Exception : The wait operation timed out
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
            /* TODO: Fix flaky test failure:
            Microsoft.Data.SqlClient.SqlException : Connection Timeout Expired.  The timeout period elapsed during the post-login phase.
                The connection could have timed out while waiting for server to complete the login process and respond;
                Or it could have timed out while attempting to create multiple active connections.
                This failure occurred while attempting to connect to the Principle server.
                The duration spent while attempting to connect to this server was - [Pre-Login] initialization=6; handshake=80;
                [Login] initialization=0; authentication=0; [Post-Login] complete=5443;
                ---- System.ComponentModel.Win32Exception : The wait operation timed out
            */
            connection.Open();


            // Assert
            // On the first connection attempt, failover partner information is available in the connection string,
            // so the connection will retry on the failover server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", connection.DataSource);

            /* TODO: Fix flaky Assert.Equal() in this test method;
            Assert.Equal() Failure: Values differ
                Expected: 1
                Actual:   2
            */
            Assert.Equal(1, failoverServer.PreLoginCount);
            Assert.Equal(1, server.PreLoginCount);
        }

        [Fact]
        [Trait("Category", "flaky")] // Assert.Equal() Failure: Values differ | System.ComponentModel.Win32Exception : The wait operation timed out
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
            /* TODO: Fix flaky test failure:
            Microsoft.Data.SqlClient.SqlException : Connection Timeout Expired.
                The timeout period elapsed during the post-login phase.
                The connection could have timed out while waiting for server to complete the login process and respond;
                Or it could have timed out while attempting to create multiple active connections.
                This failure occurred while attempting to connect to the Principle server.
                The duration spent while attempting to connect to this server was - [Pre-Login] initialization=4; handshake=68;
                [Login] initialization=0; authentication=0; [Post-Login] complete=5443;
                ---- System.ComponentModel.Win32Exception : The wait operation timed out
            */
            /* TODO: Fix flaky test failure:
            Microsoft.Data.SqlClient.SqlException : Connection Timeout Expired.
                The timeout period elapsed while attempting to consume the pre-login handshake acknowledgement.
                This could be because the pre-login handshake failed or the server was unable to respond back in time.
                This failure occurred while attempting to connect to the Principle server.
                The duration spent while attempting to connect to this server was - [Pre-Login] initialization=0; handshake=5285;
                ---- System.ComponentModel.Win32Exception : The wait operation timed out.
            */
            connection.Open();

            // Assert
            // On the first connection attempt, failover partner information is available in the connection string,
            // so the connection will retry on the failover server.
            Assert.Equal(ConnectionState.Open, connection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", connection.DataSource);
            /* TODO: Fix flaky Assert.Equal() in this test method;
            Assert.Equal() Failure: Values differ
                Expected: 1
                Actual:   2
            */
            Assert.Equal(1, server.PreLoginCount);
            Assert.Equal(0, server.Login7Count);
            Assert.Equal(1, failoverServer.PreLoginCount - failoverServer.AbandonedPreLoginCount);
        }

        /// <summary>
        /// Verifies login-phase transient SQL errors are retried on the primary endpoint and
        /// do not trigger failover-partner alternation.
        /// </summary>
        [Trait("Category", "flaky")]
        //     Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_ShouldConnectToPrimary(errorCode: 42109) [6 s]
        // ##[error]EXEC(0,0): Error Message:
        // EXEC : error Message:  [D:\a\_work\1\s\build.proj]
        //      Assert.Equal() Failure: Values differ
        //   Expected: 2
        //   Actual:   3
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_ShouldConnectToPrimary(UInt32 errorCode) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\tests\UnitTests\SimulatedServerTests\ConnectionFailoverTests.cs:line 451
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
        [Trait("Category", "flaky")] // [errorcode: 40613] Assert.Equal() Failure: Values differ
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
            /* TODO: Fix flaky Assert.Equal() in this test method;
            Assert.Equal() Failure: Values differ
                Expected: 2
                Actual:   3
            */
            Assert.Equal(2, server.PreLoginCount - server.AbandonedPreLoginCount);
            // Login-phase errors must NOT trigger failover alternation
            Assert.Equal(0, failoverServer.PreLoginCount);
        }

        [Trait("Category", "flaky")]
        //              Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_WithUserProvidedPartner_RetryDisabled_ShouldFail(errorCode: 42109) [2 s]
        // EXEC : error Message:  [/Users/runner/work/1/s/build2.proj]
        //      Assert.Fail() Failure
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_WithUserProvidedPartner_RetryDisabled_ShouldFail(UInt32 errorCode) in /Users/runner/work/1/s/src/Microsoft.Data.SqlClient/tests/UnitTests/SimulatedServerTests/ConnectionFailoverTests.cs:line 526
        //      at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
        //      at System.Reflection.MethodBaseInvoker.InvokeDirectByRefWithFewArgs(Object obj, Span`1 copyOfArgs, BindingFlags invokeAttr)
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
            // TODO: Fix flaky Assert.Fail() in this test method;
            // [errorcode: 42109] Assert.Fail() Failure
            Assert.Fail();
        }

        [Fact]
        [Trait("Category", "flaky")] // Assert.Equal() Failure: Values differ | System.ComponentModel.Win32Exception : The wait operation timed out
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
            /* TODO: Fix flaky test failure:
            Microsoft.Data.SqlClient.SqlException : A network-related or instance-specific error occurred while establishing a connection to SQL Server.
                The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections.
                (provider: TCP Provider, error: 0 - The wait operation timed out.)
                ---- System.ComponentModel.Win32Exception : The wait operation timed out.
            */
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

            /* TODO: Fix flaky Assert.Equal() in this test method;
            Assert.Equal() Failure: Strings differ
                                        ↓ (pos 13)
                Expected: "localhost,51479"
                Actual:   "localhost,51480"
                                        ↑ (pos 13)
            */
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", failoverConnection.DataSource);
            // 1 for the initial connection
            /* TODO: Fix flaky Assert.Equal() in this test method;
            Assert.Equal() Failure: Values differ
                Expected: 1
                Actual:   2
            */
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
        [Trait("Category", "flaky")]
        //     Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_WithUserProvidedPartner_Async_ShouldConnectToPrimary_NotFailover(errorCode: 42109) [4 s]
        // ##[error]EXEC(0,0): Error Message:
        // EXEC : error Message:  [D:\a\_work\1\s\build.proj]
        //      Assert.Equal() Failure: Strings differ
        //                            Γåô (pos 14)
        //   Expected: "localhost,60625"
        //   Actual:   "localhost,60624"
        //                            Γåæ (pos 14)
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_WithUserProvidedPartner_Async_ShouldConnectToPrimary_NotFailover(UInt32 errorCode) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\tests\UnitTests\SimulatedServerTests\ConnectionFailoverTests.cs:line 816
        //   --- End of stack trace from previous location ---
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
        [Trait("Category", "flaky")]
        //     Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_WithUserProvidedPartner_Pooling_ShouldNotClearPool_NotFailover(errorCode: 40613) [2 s]
        // ##[error]EXEC(0,0): Error Message:
        // EXEC : error Message:  [D:\a\_work\1\s\build.proj]
        //      Assert.Equal() Failure: Strings differ
        //                            Γåô (pos 14)
        //   Expected: "localhost,50152"
        //   Actual:   "localhost,50151"
        //                            Γåæ (pos 14)
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.TransientFault_WithUserProvidedPartner_Pooling_ShouldNotClearPool_NotFailover(UInt32 errorCode) in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\tests\UnitTests\SimulatedServerTests\ConnectionFailoverTests.cs:line 920
        //      at InvokeStub_ConnectionFailoverTests.TransientFault_WithUserProvidedPartner_Pooling_ShouldNotClearPool_NotFailover(Object, Span`1)
        //      at System.Reflection.MethodBaseInvoker.InvokeWithOneArg(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
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
        [Trait("Category", "flaky")] // Assert.Throws() Failure: No exception was thrown
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
            // TODO: Fix flakiness of Assert.Throws() in this test method; currently, if the exception is not thrown as expected,
            // Assert.Throws will fail the test with an assertion failure ("No exception was thrown").
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
        [Trait("Category", "flaky")]
        //     Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NonFatalTransientLoginError_WithUserProvidedPartner_ShouldRetryPrimary_NotFailover [1 s]
        // ##[error]EXEC(0,0): Error Message:
        // EXEC : error Message:  [/mnt/vss/_work/1/s/build.proj]
        //      Assert.Equal() Failure: Values differ
        //   Expected: 0
        //   Actual:   1
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NonFatalTransientLoginError_WithUserProvidedPartner_ShouldRetryPrimary_NotFailover() in /mnt/vss/_work/1/s/src/Microsoft.Data.SqlClient/tests/UnitTests/SimulatedServerTests/ConnectionFailoverTests.cs:line 1006
        //      at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
        //      at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
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
        [Trait("Category", "flaky")]
        //     Failed Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NonFatalTransientLoginError_WithLegacySwitch_ShouldAlternateToFailoverPartner [2 s]
        // ##[error]EXEC(0,0): Error Message:
        // EXEC : error Message:  [D:\a\_work\1\s\build.proj]
        //      Assert.Equal() Failure: Strings differ
        //                            Γåô (pos 14)
        //   Expected: "localhost,60991"
        //   Actual:   "localhost,60992"
        //                            Γåæ (pos 14)
        //     Stack Trace:
        //        at Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests.ConnectionFailoverTests.NonFatalTransientLoginError_WithLegacySwitch_ShouldAlternateToFailoverPartner() in D:\a\_work\1\s\src\Microsoft.Data.SqlClient\tests\UnitTests\SimulatedServerTests\ConnectionFailoverTests.cs:line 1114
        //      at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
        //      at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
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
