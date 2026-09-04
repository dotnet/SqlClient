// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;
using IsolationLevel = System.Data.IsolationLevel;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies that an elevated session isolation level does not survive a trip through the
    /// connection pool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// sp_reset_connection does not reset the session isolation level, so before this fix a
    /// connection returned to the pool after a Serializable SqlTransaction or TransactionScope kept
    /// that level, and the next caller to be handed the same physical connection silently inherited
    /// it. The driver now resets the session to READ COMMITTED when the connection is taken back out
    /// of the pool.
    /// </para>
    /// <para>
    /// Every test pins MaxPoolSize to 1 and asserts on @@SPID so that pool reuse is proven rather
    /// than assumed; without the SPID assertion a fresh connection would satisfy the isolation
    /// assertion for the wrong reason. Each case runs over both the synchronous and the asynchronous
    /// API, because the reset is performed during connection activation, which Open and OpenAsync
    /// both reach.
    /// </para>
    /// <para>
    /// Azure Synapse is excluded throughout: dedicated SQL pools reject every isolation level except
    /// READ UNCOMMITTED, so the Serializable setup these tests depend on cannot run there, and the
    /// driver deliberately skips the reset for those endpoints. AreConnStringsSetup and
    /// IsNotAzureServer do not filter Synapse out on their own, because IsNotAzureServer only
    /// recognizes .database.* host names.
    /// </para>
    /// </remarks>
    [Trait("Set", "3")]
    public static class IsolationLevelLeakTest
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

        /// <summary>
        /// Builds a connection string limited to a single pooled connection so that the second Open
        /// in each test is guaranteed to receive the same physical connection as the first.
        /// </summary>
        /// <param name="appName">
        /// Application name used to give each test its own pool, keeping the tests independent when
        /// the assembly is run as a whole.
        /// </param>
        /// <returns>A pooled connection string with MaxPoolSize set to 1.</returns>
        private static string BuildPooledConnString(string appName) =>
            new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 1,
                MultipleActiveResultSets = false,
                Enlist = true,
                ApplicationName = appName
            }.ConnectionString;

        /// <summary>
        /// Opens the connection over the synchronous or asynchronous API so that a single test body
        /// can exercise both activation paths.
        /// </summary>
        /// <param name="connection">The connection to open.</param>
        /// <param name="async">When true, uses OpenAsync; otherwise uses Open.</param>
        private static async Task OpenConnection(SqlConnection connection, bool async)
        {
            if (async)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }
        }

        /// <summary>
        /// Reads the server process id of the session backing the connection, used to prove that the
        /// pool handed back the same physical connection.
        /// </summary>
        /// <param name="connection">An open connection.</param>
        /// <param name="async">When true, uses ExecuteScalarAsync; otherwise uses ExecuteScalar.</param>
        /// <returns>The value of @@SPID for the current session.</returns>
        private static async Task<int> GetSpid(SqlConnection connection, bool async)
        {
            using SqlCommand command = new SqlCommand("SELECT @@SPID;", connection);
            object spid = async ? await command.ExecuteScalarAsync() : command.ExecuteScalar();
            return Convert.ToInt32(spid);
        }

        /// <summary>
        /// Reads the isolation level currently in effect for the session, as reported by
        /// sys.dm_exec_sessions rather than by driver state, so the assertion reflects the server.
        /// </summary>
        /// <param name="connection">An open connection.</param>
        /// <param name="async">When true, uses ExecuteScalarAsync; otherwise uses ExecuteScalar.</param>
        /// <param name="transaction">
        /// Transaction to run the query under. Required when the connection has an active
        /// SqlTransaction, because SqlCommand rejects a command that omits it.
        /// </param>
        /// <returns>The session isolation level name, for example "ReadCommitted".</returns>
        private static async Task<string> GetIso(
            SqlConnection connection,
            bool async,
            SqlTransaction transaction = null)
        {
            using SqlCommand command = new SqlCommand(GetIsoSql, connection, transaction);
            object level = async ? await command.ExecuteScalarAsync() : command.ExecuteScalar();
            return (string)level;
        }

        /// <summary>
        /// Verifies that a Serializable SqlTransaction does not leave the session at Serializable
        /// once the connection has been returned to the pool and handed out again.
        /// </summary>
        /// <param name="async">When true, exercises the asynchronous API surface.</param>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task SqlTransaction_SerializableDoesNotLeakAcrossPool(bool async)
        {
            string cs = BuildPooledConnString($"IsoLeakTest-SqlTx-{async}");
            int spid1;
            using (SqlConnection c = new SqlConnection(cs))
            {
                await OpenConnection(c, async);
                spid1 = await GetSpid(c, async);
                using SqlTransaction tx = c.BeginTransaction(IsolationLevel.Serializable);
                Assert.Equal("Serializable", await GetIso(c, async, tx));
                tx.Rollback();
            }

            using (SqlConnection c = new SqlConnection(cs))
            {
                await OpenConnection(c, async);
                Assert.Equal(spid1, await GetSpid(c, async)); // pool reuse
                Assert.Equal("ReadCommitted", await GetIso(c, async));
            }
        }

        /// <summary>
        /// Verifies that a Serializable TransactionScope does not leave the session at Serializable
        /// once the scope has completed and the connection has been vended again.
        /// </summary>
        /// <param name="async">When true, exercises the asynchronous API surface.</param>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task TransactionScope_SerializableDoesNotLeakAcrossPool(bool async)
        {
            string cs = BuildPooledConnString($"IsoLeakTest-TxScope-{async}");
            int spid1;
            using (var scope = new TransactionScope(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.Serializable },
                TransactionScopeAsyncFlowOption.Enabled))
            using (SqlConnection c = new SqlConnection(cs))
            {
                await OpenConnection(c, async);
                spid1 = await GetSpid(c, async);
                Assert.Equal("Serializable", await GetIso(c, async));
                scope.Complete();
            }

            using (SqlConnection c = new SqlConnection(cs))
            {
                await OpenConnection(c, async);
                Assert.Equal(spid1, await GetSpid(c, async));
                Assert.Equal("ReadCommitted", await GetIso(c, async));
            }
        }

        /// <summary>
        /// Regression guard for the interaction with #146: the reset must not run while the
        /// connection is still enlisted, so a second Open inside the same TransactionScope must still
        /// observe Serializable.
        /// </summary>
        /// <remarks>
        /// The transacted pool hands the same physical connection to every Open within a scope, so
        /// scrubbing the isolation level on the return path would silently downgrade the transaction
        /// for the connections that follow. This test fails without the enlistment gate.
        /// <para>
        /// Restricted to on-prem SQL Server. On Azure SQL DB the second Open observes ReadCommitted
        /// regardless of this fix, because Azure resets the session isolation level inside
        /// sp_reset_connection_keep_transaction. That is issue #146 itself, addressed separately by
        /// PR #4335, so asserting it here would report a pre-existing unrelated bug rather than a
        /// regression in this change.
        /// </para>
        /// </remarks>
        /// <param name="async">When true, exercises the asynchronous API surface.</param>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task TransactionScope_SecondConnectionInSameScopeKeepsIsolationLevel(bool async)
        {
            string cs = BuildPooledConnString($"IsoLeakTest-TxScopeReuse-{async}");
            try
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.RequiresNew,
                    new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.Serializable },
                    TransactionScopeAsyncFlowOption.Enabled))
                {
                    int spid1;
                    using (SqlConnection c = new SqlConnection(cs))
                    {
                        await OpenConnection(c, async);
                        spid1 = await GetSpid(c, async);
                        Assert.Equal("Serializable", await GetIso(c, async));
                    }

                    // Same scope, connection returned to the transacted pool and vended again.
                    using (SqlConnection c = new SqlConnection(cs))
                    {
                        await OpenConnection(c, async);
                        Assert.Equal(spid1, await GetSpid(c, async));
                        Assert.Equal("Serializable", await GetIso(c, async));
                    }

                    scope.Complete();
                }
            }
            finally
            {
                SqlConnection.ClearAllPools();
            }
        }
    }
}