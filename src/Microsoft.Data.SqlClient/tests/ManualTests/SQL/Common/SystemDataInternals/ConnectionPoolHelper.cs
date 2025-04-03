// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class ConnectionPoolHelper
    {
        private static FieldInfo s_dbConnectionFactoryPoolGroupList = typeof(DbConnectionFactory).GetField("_connectionPoolGroups", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_dbConnectionPoolGroupPoolCollection = typeof(DbConnectionPoolGroup).GetField("_poolCollection", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_dbConnectionPoolStackOld = typeof(WaitHandleDbConnectionPool).GetField("_stackOld", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo s_dbConnectionPoolStackNew = typeof(WaitHandleDbConnectionPool).GetField("_stackNew", BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo s_dbConnectionPoolCleanup = typeof(WaitHandleDbConnectionPool).GetMethod("CleanupCallback", BindingFlags.Instance | BindingFlags.NonPublic);

        public static int CountFreeConnections(DbConnectionPool pool)
        {
            ICollection oldStack = (ICollection)s_dbConnectionPoolStackOld.GetValue(pool);
            ICollection newStack = (ICollection)s_dbConnectionPoolStackNew.GetValue(pool);

            return (oldStack.Count + newStack.Count);
        }

        /// <summary>
        /// Finds all connection pools
        /// </summary>
        /// <returns></returns>
        public static List<Tuple<DbConnectionPool, DbConnectionPoolKey>> AllConnectionPools()
        {
            List<Tuple<DbConnectionPool, DbConnectionPoolKey>> connectionPools = new List<Tuple<DbConnectionPool, DbConnectionPoolKey>>();
            
            SqlConnectionFactory factorySingleton = SqlConnectionFactory.SingletonInstance;
            Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> AllPoolGroups = (Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup>)s_dbConnectionFactoryPoolGroupList.GetValue(factorySingleton);
            foreach (var item in AllPoolGroups)
            {
                var poolCollection = (ConcurrentDictionary<DbConnectionPoolIdentity, DbConnectionPool>)s_dbConnectionPoolGroupPoolCollection.GetValue(item.Value);
                foreach (var pool in poolCollection.Values)
                {
                    connectionPools.Add(new Tuple<DbConnectionPool, DbConnectionPoolKey>(pool, item.Key));
                }
            }

            return connectionPools;
        }

        /// <summary>
        /// Finds a connection pool based on a connection string
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static DbConnectionPool ConnectionPoolFromString(string connectionString)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            DbConnectionPool pool = null;
            SqlConnectionFactory factorySingleton = SqlConnectionFactory.SingletonInstance;
            Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup> AllPoolGroups = (Dictionary<DbConnectionPoolKey, DbConnectionPoolGroup>)s_dbConnectionFactoryPoolGroupList.GetValue(factorySingleton);
            
            if (AllPoolGroups.TryGetValue(new DbConnectionPoolKey(connectionString), out var poolGroup) && poolGroup != null)
            {
                var poolCollection = (ConcurrentDictionary<DbConnectionPoolIdentity, DbConnectionPool>)s_dbConnectionPoolGroupPoolCollection.GetValue(poolGroup);
                if (poolCollection.Count == 1)
                {
                    pool = poolCollection.First().Value;
                }
                else if (poolCollection.Count > 1)
                {
                    throw new NotSupportedException("Using multiple identities with SSPI is not supported");
                }
            }

            return pool;
        }

        /// <summary>
        /// Causes the cleanup timer code in the connection pool to be invoked
        /// </summary>
        /// <param name="obj">A connection pool object</param>
        internal static void CleanConnectionPool(DbConnectionPool pool)
        {
            s_dbConnectionPoolCleanup.Invoke(pool, new object[] { null });
        }
    }
}
