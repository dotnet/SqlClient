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
        
        protected static Task<DbConnectionInternal> GetCompletedTask()
        {
            Debug.Assert(Monitor.IsEntered(s_pendingOpenNonPooled), $"Expected {nameof(s_pendingOpenNonPooled)} lock to be held.");
            return s_completedTask ?? (s_completedTask = Task.FromResult<DbConnectionInternal>(null));
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
