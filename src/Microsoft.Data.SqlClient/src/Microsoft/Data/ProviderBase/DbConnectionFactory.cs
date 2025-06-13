// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.ConnectionPool;

namespace Microsoft.Data.ProviderBase
{
    internal abstract class DbConnectionFactory
    {
        internal abstract DbConnectionPoolGroupProviderInfo CreateConnectionPoolGroupProviderInfo(
            DbConnectionOptions connectionOptions);

        internal abstract DbConnectionPoolProviderInfo CreateConnectionPoolProviderInfo(
            DbConnectionOptions connectionOptions);

        protected virtual DbMetaDataFactory CreateMetaDataFactory(DbConnectionInternal internalConnection, out bool cacheMetaDataFactory)
        {
            // providers that support GetSchema must override this with a method that creates a meta data
            // factory appropriate for them.
            cacheMetaDataFactory = false;
            throw ADP.NotSupported();
        }
        
        protected DbConnectionOptions FindConnectionOptions(DbConnectionPoolKey key)
        {
            Debug.Assert(key != null, "key cannot be null");
            if (!string.IsNullOrEmpty(key.ConnectionString))
            {
                DbConnectionPoolGroup connectionPoolGroup;
                Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> connectionPoolGroups = _connectionPoolGroups;
                if (connectionPoolGroups.TryGetValue(key, out connectionPoolGroup))
                {
                    return connectionPoolGroup.ConnectionOptions;
                }
            }
            return null;
        }

        private static Task<DbConnectionInternal> GetCompletedTask()
        {
            Debug.Assert(Monitor.IsEntered(s_pendingOpenNonPooled), $"Expected {nameof(s_pendingOpenNonPooled)} lock to be held.");
            return s_completedTask ?? (s_completedTask = Task.FromResult<DbConnectionInternal>(null));
        }

        internal bool TryGetConnection(DbConnection owningConnection, TaskCompletionSource<DbConnectionInternal> retry, DbConnectionOptions userOptions, DbConnectionInternal oldConnection, out DbConnectionInternal connection)
        {
            Debug.Assert(owningConnection != null, "null owningConnection?");

            DbConnectionPoolGroup poolGroup;
            IDbConnectionPool connectionPool;
            connection = null;

            //  Work around race condition with clearing the pool between GetConnectionPool obtaining pool 
            //  and GetConnection on the pool checking the pool state.  Clearing the pool in this window
            //  will switch the pool into the ShuttingDown state, and GetConnection will return null.
            //  There is probably a better solution involving locking the pool/group, but that entails a major
            //  re-design of the connection pooling synchronization, so is postponed for now.

            // Use retriesLeft to prevent CPU spikes with incremental sleep
            // start with one msec, double the time every retry
            // max time is: 1 + 2 + 4 + ... + 2^(retries-1) == 2^retries -1 == 1023ms (for 10 retries)
            int retriesLeft = 10;
            int timeBetweenRetriesMilliseconds = 1;

            do
            {
                poolGroup = GetConnectionPoolGroup(owningConnection);
                // Doing this on the callers thread is important because it looks up the WindowsIdentity from the thread.
                connectionPool = GetConnectionPool(owningConnection, poolGroup);
                if (connectionPool == null)
                {
                    // If GetConnectionPool returns null, we can be certain that
                    // this connection should not be pooled via DbConnectionPool
                    // or have a disabled pool entry.
                    poolGroup = GetConnectionPoolGroup(owningConnection); // previous entry have been disabled

                    if (retry != null)
                    {
                        Task<DbConnectionInternal> newTask;
                        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                        lock (s_pendingOpenNonPooled)
                        {
                            // look for an available task slot (completed or empty)
                            int idx;
                            for (idx = 0; idx < s_pendingOpenNonPooled.Length; idx++)
                            {
                                Task task = s_pendingOpenNonPooled[idx];
                                if (task == null)
                                {
                                    s_pendingOpenNonPooled[idx] = GetCompletedTask();
                                    break;
                                }
                                else if (task.IsCompleted)
                                {
                                    break;
                                }
                            }

                            // if didn't find one, pick the next one in round-robin fashion
                            if (idx == s_pendingOpenNonPooled.Length)
                            {
                                idx = (int)(s_pendingOpenNonPooledNext % s_pendingOpenNonPooled.Length);
                                unchecked
                                {
                                    s_pendingOpenNonPooledNext++;
                                }
                            }

                            // now that we have an antecedent task, schedule our work when it is completed.
                            // If it is a new slot or a completed task, this continuation will start right away.
                            newTask = CreateReplaceConnectionContinuation(s_pendingOpenNonPooled[idx], owningConnection, retry, userOptions, oldConnection, poolGroup, cancellationTokenSource);

                            // Place this new task in the slot so any future work will be queued behind it
                            s_pendingOpenNonPooled[idx] = newTask;
                        }

                        // Set up the timeout (if needed)
                        if (owningConnection.ConnectionTimeout > 0)
                        {
                            int connectionTimeoutMilliseconds = owningConnection.ConnectionTimeout * 1000;
                            cancellationTokenSource.CancelAfter(connectionTimeoutMilliseconds);
                        }

                        // once the task is done, propagate the final results to the original caller
                        newTask.ContinueWith(
                            continuationAction: TryGetConnectionCompletedContinuation,
                            state: Tuple.Create(cancellationTokenSource, retry),
                            scheduler: TaskScheduler.Default
                        );

                        return false;
                    }

                    connection = CreateNonPooledConnection(owningConnection, poolGroup, userOptions);

                    SqlClientEventSource.Metrics.EnterNonPooledConnection();
                }
                else
                {
                    if (((SqlClient.SqlConnection)owningConnection).ForceNewConnection)
                    {
                        Debug.Assert(!(oldConnection is DbConnectionClosed), "Force new connection, but there is no old connection");
                        connection = connectionPool.ReplaceConnection(owningConnection, userOptions, oldConnection);
                    }
                    else
                    {
                        if (!connectionPool.TryGetConnection(owningConnection, retry, userOptions, out connection))
                        {
                            return false;
                        }
                    }

                    if (connection == null)
                    {
                        // connection creation failed on semaphore waiting or if max pool reached
                        if (connectionPool.IsRunning)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionFactory.GetConnection|RES|CPOOL> {0}, GetConnection failed because a pool timeout occurred.", ObjectID);
                            // If GetConnection failed while the pool is running, the pool timeout occurred.
                            throw ADP.PooledOpenTimeout();
                        }
                        else
                        {
                            // We've hit the race condition, where the pool was shut down after we got it from the group.
                            // Yield time slice to allow shut down activities to complete and a new, running pool to be instantiated
                            //  before retrying.
                            System.Threading.Thread.Sleep(timeBetweenRetriesMilliseconds);
                            timeBetweenRetriesMilliseconds *= 2; // double the wait time for next iteration
                        }
                    }
                }
            } while (connection == null && retriesLeft-- > 0);

