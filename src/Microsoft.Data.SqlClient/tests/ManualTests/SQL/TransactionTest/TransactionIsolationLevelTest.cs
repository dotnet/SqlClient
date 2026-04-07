// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Transactions;
using Xunit;

using IsolationLevel = System.Data.IsolationLevel;
using SysTxIsolationLevel = System.Transactions.IsolationLevel;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Comprehensive tests for transaction isolation level behavior with connection pooling.
    /// Covers two related bugs:
    /// <list type="bullet">
    ///   <item>
    ///     <see href="https://github.com/dotnet/SqlClient/issues/96">GH #96</see>:
    ///     Isolation level leaks to the next pool consumer after SqlTransaction/TransactionScope
    ///     completes (on-prem and Azure).
    ///   </item>
    ///   <item>
    ///     <see href="https://github.com/dotnet/SqlClient/issues/146">GH #146</see>:
    ///     Azure SQL sp_reset_connection with PRESERVE_TRANSACTION resets the isolation level
    ///     when it should not, breaking the second connection reuse within the same
    ///     TransactionScope.
    ///   </item>
    /// </list>
    /// </summary>
    public static class TransactionIsolationLevelTest
    {
        #region Helpers

        /// <summary>
        /// Returns the current session isolation level by querying DBCC USEROPTIONS.
        /// Uses DBCC USEROPTIONS instead of sys.dm_exec_sessions because the latter
        /// requires VIEW SERVER STATE permission, which is not always available on Azure SQL.
        /// </summary>
        private static string GetCurrentIsolationLevel(SqlConnection connection, SqlTransaction transaction = null)
        {
            using SqlCommand cmd = new("DBCC USEROPTIONS", connection);
            cmd.Transaction = transaction;
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(0) == "isolation level")
                {
                    return reader.GetString(1);
                }
            }

            throw new InvalidOperationException(
                "Could not determine isolation level from DBCC USEROPTIONS.");
        }

        /// <summary>
        /// Opens a new connection from the given connection string and returns the current
        /// session isolation level.
        /// </summary>
        private static string GetCurrentIsolationLevel(string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();
            return GetCurrentIsolationLevel(conn);
        }

        /// <summary>
        /// Maps a <see cref="System.Data.IsolationLevel"/> to the string returned by
        /// DBCC USEROPTIONS on both on-prem SQL Server and Azure SQL.
        /// </summary>
        /// <remarks>
        /// Azure SQL databases with READ_COMMITTED_SNAPSHOT ON return
        /// "read committed snapshot" for ReadCommitted. On-prem returns "read committed".
        /// Tests that compare ReadCommitted must account for both.
        /// </remarks>
        private static string IsolationLevelToString(IsolationLevel level) => level switch
        {
            IsolationLevel.ReadUncommitted => "read uncommitted",
            IsolationLevel.ReadCommitted => "read committed",
            IsolationLevel.RepeatableRead => "repeatable read",
            IsolationLevel.Serializable => "serializable",
            IsolationLevel.Snapshot => "snapshot",
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };

        /// <summary>
        /// Asserts that the actual isolation level matches the expected one. For ReadCommitted,
        /// Azure SQL with READ_COMMITTED_SNAPSHOT ON reports "read committed snapshot", which
        /// is accepted as equivalent.
        /// </summary>
        private static void AssertIsolationLevel(
            string expected, string actual, string message = null)
        {
            if (expected == "read committed" && actual == "read committed snapshot")
            {
                return; // Azure SQL with RCSI reports this; it's equivalent.
            }

            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Builds a pooled connection string with MaxPoolSize=1, ensuring the same physical
        /// connection is reused across open/close cycles.
        /// </summary>
        private static string BuildPooledConnectionString(bool enlist = true)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                Enlist = enlist,
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Builds a non-pooled connection string for baseline comparisons.
        /// </summary>
        private static string BuildUnpooledConnectionString()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = false,
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Maps <see cref="SysTxIsolationLevel"/> to <see cref="IsolationLevel"/>.
        /// </summary>
        private static IsolationLevel ToDataIsolationLevel(SysTxIsolationLevel level) =>
            level switch
            {
                SysTxIsolationLevel.ReadUncommitted => IsolationLevel.ReadUncommitted,
                SysTxIsolationLevel.ReadCommitted => IsolationLevel.ReadCommitted,
                SysTxIsolationLevel.RepeatableRead => IsolationLevel.RepeatableRead,
                SysTxIsolationLevel.Serializable => IsolationLevel.Serializable,
                SysTxIsolationLevel.Snapshot => IsolationLevel.Snapshot,
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            };

        #endregion

        // =====================================================================
        // Category 1: SqlTransaction — isolation level leak after pool round-trip
        //
        // After a SqlTransaction with a non-default isolation level completes
        // (commit or rollback) and the connection is returned to the pool, the
        // next consumer should observe the default isolation level (ReadCommitted).
        // GH #96
        // =====================================================================

        #region SqlTransaction — isolation level should not leak to next pool consumer

        /// <summary>
        /// After committing a SqlTransaction with a non-default isolation level, the next
        /// consumer of the same pooled connection should see ReadCommitted.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(IsolationLevel.ReadUncommitted)]
        [InlineData(IsolationLevel.RepeatableRead)]
        [InlineData(IsolationLevel.Serializable)]
        public static void SqlTransaction_Commit_DoesNotLeakIsolationLevel(
            IsolationLevel isolationLevel)
        {
            string connectionString = BuildPooledConnectionString(enlist: false);
            string expected = IsolationLevelToString(isolationLevel);

            SqlConnection.ClearAllPools();

            // Use a transaction with a non-default isolation level, then commit.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using SqlTransaction tx = conn.BeginTransaction(isolationLevel);
                string actual = GetCurrentIsolationLevel(conn, tx);
                AssertIsolationLevel(expected, actual,
                    "Isolation level should match during the transaction.");
                tx.Commit();
            }

            // Reuse the pooled connection — isolation level should be reset.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Isolation level should be reset to ReadCommitted after pool round-trip.");
            }
        }

        /// <summary>
        /// After rolling back a SqlTransaction with a non-default isolation level, the next
        /// consumer of the same pooled connection should see ReadCommitted.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(IsolationLevel.ReadUncommitted)]
        [InlineData(IsolationLevel.RepeatableRead)]
        [InlineData(IsolationLevel.Serializable)]
        public static void SqlTransaction_Rollback_DoesNotLeakIsolationLevel(
            IsolationLevel isolationLevel)
        {
            string connectionString = BuildPooledConnectionString(enlist: false);
            string expected = IsolationLevelToString(isolationLevel);

            SqlConnection.ClearAllPools();

            // Use a transaction with a non-default isolation level, then rollback.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using SqlTransaction tx = conn.BeginTransaction(isolationLevel);
                string actual = GetCurrentIsolationLevel(conn, tx);
                AssertIsolationLevel(expected, actual,
                    "Isolation level should match during the transaction.");
                tx.Rollback();
            }

            // Reuse the pooled connection — isolation level should be reset.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Isolation level should be reset to ReadCommitted after pool round-trip.");
            }
        }

        /// <summary>
        /// After disposing a SqlTransaction without explicit commit/rollback (implicit
        /// rollback), the next pool consumer should see ReadCommitted.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(IsolationLevel.ReadUncommitted)]
        [InlineData(IsolationLevel.RepeatableRead)]
        [InlineData(IsolationLevel.Serializable)]
        public static void SqlTransaction_Dispose_DoesNotLeakIsolationLevel(
            IsolationLevel isolationLevel)
        {
            string connectionString = BuildPooledConnectionString(enlist: false);

            SqlConnection.ClearAllPools();

            // Use a transaction with a non-default isolation level, then dispose
            // without commit or rollback (implicit rollback).
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction(isolationLevel))
                {
                    GetCurrentIsolationLevel(conn, tx); // exercise the connection
                }
                // tx is disposed here without commit/rollback
            }

            // Reuse the pooled connection — isolation level should be reset.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Isolation level should be reset to ReadCommitted after pool round-trip.");
            }
        }

        /// <summary>
        /// ReadCommitted is the default — using it in a SqlTransaction should not cause
        /// any reset overhead, and the next pool consumer should still see ReadCommitted.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void SqlTransaction_ReadCommitted_DoesNotLeakIsolationLevel()
        {
            string connectionString = BuildPooledConnectionString(enlist: false);

            SqlConnection.ClearAllPools();

            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using SqlTransaction tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                string actual = GetCurrentIsolationLevel(conn, tx);
                AssertIsolationLevel("read committed", actual);
                tx.Commit();
            }

            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "ReadCommitted transaction should not perturb the default.");
            }
        }

        /// <summary>
        /// Multiple successive transactions with different isolation levels should each
        /// reset properly, not accumulate or interfere with each other.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void SqlTransaction_SuccessiveTransactions_DoNotLeakIsolationLevel()
        {
            string connectionString = BuildPooledConnectionString(enlist: false);

            SqlConnection.ClearAllPools();

            IsolationLevel[] levels = new[]
            {
                IsolationLevel.Serializable,
                IsolationLevel.ReadUncommitted,
                IsolationLevel.RepeatableRead,
                IsolationLevel.Serializable,
            };

            foreach (IsolationLevel level in levels)
            {
                // Transaction with non-default isolation level
                using (SqlConnection conn = new(connectionString))
                {
                    conn.Open();
                    using SqlTransaction tx = conn.BeginTransaction(level);
                    string actual = GetCurrentIsolationLevel(conn, tx);
                    AssertIsolationLevel(IsolationLevelToString(level), actual);
                    tx.Commit();
                }

                // Next consumer should see ReadCommitted
                using (SqlConnection conn = new(connectionString))
                {
                    conn.Open();
                    string actual = GetCurrentIsolationLevel(conn);
                    AssertIsolationLevel("read committed", actual,
                        $"Isolation level should be reset after {level} transaction.");
                }
            }
        }

        #endregion

        // =====================================================================
        // Category 2: TransactionScope — isolation level leak after pool
        //             round-trip (completed/aborted scope)
        //
        // After a TransactionScope with a non-default isolation level completes
        // or is disposed without completion, the next consumer of the same pooled
        // connection should see the default isolation level (ReadCommitted).
        // GH #96
        // =====================================================================

        #region TransactionScope — isolation level should not leak to next pool consumer

        /// <summary>
        /// After completing a TransactionScope with a non-default isolation level, the next
        /// consumer of the same pooled connection should see ReadCommitted.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(SysTxIsolationLevel.ReadUncommitted)]
        [InlineData(SysTxIsolationLevel.RepeatableRead)]
        [InlineData(SysTxIsolationLevel.Serializable)]
        public static void TransactionScope_Complete_DoesNotLeakIsolationLevel(
            SysTxIsolationLevel txIsolationLevel)
        {
            string connectionString = BuildPooledConnectionString();
            string expected = IsolationLevelToString(ToDataIsolationLevel(txIsolationLevel));

            SqlConnection.ClearAllPools();

            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = txIsolationLevel }))
            {
                using (SqlConnection conn = new(connectionString))
                {
                    conn.Open();
                    string actual = GetCurrentIsolationLevel(conn);
                    AssertIsolationLevel(expected, actual,
                        "Isolation level should match TransactionScope during the scope.");
                }

                scope.Complete();
            }

            // Reuse the pooled connection — isolation level should be reset.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Isolation level should be reset after TransactionScope completes.");
            }
        }

        /// <summary>
        /// After a TransactionScope is disposed without Complete() (aborted), the next
        /// consumer of the same pooled connection should see ReadCommitted.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(SysTxIsolationLevel.ReadUncommitted)]
        [InlineData(SysTxIsolationLevel.RepeatableRead)]
        [InlineData(SysTxIsolationLevel.Serializable)]
        public static void TransactionScope_Abort_DoesNotLeakIsolationLevel(
            SysTxIsolationLevel txIsolationLevel)
        {
            string connectionString = BuildPooledConnectionString();
            string expected = IsolationLevelToString(ToDataIsolationLevel(txIsolationLevel));

            SqlConnection.ClearAllPools();

            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = txIsolationLevel }))
            {
                using (SqlConnection conn = new(connectionString))
                {
                    conn.Open();
                    string actual = GetCurrentIsolationLevel(conn);
                    AssertIsolationLevel(expected, actual,
                        "Isolation level should match TransactionScope during the scope.");
                }

                // Dispose without Complete() — transaction aborts.
            }

            // Reuse the pooled connection — isolation level should be reset.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Isolation level should be reset after aborted TransactionScope.");
            }
        }

        /// <summary>
        /// ReadCommitted TransactionScope should work without perturbing the default.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TransactionScope_ReadCommitted_DoesNotLeakIsolationLevel()
        {
            string connectionString = BuildPooledConnectionString();

            SqlConnection.ClearAllPools();

            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = SysTxIsolationLevel.ReadCommitted }))
            {
                using (SqlConnection conn = new(connectionString))
                {
                    conn.Open();
                    string actual = GetCurrentIsolationLevel(conn);
                    AssertIsolationLevel("read committed", actual);
                }

                scope.Complete();
            }

            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "ReadCommitted scope should not perturb the default.");
            }
        }

        #endregion

        // =====================================================================
        // Category 3: TransactionScope — isolation level preserved across
        //             multiple connections within the same scope
        //
        // When multiple connections are opened within a single TransactionScope,
        // they should all observe the TransactionScope's isolation level. On
        // Azure SQL, sp_reset_connection with PRESERVE_TRANSACTION incorrectly
        // resets the isolation level, causing the second connection to fall back
        // to the server default.
        // GH #146
        // =====================================================================

        #region TransactionScope — isolation level preserved within scope

        /// <summary>
        /// When a second connection is opened within the same TransactionScope (reusing the
        /// same pooled physical connection via the transacted pool), it should observe the
        /// same isolation level as the first connection.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(SysTxIsolationLevel.ReadUncommitted)]
        [InlineData(SysTxIsolationLevel.ReadCommitted)]
        [InlineData(SysTxIsolationLevel.RepeatableRead)]
        [InlineData(SysTxIsolationLevel.Serializable)]
        public static void TransactionScope_IsolationLevel_PreservedAcrossConnections(
            SysTxIsolationLevel txIsolationLevel)
        {
            string connectionString = BuildPooledConnectionString();
            string expected = IsolationLevelToString(ToDataIsolationLevel(txIsolationLevel));

            SqlConnection.ClearAllPools();

            using TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = txIsolationLevel });

            // First connection: should observe the TransactionScope's isolation level.
            string firstLevel = GetCurrentIsolationLevel(connectionString);
            AssertIsolationLevel(expected, firstLevel,
                "First connection should use the TransactionScope isolation level.");

            // Second connection: reuses the pooled connection after sp_reset_connection
            // with PRESERVE_TRANSACTION. Should still observe the same isolation level.
            string secondLevel = GetCurrentIsolationLevel(connectionString);
            Assert.Equal(firstLevel, secondLevel);
        }

        #endregion

        // =====================================================================
        // Category 4: TransactionScope + SqlTransaction interactions
        //
        // Tests that combine TransactionScope with explicit SqlTransaction usage,
        // and sequential scopes with different isolation levels.
        // =====================================================================

        #region Mixed scenarios

        /// <summary>
        /// After a TransactionScope with Serializable completes, a subsequent
        /// SqlTransaction with Serializable should also not leak.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TransactionScope_ThenSqlTransaction_DoesNotLeakIsolationLevel()
        {
            string connectionString = BuildPooledConnectionString(enlist: false);

            SqlConnection.ClearAllPools();

            // First: TransactionScope with Serializable
            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = SysTxIsolationLevel.Serializable }))
            {
                // Open with Enlist=true just for this scope
                SqlConnectionStringBuilder b = new(connectionString) { Enlist = true };
                using SqlConnection conn = new(b.ConnectionString);
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("serializable", actual);
                scope.Complete();
            }

            // Second: SqlTransaction with Serializable on the same pool
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using SqlTransaction tx = conn.BeginTransaction(IsolationLevel.Serializable);
                tx.Commit();
            }

            // Third: verify isolation level is reset
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Isolation level should be reset after mixed scope+transaction.");
            }
        }

        /// <summary>
        /// Two successive TransactionScopes with different isolation levels should each
        /// correctly set the level and reset after completion.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void SuccessiveTransactionScopes_DifferentIsolationLevels_Reset()
        {
            string connectionString = BuildPooledConnectionString();

            SqlConnection.ClearAllPools();

            // First scope: Serializable
            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = SysTxIsolationLevel.Serializable }))
            {
                string actual = GetCurrentIsolationLevel(connectionString);
                AssertIsolationLevel("serializable", actual);
                scope.Complete();
            }

            // Verify reset
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Should be reset after first Serializable scope.");
            }

            // Second scope: RepeatableRead
            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = SysTxIsolationLevel.RepeatableRead }))
            {
                string actual = GetCurrentIsolationLevel(connectionString);
                AssertIsolationLevel("repeatable read", actual);
                scope.Complete();
            }

            // Verify reset
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Should be reset after second RepeatableRead scope.");
            }
        }

        #endregion

        // =====================================================================
        // Category 5: Non-pooled connections (baseline / sanity checks)
        //
        // Without pooling, each connection is a fresh session. Isolation level
        // should always be the server default and never leak.
        // =====================================================================

        #region Non-pooled baseline

        /// <summary>
        /// Without pooling, a new connection after a Serializable SqlTransaction should
        /// always get the default isolation level because a fresh TDS session is created.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void SqlTransaction_NonPooled_NoLeakBaseline()
        {
            string connectionString = BuildUnpooledConnectionString();

            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using SqlTransaction tx = conn.BeginTransaction(IsolationLevel.Serializable);
                string actual = GetCurrentIsolationLevel(conn, tx);
                AssertIsolationLevel("serializable", actual);
                tx.Commit();
            }

            // Fresh connection — must be ReadCommitted (or RCSI on Azure).
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Non-pooled connection should always get default isolation level.");
            }
        }

        /// <summary>
        /// Without pooling, a TransactionScope should not affect the next connection.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TransactionScope_NonPooled_NoLeakBaseline()
        {
            string connectionString = BuildUnpooledConnectionString();

            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = SysTxIsolationLevel.Serializable }))
            {
                SqlConnectionStringBuilder b = new(connectionString) { Enlist = true };
                using SqlConnection conn = new(b.ConnectionString);
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("serializable", actual);
                scope.Complete();
            }

            // Fresh connection — must be ReadCommitted (or RCSI on Azure).
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "Non-pooled connection should always get default isolation level.");
            }
        }

        #endregion

        // =====================================================================
        // Category 6: MARS (Multiple Active Result Sets) interactions
        //
        // MARS connections use session multiplexing, which could interact
        // differently with isolation level reset.
        // =====================================================================

        #region MARS

        /// <summary>
        /// SqlTransaction with a non-default isolation level on a MARS connection should
        /// not leak to the next pool consumer. With MARS enabled, the TDS connection reset
        /// uses a different code path (session multiplexing with _resetConnectionEvent)
        /// than non-MARS connections. This test verifies isolation level reset works
        /// correctly on that path.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(IsolationLevel.Serializable)]
        [InlineData(IsolationLevel.RepeatableRead)]
        public static void SqlTransaction_MARS_DoesNotLeakIsolationLevel(
            IsolationLevel isolationLevel)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MultipleActiveResultSets = true,
                Enlist = false,
            };
            string connectionString = builder.ConnectionString;

            SqlConnection.ClearAllPools();

            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using SqlTransaction tx = conn.BeginTransaction(isolationLevel);
                string actual = GetCurrentIsolationLevel(conn, tx);
                AssertIsolationLevel(IsolationLevelToString(isolationLevel), actual);
                tx.Commit();
            }

            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "MARS: Isolation level should be reset after pool round-trip.");
            }
        }

        /// <summary>
        /// TransactionScope with MARS enabled — isolation level should be preserved
        /// within scope and not leak after. MARS uses a different TDS reset code path
        /// (session multiplexing) than non-MARS connections, so this validates that
        /// the reset works correctly on the MARS path.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TransactionScope_MARS_DoesNotLeakIsolationLevel()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MultipleActiveResultSets = true,
                Enlist = true,
            };
            string connectionString = builder.ConnectionString;

            SqlConnection.ClearAllPools();

            using (TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = SysTxIsolationLevel.Serializable }))
            {
                string actual = GetCurrentIsolationLevel(connectionString);
                AssertIsolationLevel("serializable", actual);
                scope.Complete();
            }

            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "MARS+TransactionScope: Isolation level should be reset.");
            }
        }

        /// <summary>
        /// Exercises MARS-specific concurrency: opens multiple active result sets on
        /// the same connection within a Serializable transaction, then verifies the
        /// isolation level does not leak to the next pool consumer.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void SqlTransaction_MARS_ConcurrentReaders_DoesNotLeakIsolationLevel()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MultipleActiveResultSets = true,
            };
            string connectionString = builder.ConnectionString;

            SqlConnection.ClearAllPools();

            // First use: open concurrent readers under a Serializable transaction.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                using SqlTransaction tx = conn.BeginTransaction(IsolationLevel.Serializable);

                // Open two readers concurrently on the same MARS connection.
                using SqlCommand cmd1 = new("SELECT 1", conn, tx);
                using SqlCommand cmd2 = new("SELECT 2", conn, tx);
                using SqlDataReader reader1 = cmd1.ExecuteReader();
                using SqlDataReader reader2 = cmd2.ExecuteReader();

                // Both readers are active simultaneously — this is the MARS scenario.
                Assert.True(reader1.Read());
                Assert.True(reader2.Read());
                Assert.Equal(1, reader1.GetInt32(0));
                Assert.Equal(2, reader2.GetInt32(0));

                reader1.Close();
                reader2.Close();
                tx.Commit();
            }

            // Second use: same pooled connection should NOT retain Serializable.
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();
                string actual = GetCurrentIsolationLevel(conn);
                AssertIsolationLevel("read committed", actual,
                    "MARS concurrent readers: Isolation level should be reset after pool round-trip.");
            }
        }

        #endregion
    }
}
