// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: This is only a stub class for clearing errors while merging other files.

using System;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.SqlServer.Server;

namespace Microsoft.Data.SqlClient
{
    public class SqlConnection : DbConnection
    {
        internal bool _applyTransientFaultHandling = false;
        internal Task _currentReconnectionTask = null;
        internal SessionData _recoverySessionData = null;
        
        #region Constructors
        
        internal SqlConnection() {}
        
        internal SqlConnection(string connectionString) {} 
        
        #endregion
        
        #region Properties
        
        internal Guid ClientConnectionId { get; set; }

        #if NETFRAMEWORK
        internal static System.Security.CodeAccessPermission ExecutePermission { get; set; }
        #endif

        internal bool HasLocalTransaction { get; set; }
        
        internal DbConnectionInternal InnerConnection { get; set; }
        
        internal bool Is2008OrNewer { get; set; }

        internal int ObjectID { get; set; }
        
        internal TdsParser Parser { get; set; }
        
        internal DbConnectionPoolGroup PoolGroup { get; set; }
        
        internal SqlStatistics Statistics { get; set; }
        
        internal bool StatisticsEnabled { get; set; }
        
        internal DbConnectionOptions UserConnectionOptions { get; set; }
        
        #endregion
        
        #region Methods
        
        internal void Abort(Exception e) { }

        internal void AddWeakReference(object value, int tag) { }
        
        internal SqlTransaction BeginTransaction() =>
            null;

        internal byte[] GetBytes(object o, out Format format, out int maxSize)
        {
            format = Format.Unknown;
            maxSize = 0;
            return null;
        }
        
        internal SqlInternalConnectionTds GetOpenTdsConnection() => null;

        internal void PermissionDemand() { }
        
        internal Task<T> RegisterForConnectionCloseNotification<T>(Task<T> outerTask, object value, int tag) =>
            Task.FromResult<T>(default);
        
        internal void SetInnerConnectionEvent(DbConnectionInternal to) { }

        internal bool SetInnerConnectionFrom(DbConnectionInternal to, DbConnectionInternal from) =>
            false;
        
        internal void SetInnerConnectionTo(DbConnectionInternal to) { }

        internal void ValidateConnectionForExecute(string method, SqlCommand command) { }

        internal Task ValidateAndReconnect(Action beforeDisconnect, int timeout) =>
            null;

        #endregion
    }
}