            if (connection == null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionFactory.GetConnection|RES|CPOOL> {0}, GetConnection failed because a pool timeout occurred and all retries were exhausted.", ObjectID);
                // exhausted all retries or timed out - give up
                throw ADP.PooledOpenTimeout();
            }

            return true;
        }

        private Task<DbConnectionInternal> CreateReplaceConnectionContinuation(Task<DbConnectionInternal> task, DbConnection owningConnection, TaskCompletionSource<DbConnectionInternal> retry, DbConnectionOptions userOptions, DbConnectionInternal oldConnection, DbConnectionPoolGroup poolGroup, CancellationTokenSource cancellationTokenSource)
        {
            return task.ContinueWith(
                (_) =>
                {
                    System.Transactions.Transaction originalTransaction = ADP.GetCurrentTransaction();
                    try
                    {
                        ADP.SetCurrentTransaction(retry.Task.AsyncState as System.Transactions.Transaction);
                        var newConnection = CreateNonPooledConnection(owningConnection, poolGroup, userOptions);
                        if ((oldConnection != null) && (oldConnection.State == ConnectionState.Open))
                        {
                            oldConnection.PrepareForReplaceConnection();
                            oldConnection.Dispose();
                        }
                        return newConnection;
                    }
                    finally
                    {
                        ADP.SetCurrentTransaction(originalTransaction);
                    }
                },
                cancellationTokenSource.Token,
                TaskContinuationOptions.LongRunning,
                TaskScheduler.Default
            );
        }

