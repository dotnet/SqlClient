// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;
using DataIsolationLevel = System.Data.IsolationLevel;
using TransactionsIsolationLevel = System.Transactions.IsolationLevel;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Serializes the isolation contract because its tests mutate cached pool-selection switches.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class IsolationLevelContractTestCollection
    {
        public const string Name = "IsolationLevelContractTests";
    }

    /// <summary>
    /// Verifies the isolation-level contract across logical connections, transactions, and pools.
    /// </summary>
    [Collection(IsolationLevelContractTestCollection.Name)]
    [Trait("Set", "3")]
    public sealed class IsolationLevelContractTest
    {
        private const string SessionOptionsSql =
            "DBCC USEROPTIONS WITH NO_INFOMSGS;";

        private static readonly DataIsolationLevel[] s_nonSnapshotLevels =
        {
            DataIsolationLevel.ReadUncommitted,
            DataIsolationLevel.ReadCommitted,
            DataIsolationLevel.RepeatableRead,
            DataIsolationLevel.Serializable
        };

        private static readonly DataIsolationLevel[] s_nonDefaultNonSnapshotLevels =
        {
            DataIsolationLevel.ReadUncommitted,
            DataIsolationLevel.RepeatableRead,
            DataIsolationLevel.Serializable
        };

        private static readonly CompletionMode[] s_completionModes =
        {
            CompletionMode.Commit,
            CompletionMode.Rollback,
            CompletionMode.Dispose
        };

        private static readonly Lazy<bool> s_snapshotIsolationEnabled =
            new(ProbeSnapshotIsolation);

        /// <summary>
        /// Supplies sync/async and MARS combinations for tests that do not use pooling.
        /// </summary>
        public static TheoryData<bool, bool> ConnectionModes =>
            new()
            {
                { false, false },
                { false, true },
                { true, false },
                { true, true }
            };

        /// <summary>
        /// Supplies sync/async, MARS, and pool implementation combinations.
        /// </summary>
        public static TheoryData<bool, bool, bool> PooledModes =>
            new()
            {
                { false, false, false },
                { false, false, true },
                { false, true, false },
                { false, true, true },
                { true, false, false },
                { true, false, true },
                { true, true, false },
                { true, true, true }
            };

        /// <summary>
        /// Supplies both pool implementations for endpoint-specific tests.
        /// </summary>
        public static TheoryData<bool> PoolVersions =>
            new()
            {
                false,
                true
            };

        /// <summary>
        /// Reports whether the configured database can run Snapshot transaction cases.
        /// </summary>
        public static bool IsSnapshotIsolationEnabled() =>
            DataTestUtility.AreConnStringsSetup() &&
            DataTestUtility.IsNotAzureSynapse() &&
            s_snapshotIsolationEnabled.Value;

        /// <summary>
        /// Reports whether the configured endpoint is an Azure Synapse dedicated SQL pool.
        /// </summary>
        public static bool IsAzureSynapseConfigured() =>
            DataTestUtility.AreConnStringsSetup() &&
            DataTestUtility.IsAzureSynapse;

        /// <summary>
        /// Reports whether the proposed legacy isolation switch exists and can be tested.
        /// </summary>
        public static bool IsLegacyIsolationSwitchTestAvailable() =>
            DataTestUtility.AreConnStringsSetup() &&
            DataTestUtility.IsNotAzureServer() &&
            DataTestUtility.IsNotAzureSynapse() &&
            GetLegacyIsolationSwitchField() != null;

        /// <summary>
        /// Reports whether driver-managed isolation reset tracking exists.
        /// </summary>
        public static bool IsIsolationResetTrackingAvailable() =>
            GetIsolationDirtyField() != null;

        /// <summary>
        /// Reports whether the configured Synapse endpoint can exercise isolation reset tracking.
        /// </summary>
        public static bool IsSynapseIsolationResetTestAvailable() =>
            IsAzureSynapseConfigured() &&
            IsIsolationResetTrackingAvailable();

        /// <summary>
        /// Verifies that a local transaction applies each requested standard isolation level and
        /// that commit, rollback, or disposal leaves that level active while the logical connection
        /// remains open.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task LocalTransactionLevelPersistsAfterCompletionWhileConnectionStaysOpen(
            bool async,
            bool mars)
        {
            foreach (DataIsolationLevel level in s_nonSnapshotLevels)
            {
                foreach (CompletionMode completion in s_completionModes)
                {
                    string cs = NonPooled(
                        $"LocalSameOpen-{level}-{completion}",
                        mars);

                    using SqlConnection connection = new(cs);
                    await Open(connection, async);

                    SqlTransaction transaction = connection.BeginTransaction(level);
                    try
                    {
                        Assert.Equal(
                            level,
                            await SessionLevel(connection, async, transaction));
                        Complete(transaction, completion);
                    }
                    finally
                    {
                        transaction.Dispose();
                    }

                    Assert.Equal(level, await SessionLevel(connection, async));
                }
            }
        }

        /// <summary>
        /// Verifies that a Snapshot local transaction applies Snapshot and leaves it active after
        /// commit, rollback, or disposal while the logical connection remains open.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(ConnectionModes))]
        public async Task SnapshotPersistsAfterLocalTransactionCompletionWhileConnectionStaysOpen(
            bool async,
            bool mars)
        {
            foreach (CompletionMode completion in s_completionModes)
            {
                string cs = NonPooled(
                    $"SnapshotSameOpen-{completion}",
                    mars);
                using SqlConnection connection = new(cs);
                await Open(connection, async);

                SqlTransaction transaction =
                    connection.BeginTransaction(DataIsolationLevel.Snapshot);
                try
                {
                    Assert.Equal(
                        DataIsolationLevel.Snapshot,
                        await SessionLevel(connection, async, transaction));
                    Complete(transaction, completion);
                }
                finally
                {
                    transaction.Dispose();
                }

                Assert.Equal(
                    DataIsolationLevel.Snapshot,
                    await SessionLevel(connection, async));
            }
        }

        /// <summary>
        /// Verifies that parameterless and Unspecified local transactions select SqlClient's
        /// ReadCommitted default instead of inheriting a prior session-level RepeatableRead setting.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task UnspecifiedLocalTransactionUsesReadCommittedDefault(
            bool async,
            bool mars)
        {
            foreach (bool parameterless in new[] { false, true })
            {
                using SqlConnection connection =
                    new(NonPooled(
                        $"LocalUnspecified-{parameterless}",
                        mars));
                await Open(connection, async);
                await SetLevel(
                    connection,
                    DataIsolationLevel.RepeatableRead,
                    async);

                using SqlTransaction transaction = parameterless
                    ? connection.BeginTransaction()
                    : connection.BeginTransaction(DataIsolationLevel.Unspecified);

                Assert.Equal(
                    DataIsolationLevel.ReadCommitted,
                    await SessionLevel(connection, async, transaction));
                transaction.Rollback();
            }
        }

        /// <summary>
        /// Verifies that SET TRANSACTION ISOLATION LEVEL can legally change an active local
        /// transaction from ReadCommitted to Serializable for subsequent statements.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task SetIsolationLevelInsideLocalTransactionChangesSubsequentLevel(
            bool async,
            bool mars)
        {
            using SqlConnection connection =
                new(NonPooled("LocalMidTransactionSet", mars));
            await Open(connection, async);

            using SqlTransaction transaction =
                connection.BeginTransaction(DataIsolationLevel.ReadCommitted);

            await SetLevel(
                connection,
                DataIsolationLevel.Serializable,
                async,
                transaction);

            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(connection, async, transaction));
            transaction.Rollback();
        }

        /// <summary>
        /// Verifies the documented SQL Server rule that a transaction started at Snapshot can
        /// change to ReadCommitted and then return to Snapshot.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(ConnectionModes))]
        public async Task SnapshotStartedTransactionCanLeaveAndReturnToSnapshot(
            bool async,
            bool mars)
        {
            using SqlConnection connection =
                new(NonPooled("SnapshotLeaveAndReturn", mars));
            await Open(connection, async);
            using Table table = new(connection, "IsolationTransition", "(Value int NOT NULL)");
            await ExecuteNonQuery(
                connection,
                $"INSERT {table.Name} VALUES (1);",
                async);

            using SqlTransaction transaction =
                connection.BeginTransaction(DataIsolationLevel.Snapshot);

            await ExecuteScalar(
                connection,
                $"SELECT Value FROM {table.Name};",
                async,
                transaction);
            await SetLevel(
                connection,
                DataIsolationLevel.ReadCommitted,
                async,
                transaction);
            await SetLevel(
                connection,
                DataIsolationLevel.Snapshot,
                async,
                transaction);

            Assert.Equal(
                DataIsolationLevel.Snapshot,
                await SessionLevel(connection, async, transaction));
            transaction.Rollback();
        }

        /// <summary>
        /// Verifies that changing a transaction that started at ReadCommitted to Snapshot causes
        /// SQL error 3951 on the next data access and aborts the transaction.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(ConnectionModes))]
        public async Task NonSnapshotStartedTransactionAbortsWhenChangedToSnapshot(
            bool async,
            bool mars)
        {
            using SqlConnection connection =
                new(NonPooled("InvalidSnapshotTransition", mars));
            await Open(connection, async);
            using Table table = new(connection, "IsolationTransition", "(Value int NOT NULL)");
            await ExecuteNonQuery(
                connection,
                $"INSERT {table.Name} VALUES (1);",
                async);

            using SqlTransaction transaction =
                connection.BeginTransaction(DataIsolationLevel.ReadCommitted);

            await ExecuteScalar(
                connection,
                $"SELECT Value FROM {table.Name};",
                async,
                transaction);
            await SetLevel(
                connection,
                DataIsolationLevel.Snapshot,
                async,
                transaction);

            SqlException error;
            if (async)
            {
                error = await Assert.ThrowsAsync<SqlException>(
                    () => ExecuteScalar(
                        connection,
                        $"SELECT Value FROM {table.Name};",
                        async: true,
                        transaction));
            }
            else
            {
                error = Assert.Throws<SqlException>(
                    () => ExecuteScalar(
                        connection,
                        $"SELECT Value FROM {table.Name};",
                        async: false,
                        transaction).GetAwaiter().GetResult());
            }

            Assert.Equal(3951, error.Number);
            Assert.Equal(
                0,
                Convert.ToInt32(
                    await ExecuteScalar(
                        connection,
                        "SELECT XACT_STATE();",
                        async,
                        transaction)));
        }

        /// <summary>
        /// Verifies that completing an ambient Serializable transaction does not reset the
        /// connection-wide isolation level while the same logical connection remains open.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task AmbientCompletionDoesNotResetStillOpenConnection(
            bool async,
            bool mars)
        {
            string cs = Pooled("AmbientSameOpen", mars);
            using SqlConnection connection = new(cs);

            using (TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Serializable))
            {
                await Open(connection, async);
                Assert.Equal(
                    DataIsolationLevel.Serializable,
                    await SessionLevel(connection, async));
                scope.Complete();
            }

            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(connection, async));
        }

        /// <summary>
        /// Verifies that non-default isolation selected by a completed local transaction is reset
        /// to ReadCommitted before the same physical connection reaches an unrelated pooled consumer.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task CompletedLocalTransactionDoesNotLeakIsolationAcrossPool(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (DataIsolationLevel level in s_nonDefaultNonSnapshotLevels)
            {
                foreach (CompletionMode completion in s_completionModes)
                {
                    string cs = Pooled(
                        $"LocalPoolBoundary-{level}-{completion}",
                        mars);
                    Guid originalConnectionId;

                    using (SqlConnection first = new(cs))
                    {
                        await Open(first, async);
                        originalConnectionId = PhysicalConnectionId(first);
                        SqlTransaction transaction = first.BeginTransaction(level);
                        try
                        {
                            Assert.Equal(
                                level,
                                await SessionLevel(first, async, transaction));
                            Complete(transaction, completion);
                        }
                        finally
                        {
                            transaction.Dispose();
                        }
                    }

                    using SqlConnection next = new(cs);
                    await Open(next, async);
                    Assert.Equal(
                        originalConnectionId,
                        PhysicalConnectionId(next));
                    Assert.Equal(
                        DataIsolationLevel.ReadCommitted,
                        await SessionLevel(next, async));
                }
            }
        }

        /// <summary>
        /// Verifies that Snapshot selected by a completed local transaction is reset to
        /// ReadCommitted before the same physical connection reaches an unrelated pooled consumer.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(PooledModes))]
        public async Task CompletedSnapshotTransactionDoesNotLeakIsolationAcrossPool(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (CompletionMode completion in s_completionModes)
            {
                string cs = Pooled(
                    $"SnapshotPoolBoundary-{completion}",
                    mars);
                Guid originalConnectionId;

                using (SqlConnection first = new(cs))
                {
                    await Open(first, async);
                    originalConnectionId = PhysicalConnectionId(first);
                    SqlTransaction transaction =
                        first.BeginTransaction(DataIsolationLevel.Snapshot);
                    try
                    {
                        Complete(transaction, completion);
                    }
                    finally
                    {
                        transaction.Dispose();
                    }
                }

                using SqlConnection next = new(cs);
                await Open(next, async);
                Assert.Equal(
                    originalConnectionId,
                    PhysicalConnectionId(next));
                Assert.Equal(
                    DataIsolationLevel.ReadCommitted,
                    await SessionLevel(next, async));
            }
        }

        /// <summary>
        /// Verifies that a completed or rolled-back ambient transaction cannot leak its
        /// non-default isolation level into an unrelated consumer of the same physical connection.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task CompletedAmbientTransactionDoesNotLeakIsolationAcrossPool(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (DataIsolationLevel level in s_nonDefaultNonSnapshotLevels)
            {
                foreach (bool completeScope in new[] { false, true })
                {
                    string cs = Pooled(
                        $"AmbientPoolBoundary-{level}-{completeScope}",
                        mars);
                    Guid originalConnectionId;

                    using (TransactionScope scope = CreateScope(
                        ToTransactionsLevel(level)))
                    {
                        using SqlConnection first = new(cs);
                        await Open(first, async);
                        originalConnectionId = PhysicalConnectionId(first);
                        Assert.Equal(level, await SessionLevel(first, async));
                        if (completeScope)
                        {
                            scope.Complete();
                        }
                    }

                    using SqlConnection next = new(cs);
                    await Open(next, async);
                    Assert.Equal(
                        originalConnectionId,
                        PhysicalConnectionId(next));
                    Assert.Equal(
                        DataIsolationLevel.ReadCommitted,
                        await SessionLevel(next, async));
                }
            }
        }

        /// <summary>
        /// Verifies that a completed or rolled-back Snapshot scope cannot leak Snapshot into an
        /// unrelated consumer of the same physical connection.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(PooledModes))]
        public async Task CompletedSnapshotScopeDoesNotLeakIsolationAcrossPool(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (bool completeScope in new[] { false, true })
            {
                string cs = Pooled(
                    $"SnapshotScopeBoundary-{completeScope}",
                    mars);
                Guid originalConnectionId;

                using (TransactionScope scope = CreateScope(
                    TransactionsIsolationLevel.Snapshot))
                {
                    using SqlConnection first = new(cs);
                    await Open(first, async);
                    originalConnectionId = PhysicalConnectionId(first);
                    if (completeScope)
                    {
                        scope.Complete();
                    }
                }

                using SqlConnection next = new(cs);
                await Open(next, async);
                Assert.Equal(
                    originalConnectionId,
                    PhysicalConnectionId(next));
                Assert.Equal(
                    DataIsolationLevel.ReadCommitted,
                    await SessionLevel(next, async));
            }
        }

        /// <summary>
        /// Verifies that isolation selected through top-level command text follows the server's
        /// pooled-reset behavior: ReadCommitted on reset-clearing servers or Serializable on
        /// reset-preserving servers.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task TopLevelSetAcrossPoolUsesServerResetBehavior(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("TopLevelSetBoundary", mars);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                await SetLevel(
                    first,
                    DataIsolationLevel.Serializable,
                    async);
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            AssertServerDependentDirectSqlLevel(
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that command text which changes a ReadCommitted local transaction to
        /// Serializable remains outside driver tracking and follows the server's pooled-reset result.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task SetInsideReadCommittedLocalTransactionUsesServerResetBehavior(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("LocalDirectSetBoundary", mars);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                using SqlTransaction transaction =
                    first.BeginTransaction(DataIsolationLevel.ReadCommitted);
                await SetLevel(
                    first,
                    DataIsolationLevel.Serializable,
                    async,
                    transaction);
                transaction.Commit();
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            AssertServerDependentDirectSqlLevel(
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that closing a connection with an active non-default local transaction rolls
        /// back the transaction and resets the reused physical connection to ReadCommitted.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task ClosingActiveLocalTransactionRollsBackAndResetsIsolation(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (DataIsolationLevel level in s_nonDefaultNonSnapshotLevels)
            {
                string cs = Pooled($"ActiveLocalClose-{level}", mars);
                Guid originalConnectionId;
                using SqlConnection setup = new(
                    NonPooled($"ActiveLocalCloseSetup-{level}", mars));
                await Open(setup, async);
                using Table table = new(
                    setup,
                    "ActiveLocalRollback",
                    "(Value int NOT NULL)");

                using (SqlConnection first = new(cs))
                {
                    await Open(first, async);
                    originalConnectionId = PhysicalConnectionId(first);
                    using SqlTransaction transaction =
                        first.BeginTransaction(level);
                    await ExecuteNonQuery(
                        first,
                        $"INSERT {table.Name} VALUES (1);",
                        async,
                        transaction);
                    first.Close();
                }

                using SqlConnection next = new(cs);
                await Open(next, async);
                Assert.Equal(
                    originalConnectionId,
                    PhysicalConnectionId(next));
                Assert.Equal(
                    DataIsolationLevel.ReadCommitted,
                    await SessionLevel(next, async));
                Assert.Equal(
                    0,
                    Convert.ToInt32(
                        await ExecuteScalar(
                            next,
                            $"SELECT COUNT(*) FROM {table.Name};",
                            async)));
            }
        }

        /// <summary>
        /// Verifies that closing a connection with an active Snapshot transaction rolls back the
        /// transaction and resets the reused physical connection to ReadCommitted.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(PooledModes))]
        public async Task ClosingActiveSnapshotTransactionRollsBackAndResetsIsolation(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("ActiveSnapshotClose", mars);
            Guid originalConnectionId;
            using SqlConnection setup = new(
                NonPooled("ActiveSnapshotCloseSetup", mars));
            await Open(setup, async);
            using Table table = new(
                setup,
                "ActiveSnapshotRollback",
                "(Value int NOT NULL)");

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                using SqlTransaction transaction =
                    first.BeginTransaction(DataIsolationLevel.Snapshot);
                await ExecuteNonQuery(
                    first,
                    $"INSERT {table.Name} VALUES (1);",
                    async,
                    transaction);
                first.Close();
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(next, async));
            Assert.Equal(
                0,
                Convert.ToInt32(
                    await ExecuteScalar(
                        next,
                        $"SELECT COUNT(*) FROM {table.Name};",
                        async)));
        }

        /// <summary>
        /// Verifies that Close followed by Open on the same SqlConnection object creates a new
        /// logical session and resets completed non-default transaction state to ReadCommitted.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task ReopeningSameConnectionObjectResetsIsolation(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (DataIsolationLevel level in s_nonDefaultNonSnapshotLevels)
            {
                foreach (CompletionMode completion in s_completionModes)
                {
                    string cs = Pooled(
                        $"SameObjectReopen-{level}-{completion}",
                        mars);
                    using SqlConnection connection = new(cs);
                    await Open(connection, async);
                    Guid originalConnectionId =
                        PhysicalConnectionId(connection);

                    SqlTransaction transaction =
                        connection.BeginTransaction(level);
                    try
                    {
                        Complete(transaction, completion);
                    }
                    finally
                    {
                        transaction.Dispose();
                    }

                    connection.Close();
                    await Open(connection, async);
                    Assert.Equal(
                        originalConnectionId,
                        PhysicalConnectionId(connection));
                    Assert.Equal(
                        DataIsolationLevel.ReadCommitted,
                        await SessionLevel(connection, async));
                }
            }
        }

        /// <summary>
        /// Verifies that Close followed by Open on the same SqlConnection object resets completed
        /// Snapshot transaction state to ReadCommitted.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(PooledModes))]
        public async Task ReopeningSameConnectionObjectResetsSnapshotIsolation(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (CompletionMode completion in s_completionModes)
            {
                string cs = Pooled(
                    $"SameObjectSnapshot-{completion}",
                    mars);
                using SqlConnection connection = new(cs);
                await Open(connection, async);
                Guid originalConnectionId =
                    PhysicalConnectionId(connection);

                SqlTransaction transaction =
                    connection.BeginTransaction(DataIsolationLevel.Snapshot);
                try
                {
                    Complete(transaction, completion);
                }
                finally
                {
                    transaction.Dispose();
                }

                connection.Close();
                await Open(connection, async);
                Assert.Equal(
                    originalConnectionId,
                    PhysicalConnectionId(connection));
                Assert.Equal(
                    DataIsolationLevel.ReadCommitted,
                    await SessionLevel(connection, async));
            }
        }

        /// <summary>
        /// Verifies that ambient completion preserves Serializable on a still-open connection, but
        /// the later logical close resets the reused physical connection for an unrelated consumer.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task CloseAfterAmbientCompletionResetsNextPooledConsumer(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("AmbientCloseBoundary", mars);
            using SqlConnection connection = new(cs);
            Guid originalConnectionId;

            using (TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Serializable))
            {
                await Open(connection, async);
                originalConnectionId = PhysicalConnectionId(connection);
                scope.Complete();
            }

            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(connection, async));
            connection.Close();

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that a TransactionScope created without explicit options uses the documented
        /// Serializable default when SqlClient enlists.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task DefaultTransactionScopeUsesSerializable(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("DefaultAmbientLevel", mars);

            using TransactionScope scope = new(
                TransactionScopeOption.RequiresNew,
                TransactionScopeAsyncFlowOption.Enabled);
            using SqlConnection connection = new(cs);
            await Open(connection, async);

            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(connection, async));
            scope.Complete();
        }

        /// <summary>
        /// Verifies that closing and reopening within one live ambient transaction reuses the same
        /// physical connection and observes the transaction's declared standard isolation level.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task LiveAmbientRecheckoutRestoresDeclaredIsolationLevel(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (DataIsolationLevel level in s_nonSnapshotLevels)
            {
                string cs = Pooled(
                    $"LiveAmbientRecheckout-{level}",
                    mars);
                using TransactionScope scope = CreateScope(
                    ToTransactionsLevel(level));
                Guid originalConnectionId;

                using (SqlConnection first = new(cs))
                {
                    await Open(first, async);
                    originalConnectionId = PhysicalConnectionId(first);
                    Assert.Equal(level, await SessionLevel(first, async));
                }

                using (SqlConnection second = new(cs))
                {
                    await Open(second, async);
                    Assert.Equal(
                        originalConnectionId,
                        PhysicalConnectionId(second));
                    Assert.Equal(level, await SessionLevel(second, async));
                }

                scope.Complete();
            }
        }

        /// <summary>
        /// Verifies that closing and reopening within one live Snapshot transaction reuses the
        /// same physical connection and continues to observe Snapshot.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(PooledModes))]
        public async Task LiveSnapshotRecheckoutRestoresSnapshot(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("LiveSnapshotRecheckout", mars);
            using TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Snapshot);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                Assert.Equal(
                    DataIsolationLevel.Snapshot,
                    await SessionLevel(first, async));
            }

            using (SqlConnection second = new(cs))
            {
                await Open(second, async);
                Assert.Equal(
                    originalConnectionId,
                    PhysicalConnectionId(second));
                Assert.Equal(
                    DataIsolationLevel.Snapshot,
                    await SessionLevel(second, async));
            }

            scope.Complete();
        }

        /// <summary>
        /// Verifies that a Snapshot-started ambient transaction is reasserted as Snapshot when the
        /// same physical connection is checked out after a session-local ReadCommitted override.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSnapshotIsolationEnabled))]
        [MemberData(nameof(PooledModes))]
        public async Task SnapshotRecheckoutRestoresSnapshotAfterSessionOverride(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("SnapshotOverrideRecheckout", mars);
            using TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Snapshot);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                Assert.Equal(
                    DataIsolationLevel.Snapshot,
                    await SessionLevel(first, async));
                await SetLevel(
                    first,
                    DataIsolationLevel.ReadCommitted,
                    async);
                Assert.Equal(
                    DataIsolationLevel.ReadCommitted,
                    await SessionLevel(first, async));
            }

            using (SqlConnection second = new(cs))
            {
                await Open(second, async);
                Assert.Equal(
                    originalConnectionId,
                    PhysicalConnectionId(second));
                Assert.Equal(
                    DataIsolationLevel.Snapshot,
                    await SessionLevel(second, async));
            }

            scope.Complete();
        }

        /// <summary>
        /// Verifies that a session-local command-text override lasts until logical close, then
        /// recheckout restores the live ambient transaction's declared non-Snapshot level.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task LiveAmbientRecheckoutRestoresDeclaredLevelAfterSessionOverride(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

            foreach (DataIsolationLevel ambientLevel in s_nonSnapshotLevels)
            {
                DataIsolationLevel overrideLevel =
                    ambientLevel == DataIsolationLevel.ReadCommitted
                        ? DataIsolationLevel.Serializable
                        : DataIsolationLevel.ReadCommitted;

                string cs = Pooled(
                    $"AmbientOverrideRecheckout-{ambientLevel}",
                    mars);
                using TransactionScope scope = CreateScope(
                    ToTransactionsLevel(ambientLevel));
                Guid originalConnectionId;

                using (SqlConnection first = new(cs))
                {
                    await Open(first, async);
                    originalConnectionId = PhysicalConnectionId(first);
                    await SetLevel(first, overrideLevel, async);
                    Assert.Equal(
                        overrideLevel,
                        await SessionLevel(first, async));
                }

                using (SqlConnection second = new(cs))
                {
                    await Open(second, async);
                    Assert.Equal(
                        originalConnectionId,
                        PhysicalConnectionId(second));
                    Assert.Equal(
                        ambientLevel,
                        await SessionLevel(second, async));
                }

                scope.Complete();
            }
        }

        /// <summary>
        /// Verifies that a direct Serializable override after ReadCommitted ambient enlistment
        /// follows the server's normal pooled-reset behavior once the ambient transaction completes.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task DirectOverrideAfterAmbientCompletionUsesServerResetBehavior(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("AmbientDirectSetCompletion", mars);
            Guid originalConnectionId;

            using (TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.ReadCommitted))
            {
                using SqlConnection first = new(cs);
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                await SetLevel(
                    first,
                    DataIsolationLevel.Serializable,
                    async);
                scope.Complete();
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            AssertServerDependentDirectSqlLevel(
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that Enlist=false prevents automatic ambient enlistment and leaves a clean
        /// connection at ReadCommitted even while a Serializable transaction is ambient.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task EnlistFalseIgnoresAmbientTransaction(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled(
                "EnlistFalse",
                mars,
                enlist: false);

            using TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Serializable);
            using SqlConnection connection = new(cs);
            await Open(connection, async);

            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(connection, async));
            scope.Complete();
        }

        /// <summary>
        /// Verifies that creating an ambient transaction after SqlConnection.Open does not
        /// retroactively enlist the already-open connection or change its ReadCommitted level.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task OpenConnectionIsNotRetroactivelyAutoEnlisted(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            using SqlConnection connection =
                new(Pooled("OpenBeforeScope", mars));
            await Open(connection, async);

            using TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Serializable);

            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(connection, async));
        }

        /// <summary>
        /// Verifies that explicitly enlisting an open Enlist=false connection applies the
        /// CommittableTransaction's declared Serializable level.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task ManualEnlistmentAppliesTransactionIsolationLevel(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            using CommittableTransaction transaction = new(
                new TransactionOptions
                {
                    IsolationLevel = TransactionsIsolationLevel.Serializable
                });

            using SqlConnection connection =
                new(Pooled(
                    "ManualEnlistment",
                    mars,
                    enlist: false));
            await Open(connection, async);
            connection.EnlistTransaction(transaction);

            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(connection, async));
            transaction.Commit();
        }

        /// <summary>
        /// Verifies that TransactionScopeOption.Suppress removes the outer Serializable ambient
        /// transaction so a connection opened inside the suppressed region uses ReadCommitted.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task SuppressedScopeUsesNonTransactedReadCommittedConnection(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("SuppressedScope", mars);

            using TransactionScope outer = CreateScope(
                TransactionsIsolationLevel.Serializable);
            using (TransactionScope suppressed = new(
                TransactionScopeOption.Suppress,
                TransactionScopeAsyncFlowOption.Enabled))
            using (SqlConnection connection = new(cs))
            {
                await Open(connection, async);
                Assert.Equal(
                    DataIsolationLevel.ReadCommitted,
                    await SessionLevel(connection, async));
                suppressed.Complete();
            }

            outer.Complete();
        }

        /// <summary>
        /// Verifies that two simultaneously open connections both observe the Serializable
        /// ambient level when distributed transaction promotion is supported.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse),
            nameof(DataTestUtility.IsSupportingDistributedTransactions))]
        [MemberData(nameof(PooledModes))]
        public async Task ConcurrentEnlistedConnectionsObserveAmbientIsolationLevel(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
#if NET8_0_OR_GREATER
            TransactionManager.ImplicitDistributedTransactions = true;
#endif
            string cs = Pooled(
                "ConcurrentAmbientConnections",
                mars,
                maxPoolSize: 2);

            using TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Serializable);
            using SqlConnection first = new(cs);
            using SqlConnection second = new(cs);
            await Open(first, async);
            await Open(second, async);

            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(first, async));
            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(second, async));
            scope.Complete();
        }

        /// <summary>
        /// Verifies that Explicit Unbind keeps a connection attached to its completed ambient
        /// transaction until close, rejects intervening commands, and then resets isolation.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task ExplicitUnbindRemainsBoundUntilCloseThenResetsIsolation(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled(
                "ExplicitUnbind",
                mars,
                transactionBinding: "Explicit Unbind");
            using SqlConnection connection = new(cs);
            Guid originalConnectionId;

            using (TransactionScope scope = CreateScope(
                TransactionsIsolationLevel.Serializable))
            {
                await Open(connection, async);
                originalConnectionId = PhysicalConnectionId(connection);
                scope.Complete();
            }

            using (SqlCommand command = new("SELECT 1;", connection))
            {
                if (async)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => command.ExecuteScalarAsync());
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(
                        () => command.ExecuteScalar());
                }
            }

            connection.Close();

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that nested RequiresNew uses a different physical connection and transaction
        /// pool subdivision, then restores the outer transaction's original connection and level.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task RequiresNewUsesIndependentTransactionPoolSubdivision(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled(
                "NestedRequiresNew",
                mars,
                maxPoolSize: 2);
            Guid outerConnectionId;

            using (TransactionScope outer = CreateScope(
                TransactionsIsolationLevel.ReadCommitted))
            {
                using (SqlConnection outerFirst = new(cs))
                {
                    await Open(outerFirst, async);
                    outerConnectionId =
                        PhysicalConnectionId(outerFirst);
                }

                using (TransactionScope inner = CreateScope(
                    TransactionsIsolationLevel.Serializable))
                using (SqlConnection innerConnection = new(cs))
                {
                    await Open(innerConnection, async);
                    Assert.NotEqual(
                        outerConnectionId,
                        PhysicalConnectionId(innerConnection));
                    Assert.Equal(
                        DataIsolationLevel.Serializable,
                        await SessionLevel(innerConnection, async));
                    inner.Complete();
                }

                using (SqlConnection outerSecond = new(cs))
                {
                    await Open(outerSecond, async);
                    Assert.Equal(
                        outerConnectionId,
                        PhysicalConnectionId(outerSecond));
                    Assert.Equal(
                        DataIsolationLevel.ReadCommitted,
                        await SessionLevel(outerSecond, async));
                }

                outer.Complete();
            }
        }

        /// <summary>
        /// Verifies that Pooling=false creates a different physical connection whose isolation
        /// starts at ReadCommitted rather than inheriting a prior Serializable command-text setting.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task PoolingFalsePreventsSessionIsolationTransfer(
            bool async,
            bool mars)
        {
            string cs = NonPooled("PoolingDisabled", mars);
            Guid firstConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                firstConnectionId = PhysicalConnectionId(first);
                await SetLevel(
                    first,
                    DataIsolationLevel.Serializable,
                    async);
            }

            using SqlConnection second = new(cs);
            await Open(second, async);
            Assert.NotEqual(
                firstConnectionId,
                PhysicalConnectionId(second));
            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(second, async));
        }

        /// <summary>
        /// Verifies that System.Transactions rejects a nested Required scope whose Serializable
        /// level conflicts with the existing ReadCommitted ambient transaction.
        /// </summary>
        [Fact]
        public void NestedRequiredScopeCannotChangeAmbientIsolationLevel()
        {
            using TransactionScope outer = CreateScope(
                TransactionsIsolationLevel.ReadCommitted);

            Assert.Throws<ArgumentException>(
                () => new TransactionScope(
                    TransactionScopeOption.Required,
                    new TransactionOptions
                    {
                        IsolationLevel =
                            TransactionsIsolationLevel.Serializable
                    }));
        }

        /// <summary>
        /// Verifies that SqlConnection.BeginTransaction rejects the unsupported Chaos isolation
        /// level before sending a transaction request to SQL Server.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task LocalTransactionRejectsChaosIsolationLevel(
            bool async,
            bool mars)
        {
            using SqlConnection connection =
                new(NonPooled("LocalChaos", mars));
            await Open(connection, async);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => connection.BeginTransaction(DataIsolationLevel.Chaos));
        }

        /// <summary>
        /// Verifies that a stored procedure can observe its temporary Serializable setting while
        /// SQL Server restores the caller's RepeatableRead level when the procedure returns.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task StoredProcedureIsolationChangeIsProcedureScoped(
            bool async,
            bool mars)
        {
            using SqlConnection connection =
                new(NonPooled("ProcedureScopedIsolation", mars));
            await Open(connection, async);
            await SetLevel(
                connection,
                DataIsolationLevel.RepeatableRead,
                async);

            using StoredProcedure procedure = new(
                connection,
                "IsolationScope",
                $@"
AS
BEGIN
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    {SessionOptionsSql}
END");

            using SqlCommand command = new(procedure.Name, connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            Assert.Equal(
                DataIsolationLevel.Serializable,
                await ReadIsolationLevel(command, async));
            Assert.Equal(
                DataIsolationLevel.RepeatableRead,
                await SessionLevel(connection, async));
        }

#if NETFRAMEWORK
        /// <summary>
        /// Verifies on .NET Framework that the obsolete Connection Reset=false setting is ignored
        /// and a reused physical connection still resets completed Serializable state.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task ConnectionResetFalseDoesNotDisablePooledReset(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);

#pragma warning disable CS0618
            string cs = new SqlConnectionStringBuilder(
                Pooled("ConnectionResetFalse", mars))
            {
                ConnectionReset = false
            }.ConnectionString;
#pragma warning restore CS0618

            Guid originalConnectionId;
            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                using SqlTransaction transaction =
                    first.BeginTransaction(DataIsolationLevel.Serializable);
                transaction.Rollback();
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(next, async));
        }
