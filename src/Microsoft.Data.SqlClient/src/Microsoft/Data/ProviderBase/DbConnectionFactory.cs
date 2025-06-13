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
