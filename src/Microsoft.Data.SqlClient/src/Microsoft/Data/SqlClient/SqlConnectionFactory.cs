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
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.ConnectionPool;

#if NET
using System.Runtime.Loader;
#endif

namespace Microsoft.Data.SqlClient
{
    // @TODO: Facade pattern (interface, use interface, add constructor overloads for providing non-default interface, reseal)
    internal class SqlConnectionFactory
    {
        #region Member Variables
        
        private static readonly TimeSpan PruningDueTime = TimeSpan.FromMinutes(4);
        private static readonly TimeSpan PruningPeriod = TimeSpan.FromSeconds(30);
        private static readonly Task<DbConnectionInternal> CompletedTask =
            Task.FromResult<DbConnectionInternal>(null);
        
        // s_pendingOpenNonPooled is an array of tasks used to throttle creation of non-pooled
        // connections to a maximum of Environment.ProcessorCount at a time.
        private static readonly Task<DbConnectionInternal>[] s_pendingOpenNonPooled =
            new Task<DbConnectionInternal>[Environment.ProcessorCount];
        
        private static int s_objectTypeCount;
        private static uint s_pendingOpenNonPooledNext = 0;

        private readonly List<DbConnectionPoolGroup> _poolGroupsToRelease;
        private readonly List<IDbConnectionPool> _poolsToRelease;
        private readonly Timer _pruningTimer;
        private Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> _connectionPoolGroups;

        #endregion
        
        #region Constructors
        
        protected SqlConnectionFactory()
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
        
        internal static SqlConnectionFactory Instance { get; } = new SqlConnectionFactory(); 
        
        internal int ObjectId { get; } = Interlocked.Increment(ref s_objectTypeCount);
        
        #endregion

        #region Public Methods
        
        internal void ClearAllPools()
        {
            using SqlClientEventScope scope = SqlClientEventScope.Create(nameof(SqlConnectionFactory));
            foreach (DbConnectionPoolGroup group in _connectionPoolGroups.Values)
            {
                group?.Clear();
            }
        }
        
        internal void ClearPool(DbConnection connection)
        {
            ADP.CheckArgumentNull(connection, nameof(connection));
            
            using SqlClientEventScope scope = SqlClientEventScope.Create("<prov.SqlConnectionFactory.ClearPool|API> {0}", GetObjectId(connection));
            DbConnectionPoolGroup poolGroup = GetConnectionPoolGroup(connection);
            poolGroup?.Clear();
        }
        
        internal void ClearPool(DbConnectionPoolKey key)
        {
            ADP.CheckArgumentNull(key.ConnectionString, $"{nameof(key)}.{nameof(key.ConnectionString)}");
            
            using SqlClientEventScope scope = SqlClientEventScope.Create("<prov.SqlConnectionFactory.ClearPool|API> connectionString");
            if (_connectionPoolGroups.TryGetValue(key, out DbConnectionPoolGroup poolGroup))
            {
                poolGroup?.Clear();
            }
        }

        internal DbConnectionPoolProviderInfo CreateConnectionPoolProviderInfo(DbConnectionOptions connectionOptions) =>
            ((SqlConnectionString)connectionOptions).UserInstance
                ? new SqlConnectionPoolProviderInfo()
                : null;
        
        internal DbConnectionInternal CreateNonPooledConnection(
            DbConnection owningConnection,
            DbConnectionPoolGroup poolGroup,
            DbConnectionOptions userOptions)
        {
            Debug.Assert(owningConnection is not null, "null owningConnection?");
            Debug.Assert(poolGroup is not null, "null poolGroup?");

            DbConnectionInternal newConnection = CreateConnection(
                poolGroup.ConnectionOptions,
                poolGroup.PoolKey,
                poolGroup.ProviderInfo,
                pool: null,
                owningConnection,
                userOptions);
            if (newConnection is not null)
            {
                SqlClientDiagnostics.Metrics.HardConnectRequest();
                newConnection.MakeNonPooledObject(owningConnection);
            }
            
            SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.CreateNonPooledConnection|RES|CPOOL> {0}, Non-pooled database connection created.", ObjectId);
            return newConnection;
        }

