// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    // Verifies that every connection opened inside a TransactionScope observes the scope's
    // isolation level, even after a pooled physical connection is re-checked-out from the
    // transacted pool. The driver re-issues SET TRANSACTION ISOLATION LEVEL on the re-attach
    // because sp_reset_connection does not preserve the session isolation level on every server
    // (notably Azure SQL DB).
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

        private static TransactionScope CreateScope(System.Transactions.IsolationLevel level) =>
            new(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = level },
                TransactionScopeAsyncFlowOption.Enabled);

        // Max Pool Size = 1 forces every open inside the scope onto the same physical
        // connection, which is what exercises the transacted-pool re-checkout path.
        private static string BuildConnectionString() =>
            new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                ApplicationName = nameof(TransactionScopeIsolationReassertTest)
            }.ConnectionString;

        private static string GetSessionIsolationLevel(string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = GetIsoSql;

            return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        }

        private static async Task<string> GetSessionIsolationLevelAsync(string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = GetIsoSql;

            object result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }
    }
}
