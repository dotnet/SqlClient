// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.Test.Stress
{
    /// <summary>
    /// Contains database-context reconnection stress scenarios for SqlClient.
    /// </summary>
    public partial class SqlClientTestGroup
    {
        private const string InitialDatabase = "master";

        /// <summary>
        /// Repeatedly verifies that a database selected with USE survives transparent reconnection.
        /// </summary>
        [StressTest("TestDatabaseContextReconnectionWithUse", Weight = 1)]
        public void TestDatabaseContextReconnectionWithUse()
        {
            using SqlConnection connection = CreateDatabaseContextConnection();
            connection.Open();

            string targetDatabase = GetDatabaseContextTarget();
            using (SqlCommand command = new($"USE [{targetDatabase}]", connection))
            {
                command.ExecuteNonQuery();
            }

            AssertDatabaseContext(connection, targetDatabase);
            Guid connectionId = connection.ClientConnectionId;

            KillConnection(connection.ServerProcessId);

            AssertDatabaseContext(connection, targetDatabase);
            DataStressErrors.Assert(
                connectionId != connection.ClientConnectionId,
                "Expected transparent reconnection to replace the physical connection.");
        }

        /// <summary>
        /// Repeatedly verifies that a database selected with ChangeDatabase survives transparent reconnection.
        /// </summary>
        [StressTest("TestDatabaseContextReconnectionWithChangeDatabase", Weight = 1)]
        public void TestDatabaseContextReconnectionWithChangeDatabase()
        {
            using SqlConnection connection = CreateDatabaseContextConnection();
            connection.Open();

            string targetDatabase = GetDatabaseContextTarget();
            connection.ChangeDatabase(targetDatabase);

            AssertDatabaseContext(connection, targetDatabase);
            Guid connectionId = connection.ClientConnectionId;

            KillConnection(connection.ServerProcessId);

            AssertDatabaseContext(connection, targetDatabase);
            DataStressErrors.Assert(
                connectionId != connection.ClientConnectionId,
                "Expected transparent reconnection to replace the physical connection.");
        }

        /// <summary>
        /// Repeatedly verifies that DDL after transparent reconnection runs in the recovered database.
        /// </summary>
        [StressTest("TestDatabaseContextReconnectionCreateTable", Weight = 1)]
        public void TestDatabaseContextReconnectionCreateTable()
        {
            string targetDatabase = GetDatabaseContextTarget();
            string tableName = "DbContext_" + Guid.NewGuid().ToString("N");

            try
            {
                using SqlConnection connection = CreateDatabaseContextConnection();
                connection.Open();

                int preQueries = RandomInstance.Next(0, 6);
                for (int i = 0; i < preQueries; i++)
                {
                    using SqlCommand command = new(
                        $"SELECT TOP {RandomInstance.Next(1, 100)} * FROM sys.objects",
                        connection);
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                    }
                }

                using (SqlCommand command = new($"USE [{targetDatabase}]", connection))
                {
                    command.ExecuteNonQuery();
                }

                int postQueries = RandomInstance.Next(0, 4);
                for (int i = 0; i < postQueries; i++)
                {
                    using SqlCommand command = new("SELECT GETDATE()", connection);
                    command.ExecuteScalar();
                }

                AssertDatabaseContext(connection, targetDatabase);
                Guid connectionId = connection.ClientConnectionId;

                KillConnection(connection.ServerProcessId);

                using (SqlCommand command = new(
                    $"CREATE TABLE [{tableName}] (Id INT)",
                    connection))
                {
                    command.ExecuteNonQuery();
                }

                AssertDatabaseContext(connection, targetDatabase);
                DataStressErrors.Assert(
                    connectionId != connection.ClientConnectionId,
                    "Expected transparent reconnection to replace the physical connection.");

                using SqlConnection verifier = CreateDatabaseContextConnection();
                verifier.Open();

                DataStressErrors.Assert(
                    TableExists(verifier, targetDatabase, tableName),
                    $"Table '{tableName}' was not created in database '{targetDatabase}'.");
                DataStressErrors.Assert(
                    !TableExists(verifier, InitialDatabase, tableName),
                    $"Table '{tableName}' was created in database '{InitialDatabase}'.");
            }
            finally
            {
                using SqlConnection cleanup = CreateDatabaseContextConnection();
                cleanup.Open();
                DropTable(cleanup, targetDatabase, tableName);
                DropTable(cleanup, InitialDatabase, tableName);
            }
        }

        /// <summary>
        /// Gets the shared database created by the stress harness for the current run.
        /// </summary>
        /// <returns>The database name used as the post-login context.</returns>
        private static string GetDatabaseContextTarget()
            => ((SqlServerDataSource)Source).Database;

        /// <summary>
        /// Creates a non-pooled connection configured for transparent reconnection from master.
        /// </summary>
        /// <returns>A closed connection configured for the reconnection scenarios.</returns>
        private static SqlConnection CreateDatabaseContextConnection()
        {
            SqlConnectionStringBuilder builder = new(
                Factory.CreateBaseConnectionString(
                    null,
                    DataStressFactory.ConnectionStringOptions.DisableMultiSubnetFailover))
            {
                InitialCatalog = InitialDatabase,
                ConnectRetryCount = 2,
                ConnectRetryInterval = 1,
                ConnectTimeout = 10,
                Pooling = false,
                MultipleActiveResultSets = false,
            };

            return new SqlConnection(builder.ConnectionString);
        }

        /// <summary>
        /// Kills a server session from a separate connection and allows dead-link detection to begin.
        /// </summary>
        /// <param name="serverProcessId">The SQL Server session id to kill.</param>
        private static void KillConnection(int serverProcessId)
        {
            using SqlConnection killer = CreateDatabaseContextConnection();
            killer.Open();
            using SqlCommand command = new($"KILL {serverProcessId}", killer);
            command.ExecuteNonQuery();
            Thread.Sleep(100);
        }

        /// <summary>
        /// Queries the server and verifies the connection's active database context.
        /// </summary>
        /// <param name="connection">The connection whose server context is queried.</param>
        /// <param name="expectedDatabase">The expected active database.</param>
        private static void AssertDatabaseContext(SqlConnection connection, string expectedDatabase)
        {
            using SqlCommand command = new("SELECT DB_NAME()", connection);
            string actualDatabase = (string)command.ExecuteScalar();

            DataStressErrors.Assert(
                string.Equals(expectedDatabase, actualDatabase, StringComparison.OrdinalIgnoreCase),
                $"Expected database '{expectedDatabase}', but the server reported '{actualDatabase}'.");
        }

        /// <summary>
        /// Checks whether a table exists in a specific database.
        /// </summary>
        /// <param name="connection">An open connection to the target server.</param>
        /// <param name="database">The database to inspect.</param>
        /// <param name="tableName">The unescaped table name.</param>
        /// <returns><see langword="true"/> when the table exists; otherwise, <see langword="false"/>.</returns>
        private static bool TableExists(
            SqlConnection connection,
            string database,
            string tableName)
        {
            using SqlCommand command = new(
                $"SELECT COUNT(*) FROM [{database}].sys.tables WHERE name = @name",
                connection);
            command.Parameters.AddWithValue("@name", tableName);
            return (int)command.ExecuteScalar() == 1;
        }

        /// <summary>
        /// Drops a table from a specific database when it exists.
        /// </summary>
        /// <param name="connection">An open connection to the target server.</param>
        /// <param name="database">The database containing the table.</param>
        /// <param name="tableName">The unescaped table name.</param>
        private static void DropTable(
            SqlConnection connection,
            string database,
            string tableName)
        {
            using SqlCommand command = new(
                $"IF OBJECT_ID(@name) IS NOT NULL DROP TABLE [{database}].dbo.[{tableName}]",
                connection);
            command.Parameters.AddWithValue("@name", $"[{database}].dbo.[{tableName}]");
            command.ExecuteNonQuery();
        }
    }
}
