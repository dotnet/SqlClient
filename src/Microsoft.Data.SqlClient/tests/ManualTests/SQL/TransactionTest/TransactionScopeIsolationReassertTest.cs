// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies that every connection opened inside a <see cref="TransactionScope"/> observes the
    /// scope's isolation level, even after a pooled physical connection is re-checked-out from the
    /// transacted pool. The driver re-issues SET TRANSACTION ISOLATION LEVEL on the re-attach
    /// because sp_reset_connection_keep_transaction does not preserve the session isolation level
    /// on every server (notably Azure SQL DB).
    /// </summary>
    /// <remarks>
    /// These tests assert driver behavior, not server behavior, so they run against every back end.
    /// On-prem SQL Server happens to preserve the level across the reset today, but running there
    /// still guards against a driver-side regression if that ever changes.
    /// </remarks>
    [Trait("Set", "3")]
    public static class TransactionScopeIsolationReassertTest
    {
        /// <summary>
        /// Reads the isolation level currently in effect on the server session, so assertions
        /// reflect server state rather than what the driver believes it requested.
        /// </summary>
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

        /// <summary>
        /// The scope isolation levels exercised by the baseline theories, paired with the name
        /// the server reports for each. ReadCommitted is included deliberately: the driver skips
        /// emitting a SET for it, because that is the level the session already reverts to after
        /// the reset, and the assertion guards that assumption.
        /// </summary>
        public static TheoryData<System.Transactions.IsolationLevel, string> IsolationLevels => new()
        {
            { System.Transactions.IsolationLevel.ReadUncommitted, "ReadUncommitted" },
            { System.Transactions.IsolationLevel.ReadCommitted, "ReadCommitted" },
            { System.Transactions.IsolationLevel.RepeatableRead, "RepeatableRead" },
            { System.Transactions.IsolationLevel.Serializable, "Serializable" },
        };

        /// <summary>
        /// Regression guard for GH#146: repeated opens inside one scope must each report the
        /// scope's isolation level, not the database default that the queued reset would leave
        /// behind. Excluded on Synapse dedicated pools, which accept only READ UNCOMMITTED.
        /// </summary>
        /// <param name="scopeLevel">Isolation level requested on the ambient scope.</param>
        /// <param name="expected">Level name the server is expected to report.</param>
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
                Assert.Equal(expected, GetSessionIsolationLevel(BuildConnectionString(tag: $"SyncBase{scopeLevel}")));
            }

            scope.Complete();
        }

        /// <summary>
        /// Async counterpart of the baseline theory. The re-attach happens on a different code
        /// path for OpenAsync, so sync/async parity is asserted explicitly.
        /// </summary>
        /// <param name="scopeLevel">Isolation level requested on the ambient scope.</param>
        /// <param name="expected">Level name the server is expected to report.</param>
        /// <returns>A task that completes when the scenario has been verified.</returns>
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
                Assert.Equal(expected, await GetSessionIsolationLevelAsync(BuildConnectionString(tag: $"AsyncBase{scopeLevel}")));
            }

            scope.Complete();
        }

        /// <summary>
        /// A session-level SET inside the scope is the hostile case: it moves the physical
        /// connection off the scope's level while the transaction is still open. The re-checkout
        /// must put the level back, otherwise the rest of the scope silently runs at the
        /// overridden level.
        /// </summary>
        /// <param name="mars">Whether MultipleActiveResultSets is enabled on the connection.</param>
        /// <param name="usePoolV2">
        /// Whether to run against ChannelDbConnectionPool; the transacted-pool re-checkout path
        /// differs between the two pool implementations, so both are covered.
        /// </param>
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
            string connectionString = BuildConnectionString(mars, usePoolV2, "OverrideSync");

            using TransactionScope scope = CreateScope(System.Transactions.IsolationLevel.Serializable);

            Assert.Equal("Serializable", GetSessionIsolationLevel(connectionString));

            // Override the level on the pooled session, then return it to the transacted pool.
            Assert.Equal("ReadCommitted", GetSessionIsolationLevel(connectionString, "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"));

            // Re-checkout of the same physical connection must restore the scope's level.
            Assert.Equal("Serializable", GetSessionIsolationLevel(connectionString));

            scope.Complete();
        }

        /// <summary>
        /// Async counterpart of the session-override scenario, asserting sync/async parity for
        /// the restore behavior.
        /// </summary>
        /// <param name="mars">Whether MultipleActiveResultSets is enabled on the connection.</param>
        /// <param name="usePoolV2">Whether to run against ChannelDbConnectionPool.</param>
        /// <returns>A task that completes when the scenario has been verified.</returns>
        [ConditionalTheory(
            typeof(TransactionScopeIsolationReassertTest),
            nameof(IsSessionOverrideScenarioSupported))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static async Task TransactionScope_ReassertsLevelAfterSessionOverride_Async(bool mars, bool usePoolV2)
        {
            using LocalAppContextSwitchesHelper switches = new() { UseConnectionPoolV2 = usePoolV2 };
            string connectionString = BuildConnectionString(mars, usePoolV2, "OverrideAsync");

            using TransactionScope scope = CreateScope(System.Transactions.IsolationLevel.Serializable);

            Assert.Equal("Serializable", await GetSessionIsolationLevelAsync(connectionString));

            // Override the level on the pooled session, then return it to the transacted pool.
            Assert.Equal("ReadCommitted", await GetSessionIsolationLevelAsync(connectionString, "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;"));

            // Re-checkout of the same physical connection must restore the scope's level.
            Assert.Equal("Serializable", await GetSessionIsolationLevelAsync(connectionString));

            scope.Complete();
        }

        /// <summary>
        /// Snapshot is covered separately because it is the one level the server refuses to move
        /// into part-way through a transaction begun under a different level. The re-assert is
        /// legal here only because the preserved transaction was itself begun under snapshot
        /// isolation, so this guards that reasoning and proves the re-issued SET does not raise
        /// error 3951 on re-checkout.
        /// </summary>
        /// <remarks>
        /// The hostile session-override variant used for the other levels is deliberately absent:
        /// SQL Server defers a SET TRANSACTION ISOLATION LEVEL issued inside an open snapshot
        /// transaction until that transaction ends, so the session cannot actually be moved off
        /// snapshot mid-scope.
        /// </remarks>
        /// <param name="mars">Whether MultipleActiveResultSets is enabled on the connection.</param>
        /// <param name="usePoolV2">Whether to run against ChannelDbConnectionPool.</param>
        [ConditionalTheory(
            typeof(TransactionScopeIsolationReassertTest),
            nameof(IsSnapshotScenarioSupported))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static void TransactionScope_SnapshotHonoredAcrossPoolReuse_Sync(bool mars, bool usePoolV2)
        {
            using LocalAppContextSwitchesHelper switches = new() { UseConnectionPoolV2 = usePoolV2 };
            string connectionString = BuildConnectionString(mars, usePoolV2, "SnapSync");

            using TransactionScope scope = CreateScope(System.Transactions.IsolationLevel.Snapshot);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal("Snapshot", GetSessionIsolationLevel(connectionString));
            }

            scope.Complete();
        }

        /// <summary>
        /// Async counterpart of the snapshot scenario, asserting sync/async parity for the
        /// snapshot re-assert.
        /// </summary>
        /// <param name="mars">Whether MultipleActiveResultSets is enabled on the connection.</param>
        /// <param name="usePoolV2">Whether to run against ChannelDbConnectionPool.</param>
        /// <returns>A task that completes when the scenario has been verified.</returns>
        [ConditionalTheory(
            typeof(TransactionScopeIsolationReassertTest),
            nameof(IsSnapshotScenarioSupported))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static async Task TransactionScope_SnapshotHonoredAcrossPoolReuse_Async(bool mars, bool usePoolV2)
        {
            using LocalAppContextSwitchesHelper switches = new() { UseConnectionPoolV2 = usePoolV2 };
            string connectionString = BuildConnectionString(mars, usePoolV2, "SnapAsync");

            using TransactionScope scope = CreateScope(System.Transactions.IsolationLevel.Snapshot);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal("Snapshot", await GetSessionIsolationLevelAsync(connectionString));
            }

            scope.Complete();
        }

        /// <summary>
        /// Gate for the session-override theories. Synapse is excluded because the scope levels
        /// used there are not available.
        /// </summary>
        /// <returns>True when the scenario can run against the configured server.</returns>
        public static bool IsSessionOverrideScenarioSupported() =>
            DataTestUtility.AreConnStringsSetup() && DataTestUtility.IsNotAzureSynapse();

        /// <summary>
        /// Gate for the snapshot theories, which additionally require ALLOW_SNAPSHOT_ISOLATION on
        /// the target database.
        /// </summary>
        /// <returns>True when the snapshot scenario can run against the configured server.</returns>
        public static bool IsSnapshotScenarioSupported() =>
            IsSessionOverrideScenarioSupported() && IsSnapshotIsolationEnabled();

        private static bool? s_snapshotIsolationEnabled;

        /// <summary>
        /// Opens a probe connection to read the target database's snapshot isolation state. The
        /// result is cached for the lifetime of the test run so the probe runs at most once, and a
        /// failed probe is treated as "not enabled" so the theories skip rather than fail.
        /// </summary>
        /// <returns>True when ALLOW_SNAPSHOT_ISOLATION is on for the target database.</returns>
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

        /// <summary>
        /// Creates an ambient scope at the requested level with async flow enabled, so the sync
        /// and async theories share identical scope semantics.
        /// </summary>
        /// <param name="level">Isolation level to request on the scope.</param>
        /// <returns>The new ambient transaction scope.</returns>
        private static TransactionScope CreateScope(System.Transactions.IsolationLevel level) =>
            new(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = level },
                TransactionScopeAsyncFlowOption.Enabled);

        /// <summary>
        /// Builds a connection string pinned to a single pooled physical connection, which is what
        /// forces every open inside the scope onto the transacted-pool re-checkout path.
        /// </summary>
        /// <param name="mars">Whether to enable MultipleActiveResultSets.</param>
        /// <param name="usePoolV2">Which pool implementation the caller is exercising.</param>
        /// <param name="tag">
        /// Per-test discriminator. It is folded into the application name so each theory case gets
        /// its own pool and cannot inherit a connection created under the other pool
        /// implementation.
        /// </param>
        /// <returns>The connection string for the scenario.</returns>
        private static string BuildConnectionString(bool mars = false, bool usePoolV2 = false, string tag = "") =>
            new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MultipleActiveResultSets = mars,
                ApplicationName = $"IsoReassert-{tag}-{mars}-{usePoolV2}"
            }.ConnectionString;

        /// <summary>
        /// Opens a connection and reads back the isolation level the server reports for that
        /// session, optionally running a preamble first to mutate the session.
        /// </summary>
        /// <param name="connectionString">Connection string to open.</param>
        /// <param name="preamble">
        /// Optional statement run before the probe. It executes as its own command rather than as
        /// part of the SELECT batch, because under MARS a SET issued inside a multi-statement
        /// batch applies only to that batch's execution environment.
        /// </param>
        /// <returns>The session isolation level name reported by the server.</returns>
        private static string GetSessionIsolationLevel(string connectionString, string preamble = null)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            if (preamble is not null)
            {
                using SqlCommand setCmd = conn.CreateCommand();
                setCmd.CommandText = preamble;
                setCmd.ExecuteNonQuery();
            }

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = GetIsoSql;

            return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Async counterpart of <see cref="GetSessionIsolationLevel"/>, used so the async theories
        /// exercise OpenAsync and ExecuteScalarAsync rather than their sync equivalents.
        /// </summary>
        /// <param name="connectionString">Connection string to open.</param>
        /// <param name="preamble">Optional statement run as its own command before the probe.</param>
        /// <returns>The session isolation level name reported by the server.</returns>
        private static async Task<string> GetSessionIsolationLevelAsync(string connectionString, string preamble = null)
        {
            using SqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            if (preamble is not null)
            {
                using SqlCommand setCmd = conn.CreateCommand();
                setCmd.CommandText = preamble;
                await setCmd.ExecuteNonQueryAsync();
            }

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = GetIsoSql;

            object result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }
    }
}