#endif

        /// <summary>
        /// Verifies on a reset-preserving SQL Server that the legacy isolation switch disables
        /// driver compensation and lets completed Serializable state survive pooled reuse.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsLegacyIsolationSwitchTestAvailable))]
        [MemberData(nameof(PooledModes))]
        public async Task LegacyIsolationSwitchRestoresResetPreservingServerBehavior(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(
                usePoolV2,
                useLegacyIsolationBehavior: true);
            string cs = Pooled("LegacyIsolationSwitch", mars);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId = PhysicalConnectionId(first);
                using SqlTransaction transaction =
                    first.BeginTransaction(DataIsolationLevel.Serializable);
                transaction.Rollback();
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                DataIsolationLevel.Serializable,
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that System.Transactions normalizes an explicitly requested Unspecified level
        /// to its Serializable default before SqlClient observes the transaction.
        /// </summary>
        [Fact]
        public void SystemTransactionsUnspecifiedUsesSerializableDefault()
        {
            using CommittableTransaction transaction = new(
                new TransactionOptions
                {
                    IsolationLevel = TransactionsIsolationLevel.Unspecified
                });

            Assert.Equal(
                TransactionsIsolationLevel.Serializable,
                transaction.IsolationLevel);
        }

        /// <summary>
        /// Verifies that SqlClient rejects manual enlistment in a Chaos transaction because TDS has
        /// no transaction-manager mapping for that isolation level.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task ManualEnlistmentRejectsChaosIsolationLevel(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            using CommittableTransaction transaction = new(
                new TransactionOptions
                {
                    IsolationLevel = TransactionsIsolationLevel.Chaos
                });

            using SqlConnection connection =
                new(Pooled(
                    "AmbientChaos",
                    mars,
                    enlist: false));
            await Open(connection, async);

            Assert.Throws<InvalidOperationException>(
                () => connection.EnlistTransaction(transaction));
        }

        /// <summary>
        /// Verifies that SqlClient rejects a second local transaction instead of changing the
        /// isolation level of the active ReadCommitted transaction.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(ConnectionModes))]
        public async Task SecondLocalTransactionCannotReplaceActiveTransaction(
            bool async,
            bool mars)
        {
            using SqlConnection connection =
                new(NonPooled("SecondLocalTransaction", mars));
            await Open(connection, async);
            using SqlTransaction transaction =
                connection.BeginTransaction(DataIsolationLevel.ReadCommitted);

            Assert.Throws<InvalidOperationException>(
                () => connection.BeginTransaction(
                    DataIsolationLevel.Serializable));
            transaction.Rollback();
        }

        /// <summary>
        /// Verifies that ClearPool discards the marked physical connection so the next Open gets a
        /// different connection identifier and the default ReadCommitted isolation level.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task ClearPoolForcesNewReadCommittedPhysicalConnection(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("ClearPool", mars);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId =
                    PhysicalConnectionId(first);
                await SetLevel(
                    first,
                    DataIsolationLevel.Serializable,
                    async);
                SqlConnection.ClearPool(first);
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.NotEqual(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that a successful ReadCommitted compensation batch resets isolation without
        /// discarding the pooled physical connection.
        /// </summary>
        [ConditionalTheory(
            typeof(DataTestUtility),
            nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureSynapse))]
        [MemberData(nameof(PooledModes))]
        public async Task SuccessfulIsolationResetRetainsPhysicalConnection(
            bool async,
            bool mars,
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            string cs = Pooled("SuccessfulIsolationReset", mars);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await Open(first, async);
                originalConnectionId =
                    PhysicalConnectionId(first);
                using SqlTransaction transaction =
                    first.BeginTransaction(DataIsolationLevel.Serializable);
                transaction.Rollback();
            }

            using SqlConnection next = new(cs);
            await Open(next, async);
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                DataIsolationLevel.ReadCommitted,
                await SessionLevel(next, async));
        }

        /// <summary>
        /// Verifies that a healthy SQL rejection of the ReadCommitted compensation batch is
        /// swallowed and the same pooled physical connection remains usable.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsIsolationResetTrackingAvailable))]
        [MemberData(nameof(PoolVersions))]
        public void HealthyIsolationResetRejectionKeepsConnectionUsable(
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            using IsolationFaultTdsServer server = new(
                IsolationFaultMode.Reject,
                "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;");
            server.Start();
            string cs = FaultServerConnectionString(
                server,
                "HealthyResetRejection");

            using (SqlConnection first = new(cs))
            {
                first.Open();
                MarkIsolationDirty(first);
            }

            using SqlConnection next = new(cs);
            next.Open();

            Assert.Equal(1, server.Login7Count);
            Assert.Equal(1, server.FaultCount);
            using SqlCommand ping = new("SELECT 1;", next);
            Assert.Equal(1, Convert.ToInt32(ping.ExecuteScalar()));
        }

        /// <summary>
        /// Verifies that a timeout while awaiting the isolation-reset response fails Open with the
        /// original timeout, dooms the uncertain physical connection, and creates a replacement.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsIsolationResetTrackingAvailable))]
        [MemberData(nameof(PoolVersions))]
        public void IsolationResetTimeoutPreservesOriginalErrorAndDoomsConnection(
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            using IsolationFaultTdsServer server = new(
                IsolationFaultMode.Stall,
                "SET TRANSACTION ISOLATION LEVEL READ COMMITTED;");
            server.Start();
            string cs = FaultServerConnectionString(
                server,
                "ResetTimeout");

            using (SqlConnection first = new(cs))
            {
                first.Open();
                MarkIsolationDirty(first);
            }

            using (SqlConnection failedCheckout = new(cs))
            {
                SqlException error = Assert.Throws<SqlException>(
                    () => failedCheckout.Open());

                Assert.Equal(-2, error.Number);
                Assert.Equal(
                    ConnectionState.Closed,
                    failedCheckout.State);
            }

            Assert.Equal(1, server.FaultCount);

            using SqlConnection replacement = new(cs);
            replacement.Open();
            Assert.Equal(2, server.Login7Count);
        }

        /// <summary>
        /// Verifies that rejecting a Serializable reassertion fails Open and dooms the physical
        /// connection rather than handing a live ambient transaction an unverified isolation level.
        /// </summary>
        [Theory]
        [MemberData(nameof(PoolVersions))]
        public void AmbientIsolationReassertionFailureDoomsConnection(
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            using IsolationFaultTdsServer server = new(
                IsolationFaultMode.Reject,
                "SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;");
            server.Start();
            string cs = FaultServerConnectionString(
                server,
                "AmbientReassertionFailure");
            using (CommittableTransaction transaction = new(
                new TransactionOptions
                {
                    IsolationLevel =
                        TransactionsIsolationLevel.Serializable
                }))
            {
                using (SqlConnection first = new(cs))
                {
                    first.Open();
                    AttachTransactionForPoolTest(first, transaction);
                }

                using (TransactionScope scope = new(
                    transaction,
                    TransactionScopeAsyncFlowOption.Enabled))
                using (SqlConnection failedCheckout = new(cs))
                {
                    SqlException error = Assert.Throws<SqlException>(
                        () => failedCheckout.Open());

                    Assert.Equal(
                        IsolationFaultTdsServer.InjectedErrorNumber,
                        error.Number);
                    Assert.Equal(
                        ConnectionState.Closed,
                        failedCheckout.State);
                    Assert.Equal(1, server.Login7Count);
                    Assert.Equal(1, server.FaultCount);
                    scope.Complete();
                }

                transaction.Rollback();
            }

            using SqlConnection replacement = new(cs);
            replacement.Open();
            Assert.Equal(2, server.Login7Count);
        }

        /// <summary>
        /// Verifies that Azure Synapse dedicated pools skip the unsupported ReadCommitted
        /// compensation after a ReadUncommitted transaction and keep the pooled connection usable.
        /// </summary>
        [ConditionalTheory(
            typeof(IsolationLevelContractTest),
            nameof(IsSynapseIsolationResetTestAvailable))]
        [MemberData(nameof(PoolVersions))]
        public async Task SynapseDedicatedPoolSkipsUnsupportedReadCommittedCompensation(
            bool usePoolV2)
        {
            using PoolVersionScope _ = new(usePoolV2);
            using DataTestUtility.MDSEventListener eventListener = new();
            string cs = Pooled(
                "SynapseDedicatedPool",
                mars: false);
            Guid originalConnectionId;

            using (SqlConnection first = new(cs))
            {
                await first.OpenAsync();
                Assert.Equal(
                    6,
                    Convert.ToInt32(
                        await ExecuteScalar(
                            first,
                            "SELECT CONVERT(int, SERVERPROPERTY('EngineEdition'));",
                            async: true)));
                originalConnectionId = PhysicalConnectionId(first);
                using SqlTransaction transaction =
                    first.BeginTransaction(
                        DataIsolationLevel.ReadUncommitted);
                transaction.Rollback();
            }

            using SqlConnection next = new(cs);
            await next.OpenAsync();
            Assert.Equal(
                originalConnectionId,
                PhysicalConnectionId(next));
            Assert.Equal(
                1,
                Convert.ToInt32(
                    await ExecuteScalar(
                        next,
                        "SELECT 1;",
                        async: true)));

            Assert.DoesNotContain(
                eventListener.EventData,
                eventData =>
                    eventData.Payload?.Any(
                        value =>
                            value?.ToString()?.IndexOf(
                                "ResetSessionIsolationLevel",
                                StringComparison.Ordinal) >= 0) == true);
        }

        /// <summary>
        /// Creates a unique pooled connection string for one contract scenario.
        /// </summary>
        /// <param name="caseName">Short scenario identifier.</param>
        /// <param name="mars">Whether Multiple Active Result Sets is enabled.</param>
        /// <param name="enlist">Whether connections auto-enlist.</param>
        /// <param name="maxPoolSize">Maximum number of physical connections.</param>
        /// <param name="transactionBinding">Optional transaction-binding mode.</param>
        /// <returns>A connection string isolated from every other test pool.</returns>
        private static string Pooled(
            string caseName,
            bool mars,
            bool enlist = true,
            int maxPoolSize = 1,
            string transactionBinding = null)
        {
            SqlConnectionStringBuilder builder = new(
                DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = maxPoolSize,
                MultipleActiveResultSets = mars,
                Enlist = enlist,
                ApplicationName =
                    $"IsolationContract-{caseName}-{Guid.NewGuid():N}"
            };

            if (transactionBinding != null)
            {
                builder.TransactionBinding = transactionBinding;
            }

            return builder.ConnectionString;
        }

        /// <summary>
        /// Creates a unique non-pooled connection string for one contract scenario.
        /// </summary>
        /// <param name="caseName">Short scenario identifier.</param>
        /// <param name="mars">Whether Multiple Active Result Sets is enabled.</param>
        /// <returns>A non-pooled connection string.</returns>
        private static string NonPooled(string caseName, bool mars) =>
            new SqlConnectionStringBuilder(
                DataTestUtility.TCPConnectionString)
            {
                Pooling = false,
                MultipleActiveResultSets = mars,
                ApplicationName =
                    $"IsolationContract-{caseName}-{Guid.NewGuid():N}"
            }.ConnectionString;

        /// <summary>
        /// Opens a connection through the selected sync or async API.
        /// </summary>
        /// <param name="connection">Connection to open.</param>
        /// <param name="async">Whether to use the async API.</param>
        private static async Task Open(
            SqlConnection connection,
            bool async)
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
        /// Executes a scalar command through the selected sync or async API.
        /// </summary>
        /// <param name="command">Command to execute.</param>
        /// <param name="async">Whether to use the async API.</param>
        /// <returns>The first value returned by the command.</returns>
        private static async Task<object> Scalar(
            SqlCommand command,
            bool async) =>
            async
                ? await command.ExecuteScalarAsync()
                : command.ExecuteScalar();

        /// <summary>
        /// Executes scalar SQL with an optional local transaction.
        /// </summary>
        /// <param name="connection">Connection used by the command.</param>
        /// <param name="sql">SQL text to execute.</param>
        /// <param name="async">Whether to use the async API.</param>
        /// <param name="transaction">Optional local transaction.</param>
        /// <returns>The first value returned by the command.</returns>
        private static async Task<object> ExecuteScalar(
            SqlConnection connection,
            string sql,
            bool async,
            SqlTransaction transaction = null)
        {
            using SqlCommand command = new(sql, connection, transaction);
            return await Scalar(command, async);
        }

        /// <summary>
        /// Executes non-query SQL with an optional local transaction.
        /// </summary>
        /// <param name="connection">Connection used by the command.</param>
        /// <param name="sql">SQL text to execute.</param>
        /// <param name="async">Whether to use the async API.</param>
        /// <param name="transaction">Optional local transaction.</param>
        private static async Task ExecuteNonQuery(
            SqlConnection connection,
            string sql,
            bool async,
            SqlTransaction transaction = null)
        {
            using SqlCommand command = new(sql, connection, transaction);
            if (async)
            {
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Reads the isolation level active for the current connection or MARS request.
        /// </summary>
        /// <param name="connection">Open connection to inspect.</param>
        /// <param name="async">Whether to use the async API.</param>
        /// <param name="transaction">Optional local transaction.</param>
        /// <returns>The current System.Data isolation level.</returns>
        private static async Task<DataIsolationLevel> SessionLevel(
            SqlConnection connection,
            bool async,
            SqlTransaction transaction = null)
        {
            using SqlCommand command = new(
                SessionOptionsSql,
                connection,
                transaction);
            return await ReadIsolationLevel(command, async);
        }

        /// <summary>
        /// Parses the isolation-level row returned by DBCC USEROPTIONS.
        /// </summary>
        /// <param name="command">Command that returns DBCC USEROPTIONS rows.</param>
        /// <param name="async">Whether to use async reader APIs.</param>
        /// <returns>The active System.Data isolation level.</returns>
        private static async Task<DataIsolationLevel> ReadIsolationLevel(
            SqlCommand command,
            bool async)
        {
            using SqlDataReader reader = async
                ? await command.ExecuteReaderAsync()
                : command.ExecuteReader();

            while (async ? await reader.ReadAsync() : reader.Read())
            {
                if (!string.Equals(
                    reader.GetString(0),
                    "isolation level",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return reader.GetString(1).ToLowerInvariant() switch
                {
                    "read uncommitted" =>
                        DataIsolationLevel.ReadUncommitted,
                    "read committed" or "read committed snapshot" =>
                        DataIsolationLevel.ReadCommitted,
                    "repeatable read" =>
                        DataIsolationLevel.RepeatableRead,
                    "serializable" =>
                        DataIsolationLevel.Serializable,
                    "snapshot" =>
                        DataIsolationLevel.Snapshot,
                    string value => throw new InvalidOperationException(
                        $"Unknown isolation-level value '{value}'.")
                };
            }

            throw new InvalidOperationException(
                "DBCC USEROPTIONS did not return an isolation level.");
        }

        /// <summary>
        /// Changes the session isolation level through command text.
        /// </summary>
        /// <param name="connection">Open connection to modify.</param>
        /// <param name="level">Isolation level to select.</param>
        /// <param name="async">Whether to use the async API.</param>
        /// <param name="transaction">Optional local transaction.</param>
        private static Task SetLevel(
            SqlConnection connection,
            DataIsolationLevel level,
            bool async,
            SqlTransaction transaction = null)
        {
            string sqlLevel = level switch
            {
                DataIsolationLevel.ReadUncommitted => "READ UNCOMMITTED",
                DataIsolationLevel.ReadCommitted => "READ COMMITTED",
                DataIsolationLevel.RepeatableRead => "REPEATABLE READ",
                DataIsolationLevel.Serializable => "SERIALIZABLE",
                DataIsolationLevel.Snapshot => "SNAPSHOT",
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            };

            return ExecuteNonQuery(
                connection,
                $"SET TRANSACTION ISOLATION LEVEL {sqlLevel};",
                async,
                transaction);
        }

        /// <summary>
        /// Reads the client identifier assigned to the physical connection.
        /// </summary>
        /// <param name="connection">Open connection to inspect.</param>
        /// <returns>The physical connection identifier.</returns>
        private static Guid PhysicalConnectionId(
            SqlConnection connection) =>
            connection.ClientConnectionId;

        /// <summary>
        /// Creates a RequiresNew scope with async flow enabled.
        /// </summary>
        /// <param name="isolationLevel">Ambient isolation level.</param>
        /// <returns>A new transaction scope.</returns>
        private static TransactionScope CreateScope(
            TransactionsIsolationLevel isolationLevel) =>
            new(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions
                {
                    IsolationLevel = isolationLevel
                },
                TransactionScopeAsyncFlowOption.Enabled);

        /// <summary>
        /// Maps System.Data isolation to System.Transactions isolation.
        /// </summary>
        /// <param name="level">System.Data isolation level.</param>
        /// <returns>The equivalent System.Transactions value.</returns>
        private static TransactionsIsolationLevel ToTransactionsLevel(
            DataIsolationLevel level) =>
            level switch
            {
                DataIsolationLevel.ReadUncommitted =>
                    TransactionsIsolationLevel.ReadUncommitted,
                DataIsolationLevel.ReadCommitted =>
                    TransactionsIsolationLevel.ReadCommitted,
                DataIsolationLevel.RepeatableRead =>
                    TransactionsIsolationLevel.RepeatableRead,
                DataIsolationLevel.Serializable =>
                    TransactionsIsolationLevel.Serializable,
                DataIsolationLevel.Snapshot =>
                    TransactionsIsolationLevel.Snapshot,
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            };

        /// <summary>
        /// Completes or leaves a local transaction according to the selected mode.
        /// </summary>
        /// <param name="transaction">Transaction to complete.</param>
        /// <param name="completion">Completion mode to exercise.</param>
        private static void Complete(
            SqlTransaction transaction,
            CompletionMode completion)
        {
            switch (completion)
            {
                case CompletionMode.Commit:
                    transaction.Commit();
                    break;
                case CompletionMode.Rollback:
                    transaction.Rollback();
                    break;
                case CompletionMode.Dispose:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(completion));
            }
        }

        /// <summary>
        /// Accepts only the two documented server reset outcomes for direct SQL.
        /// </summary>
        /// <param name="actual">Observed level after pooled reuse.</param>
        private static void AssertServerDependentDirectSqlLevel(
            DataIsolationLevel actual) =>
            Assert.Contains(
                actual,
                new[]
                {
                    DataIsolationLevel.ReadCommitted,
                    DataIsolationLevel.Serializable
                });

        /// <summary>
        /// Builds a pooled connection string for an in-process fault server.
        /// </summary>
        /// <param name="server">Started test server.</param>
        /// <param name="caseName">Scenario identifier.</param>
        /// <returns>A pooled connection string with a short activation timeout.</returns>
        private static string FaultServerConnectionString(
            IsolationFaultTdsServer server,
            string caseName) =>
            new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = true,
                MaxPoolSize = 1,
                ConnectTimeout = 1,
                PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock,
                ApplicationName =
                    $"IsolationContract-{caseName}-{Guid.NewGuid():N}"
            }.ConnectionString;

        /// <summary>
        /// Marks the internal connection as changed by a driver-managed non-default transaction.
        /// </summary>
        /// <param name="connection">Open outer connection whose physical connection will be pooled.</param>
        private static void MarkIsolationDirty(
            SqlConnection connection)
        {
            object innerConnection = connection.GetInternalConnection();
            FieldInfo field = GetIsolationDirtyField();

            Assert.NotNull(field);
            field.SetValue(innerConnection, true);
        }

        /// <summary>
        /// Attaches transaction metadata without requiring transaction-manager support from the test server.
        /// </summary>
        /// <param name="connection">Open outer connection whose physical connection will be pooled.</param>
        /// <param name="transaction">Active transaction used to select the transacted pool subdivision.</param>
        private static void AttachTransactionForPoolTest(
            SqlConnection connection,
            Transaction transaction)
        {
            object innerConnection = connection.GetInternalConnection();
            PropertyInfo property = innerConnection.GetType().GetProperty(
                "EnlistedTransaction",
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy);

            Assert.NotNull(property);
            property.SetValue(innerConnection, transaction);
        }

        /// <summary>
        /// Probes whether Snapshot isolation is enabled for the configured database.
        /// </summary>
        /// <returns>True when ALLOW_SNAPSHOT_ISOLATION is ON.</returns>
        private static bool ProbeSnapshotIsolation()
        {
            using SqlConnection connection = new(
                new SqlConnectionStringBuilder(
                    DataTestUtility.TCPConnectionString)
                {
                    Pooling = false,
                    MultipleActiveResultSets = false,
                    ApplicationName = "IsolationContract-SnapshotProbe"
                }.ConnectionString);
            connection.Open();

            using SqlCommand command = new(@"
SELECT snapshot_isolation_state
FROM sys.databases
WHERE database_id = DB_ID();", connection);

            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        /// <summary>
        /// Finds the optional cached field added with legacy isolation compensation.
        /// </summary>
        /// <returns>The switch field when the proposed behavior is present; otherwise null.</returns>
        private static FieldInfo GetLegacyIsolationSwitchField()
        {
            Type switchesType = typeof(SqlConnection).Assembly.GetType(
                "Microsoft.Data.SqlClient.LocalAppContextSwitches");

            return switchesType?.GetField(
                "s_useLegacyIsolationLevelBehavior",
                BindingFlags.Static | BindingFlags.NonPublic);
        }

        /// <summary>
        /// Finds the optional field that tracks driver-managed isolation changes.
        /// </summary>
        /// <returns>The tracking field when isolation reset behavior is present; otherwise null.</returns>
        private static FieldInfo GetIsolationDirtyField()
        {
            Type internalConnectionType = typeof(SqlConnection).Assembly.GetType(
                "Microsoft.Data.SqlClient.Connection.SqlConnectionInternal");

            return internalConnectionType?.GetField(
                "_isolationLevelDirty",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private enum IsolationFaultMode
        {
            Reject,
            Stall
        }

        /// <summary>
        /// Injects one SQL rejection or stalled response for a matching isolation batch.
        /// </summary>
        private sealed class IsolationFaultTdsServer : TdsServer
        {
            public const int InjectedErrorNumber = 50001;

            private readonly string _batchText;
            private readonly IsolationFaultMode _mode;
            private readonly object _sync = new();
            private readonly ManualResetEventSlim _releaseResponse = new(false);
            private Timer _automaticRelease;
            private int _faultCount;
            private bool _disposed;

            /// <summary>
            /// Initializes a server that faults the first matching SQL batch.
            /// </summary>
            /// <param name="mode">Whether to reject or stall the batch.</param>
            /// <param name="batchText">SQL text fragment that triggers the fault.</param>
            public IsolationFaultTdsServer(
                IsolationFaultMode mode,
                string batchText)
                : base(new TdsServerArguments())
            {
                _mode = mode;
                _batchText = batchText;
            }

            /// <summary>
            /// Gets the number of matching batches faulted by the server.
            /// </summary>
            public int FaultCount => Volatile.Read(ref _faultCount);

            /// <summary>
            /// Returns an injected error or stalls the first matching SQL batch.
            /// </summary>
            /// <param name="session">TDS session that sent the batch.</param>
            /// <param name="message">Incoming SQL batch.</param>
            /// <returns>The injected or normal server response.</returns>
            public override TDSMessageCollection OnSQLBatchRequest(
                ITDSServerSession session,
                TDSMessage message)
            {
                TDSSQLBatchToken batch = message[0] as TDSSQLBatchToken;
                bool shouldFault =
                    batch != null &&
                    batch.Text.IndexOf(
                        _batchText,
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                    Interlocked.CompareExchange(
                        ref _faultCount,
                        1,
                        0) == 0;

                if (!shouldFault)
                {
                    return base.OnSQLBatchRequest(session, message);
                }

                if (_mode == IsolationFaultMode.Stall)
                {
                    lock (_sync)
                    {
                        _automaticRelease = new Timer(
                            _ => ReleaseFaultResponse(),
                            state: null,
                            dueTime: TimeSpan.FromMilliseconds(1500),
                            period: Timeout.InfiniteTimeSpan);
                    }
                    _releaseResponse.Wait(TimeSpan.FromSeconds(30));
                    return base.OnSQLBatchRequest(session, message);
                }

                TDSErrorToken error = new(
                    InjectedErrorNumber,
                    state: 1,
                    clazz: 16,
                    message: "Injected isolation-level batch rejection.");
                TDSDoneToken done = new(
                    TDSDoneTokenStatusType.Final |
                    TDSDoneTokenStatusType.Error);
                TDSMessage response = new(
                    TDSMessageType.Response,
                    error,
                    done);
                return new TDSMessageCollection(response);
            }

            /// <summary>
            /// Releases a response deliberately stalled by the server.
            /// </summary>
            private void ReleaseFaultResponse()
            {
                lock (_sync)
                {
                    if (!_disposed)
                    {
                        _releaseResponse.Set();
                    }
                }
            }

            /// <summary>
            /// Releases blocked requests and stops the server.
            /// </summary>
            public override void Dispose()
            {
                lock (_sync)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    _automaticRelease?.Dispose();
                    _releaseResponse.Set();
                }

                try
                {
                    base.Dispose();
                }
                finally
                {
                    _releaseResponse.Dispose();
                }
            }
        }

        /// <summary>
        /// Selects one connection-pool implementation and restores global state afterward.
        /// </summary>
        private sealed class PoolVersionScope : IDisposable
        {
            private readonly LocalAppContextSwitchesHelper _switches = new();
            private readonly FieldInfo _legacyIsolationSwitchField;
            private readonly object _legacyIsolationSwitchOriginal;

            /// <summary>
            /// Selects a pool implementation and optional legacy isolation behavior.
            /// </summary>
            /// <param name="usePoolV2">Whether to select ChannelDbConnectionPool.</param>
            /// <param name="useLegacyIsolationBehavior">Whether to disable isolation compensation.</param>
            public PoolVersionScope(
                bool usePoolV2,
                bool useLegacyIsolationBehavior = false)
            {
                _switches.UseConnectionPoolV2 = usePoolV2;
                if (useLegacyIsolationBehavior)
                {
                    _legacyIsolationSwitchField =
                        GetLegacyIsolationSwitchField() ??
                        throw new InvalidOperationException(
                            "Legacy isolation switch is unavailable.");
                    _legacyIsolationSwitchOriginal =
                        _legacyIsolationSwitchField.GetValue(null);
                    _legacyIsolationSwitchField.SetValue(
                        null,
                        Enum.ToObject(
                            _legacyIsolationSwitchField.FieldType,
                            1));
                }
                SqlConnection.ClearAllPools();
            }

            /// <summary>
            /// Clears selected pools and restores cached AppContext switches.
            /// </summary>
            public void Dispose()
            {
                SqlConnection.ClearAllPools();
                if (_legacyIsolationSwitchField != null)
                {
                    _legacyIsolationSwitchField.SetValue(
                        null,
                        _legacyIsolationSwitchOriginal);
                }
                _switches.Dispose();
            }
        }

        private enum CompletionMode
        {
            Commit,
            Rollback,
            Dispose
        }
    }
}