        private void TryGetConnectionCompletedContinuation(Task<DbConnectionInternal> task, object state)
        {
            Tuple<CancellationTokenSource, TaskCompletionSource<DbConnectionInternal>> parameters = (Tuple<CancellationTokenSource, TaskCompletionSource<DbConnectionInternal>>)state;
            CancellationTokenSource source = parameters.Item1;
            source.Dispose();

            TaskCompletionSource<DbConnectionInternal> retryCompletionSource = parameters.Item2;

            if (task.IsCanceled)
            {
                retryCompletionSource.TrySetException(ADP.ExceptionWithStackTrace(ADP.NonPooledOpenTimeout()));
            }
            else if (task.IsFaulted)
            {
                retryCompletionSource.TrySetException(task.Exception.InnerException);
            }
            else
            {
                if (!retryCompletionSource.TrySetResult(task.Result))
                {
                    // The outer TaskCompletionSource was already completed
                    // Which means that we don't know if someone has messed with the outer connection in the middle of creation
                    // So the best thing to do now is to destroy the newly created connection
                    task.Result.DoomThisConnection();
                    task.Result.Dispose();
                }
                else
                {
                    SqlClientEventSource.Metrics.EnterNonPooledConnection();
                }
            }
        }

        private IDbConnectionPool GetConnectionPool(DbConnection owningObject, DbConnectionPoolGroup connectionPoolGroup)
        {
            // if poolgroup is disabled, it will be replaced with a new entry

            Debug.Assert(owningObject != null, "null owningObject?");
            Debug.Assert(connectionPoolGroup != null, "null connectionPoolGroup?");

            // It is possible that while the outer connection object has
            // been sitting around in a closed and unused state in some long
            // running app, the pruner may have come along and remove this
            // the pool entry from the master list.  If we were to use a
            // pool entry in this state, we would create "unmanaged" pools,
            // which would be bad.  To avoid this problem, we automagically
            // re-create the pool entry whenever it's disabled.

            // however, don't rebuild connectionOptions if no pooling is involved - let new connections do that work
            if (connectionPoolGroup.IsDisabled && connectionPoolGroup.PoolGroupOptions != null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionFactory.GetConnectionPool|RES|INFO|CPOOL> {0}, DisabledPoolGroup={1}", ObjectID, connectionPoolGroup?.ObjectID);

                // reusing existing pool option in case user originally used SetConnectionPoolOptions
                DbConnectionPoolGroupOptions poolOptions = connectionPoolGroup.PoolGroupOptions;

                // get the string to hash on again
                DbConnectionOptions connectionOptions = connectionPoolGroup.ConnectionOptions;
                Debug.Assert(connectionOptions != null, "prevent expansion of connectionString");

                connectionPoolGroup = GetConnectionPoolGroup(connectionPoolGroup.PoolKey, poolOptions, ref connectionOptions);
                Debug.Assert(connectionPoolGroup != null, "null connectionPoolGroup?");
                SetConnectionPoolGroup(owningObject, connectionPoolGroup);
            }
            IDbConnectionPool connectionPool = connectionPoolGroup.GetConnectionPool((SqlConnectionFactory)this);
            return connectionPool;
        }

