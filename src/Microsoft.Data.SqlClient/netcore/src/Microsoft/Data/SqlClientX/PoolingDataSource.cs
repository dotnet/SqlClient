// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.RateLimiters;

#nullable enable

namespace Microsoft.Data.SqlClientX
{
    /// <summary>
    /// A pooling implementation of SqlDataSource. Connections are recycled upon return to be reused by another operation.
    /// </summary>
    internal sealed class PoolingDataSource : SqlDataSource
    {
        // Prevents synchronous operations from blocking on all available threads,
        // which would stop async tasks from being scheduled and cause deadlocks.
        // Use ProcessorCount/2 as a balance between sync and async tasks.
        private static SemaphoreSlim SyncOverAsyncSemaphore { get; } = new(Math.Max(1, Environment.ProcessorCount / 2));

        private static int _objectTypeCount; // EventSource counter

        #region private readonly
        private readonly int _objectID = Interlocked.Increment(ref _objectTypeCount);
        private readonly DbConnectionPoolGroupOptions _connectionPoolGroupOptions;
        private readonly RateLimiterBase _connectionRateLimiter;
        //TODO: readonly TimeSpan _connectionLifetime;

        /// <summary>
        /// Tracks all connectors currently managed by this pool, whether idle or busy.
        /// Only updated rarely - when physical connections are opened/closed - but is read in perf-sensitive contexts.
        /// </summary>
        private readonly SqlConnector?[] _connectors;

        /// <summary>
        /// Reader side for the idle connector channel. Contains nulls in order to release waiting attempts after
        /// a connector has been physically closed/broken.
        /// </summary>
        private readonly ChannelReader<SqlConnector?> _idleConnectorReader;
        private readonly ChannelWriter<SqlConnector?> _idleConnectorWriter;
        #endregion

        // Counts the total number of open connectors tracked by the pool.
        private volatile int _numConnectors;

        // Counts the number of connectors currently sitting idle in the pool.
        private volatile int _idleCount;

        /// <summary>
        /// Initializes a new PoolingDataSource.
        /// </summary>
        //TODO: support auth contexts and provider info
        internal PoolingDataSource(SqlConnectionStringBuilder connectionStringBuilder,
            SqlCredential credential,
            DbConnectionPoolGroupOptions options,
            RateLimiterBase connectionRateLimiter)
            : base(connectionStringBuilder, credential)
        {
            _connectionPoolGroupOptions = options;
            _connectionRateLimiter = connectionRateLimiter;
            _connectors = new SqlConnector[MaxPoolSize];

            // We enforce Max Pool Size, so no need to to create a bounded channel (which is less efficient)
            // On the consuming side, we have the multiplexing write loop but also non-multiplexing Rents
            // On the producing side, we have connections being released back into the pool (both multiplexing and not)
            var idleChannel = Channel.CreateUnbounded<SqlConnector?>();
            _idleConnectorReader = idleChannel.Reader;
            _idleConnectorWriter = idleChannel.Writer;

            //TODO: initiate idle lifetime and pruning fields
        }

        #region properties
        internal int MinPoolSize => _connectionPoolGroupOptions.MinPoolSize;
        internal int MaxPoolSize => _connectionPoolGroupOptions.MaxPoolSize;
        internal int ObjectID => _objectID;

        internal sealed override (int Total, int Idle, int Busy) Statistics
        {
            get
            {
                var numConnectors = _numConnectors;
                var idleCount = _idleCount;
                return (numConnectors, idleCount, numConnectors - idleCount);
            }
        }
        #endregion

        /// <inheritdoc/>
        internal override async ValueTask<SqlConnector> GetInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            CheckDisposed();

            if (TryGetIdleConnector(out SqlConnector? connector))
            {
                return connector;
            }

            // First, try to open a new physical connector. This will fail if we're at max capacity.
            connector = await OpenNewInternalConnection(owningConnection, timeout, async, cancellationToken).ConfigureAwait(false);
            if (connector != null)
            {
                return connector;
            }

            // We're at max capacity. Block on the idle channel with a timeout.
            // Note that Channels guarantee fair FIFO behavior to callers of ReadAsync (first-come first-
            // served), which is crucial to us.
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken finalToken = linkedSource.Token;
            linkedSource.CancelAfter(timeout);
            //TODO: respect remaining time, linkedSource.CancelAfter(timeout.CheckAndGetTimeLeft());
            //TODO: MetricsReporter.ReportPendingConnectionRequestStart();

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
                                ConfiguredValueTaskAwaitable<SqlConnector?>.ConfiguredValueTaskAwaiter awaiter =
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

