// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    // Verifies that every connection opened inside a TransactionScope observes the scope's
    // isolation level, even after a pooled physical connection is re-checked-out from the
    // transacted pool. The driver re-issues SET TRANSACTION ISOLATION LEVEL on the re-attach
    // because sp_reset_connection_keep_transaction does not preserve the session isolation
    // level on every server (notably Azure SQL DB).
    //
    // These tests assert driver behavior, not server behavior, so they run against every
    // back end. On-prem SQL Server happens to preserve the level across the reset today, but
    // running there still guards against a driver-side regression if that ever changes.
    [Trait("Set", "3")]
    public static class TransactionScopeIsolationReassertTest
    {
        private const string GetIsoSql = @"
SELECT CASE transaction_isolation_level
    WHEN 0 THEN 'Unspecified'
    WHEN 1 THEN 'ReadUncommitted'
    WHEN 2 THEN 'ReadCommitted'
    WHEN 3 THEN 'RepeatableRead'
    WHEN 4 THEN 'Serializable'
    WHEN 5 THEN 'Snapshot'
END
FROM sys.dm_exec_sessions WHERE session_id = @@SPID;";

        public static TheoryData<System.Transactions.IsolationLevel, string> IsolationLevels => new()
        {
            { System.Transactions.IsolationLevel.ReadUncommitted, "ReadUncommitted" },
            // ReadCommitted exercises the skip path: the driver deliberately does not emit a SET
            // for it, because that is what the session already reverts to after the reset. The
            // assertion guards that assumption.
            { System.Transactions.IsolationLevel.ReadCommitted, "ReadCommitted" },
            { System.Transactions.IsolationLevel.RepeatableRead, "RepeatableRead" },
            { System.Transactions.IsolationLevel.Serializable, "Serializable" },
        };

        // Excluded on Synapse dedicated pools, which accept only READ UNCOMMITTED.
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(IsolationLevels))]
        public static void TransactionScope_IsolationLevelHonoredAcrossPoolReuse_Sync(
            System.Transactions.IsolationLevel scopeLevel,
            string expected)
        {
            using TransactionScope scope = CreateScope(scopeLevel);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(expected, GetSessionIsolationLevel(BuildConnectionString()));
            }

            scope.Complete();
        }

        // Excluded on Synapse dedicated pools, which accept only READ UNCOMMITTED.
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(IsolationLevels))]
        public static async Task TransactionScope_IsolationLevelHonoredAcrossPoolReuse_Async(
            System.Transactions.IsolationLevel scopeLevel,
            string expected)
        {
            using TransactionScope scope = CreateScope(scopeLevel);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(expected, await GetSessionIsolationLevelAsync(BuildConnectionString()));
            }

            scope.Complete();
        }

        // A session-level SET inside the scope is the hostile case: it moves the physical
        // connection off the scope's level while the transaction is still open. The re-checkout
        // must put the level back, otherwise the rest of the scope silently runs at the
        // overridden level. Covered across MARS and both pool implementations because the
        // transacted-pool re-checkout path differs between them.
        [ConditionalTheory(
            typeof(TransactionScopeIsolationReassertTest),
            nameof(IsSessionOverrideScenarioSupported))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static void TransactionScope_ReassertsLevelAfterSessionOverride_Sync(bool mars, bool usePoolV2)
        {
            using LocalAppContextSwitchesHelper switches = new() { UseConnectionPoolV2 = usePoolV2 };
            string connectionString = BuildConnectionString(mars, usePoolV2);

            using TransactionScope scope = CreateScope(System.Transactions.IsolationLevel.Serializable);

            Assert.Equal("Serializable", GetSessionIsolationLevel(connectionString));

            // Override the level on the pooled session, then return it to the transacted pool.
            Assert.Equal("ReadCommitted", GetSessionIsolationLevel(connectionString, "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"));

            // Re-checkout of the same physical connection must restore the scope's level.
            Assert.Equal("Serializable", GetSessionIsolationLevel(connectionString));

            scope.Complete();
        }

        // Snapshot is called out separately because it is the one level the server refuses to
        // move into part-way through a transaction that began under a different level. The
        // re-assert is legal here only because the preserved transaction was itself begun under
        // snapshot isolation, so this guards that reasoning.
        [ConditionalTheory(
            typeof(TransactionScopeIsolationReassertTest),
            nameof(IsSnapshotScenarioSupported))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static void TransactionScope_ReassertsSnapshotAfterSessionOverride_Sync(bool mars, bool usePoolV2)
        {
            using LocalAppContextSwitchesHelper switches = new() { UseConnectionPoolV2 = usePoolV2 };
            string connectionString = BuildConnectionString(mars, usePoolV2);

            using TransactionScope scope = CreateScope(System.Transactions.IsolationLevel.Snapshot);

            Assert.Equal("Snapshot", GetSessionIsolationLevel(connectionString));
            Assert.Equal("ReadCommitted", GetSessionIsolationLevel(connectionString, "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"));
            Assert.Equal("Snapshot", GetSessionIsolationLevel(connectionString));

            scope.Complete();
        }

        [ConditionalTheory(
            typeof(TransactionScopeIsolationReassertTest),
            nameof(IsSnapshotScenarioSupported))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static async Task TransactionScope_ReassertsSnapshotAfterSessionOverride_Async(bool mars, bool usePoolV2)
        {
            using LocalAppContextSwitchesHelper switches = new() { UseConnectionPoolV2 = usePoolV2 };
            string connectionString = BuildConnectionString(mars, usePoolV2);

            using TransactionScope scope = CreateScope(System.Transactions.IsolationLevel.Snapshot);

            Assert.Equal("Snapshot", await GetSessionIsolationLevelAsync(connectionString));
            Assert.Equal("ReadCommitted", await GetSessionIsolationLevelAsync(connectionString, "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"));
            Assert.Equal("Snapshot", await GetSessionIsolationLevelAsync(connectionString));

            scope.Complete();
        }

        // Synapse is excluded because the scope levels used above are not available there.
        public static bool IsSessionOverrideScenarioSupported() =>
            DataTestUtility.AreConnStringsSetup() && DataTestUtility.IsNotAzureSynapse();

        // Snapshot additionally requires ALLOW_SNAPSHOT_ISOLATION on the target database.
        public static bool IsSnapshotScenarioSupported() =>
            IsSessionOverrideScenarioSupported() && IsSnapshotIsolationEnabled();

        private static bool? s_snapshotIsolationEnabled;

        private static bool IsSnapshotIsolationEnabled()
        {
            if (s_snapshotIsolationEnabled.HasValue)
            {
                return s_snapshotIsolationEnabled.Value;
            }

            try
            {
                using SqlConnection conn = new(DataTestUtility.TCPConnectionString);
                conn.Open();

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = DB_ID();";

                s_snapshotIsolationEnabled = cmd.ExecuteScalar() is byte state && state == 1;
            }
            catch (SqlException)
            {
                s_snapshotIsolationEnabled = false;
            }

            return s_snapshotIsolationEnabled.Value;
        }

        private static TransactionScope CreateScope(System.Transactions.IsolationLevel level) =>
            new(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = level },
                TransactionScopeAsyncFlowOption.Enabled);

        // Max Pool Size = 1 forces every open inside the scope onto the same physical
        // connection, which is what exercises the transacted-pool re-checkout path. The
        // application name carries the test dimensions so each combination gets its own pool
        // and cannot inherit a connection created under the other pool implementation.
        private static string BuildConnectionString(bool mars = false, bool usePoolV2 = false) =>
            new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MultipleActiveResultSets = mars,
                ApplicationName = $"{nameof(TransactionScopeIsolationReassertTest)}-{mars}-{usePoolV2}"
            }.ConnectionString;

        private static string GetSessionIsolationLevel(string connectionString, string preamble = null)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = preamble is null ? GetIsoSql : preamble + GetIsoSql;

            return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        }

        private static async Task<string> GetSessionIsolationLevelAsync(string connectionString, string preamble = null)
        {
            using SqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = preamble is null ? GetIsoSql : preamble + GetIsoSql;

            object result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }
    }
}
