// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;
using IsolationLevel = System.Data.IsolationLevel;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies the opt-in behavior that prevents an elevated session isolation level from
    /// surviving a trip through the connection pool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// sp_reset_connection does not reset the session isolation level, so before this fix a
    /// connection returned to the pool after a Serializable SqlTransaction or TransactionScope kept
    /// that level, and the next caller to be handed the same physical connection silently inherited
    /// it. When <c>EnableTransactionIsolationLevelReset</c> is enabled, the driver resets the
    /// session to READ COMMITTED when the connection is taken back out of the pool.
    /// </para>
    /// <para>
    /// Every test pins MaxPoolSize to 1 and asserts on @@SPID so that pool reuse is proven rather
    /// than assumed; without the SPID assertion a fresh connection would satisfy the isolation
    /// assertion for the wrong reason. Each case runs over both the synchronous and the asynchronous
    /// API, because the reset is performed during connection activation, which Open and OpenAsync
    /// both reach.
    /// </para>
    /// <para>
    /// Azure Synapse is excluded throughout: dedicated pools reject the reset statement, and
    /// their user-database session DMV does not support the observations asserted here.
    /// Dedicated and serverless behavior require separate coverage. AreConnStringsSetup and
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
        /// <param name="async">Whether to execute the query asynchronously.</param>
        /// <returns>The value of @@SPID for the current session.</returns>
        private static async Task<int> GetSpid(SqlConnection connection, bool async)
        {
            using SqlCommand command = new SqlCommand("SELECT @@SPID;", connection);
            return Convert.ToInt32(async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
        }

        /// <summary>
        /// Reads the isolation level currently in effect for the session, as reported by
        /// sys.dm_exec_sessions rather than by driver state, so the assertion reflects the server.
        /// </summary>
        /// <param name="connection">An open connection.</param>
        /// <param name="async">Whether to execute the query asynchronously.</param>
        /// <param name="transaction">
        /// Transaction to run the query under. Required when the connection has an active
        /// SqlTransaction, because SqlCommand rejects a command that omits it.
        /// </param>
        /// <returns>The session isolation level name, for example "ReadCommitted".</returns>
        private static async Task<string> GetIso(SqlConnection connection, bool async, SqlTransaction transaction = null)
        {
            using SqlCommand command = new SqlCommand(GetIsoSql, connection, transaction);
            return (string)(async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
        }

        /// <summary>
        /// Verifies that a non-default SqlTransaction isolation level does not survive after the
        /// connection has been returned to the pool and handed out again.
        /// </summary>
        /// <param name="async">When true, exercises the asynchronous API surface.</param>
        /// <param name="isolationLevel">The non-default isolation level applied to the session.</param>
        /// <param name="expectedName">The server-reported name of the applied isolation level.</param>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, IsolationLevel.ReadUncommitted, "ReadUncommitted")]
        [InlineData(true, IsolationLevel.ReadUncommitted, "ReadUncommitted")]
        [InlineData(false, IsolationLevel.RepeatableRead, "RepeatableRead")]
        [InlineData(true, IsolationLevel.RepeatableRead, "RepeatableRead")]
        [InlineData(false, IsolationLevel.Serializable, "Serializable")]
        [InlineData(true, IsolationLevel.Serializable, "Serializable")]
        public static async Task SqlTransaction_NonDefaultIsolationLevelDoesNotLeakAcrossPool(
            bool async,
            IsolationLevel isolationLevel,
            string expectedName)
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.EnableTransactionIsolationLevelReset = true;

            string cs = BuildPooledConnString($"IsoLeakTest-SqlTx-{async}-{isolationLevel}");
            int spid1;
            using (SqlConnection c = new SqlConnection(cs))
            {
                await OpenConnection(c, async);
                spid1 = await GetSpid(c, async);
                using SqlTransaction tx = c.BeginTransaction(isolationLevel);
                Assert.Equal(expectedName, await GetIso(c, async, tx));
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
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.EnableTransactionIsolationLevelReset = true;

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
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.EnableTransactionIsolationLevelReset = true;

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

        /// <summary>
        /// Verifies that the default-off switch preserves the shipped behavior for compatibility:
        /// a changed isolation level remains on a pooled session until the reset is explicitly
        /// enabled.
        /// </summary>
        /// <param name="async">When true, exercises the asynchronous API surface.</param>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task DefaultBehavior_PreservesIsolationLevelAcrossPool(bool async)
        {
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.EnableTransactionIsolationLevelReset = false;

            string cs = BuildPooledConnString($"IsoLeakTest-Default-{async}");
            try
            {
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
                    Assert.Equal(spid1, await GetSpid(c, async));
                    Assert.Equal("Serializable", await GetIso(c, async));
                }

            }
            finally
            {
                SqlConnection.ClearAllPools();
            }
        }

        /// <summary>
        /// Exercises activation after detaching the enlistment of a real delegated transaction.
        /// The live server transaction must retain its isolation, and the dirty session must still
        /// be reset when it is reused after that transaction completes.
        /// </summary>
        /// <param name="async">Whether to open connections and execute queries asynchronously.</param>
        /// <param name="poolV2">Whether to use the channel pool.</param>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public static async Task DelegatedRoot_DetachedEnlistmentDefersResetUntilCompletion(bool async, bool poolV2)
        {
            using LocalAppContextSwitchesHelper switches = new();
            switches.EnableTransactionIsolationLevelReset = true;
            switches.UseConnectionPoolV2 = poolV2;
            string cs = BuildPooledConnString($"IsoLeakTest-DetachedRoot-{async}-{poolV2}");
            using SqlConnection connection = new(cs);
            try
            {
                int spid;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                using (var scope = new TransactionScope(
                    TransactionScopeOption.RequiresNew,
                    new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.Serializable },
                    TransactionScopeAsyncFlowOption.Enabled))
                {
                    await OpenConnection(connection, async);
                    spid = await GetSpid(connection, async);
                    object inner = typeof(SqlConnection).GetProperty("InnerConnection", flags).GetValue(connection);
                    Type type = inner.GetType();
                    PropertyInfo root = type.GetProperty("IsTransactionRoot", flags);
                    PropertyInfo enlisted = type.GetProperty("EnlistedTransaction", flags);
                    Assert.True((bool)root.GetValue(inner));
                    Assert.NotNull(enlisted.GetValue(inner));

                    // Deterministically enter the root-only window through the same detach method
                    // used by DetachCurrentTransactionIfEnded, without racing a completion callback.
                    type.GetMethod("DetachTransaction", flags).Invoke(inner, new object[] { Transaction.Current, true });
                    Assert.Null(enlisted.GetValue(inner));
                    Assert.True((bool)root.GetValue(inner));
                    type.GetMethod("Activate", flags, null, new[] { typeof(Transaction) }, null)
                        .Invoke(inner, new object[] { null });

                    using SqlCommand count = new("SELECT @@TRANCOUNT;", connection);
                    Assert.Equal(1, Convert.ToInt32(async ? await count.ExecuteScalarAsync() : count.ExecuteScalar()));
                    Assert.Equal("Serializable", await GetIso(connection, async));
                    scope.Complete();
                }
                connection.Close();
                await OpenConnection(connection, async);
                Assert.Equal(spid, await GetSpid(connection, async));
                Assert.Equal("ReadCommitted", await GetIso(connection, async));
            }
            finally
            {
                SqlConnection.ClearPool(connection);
            }
        }
    }
}