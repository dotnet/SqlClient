// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.Data.SqlClient.UnitTests.ConnectionPool;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient;

/// <summary>
/// Tests <see cref="SqlConnectionFactory"/> pool pruning and replacement routing. Pruning must
/// never retire a pool with admitted work, such as a physical connection that has not yet entered
/// pool inventory, because a later open would then lose transaction affinity by using a new pool.
/// </summary>
public class SqlConnectionFactoryTests
{
    private static readonly TimeSpan s_waitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The pool implementation registered in the factory's pool group.
    /// </summary>
    public enum PoolKind
    {
        /// <summary>The legacy <see cref="WaitHandleDbConnectionPool"/>.</summary>
        WaitHandle,

        /// <summary>The <see cref="ChannelDbConnectionPool"/>.</summary>
        Channel,
    }

    /// <summary>
    /// A sync open blocked in physical creation has an empty pool inventory. Pruning must still
    /// keep that pool running and registered so the open's connection stays in its pool.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public async Task RunPruningPass_DuringSyncPhysicalCreation_RetainsPool(PoolKind kind)
    {
        // Arrange
        // Block a sync open inside physical creation, before the connection is counted.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        factory.Creating = () => BlockCreation(entered, release);
        Task<DbConnectionInternal> open = Task.Run(() => OpenSync(pool, owner));
        try
        {
            Assert.True(entered.Wait(s_waitTimeout));
            Assert.Equal(0, pool.Count);

            // Act
            factory.RunPruningPass();

            // Assert
            Assert.True(pool.IsRunning);
            Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
        }
        finally
        {
            // Cleanup
            // Let creation finish, then return its connection before draining the group.
            release.Set();
            pool.ReturnInternalConnection(await WithTimeout(open), owner);
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// An async open blocked in physical creation has an empty pool inventory. Pruning must still
    /// keep that pool running and registered so the open's connection stays in its pool.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public async Task RunPruningPass_DuringAsyncPhysicalCreation_RetainsPool(PoolKind kind)
    {
        // Arrange
        // Block an async open's worker inside physical creation, before the connection is counted.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        factory.Creating = () => BlockCreation(entered, release);
        var completion = new TaskCompletionSource<DbConnectionInternal>();
        Assert.False(pool.TryGetConnection(owner, completion, StartTimer(), out _));
        try
        {
            Assert.True(entered.Wait(s_waitTimeout));
            Assert.Equal(0, pool.Count);

            // Act
            factory.RunPruningPass();

            // Assert
            Assert.True(pool.IsRunning);
            Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
        }
        finally
        {
            // Cleanup
            // Let creation finish, then return its connection before draining the group.
            release.Set();
            pool.ReturnInternalConnection(await WithTimeout(completion.Task), owner);
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// A failed sync create must release its admission, so an empty pool without a blocking
    /// period can still be retired.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public void RunPruningPass_AfterSyncCreationFailure_RetiresPool(PoolKind kind)
    {
        // Arrange
        // Fail the only sync open during physical creation.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        factory.Creating = () => throw new InvalidOperationException("Expected creation failure.");
        Assert.Throws<InvalidOperationException>(() => OpenSync(pool, owner));
        try
        {
            // Act
            bool retired = PruneUntilRetired(factory, pool);

            // Assert
            Assert.True(retired);
            Assert.Equal(0, pool.Count);
        }
        finally
        {
            // Cleanup
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// A failed async create must release its admission, so an empty pool without a blocking
    /// period can still be retired.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public async Task RunPruningPass_AfterAsyncCreationFailure_RetiresPool(PoolKind kind)
    {
        // Arrange
        // Fail the only async open during physical creation.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        factory.Creating = () => throw new InvalidOperationException("Expected creation failure.");
        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(pool, owner));
        try
        {
            // Act
            bool retired = PruneUntilRetired(factory, pool);

            // Assert
            Assert.True(retired);
            Assert.Equal(0, pool.Count);
        }
        finally
        {
            // Cleanup
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// Cancellation completes the caller's task, not the physical worker. Pruning must not block
    /// on, or retire the pool under, a worker that is still creating a connection.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public async Task RunPruningPass_DuringCancelledAsyncOpen_RetainsPool(PoolKind kind)
    {
        // Arrange
        // Block an async open's worker in creation, then cancel the caller's completion.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        factory.Creating = () =>
        {
            BlockCreation(entered, release);
            throw new InvalidOperationException("Expected creation failure after cancellation.");
        };
        var completion = new TaskCompletionSource<DbConnectionInternal>();
        Assert.False(pool.TryGetConnection(owner, completion, StartTimer(), out _));
        try
        {
            Assert.True(entered.Wait(s_waitTimeout));
            completion.SetCanceled();
            Assert.Equal(0, pool.Count);

            // Act
            Task pruning = Task.Run(factory.RunPruningPass);
            Task finished = await Task.WhenAny(pruning, Task.Delay(s_waitTimeout));

            // Assert
            Assert.Same(pruning, finished);
            await pruning;
            Assert.True(pool.IsRunning);
            Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
        }
        finally
        {
            // Cleanup
            // Let the worker fail and release admission before draining the group.
            release.Set();
            PruneUntilRetired(factory, pool);
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// Background minimum-size replenishment creates a connection outside any user open. Pruning
    /// must keep the pool running and registered while that creation is still in progress.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public void RunPruningPass_DuringWarmup_RetainsPool(PoolKind kind)
    {
        // Arrange
        // Start a pool whose minimum-size warmup blocks inside physical creation.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind, minPoolSize: 1, start: false);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        factory.Creating = () => BlockCreation(entered, release);
        try
        {
            pool.Startup();
            Assert.True(entered.Wait(s_waitTimeout));
            Assert.Equal(0, pool.Count);

            // Act
            factory.RunPruningPass();

            // Assert
            Assert.True(pool.IsRunning);
            Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
        }
        finally
        {
            // Cleanup
            // Let warmup add its connection before draining the group.
            release.Set();
            SpinWait.SpinUntil(() => pool.IdleCount == 1, s_waitTimeout);
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// A sync open that times out waiting for an exhausted pool must release its admission, so
    /// the pool can be retired once its connections are gone.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public void RunPruningPass_AfterSyncPoolTimeout_RetiresPool(PoolKind kind)
    {
        // Arrange
        // Exhaust the single-connection pool so a second sync open times out.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind, connectionTimeout: 1);
        using var owner = new SqlConnection();
        using var waiter = new SqlConnection();
        DbConnectionInternal connection = OpenSync(pool, owner);
        Assert.Throws<InvalidOperationException>(() => OpenSync(pool, waiter));

        // Return and destroy the connection so only a leaked admission could keep the pool.
        pool.ReturnInternalConnection(connection, owner);
        pool.Clear();
        try
        {
            // Act
            bool retired = PruneUntilRetired(factory, pool);

            // Assert
            Assert.True(retired);
        }
        finally
        {
            // Cleanup
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// An async open that times out waiting for an exhausted pool must release its admission, so
    /// the pool can be retired once its connections are gone.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public async Task RunPruningPass_AfterAsyncPoolTimeout_RetiresPool(PoolKind kind)
    {
        // Arrange
        // Exhaust the single-connection pool so a second async open times out.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind, connectionTimeout: 1);
        using var owner = new SqlConnection();
        using var waiter = new SqlConnection();
        DbConnectionInternal connection = OpenSync(pool, owner);
        await Assert.ThrowsAsync<InvalidOperationException>(() => OpenAsync(pool, waiter));

        // Return and destroy the connection so only a leaked admission could keep the pool.
        pool.ReturnInternalConnection(connection, owner);
        pool.Clear();
        try
        {
            // Act
            bool retired = PruneUntilRetired(factory, pool);

            // Assert
            Assert.True(retired);
        }
        finally
        {
            // Cleanup
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// A creation failure's blocking period must keep throttling opens, so pruning must not
    /// discard the empty pool that holds it.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public void RunPruningPass_DuringBlockingPeriod_RetainsPool(PoolKind kind)
    {
        // Arrange
        // Fail creation in a pool that always blocks after an error.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool pool = CreatePool(factory, kind, blockingPeriod: "AlwaysBlock");
        using var owner = new SqlConnection();
        factory.Creating = () => throw new InvalidOperationException("Expected creation failure.");
        Assert.Throws<InvalidOperationException>(() => OpenSync(pool, owner));
        Assert.True(pool.ErrorOccurred);
        try
        {
            // Act
            factory.RunPruningPass();

            // Assert
            Assert.True(pool.IsRunning);
            Assert.Same(pool, pool.PoolGroup.GetConnectionPool(factory));
        }
        finally
        {
            // Cleanup
            Drain(factory, pool);
        }
    }

    /// <summary>
    /// When the old connection's pool was retired, the factory routes the replacement to the
    /// group's new pool. The replacement must belong to that pool, and the old connection must be
    /// removed from its own pool's inventory without throwing.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    [Theory]
    [InlineData(PoolKind.WaitHandle)]
    [InlineData(PoolKind.Channel)]
    public void TryGetConnection_SourcePoolRetired_ReplacesInNewPool(PoolKind kind)
    {
        // Arrange
        // Open in a pool, retire that pool, then register a new pool in the same group.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool source = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        owner.PoolGroup = source.PoolGroup;
        DbConnectionInternal oldConnection = OpenSync(source, owner);
        source.PoolGroup.Clear();
        Assert.False(source.IsRunning);
        IDbConnectionPool destination = CreatePool(factory, kind);
        DbConnectionInternal? connection = null;
        try
        {
            // Act
            bool completed = factory.TryGetConnection(
                owner, null!, oldConnection, StartTimer(), forceNewConnection: true, out connection);

            // Assert
            Assert.True(completed);
            Assert.NotNull(connection);
            Assert.NotSame(oldConnection, connection);
            Assert.Same(destination, connection!.Pool);
            Assert.Equal(1, destination.Count);
            Assert.Equal(0, source.Count);
            Assert.True(oldConnection.IsConnectionDoomed);
            Assert.Equal(2, factory.CreateCount);
        }
        finally
        {
            // Cleanup
            connection?.Pool!.ReturnInternalConnection(connection, owner);
            Drain(factory, source);
        }
    }

    /// <summary>
    /// A replacement routed into a saturated pool with an idle connection must perform a physical
    /// login with the owner's session recovery data. Reusing the unrelated idle session would skip
    /// restoring the original session state.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    /// <param name="expectedDestinationCount">
    /// The new pool's size afterwards. The Channel pool retires the idle session to stay within
    /// Max Pool Size, while the WaitHandle pool's replacement path has always exceeded it.
    /// </param>
    [Theory]
    [InlineData(PoolKind.WaitHandle, 2)]
    [InlineData(PoolKind.Channel, 1)]
    public void TryGetConnection_ReplacementIntoSaturatedPool_LogsInWithRecoveryData(
        PoolKind kind, int expectedDestinationCount)
    {
        // Arrange
        // Open in a pool, retire that pool, then register a new pool in the same group.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool source = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        owner.PoolGroup = source.PoolGroup;
        DbConnectionInternal oldConnection = OpenSync(source, owner);
        source.PoolGroup.Clear();
        IDbConnectionPool destination = CreatePool(factory, kind);

        // Saturate the new pool with one idle connection owned by another caller.
        using var other = new SqlConnection();
        DbConnectionInternal idle = OpenSync(destination, other);
        destination.ReturnInternalConnection(idle, other);
        Assert.Equal(1, destination.IdleCount);

        // Give the owner session state that the replacement login must restore.
        var recoveryData = new SessionData();
        owner._recoverySessionData = recoveryData;
        DbConnectionInternal? connection = null;
        try
        {
            // Act
            bool completed = factory.TryGetConnection(
                owner, null!, oldConnection, StartTimer(), forceNewConnection: true, out connection);

            // Assert
            Assert.True(completed);
            Assert.NotNull(connection);
            Assert.NotSame(idle, connection);
            Assert.Same(destination, connection!.Pool);
            Assert.Equal(3, factory.CreateCount);
            Assert.Same(recoveryData, factory.RecoveryData[2]);
            Assert.True(oldConnection.IsConnectionDoomed);
            Assert.Equal(0, source.Count);
            Assert.Equal(expectedDestinationCount, destination.Count);
        }
        finally
        {
            // Cleanup
            owner._recoverySessionData = null;
            connection?.Pool!.ReturnInternalConnection(connection, owner);
            Drain(factory, source);
        }
    }

    /// <summary>
    /// A replacement routed into a saturated pool whose only idle connection is dead must retry
    /// the open on the wake-up written when that connection is retired, rather than wait for the
    /// timeout while a slot is free.
    /// </summary>
    /// <param name="kind">The pool implementation under the factory.</param>
    /// <param name="expectedDestinationCount">
    /// The new pool's size afterwards. The Channel pool retires the dead session to stay within
    /// Max Pool Size, while the WaitHandle pool's replacement path has always exceeded it.
    /// </param>
    [Theory]
    [InlineData(PoolKind.WaitHandle, 2)]
    [InlineData(PoolKind.Channel, 1)]
    public void TryGetConnection_ReplacementIntoPoolWithDeadIdle_OpensInFreedSlot(
        PoolKind kind, int expectedDestinationCount)
    {
        // Arrange
        // Open in a pool, retire that pool, then register a new pool in the same group.
        var factory = new PruningConnectionFactory();
        IDbConnectionPool source = CreatePool(factory, kind);
        using var owner = new SqlConnection();
        owner.PoolGroup = source.PoolGroup;
        DbConnectionInternal oldConnection = OpenSync(source, owner);
        source.PoolGroup.Clear();
        IDbConnectionPool destination = CreatePool(factory, kind);

        // Saturate the new pool with one idle connection, then make it fail its liveness check.
        using var other = new SqlConnection();
        var idle = (PruningConnection)OpenSync(destination, other);
        destination.ReturnInternalConnection(idle, other);
        Assert.Equal(1, destination.IdleCount);
        idle.Alive = false;
        DbConnectionInternal? connection = null;
        try
        {
            // Act
            // A regression waits until this deadline, so keep it short to fail fast.
            bool completed = factory.TryGetConnection(
                owner,
                null!,
                oldConnection,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                forceNewConnection: true,
                out connection);

            // Assert
            Assert.True(completed);
            Assert.NotNull(connection);
            Assert.NotSame(idle, connection);
            Assert.Same(destination, connection!.Pool);
            Assert.Equal(3, factory.CreateCount);
            Assert.True(oldConnection.IsConnectionDoomed);
            Assert.Equal(0, source.Count);
            Assert.Equal(expectedDestinationCount, destination.Count);
        }
        finally
        {
            // Cleanup
            connection?.Pool!.ReturnInternalConnection(connection, owner);
            Drain(factory, source);
        }
    }

    /// <summary>
    /// Signals that physical creation started, then blocks it until the test releases it.
    /// </summary>
    /// <param name="entered">Set when creation starts.</param>
    /// <param name="release">Waited on before creation continues.</param>
    private static void BlockCreation(ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        entered.Set();
        Assert.True(release.Wait(TimeSpan.FromSeconds(15)));
    }

    /// <summary>
    /// Runs factory pruning passes until the pool is retired or the wait times out. A released
    /// admission may still be unwinding on another thread, so a single pass is not enough.
    /// </summary>
    /// <param name="factory">The factory whose pruning pass retires idle pools.</param>
    /// <param name="pool">The pool expected to be retired.</param>
    /// <returns>True if the pool stopped running before the timeout.</returns>
    private static bool PruneUntilRetired(PruningConnectionFactory factory, IDbConnectionPool pool)
    {
        return SpinWait.SpinUntil(
            () =>
            {
                factory.RunPruningPass();
                return !pool.IsRunning;
            },
            s_waitTimeout);
    }

    /// <summary>
    /// Clears only this isolated test group and drains its factory's deferred cleanup.
    /// </summary>
    /// <param name="factory">The isolated factory whose pruning passes process cleanup.</param>
    /// <param name="pool">The pool identifying the test group to clear.</param>
    private static void Drain(PruningConnectionFactory factory, IDbConnectionPool pool)
    {
        pool.PoolGroup.Clear();
        for (int pass = 0; pass < 4; pass++)
        {
            factory.RunPruningPass();
        }
    }

    /// <summary>
    /// Registers a chosen pool implementation in the factory without changing the process-wide
    /// pool switch. Uses reflection because the group only creates pools of the switched kind.
    /// </summary>
    /// <param name="factory">The isolated factory in which to register the pool.</param>
    /// <param name="kind">The pool implementation to create.</param>
    /// <param name="minPoolSize">The minimum connection count used to exercise warmup.</param>
    /// <param name="start">Whether to start the pool immediately after registration.</param>
    /// <param name="connectionTimeout">The connection acquisition timeout, in seconds.</param>
    /// <param name="blockingPeriod">The policy controlling throttling after a failure.</param>
    /// <returns>The registered pool, started only when requested.</returns>
    private static IDbConnectionPool CreatePool(
        PruningConnectionFactory factory,
        PoolKind kind,
        int minPoolSize = 0,
        bool start = true,
        int connectionTimeout = 10,
        string blockingPeriod = "NeverBlock")
    {
        string connectionString =
            $"Data Source=localhost;Max Pool Size=1;Min Pool Size={minPoolSize};" +
            $"Connect Timeout={connectionTimeout};Pool Blocking Period={blockingPeriod};";
        var key = new ConnectionPoolKey(connectionString, null, null, null, null);
        SqlConnectionOptions options = null!;
        DbConnectionPoolGroup group = factory.GetConnectionPoolGroup(key, null, ref options);
        IDbConnectionPool pool = kind == PoolKind.Channel
            ? new ChannelDbConnectionPool(
                factory, group, DbConnectionPoolIdentity.NoIdentity, new DbConnectionPoolProviderInfo())
            : new WaitHandleDbConnectionPool(
                factory, group, DbConnectionPoolIdentity.NoIdentity, new DbConnectionPoolProviderInfo());
        var pools = (ConcurrentDictionary<DbConnectionPoolIdentity, IDbConnectionPool>)
            typeof(DbConnectionPoolGroup)
                .GetField("_poolCollection", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(group)!;
        Assert.True(pools.TryAdd(DbConnectionPoolIdentity.NoIdentity, pool));
        SqlClientDiagnostics.Metrics.EnterActiveConnectionPool();
        if (start)
        {
            pool.Startup();
        }
        return pool;
    }

    /// <summary>
    /// Opens through the pool's sync entry point.
    /// </summary>
    /// <param name="pool">The pool from which to acquire a connection.</param>
    /// <param name="owner">The logical connection that will own the acquired connection.</param>
    /// <returns>The acquired connection.</returns>
    /// <exception cref="InvalidOperationException">The pool timed out or rejected the open.</exception>
    private static DbConnectionInternal OpenSync(IDbConnectionPool pool, SqlConnection owner)
    {
        Assert.True(pool.TryGetConnection(owner, null, StartTimer(pool), out var connection));
        return connection ?? throw new InvalidOperationException("Pool acquisition timed out.");
    }

    /// <summary>
    /// Opens through the pool's queued async entry point with a bounded wait.
    /// </summary>
    /// <param name="pool">The pool from which to acquire a connection.</param>
    /// <param name="owner">The logical connection that will own the acquired connection.</param>
    /// <returns>The acquired connection.</returns>
    private static async Task<DbConnectionInternal> OpenAsync(
        IDbConnectionPool pool, SqlConnection owner)
    {
        var completion = new TaskCompletionSource<DbConnectionInternal>();
        if (pool.TryGetConnection(owner, completion, StartTimer(pool), out var connection))
        {
            return connection ?? throw new InvalidOperationException("Pool acquisition timed out.");
        }
        return await WithTimeout(completion.Task);
    }

    /// <summary>
    /// Awaits a task, failing the test instead of hanging if it does not complete in time.
    /// </summary>
    /// <param name="task">The task to await.</param>
    /// <returns>The task's result.</returns>
    private static async Task<T> WithTimeout<T>(Task<T> task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(15))));
        return await task;
    }

    /// <summary>
    /// Starts a bounded timeout for a factory or async pool request.
    /// </summary>
    /// <returns>A timer that expires after the shared wait timeout.</returns>
    private static TimeoutTimer StartTimer() => TimeoutTimer.StartNew(s_waitTimeout);

    /// <summary>
    /// Starts a timeout matching the pool's configured connection timeout.
    /// </summary>
    /// <param name="pool">The pool whose creation timeout to use.</param>
    /// <returns>A timer that expires after the pool's connection timeout.</returns>
    private static TimeoutTimer StartTimer(IDbConnectionPool pool) =>
        TimeoutTimer.StartNew(TimeSpan.FromMilliseconds(pool.PoolGroupOptions.CreationTimeout));

    /// <summary>
    /// Invokes a test hook inside physical creation and records each login's recovery data.
    /// Autonomous pruning is disabled so only the test's pruning passes run.
    /// </summary>
    private sealed class PruningConnectionFactory : SqlConnectionFactory
    {
        /// <summary>
        /// Creates a factory whose pruning timer never fires during a test.
        /// </summary>
        internal PruningConnectionFactory() : base(TimeSpan.FromDays(1), TimeSpan.FromDays(1))
        {
        }

        /// <summary>
        /// Runs inside each physical creation, before the connection enters pool inventory.
        /// </summary>
        internal Action? Creating { get; set; }

        /// <summary>
        /// The number of physical creations attempted.
        /// </summary>
        internal int CreateCount { get; private set; }

        /// <summary>
        /// The owner's session recovery data passed to each physical login, in creation order.
        /// </summary>
        internal List<SessionData?> RecoveryData { get; } = new();

        /// <summary>
        /// Counts creation attempts and invokes the test hook before returning a mock connection.
        /// </summary>
        /// <param name="options">Connection options supplied by the factory; unused.</param>
        /// <param name="poolKey">The requesting pool's key; unused.</param>
        /// <param name="poolGroupProviderInfo">Provider metadata for the pool group; unused.</param>
        /// <param name="pool">The pool requesting creation; unused.</param>
        /// <param name="owningConnection">The logical owner whose recovery data is recorded.</param>
        /// <param name="timeout">The creation deadline; unused because nothing is opened.</param>
        /// <returns>A mock physical connection for the caller to register in the pool.</returns>
        protected override DbConnectionInternal CreateConnection(
            SqlConnectionOptions options,
            ConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            TimeoutTimer timeout)
        {
            CreateCount++;
            RecoveryData.Add((owningConnection as SqlConnection)?._recoverySessionData);
            Creating?.Invoke();
            return new PruningConnection();
        }
    }

    /// <summary>
    /// A mock physical connection whose liveness a test can change while it sits idle.
    /// </summary>
    private sealed class PruningConnection
        : WaitHandleDbConnectionPoolTransactionTest.MockDbConnectionInternal
    {
        /// <summary>
        /// Whether the liveness probe reports the connection as alive.
        /// </summary>
        internal bool Alive { get; set; } = true;

        /// <summary>
        /// Reports the configured liveness instead of probing a socket.
        /// </summary>
        /// <param name="throwOnException">Unused by the mock.</param>
        /// <returns>The value of <see cref="Alive"/>.</returns>
        internal override bool IsConnectionAlive(bool throwOnException = false) => Alive;
    }
}
