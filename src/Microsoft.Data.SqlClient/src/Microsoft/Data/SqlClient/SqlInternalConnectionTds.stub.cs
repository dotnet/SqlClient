// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// @TODO: This is only a stub class for clearing errors while merging other files.

using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ConnectionPool;

namespace Microsoft.Data.SqlClient
{
    internal partial class SqlInternalConnectionTds
    {
        internal SyncAsyncLock _parserLock = null;

        internal SqlInternalConnectionTds(
            DbConnectionPoolIdentity identity,
            SqlConnectionString connectionOptions,
            SqlCredential credential,
            object providerInfo,
            string newPassword,
            SecureString newSecurePassword,
            bool redirectedUserInterface,
            SqlConnectionString userConnectionOptions = null,
            SessionData reconnectSessionData = null,
            bool applyTransientFaultHandling = false,
            string accessToken = null,
            IDbConnectionPool pool = null,
            Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> accessTokenCallback = null)
        {
        }
        
        internal bool ThreadHasParserLockForClose { get; set; }

        internal void Dispose() { }
        
        internal void DoomThisConnection() { }
        
        internal class SyncAsyncLock
        {
            internal bool CanBeReleasedFromAnyThread { get; set; }
            
            internal void Release() { }
            
            internal void Wait(bool canReleaseFromAnyThread) {}
        }
    }
}
