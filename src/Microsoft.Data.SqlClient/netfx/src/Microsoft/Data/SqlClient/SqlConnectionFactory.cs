// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{
    sealed internal class SqlConnectionFactory : DbConnectionFactory
    {
        private SqlConnectionFactory() : base(SqlPerformanceCounters.SingletonInstance)
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

        override protected DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, DbConnectionPool pool, DbConnection owningConnection)
        {
            return CreateConnection(options, poolKey, poolGroupProviderInfo, pool, owningConnection, userOptions: null);
        }

        override protected DbConnectionInternal CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, object poolGroupProviderInfo, DbConnectionPool pool, DbConnection owningConnection, DbConnectionOptions userOptions)
        {
            SqlConnectionString opt = (SqlConnectionString)options;
            SqlConnectionPoolKey key = (SqlConnectionPoolKey)poolKey;
            SqlInternalConnection result = null;
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

            if (opt.ContextConnection)
            {
                result = GetContextConnection(opt, poolGroupProviderInfo);
            }
            else
            {
                bool redirectedUserInstance = false;
                DbConnectionPoolIdentity identity = null;

                // Pass DbConnectionPoolIdentity to SqlInternalConnectionTds if using integrated security.
                // Used by notifications.
                if (opt.IntegratedSecurity || opt.UsesCertificate || opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
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

                    if ((null == pool) ||
                         (null != pool && pool.Count <= 0))
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

                            if (!instanceName.StartsWith("\\\\.\\", StringComparison.Ordinal))
                            {
                                throw SQL.NonLocalSSEInstance();
                            }

                            if (null != pool)
                            { // Pooled connection - cache result
                                SqlConnectionPoolProviderInfo providerInfo = (SqlConnectionPoolProviderInfo)pool.ProviderInfo;
                                // No lock since we are already in creation mutex
                                providerInfo.InstanceName = instanceName;
                            }
                        }
                        finally
                        {
                            if (null != sseConnection)
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
                    opt = new SqlConnectionString(opt, instanceName, false /* user instance=false */, null /* do not modify the Enlist value */);
                    poolGroupProviderInfo = null; // null so we do not pass to constructor below...
                }
                result = new SqlInternalConnectionTds(identity, opt, key.Credential, poolGroupProviderInfo, "", null, redirectedUserInstance, userOpt, recoverySessionData, key.ServerCertificateValidationCallback, key.ClientCertificateRetrievalCallback, pool, key.AccessToken, key.OriginalNetworkAddressInfo, applyTransientFaultHandling: applyTransientFaultHandling);
            }
            return result;
        }

        protected override DbConnectionOptions CreateConnectionOptions(string connectionString, DbConnectionOptions previous)
        {
            Debug.Assert(!ADP.IsEmpty(connectionString), "empty connectionString");
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

        override protected DbConnectionPoolGroupOptions CreateConnectionPoolGroupOptions(DbConnectionOptions connectionOptions)
        {
            SqlConnectionString opt = (SqlConnectionString)connectionOptions;

            DbConnectionPoolGroupOptions poolingOptions = null;

            if (!opt.ContextConnection && opt.Pooling)
            {    // never pool context connections.
                int connectionTimeout = opt.ConnectTimeout;

                if ((0 < connectionTimeout) && (connectionTimeout < Int32.MaxValue / 1000))
                    connectionTimeout *= 1000;
                else if (connectionTimeout >= Int32.MaxValue / 1000)
                    connectionTimeout = Int32.MaxValue;

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
                                                    opt.IntegratedSecurity || opt.UsesCertificate || opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                                                    opt.MinPoolSize,
                                                    opt.MaxPoolSize,
                                                    connectionTimeout,
                                                    opt.LoadBalanceTimeout,
                                                    opt.Enlist);
            }
            return poolingOptions;
        }

        // SxS (VSDD 545786): metadata files are opened from <.NetRuntimeFolder>\CONFIG\<metadatafilename.xml>
        // this operation is safe in SxS because the file is opened in read-only mode and each NDP runtime accesses its own copy of the metadata
        // under the runtime folder.
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        override protected DbMetaDataFactory CreateMetaDataFactory(DbConnectionInternal internalConnection, out bool cacheMetaDataFactory)
        {
            Debug.Assert(internalConnection != null, "internalConnection may not be null.");
            cacheMetaDataFactory = false;

            if (internalConnection is SqlInternalConnectionSmi)
            {
                throw SQL.NotAvailableOnContextConnection();
            }

            NameValueCollection settings = (NameValueCollection)PrivilegedConfigurationManager.GetSection("Microsoft.Data.SqlClient");
            Stream XMLStream = null;
            if (settings != null)
            {
                string[] values = settings.GetValues(_metaDataXml);
                if (values != null)
                {
                    XMLStream = ADP.GetXmlStreamFromValues(values, _metaDataXml);
                }
            }

            // if the xml was not obtained from machine.config use the embedded XML resource
            if (XMLStream == null)
            {
                XMLStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Data.SqlClient.SqlMetaData.xml");
                cacheMetaDataFactory = true;
            }
            Debug.Assert(XMLStream != null, "XMLstream may not be null.");

            return new SqlMetaDataFactory(XMLStream,
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
            if (null == connectionOptions)
            {
                connectionOptions = new SqlConnectionString(key.ConnectionString);
            }
            if (connectionOptions.IsEmpty)
            {
                throw ADP.NoConnectionString();
            }
            return connectionOptions;
        }

        private SqlInternalConnectionSmi GetContextConnection(SqlConnectionString options, object providerInfo)
        {
            SmiContext smiContext = SmiContextFactory.Instance.GetCurrentContext();

            SqlInternalConnectionSmi result = (SqlInternalConnectionSmi)smiContext.GetContextValue((int)SmiContextFactory.ContextKey.Connection);

            // context connections are automatically re-useable if they exist unless they've been doomed.
            if (null == result || result.IsConnectionDoomed)
            {
                if (null != result)
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
            if (null != c)
            {
                return c.PoolGroup;
            }
            return null;
        }

        override internal DbConnectionInternal GetInnerConnection(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (null != c)
            {
                return c.InnerConnection;
            }
            return null;
        }

        override protected int GetObjectId(DbConnection connection)
        {
            SqlConnection c = (connection as SqlConnection);
            if (null != c)
            {
                return c.ObjectID;
            }
            return 0;
        }

        override internal void PermissionDemand(DbConnection outerConnection)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (null != c)
            {
                c.PermissionDemand();
            }
        }

        override internal void SetConnectionPoolGroup(DbConnection outerConnection, DbConnectionPoolGroup poolGroup)
        {
            SqlConnection c = (outerConnection as SqlConnection);
            if (null != c)
            {
                c.PoolGroup = poolGroup;
            }
        }

        override internal void SetInnerConnectionEvent(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (null != c)
            {
                c.SetInnerConnectionEvent(to);
            }
        }

        override internal bool SetInnerConnectionFrom(DbConnection owningObject, DbConnectionInternal to, DbConnectionInternal from)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (null != c)
            {
                return c.SetInnerConnectionFrom(to, from);
            }
            return false;
        }

        override internal void SetInnerConnectionTo(DbConnection owningObject, DbConnectionInternal to)
        {
            SqlConnection c = (owningObject as SqlConnection);
            if (null != c)
            {
                c.SetInnerConnectionTo(to);
            }
        }

    }

    [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Name = "FullTrust")]
    sealed internal class SqlPerformanceCounters : DbConnectionPoolCounters
    {
        private const string CategoryName = ".NET Data Provider for SqlServer";
        private const string CategoryHelp = "Counters for Microsoft.Data.SqlClient";

        public static readonly SqlPerformanceCounters SingletonInstance = new SqlPerformanceCounters();

        [System.Diagnostics.PerformanceCounterPermissionAttribute(System.Security.Permissions.SecurityAction.Assert, PermissionAccess = PerformanceCounterPermissionAccess.Write, MachineName = ".", CategoryName = CategoryName)]
        private SqlPerformanceCounters() : base(CategoryName, CategoryHelp)
        {
        }
    }
}