                        if (CheckIdleConnector(connector))
                        {
                            return connector;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Debug.Assert(finalToken.IsCancellationRequested);

                        //TODO: MetricsReporter.ReportConnectionPoolTimeout();
                        /*TODO: throw new NpgsqlException(
                            $"The connection pool has been exhausted, either raise 'Max Pool Size' (currently {MaxConnections}) " +
                            $"or 'Timeout' (currently {Settings.Timeout} seconds) in your connection string.",
                            new TimeoutException());*/
                        throw new Exception("Pool exhausted", new TimeoutException());
                    }
                    catch (ChannelClosedException)
                    {
                        //throw new NpgsqlException("The connection pool has been shut down.");
                        throw new Exception("The connection pool has been shut down.");
                    }

                    // If we're here, our waiting attempt on the idle connector channel was released with a null
                    // (or bad connector), or we're in sync mode. Check again if a new idle connector has appeared since we last checked.
                    if (TryGetIdleConnector(out connector))
                    {
                        return connector;
                    }

                    // We might have closed a connector in the meantime and no longer be at max capacity
                    // so try to open a new connector and if that fails, loop again.
                    connector = await OpenNewInternalConnection(owningConnection, timeout, async, cancellationToken).ConfigureAwait(false);
                    if (connector != null)
                    {
                        return connector;
                    }
                }
            }
            finally
            {
                //TODO: MetricsReporter.ReportPendingConnectionRequestStop();
            }
        }

        /// <summary>
        /// Tries to read a connector from the idle connector channel.
        /// </summary>
        /// <param name="connector">Out parameter to store the connector result.</param>
        /// <returns>Returns true if a valid idles connector is found, otherwise returns false.</returns>
        /// TODO: profile the inlining to see if it's necessary
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetIdleConnector([NotNullWhen(true)] out SqlConnector? connector)
        {
            while (_idleConnectorReader.TryRead(out connector))
            {
                if (CheckIdleConnector(connector))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks that the provided connector is live and unexpired.
        /// If true, indicates that the connector may be returned by the pool.
        /// </summary>
        /// <param name="connector">The connector to be checked.</param>
        /// <returns>Returns true if the connector is live and unexpired, otherwise returns false.</returns>
        /// TODO: profile the inlining to see if it's necessary
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckIdleConnector([NotNullWhen(true)] SqlConnector? connector)
        {
            if (connector is null)
            {
                return false;
            }

            // Only decrement when the connector has a value.
            Interlocked.Decrement(ref _idleCount);

            // An connector could be broken because of a keepalive that occurred while it was
            // idling in the pool
            // TODO: Consider removing the pool from the keepalive code. The following branch is simply irrelevant
            // if keepalive isn't turned on.
            if (connector.IsBroken)
            {
                CloseConnector(connector);
                return false;
            }

            /* TODO: enforce connection lifetime
            if (_connectionLifetime != TimeSpan.Zero && DateTime.UtcNow > connector.OpenTimestamp + _connectionLifetime)
            {
                LogMessages.ConnectionExceededMaximumLifetime(_logger, _connectionLifetime, connector.Id);
                CloseConnector(connector);
                return false;
            }

            // The connector directly references the data source type mapper into the connector, to protect it against changes by a concurrent
            // ReloadTypes. We update them here before returning the connector from the pool.
            Debug.Assert(SerializerOptions is not null);
            Debug.Assert(DatabaseInfo is not null);
            connector.SerializerOptions = SerializerOptions;
            connector.DatabaseInfo = DatabaseInfo;

            Debug.Assert(connector.State == ConnectorState.Ready,
                $"Got idle connector but {nameof(connector.State)} is {connector.State}");
            Debug.Assert(connector.CommandsInFlightCount == 0,
                $"Got idle connector but {nameof(connector.CommandsInFlightCount)} is {connector.CommandsInFlightCount}");
            Debug.Assert(connector.MultiplexAsyncWritingLock == 0,
                $"Got idle connector but {nameof(connector.MultiplexAsyncWritingLock)} is 1");
            */
            return true;
        }

        /// <summary>
        /// Closes the provided connector and adjust pool state accordingly.
        /// </summary>
        /// <param name="connector">The connector to be closed.</param>
        private void CloseConnector(SqlConnector connector)
        {
            try
            {
                connector.Close();
            }
            catch
            {
                //TODO: LogMessages.ExceptionWhenClosingPhysicalConnection(_logger, connector.Id, exception);
            }


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

            // Only turn off the timer one time, when it was this Close that brought Open back to _min.
            //TODO: pruning
            //if (numConnectors == MinPoolSize)
            //  UpdatePruningTimer();
        }

        /// <summary>
        /// A state object used to pass context to the rate limited connector creation operation.
        /// </summary>
        internal readonly struct OpenInternalConnectionState
        {
            internal readonly SqlConnectionX _owningConnection;
            internal readonly TimeSpan _timeout;

            internal OpenInternalConnectionState(SqlConnectionX owningConnection, TimeSpan timeout)
            {
                _owningConnection = owningConnection;
                _timeout = timeout;
            }
        }

        /// <inheritdoc/>
        internal override ValueTask<SqlConnector?> OpenNewInternalConnection(SqlConnectionX owningConnection, TimeSpan timeout, bool async, CancellationToken cancellationToken)
        {
            return _connectionRateLimiter.Execute(
                RateLimitedOpen,
                new OpenInternalConnectionState(owningConnection, timeout),
                async,
                cancellationToken
            );

            ValueTask<SqlConnector?> RateLimitedOpen(OpenInternalConnectionState state, bool async, CancellationToken cancellationToken)
            {
                // As long as we're under max capacity, attempt to increase the connector count and open a new connection.
                for (var numConnectors = _numConnectors; numConnectors < MaxPoolSize; numConnectors = _numConnectors)
                {
                    // Note that we purposefully don't use SpinWait for this: https://github.com/dotnet/coreclr/pull/21437
                    if (Interlocked.CompareExchange(ref _numConnectors, numConnectors + 1, numConnectors) != numConnectors)
                    {
                        continue;
                    }

                    try
                    {
                        // We've managed to increase the open counter, open a physical connections.
#if NET7_0_OR_GREATER
                        var startTime = Stopwatch.GetTimestamp();
#endif
                        SqlConnector? connector = new SqlConnector(state._owningConnection, this);
                        //TODO: set clear counter on connector

                        //TODO: actually open the connector
                        //await connector.Open(timeout, async, cancellationToken).ConfigureAwait(false);
#if NET7_0_OR_GREATER
                        //TODO: MetricsReporter.ReportConnectionCreateTime(Stopwatch.GetElapsedTime(startTime));
#endif

                        int i;
                        for (i = 0; i < MaxPoolSize; i++)
                        {
                            if (Interlocked.CompareExchange(ref _connectors[i], connector, null) == null)
                            {
                                break;
                            }
                        }

                        Debug.Assert(i < MaxPoolSize, $"Could not find free slot in {_connectors} when opening.");
                        if (i == MaxPoolSize)
                        {
                            //TODO: generic exception?
                            throw new Exception($"Could not find free slot in {_connectors} when opening. Please report a bug.");
                        }

                        // Only start pruning if we've incremented open count past _min.
                        // Note that we don't do it only once, on equality, because the thread which incremented open count past _min might get exception
                        // on NpgsqlConnector.Open due to timeout, CancellationToken or other reasons.
                        //TODO:
                        //if (numConnectors >= MinConnections)
                        //  UpdatePruningTimer();

                        return ValueTask.FromResult<SqlConnector?>(connector);
                    }
                    catch
                    {
                        // Physical open failed, decrement the open and busy counter back down.
                        Interlocked.Decrement(ref _numConnectors);

                        // In case there's a waiting attempt on the channel, we write a null to the idle connector channel
                        // to wake it up, so it will try opening (and probably throw immediately)
                        // Statement order is important since we have synchronous completions on the channel.
                        _idleConnectorWriter.TryWrite(null);

                        // Just in case we always call UpdatePruningTimer for failed physical open
                        //TODO: UpdatePruningTimer();

                        throw;
                    }
                }

                return ValueTask.FromResult<SqlConnector?>(null);
            }
        }

        /// <inheritdoc/>
        internal override void ReturnInternalConnection(SqlConnector connector)
        {

            /*TODO: verify transaction state
            Debug.Assert(!connector.InTransaction);
            Debug.Assert(connector.MultiplexAsyncWritingLock == 0 || connector.IsBroken || connector.IsClosed,
                $"About to return multiplexing connector to the pool, but {nameof(connector.MultiplexAsyncWritingLock)} is {connector.MultiplexAsyncWritingLock}");
            */

            // If Clear/ClearAll has been been called since this connector was first opened,
            // throw it away. The same if it's broken (in which case CloseConnector is only
            // used to update state/perf counter).
            //TODO: connector.ClearCounter != _clearCounter
            if (connector.IsBroken)
            {
                CloseConnector(connector);
                return;
            }

            // Statement order is important since we have synchronous completions on the channel.
            Interlocked.Increment(ref _idleCount);
            var written = _idleConnectorWriter.TryWrite(connector);
            Debug.Assert(written);
        }

        /// <summary>
        /// Closes extra idle connections.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void PruneIdleConnections()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Warms up the pool to bring it up to min pool size.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void WarmUp()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Shutsdown the pool and disposes pool resources.
        /// </summary>
        internal void Shutdown()
        {
            SqlClientEventSource.Log.TryPoolerTraceEvent("<prov.DbConnectionPool.Shutdown|RES|INFO|CPOOL> {0}", ObjectID);
            _connectionRateLimiter?.Dispose();
        }

        /* TODO: implement clear
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
        }*/
    }
}

#endif
