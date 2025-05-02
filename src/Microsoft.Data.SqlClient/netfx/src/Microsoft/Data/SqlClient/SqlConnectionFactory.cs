// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{
    sealed internal class SqlConnectionFactory : DbConnectionFactory
    {
        private SqlConnectionFactory() : base()
        {
        }

        public static readonly SqlConnectionFactory SingletonInstance = new SqlConnectionFactory();
        private const string _metaDataXml = "MetaDataXml";

        override public DbProviderFactory ProviderFactory
        {
            get
            {
                return SqlClientFactory.Instance;
            }
        }

        override protected DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection)
        {
            return CreateConnection(options, poolKey, poolGroupProviderInfo, pool, owningConnection, userOptions: null);
        }

        override protected DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, IDbConnectionPool pool, DbConnection owningConnection, DbConnectionOptions userOptions)
        {
            SqlConnectionString opt = (SqlConnectionString)options;
            SqlConnectionPoolKey key = (SqlConnectionPoolKey)poolKey;
            SessionData recoverySessionData = null;
            SqlConnection sqlOwningConnection = owningConnection as SqlConnection;
            bool applyTransientFaultHandling = sqlOwningConnection != null ? sqlOwningConnection._applyTransientFaultHandling : false;

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

                if (pool == null || (pool != null && pool.Count <= 0))
                { // Non-pooled or pooled and no connections in the pool.

                    SqlInternalConnectionTds sseConnection = null;
                    try
                    {
                        // What about a failure - throw?  YES!
                        // BUG (VSTFDevDiv) 479687: Using TransactionScope with Linq2SQL against user instances fails with "connection has been broken" message
                        // NOTE: Cloning connection option opt to set 'UserInstance=True' and 'Enlist=False'
                        //       This first connection is established to SqlExpress to get the instance name 
                        //       of the UserInstance.
                        SqlConnectionString sseopt = new SqlConnectionString(opt, opt.DataSource, true /* user instance=true */, false /* set Enlist = false */);
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
                opt = new SqlConnectionString(
                    opt,
                    instanceName,
                    userInstance: false,
                    setEnlistValue: null); // Do not modify the enlist value
            }

            return new SqlInternalConnectionTds(
                identity,
                opt,
                key.Credential,
                providerInfo: null,
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

        protected override DbConnectionOptions CreateConnectionOptions(string connectionString, DbConnectionOptions previous)
        {
            Debug.Assert(!string.IsNullOrEmpty(connectionString), "empty connectionString");
            SqlConnectionString result = new SqlConnectionString(connectionString);
            return result;
        }

        override internal DbConnectionPoolProviderInfo CreateConnectionPoolProviderInfo(DbConnectionOptions connectionOptions)
        {
            DbConnectionPoolProviderInfo providerInfo = null;

            if (((SqlConnectionString)connectionOptions).UserInstance)
            {
                providerInfo = new SqlConnectionPoolProviderInfo();
            }

            return providerInfo;
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

                if (opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive)
                {
                    // interactive mode will always have pool's CreateTimeout = 10 x ConnectTimeout.
                    if (connectionTimeout >= Int32.MaxValue / 10)
                    {
                        connectionTimeout = Int32.MaxValue;
                    }
                    else
                    {
                        connectionTimeout *= 10;
                    }
                    SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnectionFactory.CreateConnectionPoolGroupOptions>Set connection pool CreateTimeout={0} when AD Interactive is in use.", connectionTimeout);
                }

                poolingOptions = new DbConnectionPoolGroupOptions(
                                                    opt.IntegratedSecurity || opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                                                    opt.MinPoolSize,
                                                    opt.MaxPoolSize,
                                                    connectionTimeout,
                                                    opt.LoadBalanceTimeout,
                                                    opt.Enlist);
            }
            return poolingOptions;
        }

        override protected DbMetaDataFactory CreateMetaDataFactory(DbConnectionInternal internalConnection, out bool cacheMetaDataFactory)
        {
            Debug.Assert(internalConnection != null, "internalConnection may not be null.");

            Stream xmlStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Data.SqlClient.SqlMetaData.xml");
            cacheMetaDataFactory = true;

            Debug.Assert(xmlStream != null, nameof(xmlStream) + " may not be null.");

            return new SqlMetaDataFactory(xmlStream,
                                          internalConnection.ServerVersion,
                                          internalConnection.ServerVersion); //internalConnection.ServerVersionNormalized);
        }

        override internal DbConnectionPoolGroupProviderInfo CreateConnectionPoolGroupProviderInfo(DbConnectionOptions connectionOptions)
        {
            return new SqlConnectionPoolGroupProviderInfo((SqlConnectionString)connectionOptions);
        }


        internal static SqlConnectionString FindSqlConnectionOptions(SqlConnectionPoolKey key)
        {
            SqlConnectionString connectionOptions = (SqlConnectionString)SingletonInstance.FindConnectionOptions(key);
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

        // @TODO: Should never be called
        private SqlInternalConnectionSmi GetContextConnection(SqlConnectionString options, object providerInfo)
        {
            SmiContext smiContext = SmiContextFactory.Instance.GetCurrentContext();

            SqlInternalConnectionSmi result = (SqlInternalConnectionSmi)smiContext.GetContextValue((int)SmiContextFactory.ContextKey.Connection);

            // context connections are automatically re-useable if they exist unless they've been doomed.
            if (result == null || result.IsConnectionDoomed)
            {
                if (result != null)
                {
                    result.Dispose();   // A doomed connection is a messy thing.  Dispose of it promptly in nearest receptacle.
                }

                result = new SqlInternalConnectionSmi(options, smiContext);
                smiContext.SetContextValue((int)SmiContextFactory.ContextKey.Connection, result);
            }

            result.Activate();

            return result;
        }

        override internal DbConnectionPoolGroup GetConnectionPoolGroup(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.PoolGroup;
            }
            return null;
        }

        override internal DbConnectionInternal GetInnerConnection(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.InnerConnection;
            }
            return null;
        }

        override protected int GetObjectId(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (c != null)
            {
                return c.ObjectID;
            }
            return 0;
        }

        override internal void PermissionDemand(DbConnection outerConnection)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (c != null)
            {
                c.PermissionDemand();
            }
        }

        override internal void SetConnectionPoolGroup(DbConnection outerConnection, DbConnectionPoolGroup poolGroup)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (c != null)
            {
                c.PoolGroup = poolGroup;
            }
        }

        override internal void SetInnerConnectionEvent(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                c.SetInnerConnectionEvent(to);
            }
        }

        override internal bool SetInnerConnectionFrom(DbConnection owningObject, DbConnectionInternal to, DbConnectionInternal from)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                return c.SetInnerConnectionFrom(to, from);
            }
            return false;
        }

        override internal void SetInnerConnectionTo(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (c != null)
            {
                c.SetInnerConnectionTo(to);
            }
        }

    }
}

