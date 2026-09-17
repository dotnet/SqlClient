// Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
// file to you under the MIT license.  See the LICENSE file in the project root for more
// information.

using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.EnvChange;
using Microsoft.SqlServer.TDS.Info;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Tests for database context preservation across reconnections.
    /// Reproduces the scenario from dotnet/SqlClient#4108: after executing USE [db] and then
    /// losing the connection, the reconnected session should retain the switched database.
    /// </summary>
    [Collection("SimulatedServerTests")]
    public class DatabaseContextReconnectionTests
    {
        private const string InitialDatabase = "initialdb";
        private const string SwitchedDatabase = "switcheddb";

        /// <summary>
        /// Minimum delay (ms) after disconnecting clients to ensure the
        /// <c>CheckConnectionWindow</c> (5 ms) in <c>ValidateSNIConnection</c> has expired,
        /// so that the broken connection is detected before the next command.
        /// </summary>
        private const int PostDisconnectDelayMs = 50;

        /// <summary>
        /// Maximum time (ms) to wait for the client to detect a server-side disconnect.
        /// On CI VMs with native SNI, socket close detection can take significantly longer
        /// than on local machines due to virtualized networking and CPU contention.
        /// </summary>
        private const int DisconnectDetectionTimeoutMs = 5000;

        #region Test Infrastructure

        /// <summary>
        /// Controls how the test server handles the database ENV_CHANGE during a session
        /// recovery login.
        /// </summary>
        private enum RecoveryDatabaseBehavior
        {
            /// <summary>
            /// Sends ENV_CHANGE with the recovered database name from the session recovery
            /// feature request.
            /// </summary>
            SendRecoveredDatabase,

            /// <summary>
            /// Sends ENV_CHANGE with the login packet's initial catalog instead of the
            /// recovered database, producing a database mismatch for diagnostics.
            /// </summary>
            SendInitialCatalog,

            /// <summary>
            /// Omits the database ENV_CHANGE token from the recovery login response.
            /// </summary>
            OmitDatabaseEnvChange,
        }

        /// <summary>
        /// A query engine that recognises USE [database] commands and updates the session's
        /// current database accordingly, returning the correct EnvChange tokens.
        /// </summary>
        private sealed class DatabaseContextQueryEngine : QueryEngine
        {
            private static readonly Regex s_useDbRegex = new(
                @"^\s*use\s+\[?(?<db>[^\]\s;]+)\]?\s*;?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public DatabaseContextQueryEngine(TdsServerArguments arguments)
                : base(arguments)
            {
            }

            /// <summary>
            /// Number of <c>USE</c> batches this engine has executed.
            /// </summary>
            public int UseDatabaseCount { get; private set; }

            /// <summary>
            /// Database named by the most recent <c>USE</c> batch, or <c>null</c> if none.
            /// </summary>
            public string? LastUseDatabase { get; private set; }

            protected override TDSMessageCollection CreateQueryResponse(
                ITDSServerSession session, TDSSQLBatchToken batchRequest)
            {
                string text = batchRequest.Text;
                Match match = s_useDbRegex.Match(text);

                if (match.Success)
                {
                    return HandleUseDatabase(session, match.Groups["db"].Value);
                }

                return base.CreateQueryResponse(session, batchRequest);
            }

            /// <summary>
            /// Moves the simulated session to <paramref name="newDatabase"/> and returns the
            /// tokens a server sends for a database change: an ENV_CHANGE carrying both the
            /// old and new names, the matching INFO(5701) message, and a final DONE.  Also
            /// records the batch so tests can assert the server, not just the client, moved.
            /// </summary>
            /// <param name="session">The session whose current database is updated.</param>
            /// <param name="newDatabase">The database name parsed from the batch.</param>
            /// <returns>The response tokens for the <c>USE</c> batch.</returns>
            private TDSMessageCollection HandleUseDatabase(ITDSServerSession session,
                string newDatabase)
            {
                string oldDatabase = session.Database;
                session.Database = newDatabase;

                UseDatabaseCount++;
                LastUseDatabase = newDatabase;

                var envChange = new TDSEnvChangeToken(
                    TDSEnvChangeTokenType.Database, newDatabase, oldDatabase);

                var infoToken = new TDSInfoToken(5701, 2, 0,
                    $"Changed database context to '{newDatabase}'.", "TestServer");

                var doneToken = new TDSDoneToken(TDSDoneTokenStatusType.Final);

                var response = new TDSMessage(
                    TDSMessageType.Response, envChange, infoToken, doneToken);

                return new TDSMessageCollection(response);
            }
        }

        /// <summary>
        /// A TDS server subclass that supports testing database context recovery during
        /// reconnection.  Exposes <see cref="GenericTdsServer{T}.DisconnectAllClients"/>
        /// and allows controlling whether the login response carries the recovered database
        /// or the initial catalog via <see cref="RecoveryBehavior"/>.
        /// </summary>
        private sealed class DisconnectableTdsServer : GenericTdsServer<TdsServerArguments>
        {
            private readonly DatabaseContextQueryEngine _queryEngine;

            public int Port => EndPoint.Port;

            /// <summary>
            /// Number of <c>USE</c> batches the server has executed.  Tests compare this across
            /// a reconnection to prove a corrective <c>USE</c> actually reached the server,
            /// which <see cref="SqlConnection.Database"/> alone cannot show.
            /// </summary>
            public int UseDatabaseCount => _queryEngine.UseDatabaseCount;

            /// <summary>
            /// Database named by the most recent <c>USE</c> batch, or <c>null</c> if none.
            /// </summary>
            public string? LastUseDatabase => _queryEngine.LastUseDatabase;

            /// <summary>
            /// Controls how the server responds to session recovery with a changed database.
            /// Default is <see cref="RecoveryDatabaseBehavior.SendRecoveredDatabase"/> (correct
            /// server behavior).
            /// </summary>
            public RecoveryDatabaseBehavior RecoveryBehavior { get; set; }
                = RecoveryDatabaseBehavior.SendRecoveredDatabase;

            /// <summary>
            /// The database name the server used in the most recent login ENV_CHANGE response.
            /// Useful for test assertions.
            /// </summary>
            public string? LastLoginResponseDatabase { get; private set; }

            public DisconnectableTdsServer(
                RecoveryDatabaseBehavior behavior = RecoveryDatabaseBehavior.SendRecoveredDatabase)
                : this(new TdsServerArguments(), behavior)
            {
            }

            private DisconnectableTdsServer(TdsServerArguments args,
                RecoveryDatabaseBehavior behavior)
                : this(args, new DatabaseContextQueryEngine(args), behavior)
            {
            }

            private DisconnectableTdsServer(TdsServerArguments args,
                DatabaseContextQueryEngine queryEngine,
                RecoveryDatabaseBehavior behavior)
                : base(args, queryEngine)
            {
                _queryEngine = queryEngine;
                RecoveryBehavior = behavior;
                Start();
            }

            public new void DisconnectAllClients()
                => base.DisconnectAllClients();

            /// <summary>
            /// Overrides the login response to control whether the database ENV_CHANGE
            /// carries the recovered database or the initial catalog.
            /// </summary>
            protected override TDSMessageCollection OnAuthenticationCompleted(
                ITDSServerSession session)
            {
                if (RecoveryBehavior == RecoveryDatabaseBehavior.SendInitialCatalog
                    && session.IsSessionRecoveryEnabled
                    && Login7Count > 1)
                {
                    // Force the initial catalog before the base class builds the login
                    // response, producing a mismatched database ENV_CHANGE for the test.
                    session.Database = InitialDatabase;
                }

                TDSMessageCollection result = base.OnAuthenticationCompleted(session);

                TDSMessage msg = result[0];

                if (RecoveryBehavior == RecoveryDatabaseBehavior.OmitDatabaseEnvChange
                    && session.IsSessionRecoveryEnabled
                    && Login7Count > 1)
                {
                    // Strip the database ENV_CHANGE and its accompanying INFO(5701)
                    // token from the response to simulate a server that never sends
                    // the database change notification.
                    for (int i = msg.Count - 1; i >= 0; i--)
                    {
                        if (msg[i] is TDSEnvChangeToken ec
                            && ec.Type == TDSEnvChangeTokenType.Database)
                        {
                            msg.RemoveAt(i);
                        }
                        else if (msg[i] is TDSInfoToken info && info.Number == 5701)
                        {
                            msg.RemoveAt(i);
                        }
                    }

                    LastLoginResponseDatabase = null;
                }
                else
                {
                    // Capture the database from the ENV_CHANGE for test assertions.
                    foreach (var token in msg)
                    {
                        if (token is TDSEnvChangeToken envChange
                            && envChange.Type == TDSEnvChangeTokenType.Database)
                        {
                            LastLoginResponseDatabase = (string)envChange.NewValue;
                        }
                    }
                }

                return result;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Builds a connection string for the simulated server with connection resiliency
        /// enabled and pooling disabled, so each test drives its own reconnection rather
        /// than receiving a pooled replacement connection.
        /// </summary>
        /// <param name="port">The simulated server's listening port.</param>
        /// <param name="initialCatalog">The database sent as the login initial catalog.</param>
        /// <returns>A builder for the configured connection string.</returns>
        private static SqlConnectionStringBuilder CreateConnectionStringBuilder(int port,
            string initialCatalog = InitialDatabase)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{port}",
                InitialCatalog = initialCatalog,
                Encrypt = SqlConnectionEncryptOption.Optional,
                ConnectRetryCount = 2,
                ConnectRetryInterval = 1,
                ConnectTimeout = 10,
                Pooling = false,
            };
        }

        /// <summary>
        /// Disconnect all server clients, then execute the given command, retrying until the
        /// client detects the broken connection and transparently reconnects.
        /// <para>
        /// On local machines the disconnect is usually detected within one attempt after a
        /// short sleep.  On CI VMs with native SNI (net462/Windows), socket close detection
        /// can be significantly slower due to virtualized networking and CPU contention.
        /// This method accounts for that by retrying the command with increasing delays
        /// instead of relying on a single fixed sleep.
        /// </para>
        /// <para>
        /// The method also verifies reconnection via <see cref="GenericTdsServer.Login7Count"/>
        /// because on some CI VMs the server-side socket close may not have fully propagated
        /// to the client, allowing the command to succeed on the stale connection without
        /// triggering reconnection.  In that case the method re-disconnects and retries.
        /// </para>
        /// </summary>
        private static void DisconnectAndExecute(DisconnectableTdsServer server,
            SqlConnection connection, string commandText = "SELECT 1")
        {
            int loginCountBefore = server.Login7Count;
            server.DisconnectAllClients();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int delay = PostDisconnectDelayMs;

            while (true)
            {
                System.Threading.Thread.Sleep(delay);

                try
                {
                    using SqlCommand cmd = new(commandText, connection);
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException) when (sw.ElapsedMilliseconds < DisconnectDetectionTimeoutMs)
                {
                    // The broken connection was detected but reconnection failed
                    // (shouldn't normally happen since the server is still listening).
                    // Retry with a longer delay.
                    delay = System.Math.Min(delay * 2, 500);
                    continue;
                }

                // Command succeeded.  Verify that reconnection actually happened by
                // checking whether the server saw a new Login7 handshake.  On slow CI VMs
                // the TCP FIN may not have propagated yet, allowing the command to succeed
                // on the stale connection without triggering reconnection.
                if (server.Login7Count > loginCountBefore)
                {
                    return; // Reconnection confirmed.
                }

                Assert.True(sw.ElapsedMilliseconds < DisconnectDetectionTimeoutMs,
                    $"Reconnection did not occur within {DisconnectDetectionTimeoutMs}ms after disconnect.");

                // Re-disconnect any new server-side handlers and retry.
                server.DisconnectAllClients();
                delay = System.Math.Min(delay * 2, 500);
            }
        }

        /// <summary>
        /// Disconnect all server clients and verify that the next command throws
        /// (used for no-retry scenarios where reconnection is not expected).
        /// </summary>
        private static void DisconnectAndExpectFailure(DisconnectableTdsServer server,
            SqlConnection connection, string commandText = "SELECT 1")
        {
            server.DisconnectAllClients();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int delay = PostDisconnectDelayMs;

            while (true)
            {
                System.Threading.Thread.Sleep(delay);

                try
                {
                    using SqlCommand cmd = new(commandText, connection);
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException)
                {
                    // Expected — broken connection detected and no retry available.
                    return;
                }

                // If the command succeeded, the disconnect wasn't detected yet.
                Assert.True(sw.ElapsedMilliseconds < DisconnectDetectionTimeoutMs,
                    $"Command did not fail within {DisconnectDetectionTimeoutMs}ms after disconnect.");

                // Re-disconnect any new server-side handlers and retry.
                server.DisconnectAllClients();
                delay = System.Math.Min(delay * 2, 500);
            }
        }

        /// <summary>
        /// Assert whether a corrective <c>USE</c> reached the server during the reconnection
        /// that just occurred.
        /// <para>
        /// Checking <see cref="SqlConnection.Database"/> is not sufficient on its own:
        /// <c>CompleteLogin</c> assigns the recovered database to the connection after issuing
        /// the batch, so the client-side property matches even if the batch was never sent or
        /// processed.  Comparing the server's <c>USE</c> count proves the server session was
        /// actually realigned.
        /// </para>
        /// </summary>
        /// <param name="server">The simulated server that handled the reconnection.</param>
        /// <param name="useCountBeforeReconnect">USE count captured before the disconnect.</param>
        /// <param name="expectCorrection">Whether the diagnostic switch was enabled.</param>
        private static void AssertCorrectiveUse(DisconnectableTdsServer server,
            int useCountBeforeReconnect, bool expectCorrection)
        {
            if (expectCorrection)
            {
                Assert.True(server.UseDatabaseCount > useCountBeforeReconnect,
                    "Expected a corrective USE to reach the server after reconnection, but the "
                    + $"server executed no new USE batch (count stayed at {server.UseDatabaseCount}).");
                Assert.Equal(SwitchedDatabase, server.LastUseDatabase);
            }
            else
            {
                Assert.Equal(useCountBeforeReconnect, server.UseDatabaseCount);
            }
        }

        #endregion

        #region Baseline Tests

        /// <summary>
        /// Verifies that after executing USE [database], the <see cref="SqlConnection.Database"/>
        /// property reflects the switched database context.  This is the baseline behaviour that
        /// must work even without reconnection.
        /// </summary>
        [Fact]
        public void UseDatabaseCommand_UpdatesConnectionDatabaseProperty()
        {
            using DisconnectableTdsServer server = new();
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            Assert.Equal(InitialDatabase, connection.Database);

            using SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection);
            cmd.ExecuteNonQuery();

            Assert.Equal(SwitchedDatabase, connection.Database);
        }

        /// <summary>
        /// Verifies that <see cref="SqlConnection.ChangeDatabase"/> sends the correct protocol
        /// messages and updates the Database property.
        /// </summary>
        [Fact]
        public void ChangeDatabase_UpdatesConnectionDatabaseProperty()
        {
            using DisconnectableTdsServer server = new();
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            Assert.Equal(InitialDatabase, connection.Database);

            connection.ChangeDatabase(SwitchedDatabase);

            Assert.Equal(SwitchedDatabase, connection.Database);
        }

        #endregion

        #region Reconnection Tests — Proper Server Recovery

        /// <summary>
        /// After switching the database via USE [db] and reconnecting, the server properly
        /// restores the database context via session recovery and sends the correct ENV_CHANGE.
        /// The client's <see cref="SqlConnection.Database"/> must reflect the recovered database.
        /// </summary>
        [Fact]
        public void UseDatabase_ProperRecovery_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendRecoveredDatabase);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            DisconnectAndExecute(server, connection);

            // The server sent ENV_CHANGE with the recovered database.
            Assert.Equal(SwitchedDatabase, server.LastLoginResponseDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);
        }

        /// <summary>
        /// After switching via <see cref="SqlConnection.ChangeDatabase"/> and reconnecting,
        /// the server properly restores the database context.
        /// </summary>
        [Fact]
        public void ChangeDatabase_ProperRecovery_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendRecoveredDatabase);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            connection.ChangeDatabase(SwitchedDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);

            DisconnectAndExecute(server, connection);

            Assert.Equal(SwitchedDatabase, server.LastLoginResponseDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);
        }

        /// <summary>
        /// With pooling enabled, the reconnected connection should still preserve the database
        /// context through session recovery when the server properly handles it.
        /// </summary>
        [Fact]
        public void UseDatabase_ProperRecovery_Pooled_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendRecoveredDatabase);
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.Port}",
                InitialCatalog = InitialDatabase,
                Encrypt = SqlConnectionEncryptOption.Optional,
                ConnectRetryCount = 2,
                ConnectRetryInterval = 1,
                ConnectTimeout = 10,
                Pooling = true,
            };

            using SqlConnection connection = new(builder.ConnectionString);

            try
            {
                connection.Open();

                using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
                {
                    cmd.ExecuteNonQuery();
                }

                Assert.Equal(SwitchedDatabase, connection.Database);

                DisconnectAndExecute(server, connection);

                Assert.Equal(SwitchedDatabase, server.LastLoginResponseDatabase);
                Assert.Equal(SwitchedDatabase, connection.Database);
            }
            finally
            {
                // Clear the pool even when an assertion fails, so this test's pooled
                // connections cannot be handed to a later test.
                SqlConnection.ClearPool(connection);
            }
        }

        #endregion

        #region Reconnection Tests — Mismatched database response

        /// <summary>
        /// Uses a recovery response whose ENV_CHANGE carries the initial catalog instead
        /// of the recovered database.
        ///
        /// When <c>VerifyRecoveredDatabaseContext</c> is <c>true</c>, the client detects
        /// the mismatch and issues a corrective <c>USE</c> command.  When <c>false</c>
        /// (the default), the client trusts the server's ENV_CHANGE and reports the
        /// initial catalog.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UseDatabase_BuggyRecovery_DatabaseContextDependsOnSwitch(
            bool verifyRecoveredDb)
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.VerifyRecoveredDatabaseContext = verifyRecoveredDb;

            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendInitialCatalog);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            int useCountBeforeReconnect = server.UseDatabaseCount;

            DisconnectAndExecute(server, connection);

            // The simulated response sent InitialCatalog in ENV_CHANGE.
            Assert.Equal(InitialDatabase, server.LastLoginResponseDatabase);

            string expectedDatabase = verifyRecoveredDb ? SwitchedDatabase : InitialDatabase;
            Assert.Equal(expectedDatabase, connection.Database);
            AssertCorrectiveUse(server, useCountBeforeReconnect, verifyRecoveredDb);
        }

        /// <summary>
        /// Same as <see cref="UseDatabase_BuggyRecovery_DatabaseContextDependsOnSwitch"/>
        /// but using <see cref="SqlConnection.ChangeDatabase"/>.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ChangeDatabase_BuggyRecovery_DatabaseContextDependsOnSwitch(
            bool verifyRecoveredDb)
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.VerifyRecoveredDatabaseContext = verifyRecoveredDb;

            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendInitialCatalog);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            connection.ChangeDatabase(SwitchedDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);

            int useCountBeforeReconnect = server.UseDatabaseCount;

            DisconnectAndExecute(server, connection);

            Assert.Equal(InitialDatabase, server.LastLoginResponseDatabase);

            string expectedDatabase = verifyRecoveredDb ? SwitchedDatabase : InitialDatabase;
            Assert.Equal(expectedDatabase, connection.Database);
            AssertCorrectiveUse(server, useCountBeforeReconnect, verifyRecoveredDb);
        }

        /// <summary>
        /// Guards the ordering of the recovery-snapshot clear in <c>CompleteLogin</c>.
        /// <para>
        /// With the diagnostic enabled and a mismatched recovery response the client issues a
        /// corrective <c>USE</c>.  The ENV_CHANGE that command produces must not be recorded as
        /// the connection's original database; if it were, returning the connection to the pool
        /// and reopening it would resume on the switched database and leak that context to the
        /// next pool user instead of resetting to the initial catalog.
        /// </para>
        /// </summary>
        [Fact]
        public void UseDatabase_BuggyRecovery_Pooled_PoolResetRestoresInitialCatalog()
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.VerifyRecoveredDatabaseContext = true;

            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendInitialCatalog);
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.Port}",
                InitialCatalog = InitialDatabase,
                Encrypt = SqlConnectionEncryptOption.Optional,
                ConnectRetryCount = 2,
                ConnectRetryInterval = 1,
                ConnectTimeout = 10,
                Pooling = true,
            };

            using SqlConnection connection = new(builder.ConnectionString);

            try
            {
                connection.Open();

                using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
                {
                    cmd.ExecuteNonQuery();
                }

                int useCountBeforeReconnect = server.UseDatabaseCount;

                DisconnectAndExecute(server, connection);

                AssertCorrectiveUse(server, useCountBeforeReconnect, expectCorrection: true);
                Assert.Equal(SwitchedDatabase, connection.Database);

                connection.Close();
                connection.Open();

                Assert.Equal(InitialDatabase, connection.Database);
            }
            finally
            {
                SqlConnection.ClearPool(connection);
            }
        }

        #endregion

        #region Reconnection Tests — Omitted database response

        /// <summary>
        /// Uses a recovery response that omits the database ENV_CHANGE token.
        ///
        /// When <c>VerifyRecoveredDatabaseContext</c> is <c>true</c>, the client detects
        /// the mismatch and issues a corrective <c>USE</c> command.  When <c>false</c>
        /// (the default), the client falls back to the initial catalog.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UseDatabase_OmittedEnvChange_DatabaseContextDependsOnSwitch(
            bool verifyRecoveredDb)
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.VerifyRecoveredDatabaseContext = verifyRecoveredDb;

            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.OmitDatabaseEnvChange);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            int useCountBeforeReconnect = server.UseDatabaseCount;

            DisconnectAndExecute(server, connection);

            // The server never sent a database ENV_CHANGE.
            Assert.Null(server.LastLoginResponseDatabase);

            string expectedDatabase = verifyRecoveredDb ? SwitchedDatabase : InitialDatabase;
            Assert.Equal(expectedDatabase, connection.Database);
            AssertCorrectiveUse(server, useCountBeforeReconnect, verifyRecoveredDb);
        }

        /// <summary>
        /// Same as <see cref="UseDatabase_OmittedEnvChange_DatabaseContextDependsOnSwitch"/>
        /// but using <see cref="SqlConnection.ChangeDatabase"/>.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ChangeDatabase_OmittedEnvChange_DatabaseContextDependsOnSwitch(
            bool verifyRecoveredDb)
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.VerifyRecoveredDatabaseContext = verifyRecoveredDb;

            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.OmitDatabaseEnvChange);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            connection.ChangeDatabase(SwitchedDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);

            int useCountBeforeReconnect = server.UseDatabaseCount;

            DisconnectAndExecute(server, connection);

            Assert.Null(server.LastLoginResponseDatabase);

            string expectedDatabase = verifyRecoveredDb ? SwitchedDatabase : InitialDatabase;
            Assert.Equal(expectedDatabase, connection.Database);
            AssertCorrectiveUse(server, useCountBeforeReconnect, verifyRecoveredDb);
        }

        #endregion

        #region No-Retry Tests

        /// <summary>
        /// Verifies that with ConnectRetryCount=0, session recovery is not negotiated and a
        /// broken connection raises an error rather than silently reconnecting with a wrong
        /// database context.
        /// </summary>
        [Fact]
        public void UseDatabase_ConnectionDropped_NoRetry_ThrowsOnNextCommand()
        {
            using DisconnectableTdsServer server = new();
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.Port}",
                InitialCatalog = InitialDatabase,
                Encrypt = SqlConnectionEncryptOption.Optional,
                ConnectRetryCount = 0,
                ConnectTimeout = 5,
                Pooling = false,
            };

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            // With ConnectRetryCount=0, no transparent reconnection should occur.
            DisconnectAndExpectFailure(server, connection);
        }

        #endregion
    }
}
