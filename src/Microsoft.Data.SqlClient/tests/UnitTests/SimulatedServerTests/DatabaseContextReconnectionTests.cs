// Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
// file to you under the MIT license.  See the LICENSE file in the project root for more
// information.

using System.Linq;
using System.Text.RegularExpressions;
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

        #region Test Infrastructure

        /// <summary>
        /// Controls how the test server handles the database ENV_CHANGE during a session
        /// recovery login.
        /// </summary>
        private enum RecoveryDatabaseBehavior
        {
            /// <summary>
            /// The server correctly sends ENV_CHANGE with the recovered database name taken
            /// from the session recovery feature request.  This is proper server behavior.
            /// </summary>
            SendRecoveredDatabase,

            /// <summary>
            /// The server ignores the recovered database and sends ENV_CHANGE with the
            /// login packet's initial catalog instead.  This simulates a server bug where
            /// session recovery does not restore the database context.
            /// </summary>
            SendInitialCatalog,

            /// <summary>
            /// The server omits the database ENV_CHANGE token entirely from the login
            /// response.  This simulates a server bug where the database change notification
            /// is completely missing after session recovery.
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

            private static TDSMessageCollection HandleUseDatabase(ITDSServerSession session,
                string newDatabase)
            {
                string oldDatabase = session.Database;
                session.Database = newDatabase;

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
            public int Port => EndPoint.Port;

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
                : base(args, new DatabaseContextQueryEngine(args))
            {
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
                    // Simulate a server bug: after session recovery inflated the session
                    // (which set session.Database to the recovered DB), forcibly revert
                    // session.Database to the initial catalog before the base class builds
                    // the ENV_CHANGE token.
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
        /// Disconnect all server clients and wait for the
        /// <c>CheckConnectionWindow</c> to expire so that the next
        /// <c>ValidateSNIConnection</c> call properly detects the dead link.
        /// </summary>
        private static void DisconnectAndWait(DisconnectableTdsServer server)
        {
            server.DisconnectAllClients();
            System.Threading.Thread.Sleep(PostDisconnectDelayMs);
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

            DisconnectAndWait(server);

            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                cmd.ExecuteNonQuery();
            }

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

            DisconnectAndWait(server);

            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                cmd.ExecuteNonQuery();
            }

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
            connection.Open();

            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            DisconnectAndWait(server);

            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, server.LastLoginResponseDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);

            SqlConnection.ClearPool(connection);
        }

        #endregion

        #region Reconnection Tests — Buggy Server (no database in recovery)

        /// <summary>
        /// Simulates a server bug where session recovery acknowledges the feature but does
        /// NOT restore the database context — the ENV_CHANGE carries the initial catalog
        /// instead of the recovered database.  Despite the incorrect ENV_CHANGE, the client
        /// should trust the recovery state it sent and report the recovered database.
        /// </summary>
        [Fact]
        public void UseDatabase_BuggyRecovery_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendInitialCatalog);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            DisconnectAndWait(server);

            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // The buggy server sent InitialCatalog in ENV_CHANGE.
            Assert.Equal(InitialDatabase, server.LastLoginResponseDatabase);

            // After successful recovery, the client should reflect the recovered database
            // regardless of the server's ENV_CHANGE.
            Assert.Equal(SwitchedDatabase, connection.Database);
        }

        /// <summary>
        /// Simulates a server that doesn't restore the database during session recovery
        /// using <see cref="SqlConnection.ChangeDatabase"/>.  Despite the incorrect
        /// ENV_CHANGE, the client should preserve the recovered database context.
        /// </summary>
        [Fact]
        public void ChangeDatabase_BuggyRecovery_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.SendInitialCatalog);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            connection.ChangeDatabase(SwitchedDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);

            DisconnectAndWait(server);

            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(InitialDatabase, server.LastLoginResponseDatabase);

            // After successful recovery, the client should reflect the recovered database.
            Assert.Equal(SwitchedDatabase, connection.Database);
        }

        /// <summary>
        /// Simulates a server bug where the database ENV_CHANGE token is completely
        /// omitted from the login response during session recovery.  Despite the missing
        /// ENV_CHANGE, the client should trust the recovery state it sent and report the
        /// recovered database.
        /// </summary>
        [Fact]
        public void UseDatabase_OmittedEnvChange_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.OmitDatabaseEnvChange);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            DisconnectAndWait(server);

            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // The server never sent a database ENV_CHANGE.
            Assert.Null(server.LastLoginResponseDatabase);

            // After successful recovery, the client should reflect the recovered database
            // regardless of missing ENV_CHANGE.
            Assert.Equal(SwitchedDatabase, connection.Database);
        }

        /// <summary>
        /// Same as above but using <see cref="SqlConnection.ChangeDatabase"/>.
        /// </summary>
        [Fact]
        public void ChangeDatabase_OmittedEnvChange_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new(RecoveryDatabaseBehavior.OmitDatabaseEnvChange);
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            connection.ChangeDatabase(SwitchedDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);

            DisconnectAndWait(server);

            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Null(server.LastLoginResponseDatabase);

            // After successful recovery, the client should reflect the recovered database.
            Assert.Equal(SwitchedDatabase, connection.Database);
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

            DisconnectAndWait(server);

            // With ConnectRetryCount=0, no transparent reconnection should occur.
            using SqlCommand cmd2 = new("SELECT 1", connection);
            Assert.ThrowsAny<SqlException>(() => cmd2.ExecuteNonQuery());
        }

        #endregion
    }
}