        internal DbConnectionInternal CreatePooledConnection(
            DbConnection owningConnection,
            IDbConnectionPool pool,
            DbConnectionPoolKey poolKey,
            DbConnectionOptions options,
            DbConnectionOptions userOptions)
        {
            Debug.Assert(pool != null, "null pool?");

            DbConnectionInternal newConnection = CreateConnection(
                options,
                poolKey, // @TODO: is pool.PoolGroup.Key the same thing?
                pool.PoolGroup.ProviderInfo,
                pool,
                owningConnection,
                userOptions);

            if (newConnection is null)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.CreateObjectReturnedNull);    // CreateObject succeeded, but null object
            }

            if (!newConnection.CanBePooled)
            {
                throw ADP.InternalError(ADP.InternalErrorCode.NewObjectCannotBePooled);        // CreateObject succeeded, but non-poolable object
            }

            SqlClientDiagnostics.Metrics.HardConnectRequest();
            newConnection.MakePooledConnection(pool);

            SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.CreatePooledConnection|RES|CPOOL> {0}, Pooled database connection created.", ObjectId);

            newConnection.PrePush(null);

            return newConnection;
        }

        internal DbConnectionPoolGroup GetConnectionPoolGroup(
            DbConnectionPoolKey key,
            DbConnectionPoolGroupOptions poolOptions,
            ref DbConnectionOptions userConnectionOptions)
        {
            if (string.IsNullOrEmpty(key.ConnectionString))
            {
                return null;
            }
            
            if (!_connectionPoolGroups.TryGetValue(key, out DbConnectionPoolGroup connectionPoolGroup) || 
                (connectionPoolGroup.IsDisabled && connectionPoolGroup.PoolGroupOptions != null))
            {
                // If we can't find an entry for the connection string in
                // our collection of pool entries, then we need to create a
                // new pool entry and add it to our collection.

                SqlConnectionString connectionOptions = new SqlConnectionString(key.ConnectionString);

                if (userConnectionOptions is null)
                {
					// We only allow one expansion on the connection string
                    userConnectionOptions = connectionOptions;
                    string expandedConnectionString = connectionOptions.Expand();

                    // if the expanded string is same instance (default implementation), then use the already created options
                    if ((object)expandedConnectionString != (object)key.ConnectionString)
                    {
                        // CONSIDER: caching the original string to reduce future parsing
                        DbConnectionPoolKey newKey = (DbConnectionPoolKey)key.Clone();
                        newKey.ConnectionString = expandedConnectionString;
                        return GetConnectionPoolGroup(newKey, null, ref userConnectionOptions);
                    }
                }

                if (poolOptions is null)
                {
                    if (connectionPoolGroup is not null)
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
                    if (!_connectionPoolGroups.TryGetValue(key, out connectionPoolGroup))
                    {
                        DbConnectionPoolGroup newConnectionPoolGroup =
                            new DbConnectionPoolGroup(connectionOptions, key, poolOptions)
                            {
                                ProviderInfo = CreateConnectionPoolGroupProviderInfo(connectionOptions)
                            };

                        // build new dictionary with space for new connection string
                        Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> newConnectionPoolGroups = 
                            new Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup>(1 + _connectionPoolGroups.Count);
                        foreach (KeyValuePair<DbConnectionPoolKey, DbConnectionPoolGroup> entry in _connectionPoolGroups)
                        {
                            newConnectionPoolGroups.Add(entry.Key, entry.Value);
                        }

                        // lock prevents race condition with PruneConnectionPoolGroups
                        newConnectionPoolGroups.Add(key, newConnectionPoolGroup);

                        SqlClientDiagnostics.Metrics.EnterActiveConnectionPoolGroup();
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
            else if (userConnectionOptions is null)
            {
                userConnectionOptions = connectionPoolGroup.ConnectionOptions;
            }
            
            return connectionPoolGroup;
        }

        internal SqlMetaDataFactory GetMetaDataFactory(
            DbConnectionPoolGroup poolGroup,
            DbConnectionInternal internalConnection)
        {
            Debug.Assert(poolGroup is not null, "connectionPoolGroup may not be null.");

            // Get the metadata factory from the pool entry. If it does not already have one
            // create one and save it on the pool entry
            return poolGroup.MetaDataFactory ??= CreateMetaDataFactory(internalConnection);
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
            
            SqlClientDiagnostics.Metrics.EnterInactiveConnectionPool();
            SqlClientDiagnostics.Metrics.ExitActiveConnectionPool();
        }

        internal void QueuePoolGroupForRelease(DbConnectionPoolGroup poolGroup)
        {
            Debug.Assert(poolGroup != null, "null poolGroup?");
            SqlClientEventSource.Log.TryTraceEvent("<prov.SqlConnectionFactory.QueuePoolGroupForRelease|RES|INFO|CPOOL> {0}, poolGroup={1}", ObjectId, poolGroup.ObjectID);

            lock (_poolGroupsToRelease)
            {
                _poolGroupsToRelease.Add(poolGroup);
            }

            SqlClientDiagnostics.Metrics.EnterInactiveConnectionPoolGroup();
            SqlClientDiagnostics.Metrics.ExitActiveConnectionPoolGroup();
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
                                    s_pendingOpenNonPooled[idx] = CompletedTask;
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
                            newTask = CreateReplaceConnectionContinuation(
                                s_pendingOpenNonPooled[idx],
                                owningConnection,
                                retry,
                                userOptions,
                                oldConnection,
                                poolGroup,
                                cancellationTokenSource);

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

                    SqlClientDiagnostics.Metrics.EnterNonPooledConnection();
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

        internal DbConnectionPoolGroupProviderInfo CreateConnectionPoolGroupProviderInfo(
            DbConnectionOptions connectionOptions) =>
            new SqlConnectionPoolGroupProviderInfo((SqlConnectionString)connectionOptions);

        internal SqlConnectionString FindSqlConnectionOptions(SqlConnectionPoolKey key)
        {
            Debug.Assert(key is not null, "Key cannot be null");

            DbConnectionOptions connectionOptions = null;
            
            if (!string.IsNullOrEmpty(key.ConnectionString) &&
                _connectionPoolGroups.TryGetValue(key, out DbConnectionPoolGroup poolGroup))
            {
                connectionOptions = poolGroup.ConnectionOptions;
            }
            
            if (connectionOptions is null)
            {
                connectionOptions = new SqlConnectionString(key.ConnectionString);
            }
            
            if (connectionOptions.IsEmpty)
            {
                throw ADP.NoConnectionString();
            }
            
            return (SqlConnectionString)connectionOptions;
        }

        // @TODO: All these methods seem redundant ... shouldn't we always have a SqlConnection?
        internal DbConnectionPoolGroup GetConnectionPoolGroup(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.PoolGroup;
            }
            return null;
        }

        internal DbConnectionInternal GetInnerConnection(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.InnerConnection;
            }
            return null;
        }

        internal int GetObjectId(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.ObjectID;
            }
            return 0;
        }

        internal void PermissionDemand(DbConnection outerConnection)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (c != null)
            {
                c.PermissionDemand();
            }
        }

        internal void SetConnectionPoolGroup(DbConnection outerConnection, DbConnectionPoolGroup poolGroup)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (c != null)
            {
                c.PoolGroup = poolGroup;
            }
        }

        internal void SetInnerConnectionEvent(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                c.SetInnerConnectionEvent(to);
            }
        }

        internal bool SetInnerConnectionFrom(DbConnection owningObject, DbConnectionInternal to, DbConnectionInternal from)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                return c.SetInnerConnectionFrom(to, from);
            }
            return false;
        }

        internal void SetInnerConnectionTo(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                c.SetInnerConnectionTo(to);
            }
        }

        #region Private Methods
        
        // @TODO: I think this could be broken down into methods more specific to use cases above
        protected virtual DbConnectionInternal CreateConnection(
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

            // Pass DbConnectionPoolIdentity to SqlInternalConnectionTds if using integrated security.
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

                if (pool == null || pool.Count <= 0)
                {
                    // Non-pooled or pooled and no connections in the pool.

                    // NOTE: Cloning connection option opt to set 'UserInstance=True' and 'Enlist=False'
                    //       This first connection is established to SqlExpress to get the instance name
                    //       of the UserInstance.
                    SqlConnectionString sseopt = new SqlConnectionString(
                        opt,
                        opt.DataSource,
                        userInstance: true,
                        setEnlistValue: false);

                    SqlConnectionInternal sseConnection = new SqlConnectionInternal(
                        identity,
                        sseopt,
                        key.Credential,
                        providerInfo: null,
                        newPassword: string.Empty,
                        newSecurePassword: null,
                        redirectedUserInstance: false,
                        applyTransientFaultHandling: applyTransientFaultHandling,
                        sspiContextProvider: key.SspiContextProvider);
                    using (sseConnection)
                    {
                        // NOTE: Retrieve <UserInstanceName> here. This user instance name will be
                        //     used below to connect to the SQL Express User Instance.
                        instanceName = sseConnection.InstanceName;

                        // Set future transient fault handling based on connection options
                        sqlOwningConnection._applyTransientFaultHandling = opt != null && opt.ConnectRetryCount > 0;

                        if (!instanceName.StartsWith(@"\\.\", StringComparison.Ordinal))
                        {
                            throw SQL.NonLocalSSEInstance();
                        }

                        if (pool != null)
                        {
                            // Pooled connection - cache result
                            SqlConnectionPoolProviderInfo providerInfo = (SqlConnectionPoolProviderInfo)pool.ProviderInfo;

                            // No lock since we are already in creation mutex
                            providerInfo.InstanceName = instanceName;
                        }
                    }
                }
                else
                {
                    // Cached info from pool.
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

            return new SqlConnectionInternal(
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
                key.AccessTokenCallback,
                key.SspiContextProvider);
        }

        private static DbConnectionPoolGroupOptions CreateConnectionPoolGroupOptions(SqlConnectionString connectionOptions)
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

        private static SqlMetaDataFactory CreateMetaDataFactory(DbConnectionInternal internalConnection)
        {
            Debug.Assert(internalConnection is not null, "internalConnection may not be null.");

            Stream xmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Data.SqlClient.SqlMetaData.xml");
            Debug.Assert(xmlStream is not null, $"{nameof(xmlStream)} may not be null.");
            
            return new SqlMetaDataFactory(xmlStream, internalConnection.ServerVersion);
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

        private void PruneConnectionPoolGroups(object state)
        {
            // When debugging this method, expect multiple threads at the same time
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<prov.SqlConnectionFactory.PruneConnectionPoolGroups|RES|INFO|CPOOL> {0}", ObjectId);

            // First, walk the pool release list and attempt to clear each pool, when the pool is
            // finally empty, we dispose of it. If the pool isn't empty, it's because there are
            // active connections or distributed transactions that need it.
            lock (_poolsToRelease)
            {
                if (_poolsToRelease.Count != 0)
                {
                    IDbConnectionPool[] poolsToRelease = _poolsToRelease.ToArray();
                    foreach (IDbConnectionPool pool in poolsToRelease)
                    {
                        if (pool is not null)
                        {
                            pool.Clear();

                            if (pool.Count == 0)
                            {
                                _poolsToRelease.Remove(pool);
                                
                                SqlClientEventSource.Log.TryAdvancedTraceEvent("<prov.SqlConnectionFactory.PruneConnectionPoolGroups|RES|INFO|CPOOL> {0}, ReleasePool={1}", ObjectId, pool.Id);
                                SqlClientDiagnostics.Metrics.ExitInactiveConnectionPool();
                            }
                        }
                    }
                }
            }

            // Next, walk the pool entry release list and dispose of each pool entry when it is
            // finally empty.  If the pool entry isn't empty, it's because there are active pools
            // that need it.
            lock (_poolGroupsToRelease)
            {
                if (_poolGroupsToRelease.Count != 0)
                {
                    DbConnectionPoolGroup[] poolGroupsToRelease = _poolGroupsToRelease.ToArray();
                    foreach (DbConnectionPoolGroup poolGroup in poolGroupsToRelease)
                    {
                        if (poolGroup != null)
                        {
                            int poolsLeft = poolGroup.Clear(); // may add entries to _poolsToRelease

                            if (poolsLeft == 0)
                            {
                                _poolGroupsToRelease.Remove(poolGroup);
                                SqlClientEventSource.Log.TryAdvancedTraceEvent("<prov.SqlConnectionFactory.PruneConnectionPoolGroups|RES|INFO|CPOOL> {0}, ReleasePoolGroup={1}", ObjectId, poolGroup.ObjectID);

                                SqlClientDiagnostics.Metrics.ExitInactiveConnectionPoolGroup();
                            }
                        }
                    }
                }
            }

            // Finally, we walk through the collection of connection pool entries and prune each
            // one. This will cause any empty pools to be put into the release list.
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
                    SqlClientDiagnostics.Metrics.EnterNonPooledConnection();
                }
            }
        }
        
        #if NET
        private void Unload(object sender, EventArgs e)
        {
            try
            {
                _pruningTimer.Dispose();
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

