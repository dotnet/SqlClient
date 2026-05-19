// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests that database context is preserved after transparent reconnection
    /// (session recovery) triggered by KILL on a real SQL Server.
    /// <para>
    /// The <c>VerifyRecoveredDatabaseContext</c> AppContext switch is explicitly
    /// set to <c>false</c> so the client-side defensive fix is disabled.  Any
    /// failure therefore proves that either the server or the driver (without the
    /// fix) does not correctly restore the database context during session recovery.
    /// </para>
    /// <para>
    /// Reproduces the scenario from dotnet/SqlClient#4108.
    /// </para>
    /// </summary>
    public sealed class DatabaseContextReconnectionTest : IDisposable
    {
        private const string SwitchName =
            "Switch.Microsoft.Data.SqlClient.VerifyRecoveredDatabaseContext";

        /// <summary>
        /// Temporary database created for the test run.
        /// We switch into this database and verify it survives reconnection.
        /// </summary>
        private readonly string _tempDbName;

        /// <summary>
        /// Base connection string (from environment) without extra options.
        /// </summary>
        private readonly string _baseConnectionString;

        /// <summary>
        /// Tracks tables created during a test so they can be cleaned up from
        /// the initial catalog if a test failure causes them to land in the
        /// wrong database.
        /// </summary>
        private readonly List<string> _createdTableNames = new();

        public DatabaseContextReconnectionTest()
        {
            _baseConnectionString = DataTestUtility.TCPConnectionString;
            _tempDbName = "sqlclient_dbctx_" + Guid.NewGuid().ToString("N").Substring(0, 12);

            using SqlConnection conn = new(_baseConnectionString);
            conn.Open();
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_tempDbName}]";
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            using SqlConnection conn = new(_baseConnectionString);
            conn.Open();

            // Clean up any tables that may have been created in the initial
            // catalog if the database context bug caused DDL to execute in the
            // wrong database.
            if (_createdTableNames.Count > 0)
            {
                string initialCatalog = new SqlConnectionStringBuilder(
                    _baseConnectionString).InitialCatalog;

                if (!string.IsNullOrEmpty(initialCatalog)
                    && !string.Equals(initialCatalog, _tempDbName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string tableName in _createdTableNames)
                    {
                        try
                        {
                            using SqlCommand cmd = conn.CreateCommand();
                            cmd.CommandText =
                                $"IF OBJECT_ID(N'[{initialCatalog}].dbo.[{tableName}]') " +
                                $"IS NOT NULL DROP TABLE [{initialCatalog}].dbo.[{tableName}]";
                            cmd.ExecuteNonQuery();
                        }
                        catch
                        {
                            // Best-effort cleanup
                        }
                    }
                }
            }

            DataTestUtility.DropDatabase(conn, _tempDbName);
        }

        #region Helpers

        private SqlConnectionStringBuilder BuildConnectionString(
            bool pooling = false, bool mars = false)
        {
            return new SqlConnectionStringBuilder(_baseConnectionString)
            {
                ConnectRetryCount = 2,
                ConnectRetryInterval = 1,
                ConnectTimeout = 10,
                Pooling = pooling,
                MultipleActiveResultSets = mars,
            };
        }

        /// <summary>
        /// Kill the target connection's SPID from a separate connection and
        /// wait long enough for the <c>CheckConnectionWindow</c> to expire.
        /// </summary>
        private void KillSpid(int spid)
        {
            using SqlConnection killer = new(_baseConnectionString);
            killer.Open();
            using SqlCommand cmd = new($"KILL {spid}", killer);
            cmd.ExecuteNonQuery();
            // Let the CheckConnectionWindow (5 ms) expire so that the next
            // ValidateSNIConnection call detects the dead link.
            Thread.Sleep(100);
        }

        /// <summary>
        /// Returns the server-side SPID for the connection.  Uses <c>@@SPID</c>
        /// query which works correctly with MARS connections (where
        /// <see cref="SqlConnection.ServerProcessId"/> may return 0).
        /// </summary>
        private static int GetServerSpid(SqlConnection conn)
        {
            using SqlCommand cmd = new("SELECT @@SPID", conn);
            return (short)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Queries the server for its actual current database via
        /// <c>SELECT DB_NAME()</c>.
        /// </summary>
        private static string GetServerDatabase(SqlConnection conn)
        {
            using SqlCommand cmd = new("SELECT DB_NAME()", conn);
            return (string)cmd.ExecuteScalar();
        }

        /// <summary>
        /// Assert that both client and server agree on the expected database.
        /// </summary>
        private static void AssertDatabaseContext(
            SqlConnection conn, string expectedDb, string context)
        {
            string clientDb = conn.Database;
            string serverDb = GetServerDatabase(conn);

            Assert.True(
                string.Equals(expectedDb, clientDb, StringComparison.OrdinalIgnoreCase),
                $"[{context}] Client database mismatch. " +
                $"Expected: '{expectedDb}', connection.Database: '{clientDb}'");

            Assert.True(
                string.Equals(expectedDb, serverDb, StringComparison.OrdinalIgnoreCase),
                $"[{context}] Server database mismatch. " +
                $"Expected: '{expectedDb}', DB_NAME(): '{serverDb}'");
        }

        /// <summary>
        /// Returns the <c>connection_id</c> GUID from <c>sys.dm_exec_connections</c>
        /// for the current session.  This value is unique per physical connection
        /// and is guaranteed to change after reconnection, even when SQL Server
        /// reuses the same SPID.
        /// </summary>
        private static Guid GetConnectionId(SqlConnection conn)
        {
            using SqlCommand cmd = new(
                "SELECT connection_id FROM sys.dm_exec_connections WHERE session_id = @@SPID",
                conn);
            return (Guid)cmd.ExecuteScalar();
        }

        #endregion

        #region Single-shot tests

        /// <summary>
        /// USE [tempDb] → KILL → command → verify both client and server are
        /// on tempDb.  No pooling, no MARS.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void UseDatabase_KillReconnect_PreservesContext()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: false);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            // Switch to temp database
            using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
            {
                useCmd.ExecuteNonQuery();
            }
            AssertDatabaseContext(conn, _tempDbName, "pre-kill");

            Guid connIdBefore = GetConnectionId(conn);
            KillSpid(conn.ServerProcessId);

            // This command triggers transparent reconnection
            AssertDatabaseContext(conn, _tempDbName, "post-reconnect");
            Guid connIdAfter = GetConnectionId(conn);
            Assert.NotEqual(connIdBefore, connIdAfter);
        }

        /// <summary>
        /// ChangeDatabase(tempDb) → KILL → command → verify context preserved.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void ChangeDatabase_KillReconnect_PreservesContext()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: false);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            conn.ChangeDatabase(_tempDbName);
            AssertDatabaseContext(conn, _tempDbName, "pre-kill");

            Guid connIdBefore = GetConnectionId(conn);
            KillSpid(conn.ServerProcessId);

            AssertDatabaseContext(conn, _tempDbName, "post-reconnect");
            Guid connIdAfter = GetConnectionId(conn);
            Assert.NotEqual(connIdBefore, connIdAfter);
        }

        /// <summary>
        /// Same as <see cref="UseDatabase_KillReconnect_PreservesContext"/> but
        /// with connection pooling enabled.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void UseDatabase_KillReconnect_Pooled_PreservesContext()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: true);
            // Unique pool key so we don't interfere with other tests.
            builder.ApplicationName = "DbCtxPoolTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
            {
                useCmd.ExecuteNonQuery();
            }
            AssertDatabaseContext(conn, _tempDbName, "pre-kill");

            Guid connIdBefore = GetConnectionId(conn);
            KillSpid(conn.ServerProcessId);

            AssertDatabaseContext(conn, _tempDbName, "post-reconnect");
            Guid connIdAfter = GetConnectionId(conn);
            Assert.NotEqual(connIdBefore, connIdAfter);
        }

        /// <summary>
        /// Same as <see cref="UseDatabase_KillReconnect_PreservesContext"/> but
        /// with MARS enabled.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void UseDatabase_KillReconnect_MARS_PreservesContext()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: false, mars: true);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
            {
                useCmd.ExecuteNonQuery();
            }
            AssertDatabaseContext(conn, _tempDbName, "pre-kill");

            Guid connIdBefore = GetConnectionId(conn);
            KillSpid(GetServerSpid(conn));

            AssertDatabaseContext(conn, _tempDbName, "post-reconnect");
            Guid connIdAfter = GetConnectionId(conn);
            Assert.NotEqual(connIdBefore, connIdAfter);
        }

        #endregion

        #region Stress tests

        /// <summary>
        /// Runs USE → KILL → verify in a tight loop to surface intermittent
        /// failures in session recovery.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void UseDatabase_KillReconnect_StressLoop_PreservesContext()
        {
            AppContext.SetSwitch(SwitchName, false);

            const int iterations = 100;
            var builder = BuildConnectionString(pooling: false);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            for (int i = 0; i < iterations; i++)
            {
                string context = $"USE stress iteration {i}";

                // Switch to temp database (may already be there after
                // reconnection, but USE is idempotent).
                using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
                {
                    useCmd.ExecuteNonQuery();
                }
                AssertDatabaseContext(conn, _tempDbName, context + " pre-kill");

                Guid connIdBefore = GetConnectionId(conn);
                KillSpid(conn.ServerProcessId);

                // The next command drives reconnection.
                AssertDatabaseContext(conn, _tempDbName, context + " post-reconnect");
                Guid connIdAfter = GetConnectionId(conn);
                Assert.NotEqual(connIdBefore, connIdAfter);
            }
        }

        /// <summary>
        /// Same stress loop but via <see cref="SqlConnection.ChangeDatabase"/>.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void ChangeDatabase_KillReconnect_StressLoop_PreservesContext()
        {
            AppContext.SetSwitch(SwitchName, false);

            const int iterations = 100;
            var builder = BuildConnectionString(pooling: false);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            for (int i = 0; i < iterations; i++)
            {
                string context = $"ChangeDatabase stress iteration {i}";

                conn.ChangeDatabase(_tempDbName);
                AssertDatabaseContext(conn, _tempDbName, context + " pre-kill");

                Guid connIdBefore = GetConnectionId(conn);
                KillSpid(conn.ServerProcessId);

                AssertDatabaseContext(conn, _tempDbName, context + " post-reconnect");
                Guid connIdAfter = GetConnectionId(conn);
                Assert.NotEqual(connIdBefore, connIdAfter);
            }
        }

        #endregion

        #region Object-creation tests

        /// <summary>
        /// After USE → KILL → reconnect, creates a table via DDL and verifies
        /// via a separate connection that it landed in the expected database,
        /// not the initial catalog.  This is the strongest proof that the
        /// server's session context is actually on the right database.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void UseDatabase_KillReconnect_CreateTable_LandsInCorrectDb()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: false);
            string tableName = "tbl_ctx_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _createdTableNames.Add(tableName);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
            {
                useCmd.ExecuteNonQuery();
            }

            KillSpid(conn.ServerProcessId);

            // Create a table — this DDL should execute in _tempDbName
            using (SqlCommand createCmd = new(
                $"CREATE TABLE [{tableName}] (Id INT PRIMARY KEY, Val NVARCHAR(50))", conn))
            {
                createCmd.ExecuteNonQuery();
            }

            // Insert a row to confirm the table is usable
            using (SqlCommand insertCmd = new(
                $"INSERT INTO [{tableName}] (Id, Val) VALUES (1, 'reconnect_test')", conn))
            {
                insertCmd.ExecuteNonQuery();
            }

            // Verify from a separate connection that the table exists in the
            // temp database and NOT in the initial catalog.
            using SqlConnection verifier = new(_baseConnectionString);
            verifier.Open();

            // Should exist in temp database
            using (SqlCommand checkCmd = new(
                $"SELECT COUNT(*) FROM [{_tempDbName}].INFORMATION_SCHEMA.TABLES " +
                $"WHERE TABLE_NAME = @name", verifier))
            {
                checkCmd.Parameters.AddWithValue("@name", tableName);
                int count = (int)checkCmd.ExecuteScalar();
                Assert.True(count == 1,
                    $"Table '{tableName}' was NOT found in '{_tempDbName}'. " +
                    "DDL may have executed in the wrong database.");
            }

            // Should NOT exist in the initial catalog
            string initialCatalog = new SqlConnectionStringBuilder(
                _baseConnectionString).InitialCatalog;
            if (!string.IsNullOrEmpty(initialCatalog)
                && !string.Equals(initialCatalog, _tempDbName, StringComparison.OrdinalIgnoreCase))
            {
                using SqlCommand checkInitCmd = new(
                    $"SELECT COUNT(*) FROM [{initialCatalog}].INFORMATION_SCHEMA.TABLES " +
                    $"WHERE TABLE_NAME = @name", verifier);
                checkInitCmd.Parameters.AddWithValue("@name", tableName);
                int count = (int)checkInitCmd.ExecuteScalar();
                Assert.True(count == 0,
                    $"Table '{tableName}' was found in initial catalog '{initialCatalog}'. " +
                    "DDL executed in the WRONG database after reconnection!");
            }
        }

        /// <summary>
        /// Stress loop: each iteration switches database, kills the connection,
        /// creates a uniquely-named table after reconnection, then verifies from
        /// a separate connection that every table landed in the correct database.
        /// Also runs a variable number of queries before and after the USE to
        /// vary the session state and packet buffer contents.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void UseDatabase_KillReconnect_StressCreateTables_LandInCorrectDb()
        {
            AppContext.SetSwitch(SwitchName, false);

            const int iterations = 50;
            var builder = BuildConnectionString(pooling: false);
            var rng = new Random(42); // deterministic seed for reproducibility
            string[] tableNames = new string[iterations];

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            for (int i = 0; i < iterations; i++)
            {
                string context = $"stress-create iteration {i}";

                // Variable workload BEFORE USE — pollute session state
                int preQueries = rng.Next(0, 6);
                for (int q = 0; q < preQueries; q++)
                {
                    using SqlCommand workCmd = new(
                        $"SET NOCOUNT ON; SELECT TOP {rng.Next(1, 100)} * FROM sys.objects", conn);
                    using var reader = workCmd.ExecuteReader();
                    while (reader.Read()) { }
                }

                // Add session state: SET options increase recovery payload
                if (i % 3 == 0)
                {
                    using SqlCommand setCmd = new(
                        "SET TEXTSIZE 65536; SET LOCK_TIMEOUT 5000", conn);
                    setCmd.ExecuteNonQuery();
                }

                // Switch to temp database
                using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
                {
                    useCmd.ExecuteNonQuery();
                }

                // Variable workload AFTER USE, BEFORE kill
                int postQueries = rng.Next(0, 4);
                for (int q = 0; q < postQueries; q++)
                {
                    using SqlCommand workCmd = new("SELECT GETDATE()", conn);
                    workCmd.ExecuteScalar();
                }

                AssertDatabaseContext(conn, _tempDbName, context + " pre-kill");

                Guid connIdBefore = GetConnectionId(conn);
                KillSpid(conn.ServerProcessId);

                // Reconnection happens here — create a table
                string tableName = $"tbl_s{i}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
                tableNames[i] = tableName;
                _createdTableNames.Add(tableName);

                using (SqlCommand createCmd = new(
                    $"CREATE TABLE [{tableName}] (Id INT)", conn))
                {
                    createCmd.ExecuteNonQuery();
                }

                AssertDatabaseContext(conn, _tempDbName, context + " post-create");
                Guid connIdAfter = GetConnectionId(conn);
                Assert.NotEqual(connIdBefore, connIdAfter);
            }

            // Bulk verification: every table must exist in _tempDbName
            using SqlConnection verifier = new(_baseConnectionString);
            verifier.Open();

            string initialCatalog = new SqlConnectionStringBuilder(
                _baseConnectionString).InitialCatalog;

            for (int i = 0; i < iterations; i++)
            {
                using SqlCommand checkCmd = new(
                    $"SELECT COUNT(*) FROM [{_tempDbName}].INFORMATION_SCHEMA.TABLES " +
                    $"WHERE TABLE_NAME = @name", verifier);
                checkCmd.Parameters.AddWithValue("@name", tableNames[i]);
                int found = (int)checkCmd.ExecuteScalar();
                Assert.True(found == 1,
                    $"Iteration {i}: Table '{tableNames[i]}' NOT found in '{_tempDbName}'. " +
                    "Object creation landed in the wrong database.");

                // Negative check against initial catalog
                if (!string.IsNullOrEmpty(initialCatalog)
                    && !string.Equals(initialCatalog, _tempDbName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    using SqlCommand negCmd = new(
                        $"SELECT COUNT(*) FROM [{initialCatalog}].INFORMATION_SCHEMA.TABLES " +
                        $"WHERE TABLE_NAME = @name", verifier);
                    negCmd.Parameters.AddWithValue("@name", tableNames[i]);
                    int wrongDb = (int)negCmd.ExecuteScalar();
                    Assert.True(wrongDb == 0,
                        $"Iteration {i}: Table '{tableNames[i]}' found in initial catalog " +
                        $"'{initialCatalog}' — DDL executed in WRONG database!");
                }
            }
        }

        /// <summary>
        /// Two database switches before kill: USE initialCatalog → USE tempDb → KILL.
        /// After reconnection, creates a table and verifies the *last* switch won.
        /// Catches bugs where only the first database change is recorded in recovery data.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void MultipleDatabaseSwitches_KillReconnect_LastSwitchWins()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: false);
            string initialCatalog = new SqlConnectionStringBuilder(
                _baseConnectionString).InitialCatalog;
            string tableName = "tbl_multi_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _createdTableNames.Add(tableName);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            // Switch away and back multiple times
            using (SqlCommand cmd = new($"USE [{_tempDbName}]", conn))
            {
                cmd.ExecuteNonQuery();
            }
            using (SqlCommand cmd = new($"USE [{initialCatalog}]", conn))
            {
                cmd.ExecuteNonQuery();
            }
            using (SqlCommand cmd = new($"USE [{_tempDbName}]", conn))
            {
                cmd.ExecuteNonQuery();
            }

            AssertDatabaseContext(conn, _tempDbName, "after triple switch");

            KillSpid(conn.ServerProcessId);

            // After reconnection, create a table — must land in _tempDbName
            using (SqlCommand createCmd = new(
                $"CREATE TABLE [{tableName}] (Id INT)", conn))
            {
                createCmd.ExecuteNonQuery();
            }

            AssertDatabaseContext(conn, _tempDbName, "post-reconnect");

            // Verify object location
            using SqlConnection verifier = new(_baseConnectionString);
            verifier.Open();

            using (SqlCommand checkCmd = new(
                $"SELECT COUNT(*) FROM [{_tempDbName}].INFORMATION_SCHEMA.TABLES " +
                $"WHERE TABLE_NAME = @name", verifier))
            {
                checkCmd.Parameters.AddWithValue("@name", tableName);
                Assert.Equal(1, (int)checkCmd.ExecuteScalar());
            }

            if (!string.Equals(initialCatalog, _tempDbName,
                    StringComparison.OrdinalIgnoreCase))
            {
                using SqlCommand negCmd = new(
                    $"SELECT COUNT(*) FROM [{initialCatalog}].INFORMATION_SCHEMA.TABLES " +
                    $"WHERE TABLE_NAME = @name", verifier);
                negCmd.Parameters.AddWithValue("@name", tableName);
                Assert.Equal(0, (int)negCmd.ExecuteScalar());
            }
        }

        /// <summary>
        /// Kills the connection twice in rapid succession: once after USE, and
        /// again immediately after the first reconnection completes (before any
        /// user queries).  Then creates a table and verifies it lands in the
        /// correct database.  Tests that recovery data is re-snapshotted
        /// correctly on the second reconnection.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public void UseDatabase_DoubleKill_CreateTable_LandsInCorrectDb()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: false);
            builder.ConnectRetryCount = 3; // Need extra retries for double kill
            string tableName = "tbl_dblkill_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _createdTableNames.Add(tableName);

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
            {
                useCmd.ExecuteNonQuery();
            }

            // First kill
            KillSpid(conn.ServerProcessId);

            // Force reconnection by querying — this triggers session recovery
            string dbAfterFirst = GetServerDatabase(conn);
            Assert.True(
                string.Equals(_tempDbName, dbAfterFirst, StringComparison.OrdinalIgnoreCase),
                $"After first kill: expected '{_tempDbName}', got '{dbAfterFirst}'");

            // Second kill — immediately after first reconnection
            KillSpid(conn.ServerProcessId);

            // Create table after second reconnection
            using (SqlCommand createCmd = new(
                $"CREATE TABLE [{tableName}] (Id INT)", conn))
            {
                createCmd.ExecuteNonQuery();
            }

            AssertDatabaseContext(conn, _tempDbName, "after double kill");

            // Verify object location
            using SqlConnection verifier = new(_baseConnectionString);
            verifier.Open();
            using SqlCommand checkCmd = new(
                $"SELECT COUNT(*) FROM [{_tempDbName}].INFORMATION_SCHEMA.TABLES " +
                $"WHERE TABLE_NAME = @name", verifier);
            checkCmd.Parameters.AddWithValue("@name", tableName);
            Assert.Equal(1, (int)checkCmd.ExecuteScalar());
        }

        /// <summary>
        /// Uses async code paths (<c>ExecuteNonQueryAsync</c>,
        /// <c>ExecuteScalarAsync</c>) for the USE, post-reconnect DDL, and
        /// verification queries.  Async internals differ from sync and may
        /// race differently during session recovery.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer))]
        public async Task UseDatabase_KillReconnect_Async_CreateTable_LandsInCorrectDb()
        {
            AppContext.SetSwitch(SwitchName, false);

            var builder = BuildConnectionString(pooling: false);
            string tableName = "tbl_async_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _createdTableNames.Add(tableName);

            using SqlConnection conn = new(builder.ConnectionString);
            await conn.OpenAsync();

            using (SqlCommand useCmd = new($"USE [{_tempDbName}]", conn))
            {
                await useCmd.ExecuteNonQueryAsync();
            }

            KillSpid(conn.ServerProcessId);

            // Async DDL after reconnection
            using (SqlCommand createCmd = new(
                $"CREATE TABLE [{tableName}] (Id INT PRIMARY KEY, Val NVARCHAR(50))", conn))
            {
                await createCmd.ExecuteNonQueryAsync();
            }

            using (SqlCommand insertCmd = new(
                $"INSERT INTO [{tableName}] (Id, Val) VALUES (1, 'async_test')", conn))
            {
                await insertCmd.ExecuteNonQueryAsync();
            }

            // Async read-back
            using (SqlCommand readCmd = new($"SELECT Val FROM [{tableName}] WHERE Id = 1", conn))
            {
                string val = (string)await readCmd.ExecuteScalarAsync();
                Assert.Equal("async_test", val);
            }

            // Async DB_NAME check
            using (SqlCommand dbCmd = new("SELECT DB_NAME()", conn))
            {
                string serverDb = (string)await dbCmd.ExecuteScalarAsync();
                Assert.True(
                    string.Equals(_tempDbName, serverDb, StringComparison.OrdinalIgnoreCase),
                    $"Async: expected '{_tempDbName}', DB_NAME(): '{serverDb}'");
            }

            // Verify from separate connection
            using SqlConnection verifier = new(_baseConnectionString);
            verifier.Open();
            using SqlCommand checkCmd = new(
                $"SELECT COUNT(*) FROM [{_tempDbName}].INFORMATION_SCHEMA.TABLES " +
                $"WHERE TABLE_NAME = @name", verifier);
            checkCmd.Parameters.AddWithValue("@name", tableName);
            Assert.Equal(1, (int)checkCmd.ExecuteScalar());
        }

        #endregion
    }
}