        internal DbConnectionPoolGroup GetConnectionPoolGroup(DbConnectionPoolKey key, DbConnectionPoolGroupOptions poolOptions, ref DbConnectionOptions userConnectionOptions)
        {
            if (string.IsNullOrEmpty(key.ConnectionString))
            {
                return (DbConnectionPoolGroup)null;
            }

            DbConnectionPoolGroup connectionPoolGroup;
            Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> connectionPoolGroups = _connectionPoolGroups;
            if (!connectionPoolGroups.TryGetValue(key, out connectionPoolGroup) || (connectionPoolGroup.IsDisabled && connectionPoolGroup.PoolGroupOptions != null))
            {
                // If we can't find an entry for the connection string in
                // our collection of pool entries, then we need to create a
                // new pool entry and add it to our collection.

                DbConnectionOptions connectionOptions = CreateConnectionOptions(key.ConnectionString, userConnectionOptions);
                if (connectionOptions == null)
                {
                    throw ADP.InternalConnectionError(ADP.ConnectionError.ConnectionOptionsMissing);
                }

                if (userConnectionOptions == null)
                {
					// we only allow one expansion on the connection string

                    userConnectionOptions = connectionOptions;
                    string expandedConnectionString = connectionOptions.Expand();

                    // if the expanded string is same instance (default implementation), then use the already created options
                    if ((object)expandedConnectionString != (object)key.ConnectionString)
                    {
                        // CONSIDER: caching the original string to reduce future parsing
                        DbConnectionPoolKey newKey = (DbConnectionPoolKey)((ICloneable)key).Clone();
                        newKey.ConnectionString = expandedConnectionString;
                        return GetConnectionPoolGroup(newKey, null, ref userConnectionOptions);
                    }
                }

                if (poolOptions == null)
                {
                    if (connectionPoolGroup != null)
                    {
                        // reusing existing pool option in case user originally used SetConnectionPoolOptions
                        poolOptions = connectionPoolGroup.PoolGroupOptions;
                    }
                    else
                    {
                        // Note: may return null for non-pooled connections
                        poolOptions = CreateConnectionPoolGroupOptions(connectionOptions);
                    }
                }

                lock (this)
                {
                    connectionPoolGroups = _connectionPoolGroups;
                    if (!connectionPoolGroups.TryGetValue(key, out connectionPoolGroup))
                    {
                        DbConnectionPoolGroup newConnectionPoolGroup = new DbConnectionPoolGroup(connectionOptions, key, poolOptions);
                        newConnectionPoolGroup.ProviderInfo = CreateConnectionPoolGroupProviderInfo(connectionOptions);

                        // build new dictionary with space for new connection string
                        Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> newConnectionPoolGroups = new Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup>(1 + connectionPoolGroups.Count);
                        foreach (KeyValuePair<DbConnectionPoolKey, DbConnectionPoolGroup> entry in connectionPoolGroups)
                        {
                            newConnectionPoolGroups.Add(entry.Key, entry.Value);
                        }

                        // lock prevents race condition with PruneConnectionPoolGroups
                        newConnectionPoolGroups.Add(key, newConnectionPoolGroup);

                        SqlClientEventSource.Metrics.EnterActiveConnectionPoolGroup();
                        connectionPoolGroup = newConnectionPoolGroup;
                        _connectionPoolGroups = newConnectionPoolGroups;
                    }
                    else
                    {
                        Debug.Assert(!connectionPoolGroup.IsDisabled, "Disabled pool entry discovered");
                    }
                }
                Debug.Assert(connectionPoolGroup != null, "how did we not create a pool entry?");
                Debug.Assert(userConnectionOptions != null, "how did we not have user connection options?");
            }
            else if (userConnectionOptions == null)
            {
                userConnectionOptions = connectionPoolGroup.ConnectionOptions;
            }
            return connectionPoolGroup;
        }

        internal DbMetaDataFactory GetMetaDataFactory(DbConnectionPoolGroup connectionPoolGroup, DbConnectionInternal internalConnection)
        {
            Debug.Assert(connectionPoolGroup != null, "connectionPoolGroup may not be null.");

            // get the matadatafactory from the pool entry. If it does not already have one
            // create one and save it on the pool entry
            DbMetaDataFactory metaDataFactory = connectionPoolGroup.MetaDataFactory;

            // consider serializing this so we don't construct multiple metadata factories
            // if two threads happen to hit this at the same time.  One will be GC'd
            if (metaDataFactory == null)
            {
                bool allowCache = false;
                metaDataFactory = CreateMetaDataFactory(internalConnection, out allowCache);
                if (allowCache)
                {
                    connectionPoolGroup.MetaDataFactory = metaDataFactory;
                }
            }
            return metaDataFactory;
        }

