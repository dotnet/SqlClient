// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: This is only a stub class for removing clearing errors while merging other files.

using System;
using System.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;

namespace Microsoft.Data.SqlClient
{
    public class SqlConnection : DbConnection
    {
        internal bool _applyTransientFaultHandling = false;
        internal SessionData _recoverySessionData = null;
        
        internal Guid ClientConnectionId { get; set; }

        #if NETFRAMEWORK
        internal static System.Security.CodeAccessPermission ExecutePermission { get; set; }
        #endif

        internal DbConnectionInternal InnerConnection { get; set; }
        
        internal int ObjectID { get; set; }
        
        internal DbConnectionPoolGroup PoolGroup { get; set; } 
        
        internal SqlStatistics Statistics { get; set; }
        
        internal DbConnectionOptions UserConnectionOptions { get; set; }

        internal void Abort(Exception e) { }
        
        internal void PermissionDemand() { }

        internal void SetInnerConnectionEvent(DbConnectionInternal to) { }

        internal bool SetInnerConnectionFrom(DbConnectionInternal to, DbConnectionInternal from) => false;

        internal void SetInnerConnectionTo(DbConnectionInternal to) { }
    }
}
