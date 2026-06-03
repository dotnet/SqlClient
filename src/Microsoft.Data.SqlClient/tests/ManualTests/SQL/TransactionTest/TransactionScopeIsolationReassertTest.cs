// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    // Verifies that every connection opened inside a TransactionScope observes
    // the scope's isolation level, even after a pooled physical connection is
    // re-checked-out from the transacted pool. The driver must re-issue
    // SET TRANSACTION ISOLATION LEVEL on the re-attach because
    // sp_reset_connection does not preserve the session isolation level on
    // every server (notably Azure SQL DB).
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

        // Only meaningful on Azure SQL DB, where sp_reset_connection resets the
        // session isolation level. On on-prem the symptom does not surface
        // because the level survives the reset.
        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsAzureServer))]
        public static async Task TransactionScope_SerializableHonoredAcrossPoolReuse()
        {
            string cs = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                ApplicationName = nameof(TransactionScopeIsolationReassertTest)
            }.ConnectionString;

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.Serializable },
                TransactionScopeAsyncFlowOption.Enabled))
            {
                for (int i = 0; i < 3; i++)
                {
                    string level = await GetSessionIsolationLevelAsync(cs);
                    Assert.Equal("Serializable", level);
                }

                scope.Complete();
            }
        }

        [ConditionalFact(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsAzureServer))]
        public static async Task TransactionScope_ReadUncommittedHonoredAcrossPoolReuse()
        {
            string cs = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                ApplicationName = nameof(TransactionScopeIsolationReassertTest)
            }.ConnectionString;

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted },
                TransactionScopeAsyncFlowOption.Enabled))
            {
                for (int i = 0; i < 3; i++)
                {
                    string level = await GetSessionIsolationLevelAsync(cs);
                    Assert.Equal("ReadUncommitted", level);
                }

                scope.Complete();
            }
        }

        private static async Task<string> GetSessionIsolationLevelAsync(string cs)
        {
            using SqlConnection conn = new(cs);
            await conn.OpenAsync();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = GetIsoSql;

            object result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }
    }
}
