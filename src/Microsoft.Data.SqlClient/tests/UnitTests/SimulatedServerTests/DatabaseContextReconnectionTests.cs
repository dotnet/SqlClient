// Licensed to the .NET Foundation under one or more agreements.  The .NET Foundation licenses this
// file to you under the MIT license.  See the LICENSE file in the project root for more
// information.

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
    /// Baseline tests (no reconnection) pass.  The three reconnection tests are expected to
    /// FAIL until issue #4108 is fixed — they demonstrate that <c>connection.Database</c>
    /// silently reverts to <c>InitialCatalog</c> after the physical connection is replaced.
    /// </summary>
    [Collection("SimulatedServerTests")]
    public class DatabaseContextReconnectionTests
    {
        private const string InitialDatabase = "initialdb";
        private const string SwitchedDatabase = "switcheddb";

        #region Test Infrastructure

        /// <summary>
        /// A query engine that recognises USE [database] commands and updates the session's
        /// current database accordingly, returning the correct EnvChange tokens.
        /// </summary>
        private sealed class DatabaseContextQueryEngine : QueryEngine
        {
            private static readonly Regex s_useDbRegex = new(
                @"^\s*use\s+\[?(?<db>[^\]\s;]+)\]?\s*;?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            /// <summary>
            /// The database name received during the most recent session recovery login.
            /// Null if no recovery login has occurred.
            /// </summary>
            public string? LastRecoveryDatabase { get; set; }

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

                // Fall back to the built-in query engine for everything else (SELECT 1,
                // db_name(), etc.)
                return base.CreateQueryResponse(session, batchRequest);
            }

            private static TDSMessageCollection HandleUseDatabase(ITDSServerSession session,
                string newDatabase)
            {
                string oldDatabase = session.Database;
                session.Database = newDatabase;

                // Build the response: ENV_CHANGE(Database) + INFO(5701) + DONE
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
        /// A TDS server subclass that exposes the protected
        /// <see cref="GenericTdsServer{T}.DisconnectAllClients"/>
        /// method and wires up the custom query engine.
        /// </summary>
        private sealed class DisconnectableTdsServer : GenericTdsServer<TdsServerArguments>
        {
            public DatabaseContextQueryEngine QueryEngine { get; }

            public int Port => EndPoint.Port;

            public DisconnectableTdsServer()
                : this(new TdsServerArguments())
            {
            }

            private DisconnectableTdsServer(TdsServerArguments args)
                : base(args, new DatabaseContextQueryEngine(args))
            {
                QueryEngine = (DatabaseContextQueryEngine)Engine;
                Start();
            }

            /// <summary>
            /// Publicly expose the protected <see cref="GenericTdsServer{T}.DisconnectAllClients"/>
            /// for use by tests.
            /// </summary>
            public new void DisconnectAllClients()
                => base.DisconnectAllClients();
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
        /// messages and updates the Database property, as a contrast to the raw USE command path.
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

        #region Reconnection Tests

        /// <summary>
        /// Reproduces issue dotnet/SqlClient#4108: after switching the database via USE [db], the
        /// connection is broken and then transparently reconnected. The expectation is that the
        /// reconnected session restores the switched database context.
        ///
        /// The critical invariant is: the database context must NEVER silently revert to
        /// InitialCatalog. Either reconnection preserves the switched database, or it throws.
        /// </summary>
        [Fact]
        public void UseDatabase_ConnectionDropped_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new();
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            // Switch database via USE command
            using (SqlCommand cmd = new($"USE [{SwitchedDatabase}]", connection))
            {
                cmd.ExecuteNonQuery();
            }

            Assert.Equal(SwitchedDatabase, connection.Database);

            // Forcibly break all TCP connections on the server side. The listener stays up so
            // the client can reconnect.
            server.DisconnectAllClients();

            // The next command should trigger ValidateAndReconnect, which detects the broken SNI
            // link, snapshots the SessionData (including the current database), and performs a
            // reconnection with session recovery.
            bool reconnected = false;
            using (SqlCommand cmd = new("SELECT 1", connection))
            {
                try
                {
                    cmd.ExecuteNonQuery();
                    reconnected = true;
                }
                catch (SqlException)
                {
                    // Reconnection failed — acceptable.
                }
            }

            // Issue #4108 core assertion: regardless of whether reconnection succeeded or
            // failed, the Database property must not have silently reverted to the initial catalog.
            Assert.NotEqual(InitialDatabase, connection.Database);

            if (reconnected)
            {
                Assert.Equal(SwitchedDatabase, connection.Database);
            }
        }

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

            server.DisconnectAllClients();

            // With ConnectRetryCount=0, no transparent reconnection should occur.  The next
            // command must throw.
            using SqlCommand cmd2 = new("SELECT 1", connection);
            Assert.ThrowsAny<SqlException>(() => cmd2.ExecuteNonQuery());
        }

        /// <summary>
        /// Verifies that ChangeDatabase context is preserved across a transparent reconnection,
        /// similar to the USE [db] test but exercising the ChangeDatabase API path.
        /// </summary>
        [Fact]
        public void ChangeDatabase_ConnectionDropped_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new();
            SqlConnectionStringBuilder builder = CreateConnectionStringBuilder(server.Port);

            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();

            connection.ChangeDatabase(SwitchedDatabase);
            Assert.Equal(SwitchedDatabase, connection.Database);

            server.DisconnectAllClients();

            bool reconnected = false;
            try
            {
                using SqlCommand cmd = new("SELECT 1", connection);
                cmd.ExecuteNonQuery();
                reconnected = true;
            }
            catch (SqlException)
            {
                // Reconnection may fail — acceptable.
            }

            Assert.NotEqual(InitialDatabase, connection.Database);

            if (reconnected)
            {
                Assert.Equal(SwitchedDatabase, connection.Database);
            }
        }

        /// <summary>
        /// When <c>Pooling=true</c>, a reconnected connection obtained from the pool after a
        /// severed link should still preserve the database context through session recovery.
        /// </summary>
        [Fact]
        public void UseDatabase_ConnectionDropped_Pooled_DatabaseContextPreservedAfterReconnect()
        {
            using DisconnectableTdsServer server = new();
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

            server.DisconnectAllClients();

            bool reconnected = false;
            try
            {
                using SqlCommand cmd = new("SELECT 1", connection);
                cmd.ExecuteNonQuery();
                reconnected = true;
            }
            catch (SqlException)
            {
                // Reconnection may fail — acceptable.
            }

            Assert.NotEqual(InitialDatabase, connection.Database);

            if (reconnected)
            {
                Assert.Equal(SwitchedDatabase, connection.Database);
            }

            // Clean up the pool for this connection string so it doesn't leak into other tests.
            SqlConnection.ClearPool(connection);
        }

        #endregion
    }
}
