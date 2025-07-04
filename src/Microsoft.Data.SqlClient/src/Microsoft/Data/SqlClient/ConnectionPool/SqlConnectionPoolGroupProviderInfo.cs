// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security;
using Microsoft.Data.Common.ConnectionString;

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    internal sealed class SqlConnectionPoolGroupProviderInfo : DbConnectionPoolGroupProviderInfo
    {
        private string _alias;
        private string _failoverPartner;
        private bool _useFailoverPartner;
#if NETFRAMEWORK
        private PermissionSet _failoverPermissionSet;
#endif

        internal SqlConnectionPoolGroupProviderInfo(SqlConnectionString connectionOptions)
        {
            // This is for the case where the user specified the failover partner
            // in the connection string and we have not yet connected to get the
            // env change.
            _failoverPartner = connectionOptions.FailoverPartner;

            if (string.IsNullOrEmpty(_failoverPartner))
            {
                _failoverPartner = null;
            }
        }

        internal string FailoverPartner => _failoverPartner;

        internal bool UseFailoverPartner => _useFailoverPartner;

        internal void AliasCheck(string server)
        {
            if (_alias != server)
            {
                lock (this)
                {
                    if (_alias == null)
                    {
                        _alias = server;
                    }
                    else if (_alias != server)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("SqlConnectionPoolGroupProviderInfo.AliasCheck | Info | Alias change detected. Clearing PoolGroup.");
                        base.PoolGroup.Clear();
                        _alias = server;
                    }
                }
            }
        }

        internal void FailoverCheck(bool actualUseFailoverPartner, SqlConnectionString userConnectionOptions, string actualFailoverPartner)
        {
            if (UseFailoverPartner != actualUseFailoverPartner)
            {
                SqlClientEventSource.Log.TryTraceEvent("SqlConnectionPoolGroupProviderInfo.FailoverCheck | Info | Failover detected. Failover partner '{0}'. Clearing PoolGroup", actualFailoverPartner);
                base.PoolGroup.Clear();
                _useFailoverPartner = actualUseFailoverPartner;
            }
            // Only construct a new permission set when we're connecting to the
            // primary data source, not the failover partner.
            if (!_useFailoverPartner && _failoverPartner != actualFailoverPartner)
            {
                // NOTE: we optimistically generate the permission set to keep
                //       lock short, but we only do this when we get a new
                //       failover partner.

#if NETFRAMEWORK
                PermissionSet failoverPermissionSet = CreateFailoverPermission(userConnectionOptions, actualFailoverPartner);
#endif
                lock (this)
                {
                    if (_failoverPartner != actualFailoverPartner)
                    {
                        _failoverPartner = actualFailoverPartner;
#if NETFRAMEWORK
                        _failoverPermissionSet = failoverPermissionSet;
#endif
                    }
                }
            }
        }

#if NETFRAMEWORK
        private PermissionSet CreateFailoverPermission(SqlConnectionString userConnectionOptions, string actualFailoverPartner)
        {
            string keywordToReplace;

            // RULES FOR CONSTRUCTING THE CONNECTION STRING TO DEMAND ON:
            //
            // 1) If no Failover Partner was specified in the original string:
            //
            //          Server=actualFailoverPartner
            //
            // 2) If Failover Partner was specified in the original string:
            //
            //          Server=originalValue; Failover Partner=actualFailoverPartner
            //
            // NOTE: in all cases, when we get a failover partner name from 
            //       the server, we will use that name over what was specified  
            //       in the original connection string.

            if (userConnectionOptions.ContainsKey(DbConnectionStringKeywords.FailoverPartner) &&
                userConnectionOptions[DbConnectionStringKeywords.FailoverPartner] == null)
            {
                keywordToReplace = DbConnectionStringKeywords.DataSource;
            }
            else
            {
                keywordToReplace = DbConnectionStringKeywords.FailoverPartner;
            }

            string failoverConnectionString = userConnectionOptions.ExpandKeyword(keywordToReplace, actualFailoverPartner);
            return (new SqlConnectionString(failoverConnectionString)).CreatePermissionSet();
        }

        internal void FailoverPermissionDemand()
        {
            if (_useFailoverPartner)
            {
                // Note that we only demand when there is a permission set, which only
                // happens once we've identified a failover situation in FailoverCheck
                PermissionSet failoverPermissionSet = _failoverPermissionSet;
                if (failoverPermissionSet != null)
                {
                    // demand on pooled failover connections
                    failoverPermissionSet.Demand();
                }
            }
        }
#endif
    }
}