        protected void PruneConnectionPoolGroups(object state)
        {
            // when debugging this method, expect multiple threads at the same time
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<prov.DbConnectionFactory.PruneConnectionPoolGroups|RES|INFO|CPOOL> {0}", ObjectID);

            // First, walk the pool release list and attempt to clear each
            // pool, when the pool is finally empty, we dispose of it.  If the
            // pool isn't empty, it's because there are active connections or
            // distributed transactions that need it.
            lock (_poolsToRelease)
            {
                if (0 != _poolsToRelease.Count)
                {
                    IDbConnectionPool[] poolsToRelease = _poolsToRelease.ToArray();
                    foreach (IDbConnectionPool pool in poolsToRelease)
                    {
                        if (pool != null)
                        {
                            pool.Clear();

                            if (0 == pool.Count)
                            {
                                _poolsToRelease.Remove(pool);
                                SqlClientEventSource.Log.TryAdvancedTraceEvent("<prov.DbConnectionFactory.PruneConnectionPoolGroups|RES|INFO|CPOOL> {0}, ReleasePool={1}", ObjectID, pool.Id);

                                SqlClientEventSource.Metrics.ExitInactiveConnectionPool();
                            }
                        }
                    }
                }
            }

            // Next, walk the pool entry release list and dispose of each
            // pool entry when it is finally empty.  If the pool entry isn't
            // empty, it's because there are active pools that need it.
            lock (_poolGroupsToRelease)
            {
                if (0 != _poolGroupsToRelease.Count)
                {
                    DbConnectionPoolGroup[] poolGroupsToRelease = _poolGroupsToRelease.ToArray();
                    foreach (DbConnectionPoolGroup poolGroup in poolGroupsToRelease)
                    {
                        if (poolGroup != null)
                        {
                            int poolsLeft = poolGroup.Clear(); // may add entries to _poolsToRelease

                            if (0 == poolsLeft)
                            {
                                _poolGroupsToRelease.Remove(poolGroup);
                                SqlClientEventSource.Log.TryAdvancedTraceEvent("<prov.DbConnectionFactory.PruneConnectionPoolGroups|RES|INFO|CPOOL> {0}, ReleasePoolGroup={1}", ObjectID, poolGroup.ObjectID);

                                SqlClientEventSource.Metrics.ExitInactiveConnectionPoolGroup();
                            }
                        }
                    }
                }
            }

            // Finally, we walk through the collection of connection pool entries
            // and prune each one.  This will cause any empty pools to be put
            // into the release list.
            lock (this)
            {
                Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> connectionPoolGroups = _connectionPoolGroups;
                Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> newConnectionPoolGroups = new Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup>(connectionPoolGroups.Count);

                foreach (KeyValuePair<DbConnectionPoolKey, DbConnectionPoolGroup> entry in connectionPoolGroups)
                {
                    if (entry.Value != null)
                    {
                        Debug.Assert(!entry.Value.IsDisabled, "Disabled pool entry discovered");

                        // entries start active and go idle during prune if all pools are gone
                        // move idle entries from last prune pass to a queue for pending release
                        // otherwise process entry which may move it from active to idle
                        if (entry.Value.Prune())
                        {
                            // may add entries to _poolsToRelease
                            QueuePoolGroupForRelease(entry.Value);
                        }
                        else
                        {
                            newConnectionPoolGroups.Add(entry.Key, entry.Value);
                        }
                    }
                }
                _connectionPoolGroups = newConnectionPoolGroups;
            }
        }

        internal void QueuePoolForRelease(IDbConnectionPool pool, bool clearing)
        {
            // Queue the pool up for release -- we'll clear it out and dispose
            // of it as the last part of the pruning timer callback so we don't
            // do it with the pool entry or the pool collection locked.
            Debug.Assert(pool != null, "null pool?");

            // set the pool to the shutdown state to force all active
            // connections to be automatically disposed when they
            // are returned to the pool
            pool.Shutdown();

            lock (_poolsToRelease)
            {
                if (clearing)
                {
                    pool.Clear();
                }
                _poolsToRelease.Add(pool);
            }
            SqlClientEventSource.Metrics.EnterInactiveConnectionPool();
            SqlClientEventSource.Metrics.ExitActiveConnectionPool();
        }

        internal void QueuePoolGroupForRelease(DbConnectionPoolGroup poolGroup)
        {
            Debug.Assert(poolGroup != null, "null poolGroup?");
            SqlClientEventSource.Log.TryTraceEvent("<prov.DbConnectionFactory.QueuePoolGroupForRelease|RES|INFO|CPOOL> {0}, poolGroup={1}", ObjectID, poolGroup.ObjectID);

            lock (_poolGroupsToRelease)
            {
                _poolGroupsToRelease.Add(poolGroup);
            }

            SqlClientEventSource.Metrics.EnterInactiveConnectionPoolGroup();
            SqlClientEventSource.Metrics.ExitActiveConnectionPoolGroup();
        }

        abstract protected DbConnectionOptions CreateConnectionOptions(string connectionString, DbConnectionOptions previous);

        abstract protected DbConnectionPoolGroupOptions CreateConnectionPoolGroupOptions(DbConnectionOptions options);

        abstract internal DbConnectionPoolGroup GetConnectionPoolGroup(DbConnection connection);

        abstract internal DbConnectionInternal GetInnerConnection(DbConnection connection);

        abstract protected int GetObjectId(DbConnection connection);

        abstract internal void PermissionDemand(DbConnection outerConnection);

        abstract internal void SetConnectionPoolGroup(DbConnection outerConnection, DbConnectionPoolGroup poolGroup);

        abstract internal void SetInnerConnectionEvent(DbConnection owningObject, DbConnectionInternal to);

        abstract internal bool SetInnerConnectionFrom(DbConnection owningObject, DbConnectionInternal to, DbConnectionInternal from);

        abstract internal void SetInnerConnectionTo(DbConnection owningObject, DbConnectionInternal to);

        virtual internal void Unload()
        {
            _pruningTimer.Dispose();
        }
    }
}
