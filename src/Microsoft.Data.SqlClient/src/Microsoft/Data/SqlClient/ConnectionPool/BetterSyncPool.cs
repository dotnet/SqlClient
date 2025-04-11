// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.RateLimiter;

using static Microsoft.Data.SqlClient.ConnectionPool.DbConnectionPoolState;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal sealed class BetterSyncPool : DbConnectionPool
    {
        #region Interface
        internal override int Count => _numConnectors;

        internal override DbConnectionFactory ConnectionFactory => _connectionFactory;

        internal override bool ErrorOccurred => false;

        private bool HasTransactionAffinity => PoolGroupOptions.HasTransactionAffinity;

        internal override TimeSpan LoadBalanceTimeout => PoolGroupOptions.LoadBalanceTimeout;

        internal override DbConnectionPoolIdentity Identity => _identity;

        internal override bool IsRunning
        {
            get { return State is Running; }
        }

        private int MaxPoolSize => PoolGroupOptions.MaxPoolSize;

        private int MinPoolSize => PoolGroupOptions.MinPoolSize;

        internal override DbConnectionPoolGroup PoolGroup => _connectionPoolGroup;

        internal override DbConnectionPoolGroupOptions PoolGroupOptions => _connectionPoolGroupOptions;

        internal override DbConnectionPoolProviderInfo ProviderInfo => _connectionPoolProviderInfo;

        /// <summary>
        /// Return the pooled authentication contexts.
        /// </summary>
        internal override ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> AuthenticationContexts => _pooledDbAuthenticationContexts;

        internal override bool UseLoadBalancing => PoolGroupOptions.UseLoadBalancing;

        /// <summary>
        /// This only clears idle connections. Connections that are in use are not affected.
        /// Different from previous behavior where all connections in the pool are doomed and eventually cleaned up.
        /// </summary>
        internal override void Clear()
        {
            Interlocked.Increment(ref _clearCounter);

            if (Interlocked.CompareExchange(ref _isClearing, 1, 0) == 1)
                return;

            try
            {
                var count = _idleCount;
                while (count > 0 && _idleConnectorReader.TryRead(out var connector))
                {
                    if (connector is null)
                    {
                        continue;
                    }
                    if (CheckIdleConnector(connector))
                    {
                        CloseConnector(connector);
                        count--;
                    }
                }
            }
            finally
            {
                _isClearing = 0;
            }
        }

        internal override bool TryGetConnection(DbConnection owningObject, TaskCompletionSource<DbConnectionInternal> taskCompletionSource, DbConnectionOptions userOptions, out DbConnectionInternal? connection)
        {
            if (taskCompletionSource is not null)
            {
                ThreadPool.QueueUserWorkItem(async (_) =>
                {
                    //TODO: use same timespan everywhere and tick down for queueuing and actual connection opening work
                    var connection = await GetInternalConnection(owningObject, userOptions, TimeSpan.FromSeconds(owningObject.ConnectionTimeout), false, CancellationToken.None).ConfigureAwait(false);
                    //TODO set transaction if necessary
                    PrepareConnection(owningObject, connection, null);
                    taskCompletionSource.SetResult(connection);
                });
                connection = null;
                return false;
            } 
            else
            {
                //TODO: use same timespan everywhere and tick down for queueuing and actual connection opening work
                connection = GetInternalConnection(owningObject, userOptions, TimeSpan.FromSeconds(owningObject.ConnectionTimeout), false, CancellationToken.None).Result;
                //TODO set transaction if necessary
                PrepareConnection(owningObject, connection, null);
                return connection is not null;
            }
        }

        private void PrepareConnection(DbConnection owningObject, DbConnectionInternal obj, Transaction? transaction)
        {
            lock (obj)
            {   // Protect against Clear and ReclaimEmancipatedObjects, which call IsEmancipated, which is affected by PrePush and PostPop
                obj.PostPop(owningObject);
            }
            try
            {
                obj.ActivateConnection(transaction);
            }
            catch
            {
                // if Activate throws an exception
                // put it back in the pool or have it properly disposed of
                this.ReturnInternalConnection(obj, owningObject);
                throw;
            }
        }

        /// <summary>
        /// Creates a new connection to replace an existing connection
        /// </summary>
        /// <param name="owningObject">Outer connection that currently owns <paramref name="oldConnection"/></param>
        /// <param name="userOptions">Options used to create the new connection</param>
        /// <param name="oldConnection">Inner connection that will be replaced</param>
        /// <returns>A new inner connection that is attached to the <paramref name="owningObject"/></returns>
        internal override DbConnectionInternal ReplaceConnection(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
        {
            //TODO
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.

        }

        internal override void ReturnInternalConnection(DbConnectionInternal connector, object? owningObject)
        {
            // Once a connection is closing (which is the state that we're in at
            // this point in time) you cannot delegate a transaction to or enlist
            // a transaction in it, so we can correctly presume that if there was
            // not a delegated or enlisted transaction to start with, that there
            // will not be a delegated or enlisted transaction once we leave the
            // lock.

            lock (connector)
            {
                // Calling PrePush prevents the object from being reclaimed
                // once we leave the lock, because it sets _pooledCount such
                // that it won't appear to be out of the pool.  What that
                // means, is that we're now responsible for this connection:
                // it won't get reclaimed if it gets lost.
                connector.PrePush(owningObject);

                // TODO: Consider using a Cer to ensure that we mark the object for reclaimation in the event something bad happens?
            }

            //TODO: verify transaction state

            if (!CheckConnector(connector))
            {
                return;
            }

            connector.DeactivateConnection();

            // Statement order is important since we have synchronous completions on the channel.
            Interlocked.Increment(ref _idleCount);
            var written = _idleConnectorWriter.TryWrite(connector);
            Debug.Assert(written);
        }

        internal override void PutObjectFromTransactedPool(DbConnectionInternal obj)
        {
            //TODO
        }

        internal override void Startup()
        {
            WarmUp();
        }

        internal override void Shutdown()
        {
            // NOTE: this occupies a thread for the whole duration of the shutdown process.
            var shutdownTask = new Task(async () => await ShutdownAsync());
            shutdownTask.RunSynchronously();
        }

        // TransactionEnded merely provides the plumbing for DbConnectionInternal to access the transacted pool
        //   that is implemented inside DbConnectionPool. This method's counterpart (PutTransactedObject) should
        //   only be called from DbConnectionPool.DeactivateObject and thus the plumbing to provide access to 
        //   other objects is unnecessary (hence the asymmetry of Ended but no Begin)
        internal override void TransactionEnded(Transaction transaction, DbConnectionInternal transactedObject)
        {
            //TODO
        }
        #endregion

        #region Implementation
        private readonly DbConnectionPoolIdentity _identity;

        private readonly DbConnectionFactory _connectionFactory;
        private readonly DbConnectionPoolGroup _connectionPoolGroup;
        private readonly DbConnectionPoolGroupOptions _connectionPoolGroupOptions;
        private DbConnectionPoolProviderInfo _connectionPoolProviderInfo;

        /// <summary>
        /// The private member which carries the set of authenticationcontexts for this pool (based on the user's identity).
        /// </summary>
        private readonly ConcurrentDictionary<DbConnectionPoolAuthenticationContextKey, DbConnectionPoolAuthenticationContext> _pooledDbAuthenticationContexts;

        // Prevents synchronous operations from blocking on all available threads,
        // which would stop async tasks from being scheduled and cause deadlocks.
        // Use ProcessorCount/2 as a balance between sync and async tasks.
        private static SemaphoreSlim SyncOverAsyncSemaphore { get; } = new(Math.Max(1, Environment.ProcessorCount / 2));

        private static int _objectTypeCount; // EventSource counter
        private static TimeSpan DefaultPruningPeriod = TimeSpan.FromMinutes(2);
        private static TimeSpan MinIdleCountPeriod = TimeSpan.FromSeconds(1);

        #region private readonly
        private readonly int _objectID = Interlocked.Increment(ref _objectTypeCount);
        private readonly RateLimiterBase _connectionRateLimiter;
        //TODO: readonly TimeSpan _connectionLifetime;

        /// <summary>
        /// Tracks all connectors currently managed by this pool, whether idle or busy.
        /// Only updated rarely - when physical connections are opened/closed - but is read in perf-sensitive contexts.
        /// </summary>
        private readonly DbConnectionInternal?[] _connectors;

        /// <summary>
        /// Reader side for the idle connector channel. Contains nulls in order to release waiting attempts after
        /// a connector has been physically closed/broken.
        /// </summary>
        private readonly ChannelReader<DbConnectionInternal?> _idleConnectorReader;
        private readonly ChannelWriter<DbConnectionInternal?> _idleConnectorWriter;

        private readonly CancellationTokenSource _shutdownCTS;
        private readonly CancellationToken _shutdownCT;

        private Task _warmupTask;
        private readonly SemaphoreSlim _warmupLock;


        private readonly Timer _pruningTimer;
        private readonly Timer _minIdleCountTimer;
        private readonly SemaphoreSlim _pruningLock;

        internal int _minIdleCount;
        internal Timer PruningTimer => _pruningTimer;
        internal Timer MinIdleCountTimer => _minIdleCountTimer;
        internal Task PruningTask { get; set; }
        internal SemaphoreSlim PruningLock => _pruningLock;
        #endregion

        // Counts the total number of open connectors tracked by the pool.
        private volatile int _numConnectors;

        // Counts the number of connectors currently sitting idle in the pool.
        private volatile int _idleCount;

        /// <summary>
        /// Incremented every time this pool is cleared. Allows us to identify connections which were
        /// created before the clear.
        /// </summary>
        private volatile int _clearCounter;
        private volatile int _isClearing;

        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        //TODO: support auth contexts and provider info
        internal BetterSyncPool(
            DbConnectionFactory connectionFactory,
            DbConnectionPoolGroup connectionPoolGroup,
            DbConnectionPoolIdentity identity,
            DbConnectionPoolProviderInfo connectionPoolProviderInfo,
            RateLimiterBase connectionRateLimiter)
        {
            State = Initializing;

            _connectionRateLimiter = connectionRateLimiter;
            _connectionFactory = connectionFactory;
            _connectionPoolGroup = connectionPoolGroup;
            _connectionPoolGroupOptions = connectionPoolGroup.PoolGroupOptions;
            _connectionPoolProviderInfo = connectionPoolProviderInfo;
            _identity = identity;
            _pooledDbAuthenticationContexts = new ConcurrentDictionary<
                DbConnectionPoolAuthenticationContextKey, 
                DbConnectionPoolAuthenticationContext>(
                    concurrencyLevel: 4 * Environment.ProcessorCount,
                    capacity: 2);

            _connectors = new DbConnectionInternal[MaxPoolSize];

            // We enforce Max Pool Size, so no need to to create a bounded channel (which is less efficient)
            // On the consuming side, we have the multiplexing write loop but also non-multiplexing Rents
            // On the producing side, we have connections being released back into the pool (both multiplexing and not)
            var idleChannel = Channel.CreateUnbounded<DbConnectionInternal?>();
            _idleConnectorReader = idleChannel.Reader;
            _idleConnectorWriter = idleChannel.Writer;

            _shutdownCTS = new CancellationTokenSource();
            _shutdownCT = _shutdownCTS.Token;

            _warmupTask = Task.CompletedTask;
            _warmupLock = new SemaphoreSlim(1);

            _pruningTimer = new Timer(PruneIdleConnections, this, DefaultPruningPeriod, DefaultPruningPeriod);

            _minIdleCount = int.MaxValue;

            // TODO: make these private readonly if possible
            // TODO: base pruning timer on a user provided param?
            _minIdleCountTimer = new Timer(UpdateMinIdleCount, this, MinIdleCountPeriod, MinIdleCountPeriod);
            _pruningLock = new SemaphoreSlim(1);
            PruningTask = Task.CompletedTask;

            State = Running;
        }

        #region properties
        internal TimeSpan ConnectionLifetime => PoolGroupOptions.LoadBalanceTimeout;
        internal int ObjectID => _objectID;
        internal bool IsWarmupEnabled { get; set; } = true;

#if NETFRAMEWORK
        internal override DbConnectionPoolCounters PerformanceCounters => throw new NotImplementedException();
#endif
#endregion


        /// <inheritdoc/>
        internal async Task<DbConnectionInternal> GetInternalConnection(DbConnection owningConnection, DbConnectionOptions userOptions, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            DbConnectionInternal? connector = GetIdleConnector();
            if (connector != null)
            {
                // TODO: transactions
                return connector;
            }

            // First, try to open a new physical connector. This will fail if we're at max capacity.
            connector = await OpenNewInternalConnection(owningConnection, userOptions, timeout, async, cancellationToken).ConfigureAwait(false);
            if (connector != null)
            {
                // TODO: transactions
                return connector;
            }

            // We're at max capacity. Block on the idle channel with a timeout.
            // Note that Channels guarantee fair FIFO behavior to callers of ReadAsync (first-come first-
            // served), which is crucial to us.
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken finalToken = linkedSource.Token;
            linkedSource.CancelAfter(timeout);
            //TODO: respect remaining time, linkedSource.CancelAfter(timeout.CheckAndGetTimeLeft());

            try
            {
                while (true)
                {
                    try
                    {
                        if (async)
                        {
                            connector = await _idleConnectorReader.ReadAsync(finalToken).ConfigureAwait(false);
                        }
                        else
                        {
                            SyncOverAsyncSemaphore.Wait(finalToken);
                            try
                            {
                                ConfiguredValueTaskAwaitable<DbConnectionInternal?>.ConfiguredValueTaskAwaiter awaiter =
                                    _idleConnectorReader.ReadAsync(finalToken).ConfigureAwait(false).GetAwaiter();
                                using ManualResetEventSlim mres = new ManualResetEventSlim(false, 0);

                                // Cancellation happens through the ReadAsync call, which will complete the task.
                                awaiter.UnsafeOnCompleted(() => mres.Set());
                                mres.Wait(CancellationToken.None);
                                connector = awaiter.GetResult();
                            }
                            finally
                            {
                                SyncOverAsyncSemaphore.Release();
                            }
                        }

                        if (connector != null && CheckIdleConnector(connector))
                        {
                            // TODO: transactions
                            return connector;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Debug.Assert(finalToken.IsCancellationRequested);

                        throw ADP.PooledOpenTimeout();
                    }
                    catch (ChannelClosedException)
                    {
                        //TODO: exceptions from resource file
                        throw new Exception("The connection pool has been shut down.");
                    }

                    // If we're here, our waiting attempt on the idle connector channel was released with a null
                    // (or bad connector), or we're in sync mode. Check again if a new idle connector has appeared since we last checked.
                    connector = GetIdleConnector();
                    if (connector != null)
                    {
                        // TODO: transactions
                        connector.ActivateConnection(null);
                        return connector;
                    }

                    // We might have closed a connector in the meantime and no longer be at max capacity
                    // so try to open a new connector and if that fails, loop again.
                    connector = await OpenNewInternalConnection(owningConnection, userOptions, timeout, async, cancellationToken).ConfigureAwait(false);
                    if (connector != null)
                    {
                        // TODO: transactions
                        connector.ActivateConnection(null);
                        return connector;
                    }
                }
            }
            finally
            {
                //TODO: log error
            }
        }

        /// <summary>
        /// Tries to read a connector from the idle connector channel.
        /// </summary>
        /// <returns>Returns true if a valid idles connector is found, otherwise returns false.</returns>
        /// TODO: profile the inlining to see if it's necessary
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DbConnectionInternal? GetIdleConnector()
        {

            while (_idleConnectorReader.TryRead(out DbConnectionInternal? connector))
            {
                if (CheckIdleConnector(connector))
                {
                    return connector;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks that the provided connector is live and unexpired and closes it if needed.
        /// Decrements the idle count as long as the connector is not null.
        /// </summary>
        /// <param name="connector">The connector to be checked.</param>
        /// <returns>Returns true if the connector is live and unexpired, otherwise returns false.</returns>
        /// TODO: profile the inlining to see if it's necessary
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckIdleConnector(DbConnectionInternal? connector)
        {
            if (connector is null)
            {
                return false;
            }

            // Only decrement when the connector has a value.
            Interlocked.Decrement(ref _idleCount);

            return CheckConnector(connector);
        }

        /// <summary>
        /// Checks that the provided connector is live and unexpired and closes it if needed.
        /// </summary>
        /// <param name="connector"></param>
        /// <returns>Returns true if the connector is live and unexpired, otherwise returns false.</returns>
        private bool CheckConnector(DbConnectionInternal connector)
        {
            // If Clear/ClearAll has been been called since this connector was first opened,
            // throw it away. The same if it's broken (in which case CloseConnector is only
            // used to update state/perf counter).
            //TODO: check clear counter

            // An connector could be broken because of a keepalive that occurred while it was
            // idling in the pool
            // TODO: Consider removing the pool from the keepalive code. The following branch is simply irrelevant
            // if keepalive isn't turned on.
            if (!connector.IsConnectionAlive())
            {
                CloseConnector(connector);
                return false;
            }

            if (ConnectionLifetime != TimeSpan.Zero && DateTime.UtcNow > connector.OpenTimestamp + ConnectionLifetime)
            {
                CloseConnector(connector);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Closes the provided connector and adjust pool state accordingly.
        /// </summary>
        /// <param name="connector">The connector to be closed.</param>
        private void CloseConnector(DbConnectionInternal connector)
        {
            try
            {
                connector.Dispose();
            }
            catch
            {
                //TODO: log error
            }

            // TODO: check clear counter so that we don't clear new connections

            int i;
            for (i = 0; i < MaxPoolSize; i++)
            {
                if (Interlocked.CompareExchange(ref _connectors[i], null, connector) == connector)
                {
                    break;
                }
            }

            // If CloseConnector is being called from within OpenNewConnector (e.g. an error happened during a connection initializer which
            // causes the connector to Break, and therefore return the connector), then we haven't yet added the connector to Connectors.
            // In this case, there's no state to revert here (that's all taken care of in OpenNewConnector), skip it.
            if (i == MaxPoolSize)
            {
                return;
            }

            var numConnectors = Interlocked.Decrement(ref _numConnectors);
            Debug.Assert(numConnectors >= 0);

            // If a connector has been closed for any reason, we write a null to the idle connector channel to wake up
            // a waiter, who will open a new physical connection
            // Statement order is important since we have synchronous completions on the channel.
            _idleConnectorWriter.TryWrite(null);

            // Ensure that we return to min pool size if closing this connector brought us below min pool size.
            WarmUp();
        }

        /// <summary>
        /// A state object used to pass context to the rate limited connector creation operation.
        /// </summary>
        internal readonly struct OpenInternalConnectionState
        {
            internal OpenInternalConnectionState(
                BetterSyncPool pool,
                DbConnection? owningConnection,
                DbConnectionOptions userOptions,
                TimeSpan timeout)
            {
                Pool = pool;
                OwningConnection = owningConnection;
                UserOptions = userOptions;
                Timeout = timeout;
            }

            internal readonly BetterSyncPool Pool;
            internal readonly DbConnection? OwningConnection;
            internal readonly DbConnectionOptions UserOptions;
            internal readonly TimeSpan Timeout;
        }

        /// <inheritdoc/>
        internal Task<DbConnectionInternal?> OpenNewInternalConnection(DbConnection? owningConnection, DbConnectionOptions userOptions, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            return _connectionRateLimiter.Execute(
                RateLimitedOpen,
                new OpenInternalConnectionState(
                    pool: this,
                    owningConnection: owningConnection,
                    userOptions: userOptions,
                    timeout: timeout
                ),
                async,
                cancellationToken
            );


            static Task<DbConnectionInternal?> RateLimitedOpen(OpenInternalConnectionState state, bool async, CancellationToken cancellationToken)
            {
                // As long as we're under max capacity, attempt to increase the connector count and open a new connection.
                for (var numConnectors = state.Pool._numConnectors; numConnectors < state.Pool.MaxPoolSize; numConnectors = state.Pool._numConnectors)
                {
                    // Note that we purposefully don't use SpinWait for this: https://github.com/dotnet/coreclr/pull/21437
                    if (Interlocked.CompareExchange(ref state.Pool._numConnectors, numConnectors + 1, numConnectors) != numConnectors)
                    {
                        continue;
                    }

                    try
                    {
                        // We've managed to increase the open counter, open a physical connection.
                        var startTime = Stopwatch.GetTimestamp();

                        // TODO: This blocks the thread for several network calls!
                        // This will be unexpected to async callers.
                        // Our options are limited because DbConnectionInternal doesn't support async open.
                        // It's better to block this thread and keep throughput high than to queue all of our opens onto a single worker thread.
                        // Add an async path when this support is added to DbConnectionInternal.
                        DbConnectionInternal? newConnection = state.Pool.ConnectionFactory.CreatePooledConnection(
                            state.Pool,
                            state.OwningConnection,
                            state.Pool._connectionPoolGroup.ConnectionOptions,
                            state.Pool._connectionPoolGroup.PoolKey,
                            state.UserOptions);

                        if (newConnection == null)
                        {
                            throw ADP.InternalError(ADP.InternalErrorCode.CreateObjectReturnedNull);    // CreateObject succeeded, but null object
                        }
                        if (!newConnection.CanBePooled)
                        {
                            throw ADP.InternalError(ADP.InternalErrorCode.NewObjectCannotBePooled);        // CreateObject succeeded, but non-poolable object
                        }

                        newConnection.PrePush(null);

                        int i;
                        for (i = 0; i < state.Pool.MaxPoolSize; i++)
                        {
                            if (Interlocked.CompareExchange(ref state.Pool._connectors[i], newConnection, null) == null)
                            {
                                break;
                            }
                        }

                        Debug.Assert(i < state.Pool.MaxPoolSize, $"Could not find free slot in {state.Pool._connectors} when opening.");
                        if (i == state.Pool.MaxPoolSize)
                        {
                            //TODO: generic exception?
                            throw new Exception($"Could not find free slot in {state.Pool._connectors} when opening. Please report a bug.");
                        }

                        return Task.FromResult<DbConnectionInternal?>(newConnection);
                    }
                    catch
                    {
                        // Physical open failed, decrement the open and busy counter back down.
                        Interlocked.Decrement(ref state.Pool._numConnectors);

                        // In case there's a waiting attempt on the channel, we write a null to the idle connector channel
                        // to wake it up, so it will try opening (and probably throw immediately)
                        // Statement order is important since we have synchronous completions on the channel.
                        state.Pool._idleConnectorWriter.TryWrite(null);

                        throw;
                    }
                }

                return Task.FromResult<DbConnectionInternal?>(null);
            }
        }

        /// <summary>
        /// Initiates a task to prune idle connections from the pool.
        /// </summary>
        internal void PruneIdleConnections(object? state)
        {
            _shutdownCT.ThrowIfCancellationRequested();

            if (State is ShuttingDown || !PruningTask.IsCompleted || !PruningLock.Wait(0))
            {
                return;
            }

            try
            {
                if (PruningTask.IsCompleted && State is Running)
                {
                    PruningTask = _PruneIdleConnections();
                }
            }
            finally
            {
                PruningLock.Release();
            }

            return;

            async Task _PruneIdleConnections()
            {
                try
                {
                    int numConnectionsToPrune = _minIdleCount;

                    // Reset _minIdleCount for the next pruning period
                    _minIdleCount = int.MaxValue;

                    // If we don't stop on null, we might cycle a bit?
                    // we might read out all of the nulls we just wrote into the channel
                    // that might not be bad...
                    while (numConnectionsToPrune > 0 &&
                           _numConnectors > MinPoolSize &&
                           _idleConnectorReader.TryRead(out var connector))
                    {
                        _shutdownCT.ThrowIfCancellationRequested();

                        if (connector == null)
                        {
                            continue;
                        }

                        if (CheckIdleConnector(connector))
                        {
                            CloseConnector(connector);
                        }

                        numConnectionsToPrune--;
                    }
                }
                catch
                {
                    // TODO: log exception
                }

                // Min pool size check above is best effort and may over prune.
                // Ensure warmup runs to bring us back up to min pool size if necessary.
                await WarmUp();
            }
        }

        /// <summary>
        /// Periodically checks the current idle connector count to maintain a minimum idle connector count.
        /// Runs indefinitely until the timer is disposed or a cancellation is indicated on the pool shutdown
        /// cancellation token.
        /// </summary>
        /// <returns>A ValueTask tracking this operation.</returns>
        internal void UpdateMinIdleCount(object? state)
        {
            _shutdownCT.ThrowIfCancellationRequested();

            try
            {
                if (State is not Running)
                {
                    return;
                }
                try
                {
                    int currentMinIdle;
                    int currentIdle;
                    do
                    {
                        currentMinIdle = _minIdleCount;
                        currentIdle = _idleCount;
                        if (currentIdle >= currentMinIdle)
                        {
                            break;
                        }
                    }
                    while (Interlocked.CompareExchange(ref _minIdleCount, currentIdle, currentMinIdle) != currentMinIdle);
                }
                catch
                {
                    // TODO: log exception
                }
            } catch (OperationCanceledException)
            {
                // TODO: log here?
            }
        }

        /// <summary>
        /// Warms up the pool by bringing it up to min pool size.
        /// We may await the underlying operation multiple times, so we need to use Task
        /// in place of ValueTask so that it cannot be recycled.
        /// </summary>
        /// <returns>A ValueTask containing a Task that represents the warmup process.</returns>
        internal Task WarmUp()
        {
            if (State is ShuttingDown || !IsWarmupEnabled)
            {
                return Task.CompletedTask;
            }

            // Avoid semaphore wait if task is still running
            if (!_warmupTask.IsCompleted || !_warmupLock.Wait(0))
            {
                return _warmupTask;
            }

            try
            {
                // The task may have been started by another thread while we were
                // waiting on the semaphore
                if (_warmupTask.IsCompleted && State is Running)
                {
                    _warmupTask = _WarmUp(_shutdownCT);
                }
            }
            finally
            {
                _warmupLock.Release();
            }

            return _warmupTask;

            async Task _WarmUp(CancellationToken ct)
            {
                // Best effort, we may over or under create due to race conditions.
                // Open new connections slowly. If many connections are needed immediately 
                // upon pool creation they can always be created via user-initiated requests as fast
                // as a parallel, pool-initiated approach could.
                while (_numConnectors < MinPoolSize)
                {
                    ct.ThrowIfCancellationRequested();

                    // Obey the same rate limit as user-initiated opens.
                    // Ensures that pool-initiated opens are queued properly alongside user requests.
                    DbConnectionInternal? connector = await OpenNewInternalConnection(
                        null,
                        // connections opened by the pool use the pool groups options in place of user provided options
                        _connectionPoolGroup.ConnectionOptions,
                        ConnectionLifetime,
                        true,
                        ct)
                        .ConfigureAwait(false);

                    // If connector is null, then we hit the max pool size and can stop
                    // warming up the pool.
                    if (connector == null)
                    {
                        return;
                    }

                    // The connector has never been used, so it's safe to immediately return it to the
                    // pool without resetting it.
                    ReturnInternalConnection(connector, null);
                }
            }
        }

        /// <summary>
        /// Shutsdown the pool and disposes pool resources.
        /// </summary>
        internal async Task ShutdownAsync()
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.Shutdown|RES|INFO|CPOOL> {0}", ObjectID);

            State = ShuttingDown;

            // Cancel background tasks
            _shutdownCTS.Cancel();
            await Task.WhenAll(
                PruningTask,
                _warmupTask);

            // Clean pool state
            Clear();

            // Handle disposable resources
            _shutdownCTS.Dispose();
            _warmupLock.Dispose();
            PruningTimer.Dispose();
            MinIdleCountTimer.Dispose();
            _connectionRateLimiter?.Dispose();
        }

        // TODO: override clear method
        #endregion
    }
}
