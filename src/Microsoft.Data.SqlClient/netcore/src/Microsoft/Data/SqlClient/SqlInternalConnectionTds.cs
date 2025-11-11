// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SessionStateRecord
    {
        internal bool _recoverable;
        internal uint _version;
        internal int _dataLength;
        internal byte[] _data;
    }

    internal sealed class SessionData
    {
        internal const int _maxNumberOfSessionStates = 256;
        internal uint _tdsVersion;
        internal bool _encrypted;

        internal string _database;
        internal SqlCollation _collation;
        internal string _language;

        internal string _initialDatabase;
        internal SqlCollation _initialCollation;
        internal string _initialLanguage;

        internal byte _unrecoverableStatesCount = 0;

        // @TODO: Introduce record/struct type to replace the tuple.
        internal Dictionary<string, Tuple<string, string>> _resolvedAliases;

#if DEBUG
        internal bool _debugReconnectDataApplied;
#endif

        internal SessionStateRecord[] _delta = new SessionStateRecord[_maxNumberOfSessionStates];
        internal bool _deltaDirty = false;
        internal byte[][] _initialState = new byte[_maxNumberOfSessionStates][];

        public SessionData(SessionData recoveryData)
        {
            _initialDatabase = recoveryData._initialDatabase;
            _initialCollation = recoveryData._initialCollation;
            _initialLanguage = recoveryData._initialLanguage;
            _resolvedAliases = recoveryData._resolvedAliases;

            for (int i = 0; i < _maxNumberOfSessionStates; i++)
            {
                if (recoveryData._initialState[i] != null)
                {
                    _initialState[i] = (byte[])recoveryData._initialState[i].Clone();
                }
            }
        }

        public SessionData()
        {
            _resolvedAliases = new Dictionary<string, Tuple<string, string>>(2);
        }

        public void Reset()
        {
            _database = null;
            _collation = null;
            _language = null;
            if (_deltaDirty)
            {
                Array.Clear(_delta, 0, _delta.Length);
                _deltaDirty = false;
            }
            _unrecoverableStatesCount = 0;
        }

        [Conditional("DEBUG")]
        public void AssertUnrecoverableStateCountIsCorrect()
        {
            byte unrecoverableCount = 0;
            foreach (var state in _delta)
            {
                if (state != null && !state._recoverable)
                {
                    unrecoverableCount++;
                }
            }
            Debug.Assert(unrecoverableCount == _unrecoverableStatesCount, "Unrecoverable count does not match");
        }
    }

    internal sealed partial class SqlInternalConnectionTds : SqlInternalConnection, IDisposable
    {
        internal partial class SyncAsyncLock
        {
            private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

            internal void Wait(bool canReleaseFromAnyThread)
            {
                Monitor.Enter(_semaphore); // semaphore is used as lock object, no relation to SemaphoreSlim.Wait/Release methods
                if (canReleaseFromAnyThread || _semaphore.CurrentCount == 0)
                {
                    _semaphore.Wait();
                    if (canReleaseFromAnyThread)
                    {
                        Monitor.Exit(_semaphore);
                    }
                    else
                    {
                        _semaphore.Release();
                    }
                }
            }

            internal void Wait(bool canReleaseFromAnyThread, int timeout, ref bool lockTaken)
            {
                lockTaken = false;
                bool hasMonitor = false;
                try
                {
                    Monitor.TryEnter(_semaphore, timeout, ref hasMonitor); // semaphore is used as lock object, no relation to SemaphoreSlim.Wait/Release methods
                    if (hasMonitor)
                    {
                        if ((canReleaseFromAnyThread) || (_semaphore.CurrentCount == 0))
                        {
                            if (_semaphore.Wait(timeout))
                            {
                                if (canReleaseFromAnyThread)
                                {
                                    Monitor.Exit(_semaphore);
                                    hasMonitor = false;
                                }
                                else
                                {
                                    _semaphore.Release();
                                }
                                lockTaken = true;
                            }
                        }
                        else
                        {
                            lockTaken = true;
                        }
                    }
                }
                finally
                {
                    if ((!lockTaken) && (hasMonitor))
                    {
                        Monitor.Exit(_semaphore);
                    }
                }
            }

            internal void Release()
            {
                if (_semaphore.CurrentCount == 0)
                {  //  semaphore methods were used for locking
                    _semaphore.Release();
                }
                else
                {
                    Monitor.Exit(_semaphore);
                }
            }


            internal bool CanBeReleasedFromAnyThread
            {
                get
                {
                    return _semaphore.CurrentCount == 0;
                }
            }

            // Necessary but not sufficient condition for thread to have lock (since semaphore may be obtained by any thread)
            internal bool ThreadMayHaveLock()
            {
                return Monitor.IsEntered(_semaphore) || _semaphore.CurrentCount == 0;
            }
        }

        internal SyncAsyncLock _parserLock = new SyncAsyncLock();
    }

    internal sealed class ServerInfo
    {
        internal string ExtendedServerName { get; private set; } // the resolved servername with protocol
        internal string ResolvedServerName { get; private set; } // the resolved servername only
        internal string ResolvedDatabaseName { get; private set; } // name of target database after resolution
        internal string UserProtocol { get; private set; } // the user specified protocol
        internal string ServerSPN { get; private set; } // the server SPN

        // The original user-supplied server name from the connection string.
        // If connection string has no Data Source, the value is set to string.Empty.
        // In case of routing, will be changed to routing destination
        internal string UserServerName
        {
            get
            {
                return _userServerName;
            }
            private set
            {
                _userServerName = value;
            }
        }
        private string _userServerName;

        internal readonly string PreRoutingServerName;

        // Initialize server info from connection options,
        internal ServerInfo(SqlConnectionString userOptions) : this(userOptions, userOptions.DataSource, userOptions.ServerSPN) { }

        // Initialize server info from connection options, but override DataSource and ServerSPN with given server name and server SPN
        internal ServerInfo(SqlConnectionString userOptions, string serverName, string serverSPN) : this(userOptions, serverName)
        {
            ServerSPN = serverSPN;
        }

        // Initialize server info from connection options, but override DataSource with given server name
        private ServerInfo(SqlConnectionString userOptions, string serverName)
        {
            //-----------------
            // Preconditions
            Debug.Assert(userOptions != null);

            //-----------------
            //Method body

            Debug.Assert(serverName != null, "server name should never be null");
            UserServerName = (serverName ?? string.Empty); // ensure user server name is not null

            UserProtocol = string.Empty;
            ResolvedDatabaseName = userOptions.InitialCatalog;
            PreRoutingServerName = null;
        }


        // Initialize server info from connection options, but override DataSource with given server name
        internal ServerInfo(SqlConnectionString userOptions, RoutingInfo routing, string preRoutingServerName, string serverSPN)
        {
            //-----------------
            // Preconditions
            Debug.Assert(userOptions != null && routing != null);

            //-----------------
            //Method body
            Debug.Assert(routing.ServerName != null, "server name should never be null");
            if (routing == null || routing.ServerName == null)
            {
                UserServerName = string.Empty; // ensure user server name is not null
            }
            else
            {
                UserServerName = string.Format(CultureInfo.InvariantCulture, "{0},{1}", routing.ServerName, routing.Port);
            }
            PreRoutingServerName = preRoutingServerName;
            UserProtocol = TdsEnums.TCP;
            SetDerivedNames(UserProtocol, UserServerName);
            ResolvedDatabaseName = userOptions.InitialCatalog;
            ServerSPN = serverSPN;
        }

        internal void SetDerivedNames(string protocol, string serverName)
        {
            // The following concatenates the specified netlib network protocol to the host string, if netlib is not null
            // and the flag is on.  This allows the user to specify the network protocol for the connection - but only
            // when using the Dbnetlib dll.  If the protocol is not specified, the netlib will
            // try all protocols in the order listed in the Client Network Utility.  Connect will
            // then fail if all protocols fail.
            if (!string.IsNullOrEmpty(protocol))
            {
                ExtendedServerName = protocol + ":" + serverName;
            }
            else
            {
                ExtendedServerName = serverName;
            }
            ResolvedServerName = serverName;
        }
    }
}
