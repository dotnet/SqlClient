// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;

#if NET
using System.Runtime.Loader;
#endif

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlConnectionFactory : DbConnectionFactory
    {
        #region Member Variables
        
        private static readonly TimeSpan PruningDueTime = TimeSpan.FromMinutes(4);
        private static readonly TimeSpan PruningPeriod = TimeSpan.FromSeconds(30);
        
        // s_pendingOpenNonPooled is an array of tasks used to throttle creation of non-pooled
        // connections to a maximum of Environment.ProcessorCount at a time.
        private static Task<DbConnectionInternal> s_completedTask;
        private static int s_objectTypeCount;
        private static Task<DbConnectionInternal>[] s_pendingOpenNonPooled =
            new Task<DbConnectionInternal>[Environment.ProcessorCount];
        private static uint s_pendingOpenNonPooledNext = 0;

        private readonly List<DbConnectionPoolGroup> _poolGroupsToRelease;
        private readonly List<IDbConnectionPool> _poolsToRelease;
        private readonly Timer _pruningTimer;
        private Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> _connectionPoolGroups;

        #endregion
        
        #region Constructors
        
        private SqlConnectionFactory()
        {
            _connectionPoolGroups = new Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup>();
            _poolsToRelease = new List<IDbConnectionPool>();
            _poolGroupsToRelease = new List<DbConnectionPoolGroup>();
            _pruningTimer = ADP.UnsafeCreateTimer(
                PruneConnectionPoolGroups,
                state: null,
                PruningDueTime,
                PruningPeriod);
            
            #if NET
            SubscribeToAssemblyLoadContextUnload();
            #endif
        }
        
        #endregion
        
        #region Properties

        internal static DbProviderFactory ProviderFactory => SqlClientFactory.Instance;
        
        internal static SqlConnectionFactory Instance { get; } = new SqlConnectionFactory(); 
        
        internal int ObjectId { get; } = Interlocked.Increment(ref s_objectTypeCount);
        
        #endregion

        #region Public Methods
        
        internal void ClearAllPools()
        {
            using TryEventScope scope = TryEventScope.Create(nameof(SqlConnectionFactory));
            foreach ((DbConnectionPoolKey _, DbConnectionPoolGroup group) in _connectionPoolGroups)
            {
                group?.Clear();
            }
        }
        
        internal void ClearPool(DbConnection connection)
        {
            ADP.CheckArgumentNull(connection, nameof(connection));
            
            using TryEventScope scope = TryEventScope.Create("<prov.SqlConnectionFactory.ClearPool|API> {0}", GetObjectId(connection));
            DbConnectionPoolGroup poolGroup = GetConnectionPoolGroup(connection);
            poolGroup?.Clear();
        }
        
        internal void ClearPool(DbConnectionPoolKey key)
        {
            ADP.CheckArgumentNull(key.ConnectionString, $"{nameof(key)}.{nameof(key.ConnectionString)}");
            
            using TryEventScope scope = TryEventScope.Create("<prov.SqlConnectionFactory.ClearPool|API> connectionString");
            if (_connectionPoolGroups.TryGetValue(key, out DbConnectionPoolGroup poolGroup))
            {
                poolGroup?.Clear();
            }
        }

        internal DbConnectionPoolProviderInfo CreateConnectionPoolProviderInfo(DbConnectionOptions connectionOptions) =>
            ((SqlConnectionString)connectionOptions).UserInstance
                ? new SqlConnectionPoolProviderInfo()
                : null;
        
        internal SqlInternalConnectionTds CreateNonPooledConnection(
            DbConnection owningConnection,
            DbConnectionPoolGroup poolGroup,
            DbConnectionOptions userOptions)
        {
            Debug.Assert(owningConnection is not null, "null owningConnection?");
            Debug.Assert(poolGroup is not null, "null poolGroup?");

            SqlInternalConnectionTds newConnection = CreateConnection(
                poolGroup.ConnectionOptions,
                poolGroup.PoolKey,
                poolGroup.ProviderInfo,
                pool: null,
                owningConnection,
                userOptions);
            if (newConnection is not null)
            {
                SqlClientEventSource.Metrics.HardConnectRequest();
                newConnection.MakeNonPooledObject(owningConnection);
            }
            
            SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.CreateNonPooledConnection|RES|CPOOL> {0}, Non-pooled database connection created.", ObjectId);
            return newConnection;
        }

        internal SqlInternalConnectionTds CreatePooledConnection(
            DbConnection owningConnection,
            IDbConnectionPool pool,
            DbConnectionPoolKey poolKey,
            DbConnectionOptions options,
            DbConnectionOptions userOptions)
        {
            Debug.Assert(pool != null, "null pool?");

            SqlInternalConnectionTds newConnection = CreateConnection(
                options,
                poolKey, // @TODO: is pool.PoolGroup.Key the same thing?
                pool.PoolGroup.ProviderInfo,
                pool,
                owningConnection,
                userOptions);
            if (newConnection is not null)
            {
                SqlClientEventSource.Metrics.HardConnectRequest();
                newConnection.MakePooledConnection(pool);
            }
            
            SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.CreatePooledConnection|RES|CPOOL> {0}, Pooled database connection created.", ObjectId);
            return newConnection;
        }

        internal void QueuePoolForRelease(IDbConnectionPool pool, bool clearing)
        {
            // Queue the pool up for release -- we'll clear it out and dispose of it as the last
            // part of the pruning timer callback so we don't do it with the pool entry or the pool
            // collection locked.
            Debug.Assert(pool != null, "null pool?");

            // Set the pool to the shutdown state to force all active connections to be
            // automatically disposed when they are returned to the pool
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
            SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.QueuePoolGroupForRelease|RES|INFO|CPOOL> {0}, poolGroup={1}", ObjectId, poolGroup.ObjectID);

            lock (_poolGroupsToRelease)
            {
                _poolGroupsToRelease.Add(poolGroup);
            }

            SqlClientEventSource.Metrics.EnterInactiveConnectionPoolGroup();
            SqlClientEventSource.Metrics.ExitActiveConnectionPoolGroup();
        }

        internal bool TryGetConnection(
            DbConnection owningConnection,
            TaskCompletionSource<DbConnectionInternal> retry,
            DbConnectionOptions userOptions,
            DbConnectionInternal oldConnection,
            out DbConnectionInternal connection)
        {
            Debug.Assert(owningConnection is not null, "null owningConnection?");

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
                DbConnectionPoolGroup poolGroup = GetConnectionPoolGroup(owningConnection);
                
                // Doing this on the callers thread is important because it looks up the WindowsIdentity from the thread.
                IDbConnectionPool connectionPool = GetConnectionPool(owningConnection, poolGroup);
                if (connectionPool == null)
                {
                    // If GetConnectionPool returns null, we can be certain that this connection
                    // should not be pooled via DbConnectionPool or have a disabled pool entry.
                    poolGroup = GetConnectionPoolGroup(owningConnection); // previous entry have been disabled

                    if (retry is not null)
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
                                if (task is null)
                                {
                                    s_pendingOpenNonPooled[idx] = GetCompletedTask();
                                    break;
                                }
                                
                                if (task.IsCompleted)
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
                    if (((SqlConnection)owningConnection).ForceNewConnection)
                    {
                        Debug.Assert(oldConnection is not DbConnectionClosed, "Force new connection, but there is no old connection");
                        
                        connection = connectionPool.ReplaceConnection(owningConnection, userOptions, oldConnection);
                    }
                    else
                    {
                        if (!connectionPool.TryGetConnection(owningConnection, retry, userOptions, out connection))
                        {
                            return false;
                        }
                    }

                    if (connection is null)
                    {
                        // connection creation failed on semaphore waiting or if max pool reached
                        if (connectionPool.IsRunning)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.GetConnection|RES|CPOOL> {0}, GetConnection failed because a pool timeout occurred.", ObjectId);
                            // If GetConnection failed while the pool is running, the pool timeout occurred.
                            throw ADP.PooledOpenTimeout();
                        }

                        // We've hit the race condition, where the pool was shut down after we
                        // got it from the group. Yield time slice to allow shutdown activities
                        // to complete and a new, running pool to be instantiated before
                        // retrying.
                        Thread.Sleep(timeBetweenRetriesMilliseconds);
                        timeBetweenRetriesMilliseconds *= 2; // double the wait time for next iteration
                    }
                }
            } while (connection == null && retriesLeft-- > 0);

            if (connection == null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.GetConnection|RES|CPOOL> {0}, GetConnection failed because a pool timeout occurred and all retries were exhausted.", ObjectId);
                // exhausted all retries or timed out - give up
                throw ADP.PooledOpenTimeout();
            }

            return true;
        }
        
        #endregion

        protected override DbConnectionOptions CreateConnectionOptions(string connectionString, DbConnectionOptions previous)
        {
            Debug.Assert(!string.IsNullOrEmpty(connectionString), "empty connectionString");
            SqlConnectionString result = new SqlConnectionString(connectionString);
            return result;
        }

        protected override DbConnectionPoolGroupOptions CreateConnectionPoolGroupOptions(DbConnectionOptions connectionOptions)
        {
            SqlConnectionString opt = (SqlConnectionString)connectionOptions;

            DbConnectionPoolGroupOptions poolingOptions = null;

            if (opt.Pooling)
            {    // never pool context connections.
                int connectionTimeout = opt.ConnectTimeout;

                if (connectionTimeout > 0 && connectionTimeout < int.MaxValue / 1000)
                {
                    connectionTimeout *= 1000;
                }
                else if (connectionTimeout >= int.MaxValue / 1000)
                {
                    connectionTimeout = int.MaxValue;
                }
                
                if (opt.Authentication is SqlAuthenticationMethod.ActiveDirectoryInteractive
                                       or SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
                {
                    // interactive/device code flow mode will always have pool's CreateTimeout = 10 x ConnectTimeout.
                    if (connectionTimeout >= Int32.MaxValue / 10)
                    {
                        connectionTimeout = Int32.MaxValue;
                    }
                    else
                    {
                        connectionTimeout *= 10;
                    }
                    SqlClientEventSource.Log.TryTraceEvent("SqlConnectionFactory.CreateConnectionPoolGroupOptions | Set connection pool CreateTimeout '{0}' when Authentication mode '{1}' is used.", connectionTimeout, opt.Authentication);
                }

                poolingOptions = new DbConnectionPoolGroupOptions(
                    opt.IntegratedSecurity || opt.Authentication is SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                    opt.MinPoolSize,
                    opt.MaxPoolSize,
                    connectionTimeout,
                    opt.LoadBalanceTimeout,
                    opt.Enlist);
            }
            return poolingOptions;
        }

        internal DbConnectionPoolGroupProviderInfo CreateConnectionPoolGroupProviderInfo(
            DbConnectionOptions connectionOptions) =>
            new SqlConnectionPoolGroupProviderInfo((SqlConnectionString)connectionOptions);

        internal SqlConnectionString FindSqlConnectionOptions(SqlConnectionPoolKey key)
        {
            SqlConnectionString connectionOptions = (SqlConnectionString)FindConnectionOptions(key);
            if (connectionOptions == null)
            {
                connectionOptions = new SqlConnectionString(key.ConnectionString);
            }
            if (connectionOptions.IsEmpty)
            {
                throw ADP.NoConnectionString();
            }
            return connectionOptions;
        }

        // @TODO: All these methods seem redundant ... shouldn't we always have a SqlConnection?
        internal override DbConnectionPoolGroup GetConnectionPoolGroup(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.PoolGroup;
            }
            return null;
        }

        internal override DbConnectionInternal GetInnerConnection(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.InnerConnection;
            }
            return null;
        }

        protected override int GetObjectId(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.ObjectID;
            }
            return 0;
        }

        internal override void PermissionDemand(DbConnection outerConnection)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (c != null)
            {
                c.PermissionDemand();
            }
        }

        internal override void SetConnectionPoolGroup(DbConnection outerConnection, DbConnectionPoolGroup poolGroup)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (c != null)
            {
                c.PoolGroup = poolGroup;
            }
        }

        internal override void SetInnerConnectionEvent(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                c.SetInnerConnectionEvent(to);
            }
        }

        internal override bool SetInnerConnectionFrom(DbConnection owningObject, DbConnectionInternal to, DbConnectionInternal from)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                return c.SetInnerConnectionFrom(to, from);
            }
            return false;
        }

        internal override void SetInnerConnectionTo(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                c.SetInnerConnectionTo(to);
            }
        }

        protected override DbMetaDataFactory CreateMetaDataFactory(DbConnectionInternal internalConnection, out bool cacheMetaDataFactory)
        {
            Debug.Assert(internalConnection != null, "internalConnection may not be null.");

            Stream xmlStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Data.SqlClient.SqlMetaData.xml");
            cacheMetaDataFactory = true;

            Debug.Assert(xmlStream != null, nameof(xmlStream) + " may not be null.");

            return new SqlMetaDataFactory(xmlStream,
                                          internalConnection.ServerVersion,
                                          internalConnection.ServerVersion);
        }

        #region Private Methods
        
        // @TODO: I think this could be broken down into methods more specific to use cases above
        private static SqlInternalConnectionTds CreateConnection(
            DbConnectionOptions options,
            DbConnectionPoolKey poolKey,
            DbConnectionPoolGroupProviderInfo poolGroupProviderInfo,
            IDbConnectionPool pool,
            DbConnection owningConnection,
            DbConnectionOptions userOptions)
        {
            SqlConnectionString opt = (SqlConnectionString)options;
            SqlConnectionPoolKey key = (SqlConnectionPoolKey)poolKey;
            SessionData recoverySessionData = null;

            SqlConnection sqlOwningConnection = owningConnection as SqlConnection;
            bool applyTransientFaultHandling = sqlOwningConnection?._applyTransientFaultHandling ?? false;

            SqlConnectionString userOpt = null;
            if (userOptions != null)
            {
                userOpt = (SqlConnectionString)userOptions;
            }
            else if (sqlOwningConnection != null)
            {
                userOpt = (SqlConnectionString)(sqlOwningConnection.UserConnectionOptions);
            }

            if (sqlOwningConnection != null)
            {
                recoverySessionData = sqlOwningConnection._recoverySessionData;
            }

            bool redirectedUserInstance = false;
            DbConnectionPoolIdentity identity = null;

            // Pass DbConnectionPoolIdentity to SqlInternalConnectionTds if using integrated security
            // or active directory integrated security.
            // Used by notifications.
            if (opt.IntegratedSecurity || opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
            {
                if (pool != null)
                {
                    identity = pool.Identity;
                }
                else
                {
                    identity = DbConnectionPoolIdentity.GetCurrent();
                }
            }

            // FOLLOWING IF BLOCK IS ENTIRELY FOR SSE USER INSTANCES
            // If "user instance=true" is in the connection string, we're using SSE user instances
            if (opt.UserInstance)
            {
                // opt.DataSource is used to create the SSE connection
                redirectedUserInstance = true;
                string instanceName;

                if (pool == null || (pool != null && pool.Count <= 0))
                { // Non-pooled or pooled and no connections in the pool.
                    SqlInternalConnectionTds sseConnection = null;
                    try
                    {
                        // We throw an exception in case of a failure
                        // NOTE: Cloning connection option opt to set 'UserInstance=True' and 'Enlist=False'
                        //       This first connection is established to SqlExpress to get the instance name
                        //       of the UserInstance.
                        SqlConnectionString sseopt = new SqlConnectionString(opt, opt.DataSource, userInstance: true, setEnlistValue: false);
                        sseConnection = new SqlInternalConnectionTds(identity, sseopt, key.Credential, null, "", null, false, applyTransientFaultHandling: applyTransientFaultHandling);
                        // NOTE: Retrieve <UserInstanceName> here. This user instance name will be used below to connect to the Sql Express User Instance.
                        instanceName = sseConnection.InstanceName;

                        // Set future transient fault handling based on connection options
                        sqlOwningConnection._applyTransientFaultHandling = opt != null && opt.ConnectRetryCount > 0;

                        if (!instanceName.StartsWith("\\\\.\\", StringComparison.Ordinal))
                        {
                            throw SQL.NonLocalSSEInstance();
                        }

                        if (pool != null)
                        { // Pooled connection - cache result
                            SqlConnectionPoolProviderInfo providerInfo = (SqlConnectionPoolProviderInfo)pool.ProviderInfo;
                            // No lock since we are already in creation mutex
                            providerInfo.InstanceName = instanceName;
                        }
                    }
                    finally
                    {
                        if (sseConnection != null)
                        {
                            sseConnection.Dispose();
                        }
                    }
                }
                else
                { // Cached info from pool.
                    SqlConnectionPoolProviderInfo providerInfo = (SqlConnectionPoolProviderInfo)pool.ProviderInfo;
                    // No lock since we are already in creation mutex
                    instanceName = providerInfo.InstanceName;
                }

                // NOTE: Here connection option opt is cloned to set 'instanceName=<UserInstanceName>' that was
                //       retrieved from the previous SSE connection. For this UserInstance connection 'Enlist=True'.
                // options immutable - stored in global hash - don't modify
                opt = new SqlConnectionString(opt, instanceName, userInstance: false, setEnlistValue: null);
                poolGroupProviderInfo = null; // null so we do not pass to constructor below...
            }

            return new SqlInternalConnectionTds(
                identity,
                opt,
                key.Credential,
                poolGroupProviderInfo,
                newPassword: string.Empty,
                newSecurePassword: null,
                redirectedUserInstance,
                userOpt,
                recoverySessionData,
                applyTransientFaultHandling,
                key.AccessToken,
                pool,
                key.AccessTokenCallback);
        }

        private Task<DbConnectionInternal> CreateReplaceConnectionContinuation(
            Task<DbConnectionInternal> task,
            DbConnection owningConnection,
            TaskCompletionSource<DbConnectionInternal> retry,
            DbConnectionOptions userOptions,
            DbConnectionInternal oldConnection,
            DbConnectionPoolGroup poolGroup,
            CancellationTokenSource cancellationTokenSource)
        {
            return task.ContinueWith(
                _ =>
                {
                    System.Transactions.Transaction originalTransaction = ADP.GetCurrentTransaction();
                    try
                    {
                        ADP.SetCurrentTransaction(retry.Task.AsyncState as System.Transactions.Transaction);
                        
                        DbConnectionInternal newConnection = CreateNonPooledConnection(owningConnection, poolGroup, userOptions);
                        
                        if (oldConnection?.State == ConnectionState.Open)
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

        private IDbConnectionPool GetConnectionPool(
            DbConnection owningObject,
            DbConnectionPoolGroup connectionPoolGroup)
        {
            // If poolgroup is disabled, it will be replaced with a new entry

            Debug.Assert(owningObject != null, "null owningObject?");
            Debug.Assert(connectionPoolGroup != null, "null connectionPoolGroup?");

            // It is possible that while the outer connection object has been sitting around in a
            // closed and unused state in some long-running app, the pruner may have come along and
            // remove this the pool entry from the master list. If we were to use a pool entry in
            // this state, we would create "unmanaged" pools, which would be bad. To avoid this
            // problem, we automagically re-create the pool entry whenever it's disabled.

            // however, don't rebuild connectionOptions if no pooling is involved - let new connections do that work
            if (connectionPoolGroup.IsDisabled && connectionPoolGroup.PoolGroupOptions != null)
            {
                SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.GetConnectionPool|RES|INFO|CPOOL> {0}, DisabledPoolGroup={1}", ObjectId, connectionPoolGroup.ObjectID);

                // reusing existing pool option in case user originally used SetConnectionPoolOptions
                DbConnectionPoolGroupOptions poolOptions = connectionPoolGroup.PoolGroupOptions;

                // get the string to hash on again
                DbConnectionOptions connectionOptions = connectionPoolGroup.ConnectionOptions;
                Debug.Assert(connectionOptions != null, "prevent expansion of connectionString");

                connectionPoolGroup = GetConnectionPoolGroup(connectionPoolGroup.PoolKey, poolOptions, ref connectionOptions);
                Debug.Assert(connectionPoolGroup != null, "null connectionPoolGroup?");
                SetConnectionPoolGroup(owningObject, connectionPoolGroup);
            }
            
            IDbConnectionPool connectionPool = connectionPoolGroup.GetConnectionPool(this);
            return connectionPool;
        }
        
        private void TryGetConnectionCompletedContinuation(Task<DbConnectionInternal> task, object state)
        {
            // Decompose the state into the parameters we want
            (CancellationTokenSource cts, TaskCompletionSource<DbConnectionInternal> tcs) =
                (Tuple<CancellationTokenSource, TaskCompletionSource<DbConnectionInternal>>)state;
            
            cts.Dispose();

            if (task.IsCanceled)
            {
                tcs.TrySetException(ADP.ExceptionWithStackTrace(ADP.NonPooledOpenTimeout()));
            }
            else if (task.IsFaulted)
            {
                tcs.TrySetException(task.Exception.InnerException);
            }
            else
            {
                if (!tcs.TrySetResult(task.Result))
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
        
        #if NET
        private void Unload(object sender, EventArgs e)
        {
            try
            {
                Unload();
            }
            finally
            {
                ClearAllPools();
            }
        }

        private void SqlConnectionFactoryAssemblyLoadContext_Unloading(AssemblyLoadContext obj)
        {
            Unload(obj, EventArgs.Empty);
        }
        
        private void SubscribeToAssemblyLoadContextUnload()
        {
            AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += 
                SqlConnectionFactoryAssemblyLoadContext_Unloading;
        }
        #endif
        
        #endregion
    }
}

