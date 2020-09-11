// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;

namespace Microsoft.Data.SqlClient
{
    sealed internal class SqlConnectionPoolGroupProviderInfo : DbConnectionPoolGroupProviderInfo
    {
        private string _alias;
        private System.Security.PermissionSet _failoverPermissionSet;
        private string _failoverPartner;
        private bool _useFailoverPartner;

        internal SqlConnectionPoolGroupProviderInfo(SqlConnectionString connectionOptions)
        {
            // This is for the case where the user specified the failover partner
            // in the connection string and we have not yet connected to get the 
            // env change.
            _failoverPartner = connectionOptions.FailoverPartner;

            if (ADP.IsEmpty(_failoverPartner))
            {
                _failoverPartner = null;
            }
        }

        internal string FailoverPartner
        {
            get
            {
                return _failoverPartner;
            }
        }

        internal bool UseFailoverPartner
        {
            get
            {
                return _useFailoverPartner;
            }
        }

        internal void AliasCheck(string server)
        {
            if (_alias != server)
            {
                lock (this)
                {
                    if (null == _alias)
                    {
                        _alias = server;
                    }
                    else if (_alias != server)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnectionPoolGroupProviderInfo|INFO> alias change detected. Clearing PoolGroup");
                        base.PoolGroup.Clear();
                        _alias = server;
                    }
                }
            }
        }

        private System.Security.PermissionSet CreateFailoverPermission(SqlConnectionString userConnectionOptions, string actualFailoverPartner)
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

            if (null == userConnectionOptions[SqlConnectionString.KEY.FailoverPartner])
            {
                keywordToReplace = SqlConnectionString.KEY.Data_Source;
            }
            else
            {
                keywordToReplace = SqlConnectionString.KEY.FailoverPartner;
            }

            string failoverConnectionString = userConnectionOptions.ExpandKeyword(keywordToReplace, actualFailoverPartner);
            return (new SqlConnectionString(failoverConnectionString)).CreatePermissionSet();
        }

        internal void FailoverCheck(SqlInternalConnection connection, bool actualUseFailoverPartner, SqlConnectionString userConnectionOptions, string actualFailoverPartner)
        {
            if (UseFailoverPartner != actualUseFailoverPartner)
            {
                // TODO: will connections in progress somehow be active for two different datasources?
                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnectionPoolGroupProviderInfo|INFO> Failover detected. failover partner='{0}'. Clearing PoolGroup", actualFailoverPartner);

                base.PoolGroup.Clear();
                _useFailoverPartner = actualUseFailoverPartner;
            }
            // Only construct a new permission set when we're connecting to the
            // primary data source, not the failover partner.
            if (!_useFailoverPartner && _failoverPartner != actualFailoverPartner)
            {
                // NOTE: we optimisitically generate the permission set to keep 
                //       lock short, but we only do this when we get a new
                //       failover partner.
                // TODO: it seems to me that being optimistic here may not be such a good idea; what if there are 100s of concurrent failovers?

                System.Security.PermissionSet failoverPermissionSet = CreateFailoverPermission(userConnectionOptions, actualFailoverPartner);

                lock (this)
                {
                    if (_failoverPartner != actualFailoverPartner)
                    {
                        _failoverPartner = actualFailoverPartner;
                        _failoverPermissionSet = failoverPermissionSet;
                    }
                }
            }
        }

        internal void FailoverPermissionDemand()
        {
            if (_useFailoverPartner)
            {
                // Note that we only demand when there is a permission set, which only
                // happens once we've identified a failover situation in FailoverCheck
                System.Security.PermissionSet failoverPermissionSet = _failoverPermissionSet;
                if (null != failoverPermissionSet)
                {
                    // demand on pooled failover connections
                    failoverPermissionSet.Demand();
                }
            }
        }
    }
}
