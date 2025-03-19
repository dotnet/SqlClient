// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Microsoft.Identity.Client;
using System.Transactions;


namespace Microsoft.Data.SqlClient
{
    internal class SessionStateRecord
    {
        internal bool _recoverable;
        internal UInt32 _version;
        internal Int32 _dataLength;
        internal byte[] _data;
    }

    internal class SessionData
    {
        internal const int _maxNumberOfSessionStates = 256;
        internal UInt32 _tdsVersion;
        internal bool _encrypted;

        internal string _database;
        internal SqlCollation _collation;
        internal string _language;

        internal string _initialDatabase;
        internal SqlCollation _initialCollation;
        internal string _initialLanguage;

        internal byte _unrecoverableStatesCount = 0;
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
                _delta = new SessionStateRecord[_maxNumberOfSessionStates];
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
                    unrecoverableCount++;
            }
            Debug.Assert(unrecoverableCount == _unrecoverableStatesCount, "Unrecoverable count does not match");
        }
    }

    sealed internal class SqlInternalConnectionTds : SqlInternalConnection, IDisposable
    {
        // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/retry-after#simple-retry-for-errors-with-http-error-codes-500-600
        internal const int MsalHttpRetryStatusCode = 429;

        // Connection re-route limit
        internal const int _maxNumberOfRedirectRoute = 10;

        // CONNECTION AND STATE VARIABLES
        private readonly SqlConnectionPoolGroupProviderInfo _poolGroupProviderInfo; // will only be null when called for ChangePassword, or creating SSE User Instance
        private TdsParser _parser;
        private SqlLoginAck _loginAck;
        private SqlCredential _credential;
        private FederatedAuthenticationFeatureExtensionData _fedAuthFeatureExtensionData;

        // Connection Resiliency
        private bool _sessionRecoveryRequested;
        internal bool _sessionRecoveryAcknowledged;
        internal SessionData _currentSessionData; // internal for use from TdsParser only, other should use CurrentSessionData property that will fix database and language
        private SessionData _recoverySessionData;

        // Federated Authentication
        // Response obtained from the server for FEDAUTHREQUIRED prelogin option.
        internal bool _fedAuthRequired;

        internal bool _federatedAuthenticationRequested;
        internal bool _federatedAuthenticationAcknowledged;
        internal bool _federatedAuthenticationInfoRequested; // Keep this distinct from _federatedAuthenticationRequested, since some fedauth library types may not need more info
        internal bool _federatedAuthenticationInfoReceived;

        // The Federated Authentication returned by TryGetFedAuthTokenLocked or GetFedAuthToken.
        SqlFedAuthToken _fedAuthToken = null;
        internal byte[] _accessTokenInBytes;
        internal readonly Func<SqlAuthenticationParameters, CancellationToken,Task<SqlAuthenticationToken>> _accessTokenCallback;

        private readonly ActiveDirectoryAuthenticationTimeoutRetryHelper _activeDirectoryAuthTimeoutRetryHelper;

        internal bool _cleanSQLDNSCaching = false;
        private bool _serverSupportsDNSCaching = false;

        /// <summary>
        /// Returns buffer time allowed before access token expiry to continue using the access token.
        /// </summary>
        private int accessTokenExpirationBufferTime
        {
            get
            {
                return (ConnectionOptions.ConnectTimeout == ADP.InfiniteConnectionTimeout || ConnectionOptions.ConnectTimeout >= ADP.MaxBufferAccessTokenExpiry)
                    ? ADP.MaxBufferAccessTokenExpiry : ConnectionOptions.ConnectTimeout;
            }
        }

        /// <summary>
        /// Get or set if SQLDNSCaching FeatureExtAck is supported by the server.
        /// </summary>
        internal bool IsSQLDNSCachingSupported
        {
            get
            {
                return _serverSupportsDNSCaching;
            }
            set
            {
                _serverSupportsDNSCaching = value;
            }
        }

        private bool _SQLDNSRetryEnabled = false;

        /// <summary>
        /// Get or set if we need retrying with IP received from FeatureExtAck.
        /// </summary>
        internal bool IsSQLDNSRetryEnabled
        {
            get
            {
                return _SQLDNSRetryEnabled;
            }
            set
            {
                _SQLDNSRetryEnabled = value;
            }
        }

        private bool DNSCachingBeforeRedirect = false;

        /// <summary>
        /// Get or set if the control ring send redirect token and SQLDNSCaching FeatureExtAck with true
        /// </summary>
        internal bool IsDNSCachingBeforeRedirectSupported
        {
            get
            {
                return DNSCachingBeforeRedirect;
            }
            set
            {
                DNSCachingBeforeRedirect = value;
            }
        }

        internal SQLDNSInfo pendingSQLDNSObject = null;

        // Json Support Flag
        internal bool IsJsonSupportEnabled = false;

        // Vector Support Flag
        internal bool IsVectorSupportEnabled = false;

        // TCE flags
        internal byte _tceVersionSupported;

        // The pool that this connection is associated with, if at all it is.
        private DbConnectionPool _dbConnectionPool;

        // This is used to preserve the authentication context object if we decide to cache it for subsequent connections in the same pool.
        // This will finally end up in _dbConnectionPool.AuthenticationContexts, but only after 1 successful login to SQL Server using this context.
        // This variable is to persist the context after we have generated it, but before we have successfully completed the login with this new context.
        // If this connection attempt ended up re-using the existing context and not create a new one, this will be null (since the context is not new).
        private DbConnectionPoolAuthenticationContext _newDbConnectionPoolAuthenticationContext;

        // The key of the authentication context, built from information found in the FedAuthInfoToken.
        private DbConnectionPoolAuthenticationContextKey _dbConnectionPoolAuthenticationContextKey;

#if DEBUG
        // This is a test hook to enable testing of the retry paths for MSAL get access token.
        // Sample code to enable:
        //
        //    Type type = typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.SQLInternalConnectionTds");
        //    System.Reflection.FieldInfo field = type.GetField("_forceMsalRetry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        //    if (field != null) {
        //        field.SetValue(null, true);
        //    }
        //
        internal static bool _forceMsalRetry = false;

        // This is a test hook to simulate a token expiring within the next 45 minutes.
        private static bool _forceExpiryLocked = false;

        // This is a test hook to simulate a token expiring within the next 10 minutes.
        private static bool _forceExpiryUnLocked = false;
#endif //DEBUG

        // The timespan defining the amount of time the authentication context needs to be valid for at-least, to re-use the cached context,
        // without making an attempt to refresh it. IF the context is expiring within the next 45 mins, then try to take a lock and refresh
        // the context, if the lock is acquired.
        private static readonly TimeSpan _dbAuthenticationContextLockedRefreshTimeSpan = new TimeSpan(hours: 0, minutes: 45, seconds: 00);

        // The timespan defining the minimum amount of time the authentication context needs to be valid for re-using the cached context.
        // If the context is expiring within the next 10 mins, then create a new context, irrespective of if another thread is trying to do the same.
        private static readonly TimeSpan _dbAuthenticationContextUnLockedRefreshTimeSpan = new TimeSpan(hours: 0, minutes: 10, seconds: 00);

        private readonly TimeoutTimer _timeout;

        private static HashSet<int> transientErrors = new HashSet<int>();

        internal SessionData CurrentSessionData
        {
            get
            {
                if (_currentSessionData != null)
                {
                    _currentSessionData._database = CurrentDatabase;
                    _currentSessionData._language = _currentLanguage;
                }
                return _currentSessionData;
            }
        }

        // FOR POOLING
        private bool _fConnectionOpen = false;

        // FOR CONNECTION RESET MANAGEMENT
        private bool _fResetConnection;
        private string _originalDatabase;
        private string _currentFailoverPartner;                     // only set by ENV change from server
        private string _originalLanguage;
        private string _currentLanguage;
        private int _currentPacketSize;
        private int _asyncCommandCount; // number of async Begins minus number of async Ends.

        // FOR SSE
        private string _instanceName = string.Empty;

        // FOR NOTIFICATIONS
        private DbConnectionPoolIdentity _identity; // Used to lookup info for notification matching Start().

        // FOR SYNCHRONIZATION IN TdsParser
        // How to use these locks:
        // 1. Whenever writing to the connection (with the exception of Cancellation) the _parserLock MUST be taken
        // 2. _parserLock will also be taken during close (to prevent closing in the middle of a write)
        // 3. Whenever you have the _parserLock and are calling a method that would cause the connection to close if it failed (with the exception of any writing method), you MUST set ThreadHasParserLockForClose to true
        //      * This is to prevent the connection deadlocking with itself (since you already have the _parserLock, and Closing the connection will attempt to re-take that lock)
        //      * It is safe to set ThreadHasParserLockForClose to true when writing as well, but it is unneccesary
        //      * If you have a method that takes _parserLock, it is a good idea check ThreadHasParserLockForClose first (if you don't expect _parserLock to be taken by something higher on the stack, then you should at least assert that it is false)
        // 4. ThreadHasParserLockForClose is thread-specific - this means that you must set it to false before returning a Task, and set it back to true in the continuation
        // 5. ThreadHasParserLockForClose should only be modified if you currently own the _parserLock
        // 6. Reading ThreadHasParserLockForClose is thread-safe
        internal class SyncAsyncLock
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(1);

            internal void Wait(bool canReleaseFromAnyThread)
            {
                Monitor.Enter(semaphore); // semaphore is used as lock object, no relation to SemaphoreSlim.Wait/Release methods
                if (canReleaseFromAnyThread || semaphore.CurrentCount == 0)
                {
                    semaphore.Wait();
                    if (canReleaseFromAnyThread)
                    {
                        Monitor.Exit(semaphore);
                    }
                    else
                    {
                        semaphore.Release();
                    }
                }
            }

            internal void Wait(bool canReleaseFromAnyThread, int timeout, ref bool lockTaken)
            {
                lockTaken = false;
                bool hasMonitor = false;
                try
                {
                    Monitor.TryEnter(semaphore, timeout, ref hasMonitor); // semaphore is used as lock object, no relation to SemaphoreSlim.Wait/Release methods
                    if (hasMonitor)
                    {
                        if ((canReleaseFromAnyThread) || (semaphore.CurrentCount == 0))
                        {
                            if (semaphore.Wait(timeout))
                            {
                                if (canReleaseFromAnyThread)
                                {
                                    Monitor.Exit(semaphore);
                                    hasMonitor = false;
                                }
                                else
                                {
                                    semaphore.Release();
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
                        Monitor.Exit(semaphore);
                    }
                }
            }

            internal void Release()
            {
                if (semaphore.CurrentCount == 0)
                {  //  semaphore methods were used for locking
                    semaphore.Release();
                }
                else
                {
                    Monitor.Exit(semaphore);
                }
            }


            internal bool CanBeReleasedFromAnyThread
            {
                get
                {
                    return semaphore.CurrentCount == 0;
                }
            }

            // Necessary but not sufficient condition for thread to have lock (since sempahore may be obtained by any thread)
            internal bool ThreadMayHaveLock()
            {
                return Monitor.IsEntered(semaphore) || semaphore.CurrentCount == 0;
            }
        }


        internal SyncAsyncLock _parserLock = new SyncAsyncLock();
        private int _threadIdOwningParserLock = -1;

        private SqlConnectionTimeoutErrorInternal _timeoutErrorInternal;

        internal SqlConnectionTimeoutErrorInternal TimeoutErrorInternal
        {
            get { return _timeoutErrorInternal; }
        }

        // OTHER STATE VARIABLES AND REFERENCES

        internal Guid _clientConnectionId = Guid.Empty;

        // Routing information (ROR)
        RoutingInfo _routingInfo = null;
        private Guid _originalClientConnectionId = Guid.Empty;
        private string _routingDestination = null;

        static SqlInternalConnectionTds()
        {
            populateTransientErrors();
        }

        // although the new password is generally not used it must be passed to the c'tor
        // the new Login7 packet will always write out the new password (or a length of zero and no bytes if not present)
        //
        internal SqlInternalConnectionTds(
                DbConnectionPoolIdentity identity,
                SqlConnectionString connectionOptions,
                SqlCredential credential,
                object providerInfo,
                string newPassword,
                SecureString newSecurePassword,
                bool redirectedUserInstance,
                SqlConnectionString userConnectionOptions = null, // NOTE: userConnectionOptions may be different to connectionOptions if the connection string has been expanded (see SqlConnectionString.Expand)
                SessionData reconnectSessionData = null,
                DbConnectionPool pool = null,
                string accessToken = null,
                bool applyTransientFaultHandling = false,
                Func<SqlAuthenticationParameters, CancellationToken,
                Task<SqlAuthenticationToken>> accessTokenCallback = null) : base(connectionOptions)
        {

#if DEBUG
            if (reconnectSessionData != null)
            {
                reconnectSessionData._debugReconnectDataApplied = true;
            }
            try
            { // use this to help validate this object is only created after the following permission has been previously demanded in the current codepath
                if (userConnectionOptions != null)
                {
                    // As mentioned above, userConnectionOptions may be different to connectionOptions, so we need to demand on the correct connection string
                    userConnectionOptions.DemandPermission();
                }
                else
                {
                    connectionOptions.DemandPermission();
                }
            }
            catch (System.Security.SecurityException)
            {
                System.Diagnostics.Debug.Assert(false, "unexpected SecurityException for current codepath");
                throw;
            }
#endif
            Debug.Assert(reconnectSessionData == null || connectionOptions.ConnectRetryCount > 0, "Reconnect data supplied with CR turned off");

            _dbConnectionPool = pool;

            if (connectionOptions.ConnectRetryCount > 0)
            {
                _recoverySessionData = reconnectSessionData;
                if (reconnectSessionData == null)
                {
                    _currentSessionData = new SessionData();
                }
                else
                {
                    _currentSessionData = new SessionData(_recoverySessionData);
                    _originalDatabase = _recoverySessionData._initialDatabase;
                    _originalLanguage = _recoverySessionData._initialLanguage;
                }
            }

            if (connectionOptions.UserInstance && InOutOfProcHelper.InProc)
            {
                throw SQL.UserInstanceNotAvailableInProc();
            }

            if (accessToken != null)
            {
                _accessTokenInBytes = System.Text.Encoding.Unicode.GetBytes(accessToken);
            }

            _accessTokenCallback = accessTokenCallback;

            _activeDirectoryAuthTimeoutRetryHelper = new ActiveDirectoryAuthenticationTimeoutRetryHelper();

            _identity = identity;
            Debug.Assert(newSecurePassword != null || newPassword != null, "cannot have both new secure change password and string based change password to be null");
            Debug.Assert(credential == null || (string.IsNullOrEmpty(connectionOptions.UserID) && string.IsNullOrEmpty(connectionOptions.Password)), "cannot mix the new secure password system and the connection string based password");

            Debug.Assert(credential == null || !connectionOptions.IntegratedSecurity, "Cannot use SqlCredential and Integrated Security");
            Debug.Assert(credential == null || !connectionOptions.ContextConnection, "Cannot use SqlCredential with context connection");

            _poolGroupProviderInfo = (SqlConnectionPoolGroupProviderInfo)providerInfo;
            _fResetConnection = connectionOptions.ConnectionReset;
            if (_fResetConnection && _recoverySessionData == null)
            {
                _originalDatabase = connectionOptions.InitialCatalog;
                _originalLanguage = connectionOptions.CurrentLanguage;
            }

            _timeoutErrorInternal = new SqlConnectionTimeoutErrorInternal();
            _credential = credential;

            _parserLock.Wait(canReleaseFromAnyThread: false);
            ThreadHasParserLockForClose = true;   // In case of error, let ourselves know that we already own the parser lock
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                _timeout = TimeoutTimer.StartSecondsTimeout(connectionOptions.ConnectTimeout);

                // If transient fault handling is enabled then we can retry the login upto the ConnectRetryCount.
                int connectionEstablishCount = applyTransientFaultHandling ? connectionOptions.ConnectRetryCount + 1 : 1;
                int transientRetryIntervalInMilliSeconds = connectionOptions.ConnectRetryInterval * 1000; // Max value of transientRetryInterval is 60*1000 ms. The max value allowed for ConnectRetryInterval is 60
                for (int i = 0; i < connectionEstablishCount; i++)
                {
                    try
                    {
                        OpenLoginEnlist(_timeout, connectionOptions, credential, newPassword, newSecurePassword, redirectedUserInstance);
                        break;
                    }
                    catch (SqlException sqlex)
                    {
                        if (i + 1 == connectionEstablishCount
                          || !applyTransientFaultHandling
                          || _timeout.IsExpired
                          || _timeout.MillisecondsRemaining < transientRetryIntervalInMilliSeconds
                          || !IsTransientError(sqlex))
                        {
                            throw;
                        }
                        else
                        {
                            Thread.Sleep(transientRetryIntervalInMilliSeconds);
                        }
                    }
                }
            }
            catch (System.OutOfMemoryException)
            {
                DoomThisConnection();
                throw;
            }
            catch (System.StackOverflowException)
            {
                DoomThisConnection();
                throw;
            }
            catch (System.Threading.ThreadAbortException)
            {
                DoomThisConnection();
                throw;
            }
            finally
            {
                ThreadHasParserLockForClose = false;
                _parserLock.Release();
            }
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.ctor|ADV> {0}, constructed new TDS internal connection", ObjectID);
        }

        // The errors in the transient error set are contained in
        // https://azure.microsoft.com/en-us/documentation/articles/sql-database-develop-error-messages/#transient-faults-connection-loss-and-other-temporary-errors
        private static void populateTransientErrors()
        {
            // SQL Error Code: 4060
            // Cannot open database "%.*ls" requested by the login. The login failed.
            transientErrors.Add(4060);
            // SQL Error Code: 10928
            // Resource ID: %d. The %s limit for the database is %d and has been reached.
            transientErrors.Add(10928);
            // SQL Error Code: 10929
            // Resource ID: %d. The %s minimum guarantee is %d, maximum limit is %d and the current usage for the database is %d.
            // However, the server is currently too busy to support requests greater than %d for this database.
            transientErrors.Add(10929);
            // SQL Error Code: 40197
            // You will receive this error, when the service is down due to software or hardware upgrades, hardware failures,
            // or any other failover problems. The error code (%d) embedded within the message of error 40197 provides
            // additional information about the kind of failure or failover that occurred. Some examples of the error codes are
            // embedded within the message of error 40197 are 40020, 40143, 40166, and 40540.
            transientErrors.Add(40197);
            transientErrors.Add(40020);
            transientErrors.Add(40143);
            transientErrors.Add(40166);
            // The service has encountered an error processing your request. Please try again.
            transientErrors.Add(40540);
            // The service is currently busy. Retry the request after 10 seconds. Incident ID: %ls. Code: %d.
            transientErrors.Add(40501);
            // Database '%.*ls' on server '%.*ls' is not currently available. Please retry the connection later.
            // If the problem persists, contact customer support, and provide them the session tracing ID of '%.*ls'.
            transientErrors.Add(40613);

            // Can not connect to the SQL pool since it is paused. Please resume the SQL pool and try again.
            transientErrors.Add(42108);

            // The SQL pool is warming up. Please try again.
            transientErrors.Add(42109);
            // Do federation errors deserve to be here ?
            // Note: Federation errors 10053 and 10054 might also deserve inclusion in your retry logic.
            //transientErrors.Add(10053);
            //transientErrors.Add(10054);
        }


        // Returns true if the Sql error is a transient.
        private bool IsTransientError(SqlException exc)
        {
            if (exc == null)
            {
                return false;
            }
            foreach (SqlError error in exc.Errors)
            {
                if (transientErrors.Contains(error.Number))
                {
                    // When server timeouts, connection is doomed. Reset here to allow reconnect.
                    UnDoomThisConnection();
                    return true;
                }
            }
            return false;
        }

        internal Guid ClientConnectionId
        {
            get
            {
                return _clientConnectionId;
            }
        }

        internal Guid OriginalClientConnectionId
        {
            get
            {
                return _originalClientConnectionId;
            }
        }

        internal string RoutingDestination
        {
            get
            {
                return _routingDestination;
            }
        }

        internal RoutingInfo RoutingInfo
        {
            get
            {
                return _routingInfo;
            }
        }

        override internal SqlInternalTransaction CurrentTransaction
        {
            get
            {
                return _parser.CurrentTransaction;
            }
        }

        override internal SqlInternalTransaction AvailableInternalTransaction
        {
            get
            {
                return _parser._fResetConnection ? null : CurrentTransaction;
            }
        }


        override internal SqlInternalTransaction PendingTransaction
        {
            get
            {
                return _parser.PendingTransaction;
            }
        }

        internal DbConnectionPoolIdentity Identity
        {
            get
            {
                return _identity;
            }
        }

        internal string InstanceName
        {
            get
            {
                return _instanceName;
            }
        }

        override internal bool IsLockedForBulkCopy
        {
            get
            {
                return (!Parser.MARSOn && Parser._physicalStateObj.BcpLock);
            }
        }

        override protected internal bool IsNonPoolableTransactionRoot
        {
            get
            {
                return IsTransactionRoot && (!Is2008OrNewer || Pool == null);
            }
        }

        override internal bool Is2008OrNewer
        {
            get
            {
                return _parser.Is2008OrNewer;
            }
        }

        internal int PacketSize
        {
            get
            {
                return _currentPacketSize;
            }
        }

        internal TdsParser Parser
        {
            get
            {
                return _parser;
            }
        }

        internal string ServerProvidedFailOverPartner
        {
            get
            {
                return _currentFailoverPartner;
            }
        }

        internal SqlConnectionPoolGroupProviderInfo PoolGroupProviderInfo
        {
            get
            {
                return _poolGroupProviderInfo;
            }
        }

        override protected bool ReadyToPrepareTransaction
        {
            get
            {
                // TODO: probably need to use a different method, but that's a different bug
                bool result = FindLiveReader(null) == null; // can't prepare with a live data reader...
                return result;
            }
        }

        override public string ServerVersion
        {
            get
            {
                return (string.Format("{0:00}.{1:00}.{2:0000}", _loginAck.majorVersion,
                       (short)_loginAck.minorVersion, _loginAck.buildNum));
            }
        }
        public int ServerProcessId
        {
            get
            {
                return Parser._physicalStateObj._spid;
            }
        }

        /// <summary>
        /// Get boolean that specifies whether an enlisted transaction can be unbound from
        /// the connection when that transaction completes.
        /// </summary>
        /// <value>
        /// This override always returns false.
        /// </value>
        /// <remarks>
        /// The SqlInternalConnectionTds.CheckEnlistedTransactionBinding method handles implicit unbinding for disposed transactions.
        /// </remarks>
        protected override bool UnbindOnTransactionCompletion
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Validates if federated authentication is used, Access Token used by this connection is active for the value of 'accessTokenExpirationBufferTime'.
        /// </summary>
        internal override bool IsAccessTokenExpired => _federatedAuthenticationInfoRequested && DateTime.FromFileTimeUtc(_fedAuthToken.expirationFileTime) < DateTime.UtcNow.AddSeconds(accessTokenExpirationBufferTime);

        ////////////////////////////////////////////////////////////////////////////////////////
        // GENERAL METHODS
        ////////////////////////////////////////////////////////////////////////////////////////
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")] // copied from Triaged.cs
        override protected void ChangeDatabaseInternal(string database)
        {
            // MDAC 73598 - add brackets around database
            database = SqlConnection.FixupDatabaseTransactionName(database);
            System.Threading.Tasks.Task executeTask = _parser.TdsExecuteSQLBatch("use " + database, ConnectionOptions.ConnectTimeout, null, _parser._physicalStateObj, sync: true);
            Debug.Assert(executeTask == null, "Shouldn't get a task when doing sync writes");
            _parser.Run(RunBehavior.UntilDone, null, null, null, _parser._physicalStateObj);
        }

        override public void Dispose()
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.Dispose|ADV> {0} disposing", ObjectID);
            try
            {
                TdsParser parser = Interlocked.Exchange(ref _parser, null);  // guard against multiple concurrent dispose calls -- Delegated Transactions might cause this.

                Debug.Assert(parser != null && _fConnectionOpen || parser == null && !_fConnectionOpen, "Unexpected state on dispose");
                if (parser != null)
                {
                    parser.Disconnect();
                }
            }
            finally
            { // UNDONE: MDAC 77928
                // close will always close, even if exception is thrown
                // remember to null out any object references
                _loginAck = null;
                _fConnectionOpen = false; // mark internal connection as closed
            }
            base.Dispose();
        }

        override internal void ValidateConnectionForExecute(SqlCommand command)
        {
            TdsParser parser = _parser;
            if ((parser == null) || (parser.State == TdsParserState.Broken) || (parser.State == TdsParserState.Closed))
            {
                throw ADP.ClosedConnectionError();
            }
            else
            {
                SqlDataReader reader = null;
                if (parser.MARSOn)
                {
                    if (command != null)
                    { // command can't have datareader already associated with it
                        reader = FindLiveReader(command);
                    }
                }
                else
                { // single execution/datareader per connection
                    if (_asyncCommandCount > 0)
                    {
                        throw SQL.MARSUnsupportedOnConnection();
                    }

                    reader = FindLiveReader(null);
                }
                if (reader != null)
                {
                    // if MARS is on, then a datareader associated with the command exists
                    // or if MARS is off, then a datareader exists
                    throw ADP.OpenReaderExists(parser.MARSOn); // MDAC 66411
                }
                else if (!parser.MARSOn && parser._physicalStateObj.HasPendingData)
                {
                    parser.DrainData(parser._physicalStateObj);
                }
                Debug.Assert(!parser._physicalStateObj.HasPendingData, "Should not have a busy physicalStateObject at this point!");

                parser.RollbackOrphanedAPITransactions();
            }
        }

        /// <summary>
        /// Validate the enlisted transaction state, taking into consideration the ambient transaction and transaction unbinding mode.
        /// If there is no enlisted transaction, this method is a nop.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method must be called while holding a lock on the SqlInternalConnection instance,
        /// to ensure we don't accidentally execute after the transaction has completed on a different thread,
        /// causing us to unwittingly execute in auto-commit mode.
        /// </para>
        ///
        /// <para>
        /// When using Explicit transaction unbinding,
        /// verify that the enlisted transaction is active and equal to the current ambient transaction.
        /// </para>
        ///
        /// <para>
        /// When using Implicit transaction unbinding,
        /// verify that the enlisted transaction is active.
        /// If it is not active, and the transaction object has been diposed, unbind from the transaction.
        /// If it is not active and not disposed, throw an exception.
        /// </para>
        /// </remarks>
        internal void CheckEnlistedTransactionBinding()
        {
            // If we are enlisted in a transaction, check that transaction is active.
            // When using explicit transaction unbinding, also verify that the enlisted transaction is the current transaction.
            Transaction enlistedTransaction = EnlistedTransaction;

            if (enlistedTransaction != null)
            {
                bool requireExplicitTransactionUnbind = ConnectionOptions.TransactionBinding == SqlConnectionString.TransactionBindingEnum.ExplicitUnbind;

                if (requireExplicitTransactionUnbind)
                {
                    Transaction currentTransaction = Transaction.Current;

                    if (TransactionStatus.Active != enlistedTransaction.TransactionInformation.Status || !enlistedTransaction.Equals(currentTransaction))
                    {
                        throw ADP.TransactionConnectionMismatch();
                    }
                }
                else // implicit transaction unbind
                {
                    if (TransactionStatus.Active != enlistedTransaction.TransactionInformation.Status)
                    {
                        if (EnlistedTransactionDisposed)
                        {
                            DetachTransaction(enlistedTransaction, true);
                        }
                        else
                        {
                            throw ADP.TransactionCompletedButNotDisposed();
                        }
                    }
                }
            }
        }

        internal override bool IsConnectionAlive(bool throwOnException) =>
            _parser._physicalStateObj.IsConnectionAlive(throwOnException);

        ////////////////////////////////////////////////////////////////////////////////////////
        // POOLING METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        override protected void Activate(Transaction transaction)
        {
            FailoverPermissionDemand(); // Demand for unspecified failover pooled connections

            // When we're required to automatically enlist in transactions and
            // there is one we enlist in it. On the other hand, if there isn't a
            // transaction and we are currently enlisted in one, then we
            // unenlist from it.
            //
            // Regardless of whether we're required to automatically enlist,
            // when there is not a current transaction, we cannot leave the
            // connection enlisted in a transaction.
            if (transaction != null)
            {
                if (ConnectionOptions.Enlist)
                {
                    Enlist(transaction);
                }
            }
            else
            {
                Enlist(null);
            }
        }

        override protected void InternalDeactivate()
        {
            // When we're deactivated, the user must have called End on all
            // the async commands, or we don't know that we're in a state that
            // we can recover from.  We doom the connection in this case, to
            // prevent odd cases when we go to the wire.
            if (0 != _asyncCommandCount)
            {
                DoomThisConnection();
            }

            // If we're deactivating with a delegated transaction, we
            // should not be cleaning up the parser just yet, that will
            // cause our transaction to be rolled back and the connection
            // to be reset.  We'll get called again once the delegated
            // transaction is completed and we can do it all then.
            if (!IsNonPoolableTransactionRoot)
            {
                Debug.Assert(_parser != null || IsConnectionDoomed, "Deactivating a disposed connection?");
                if (_parser != null)
                {

                    _parser.Deactivate(IsConnectionDoomed);

                    if (!IsConnectionDoomed)
                    {
                        ResetConnection();
                    }
                }
            }
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")] // copied from Triaged.cs
        private void ResetConnection()
        {
            // For implicit pooled connections, if connection reset behavior is specified,
            // reset the database and language properties back to default.  It is important
            // to do this on activate so that the hashtable is correct before SqlConnection
            // obtains a clone.

            Debug.Assert(!HasLocalTransactionFromAPI, "Upon ResetConnection SqlInternalConnectionTds has a currently ongoing local transaction.");
            Debug.Assert(!_parser._physicalStateObj.HasPendingData, "Upon ResetConnection SqlInternalConnectionTds has pending data.");

            if (_fResetConnection)
            {
                // Pooled connections that are enlisted in a transaction must have their transaction
                // preserved when reseting the connection state. Otherwise, future uses of the connection
                // from the pool will execute outside of the transaction, in auto-commit mode.
                // https://github.com/dotnet/SqlClient/issues/2970
                _parser.PrepareResetConnection(EnlistedTransaction is not null && Pool is not null);

                // Reset hashtable values, since calling reset will not send us env_changes.
                CurrentDatabase = _originalDatabase;
                _currentLanguage = _originalLanguage;
            }
        }

        internal void DecrementAsyncCount()
        {
            Debug.Assert(_asyncCommandCount > 0);
            Interlocked.Decrement(ref _asyncCommandCount);
        }

        internal void IncrementAsyncCount()
        {
            Interlocked.Increment(ref _asyncCommandCount);
        }


        ////////////////////////////////////////////////////////////////////////////////////////
        // LOCAL TRANSACTION METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        override internal void DisconnectTransaction(SqlInternalTransaction internalTransaction)
        {
            TdsParser parser = Parser;

            if (parser != null)
            {
                parser.DisconnectTransaction(internalTransaction);
            }
        }

        internal void ExecuteTransaction(TransactionRequest transactionRequest, string name, System.Data.IsolationLevel iso)
        {
            ExecuteTransaction(transactionRequest, name, iso, null, false);
        }

        override internal void ExecuteTransaction(TransactionRequest transactionRequest, string name, System.Data.IsolationLevel iso, SqlInternalTransaction internalTransaction, bool isDelegateControlRequest)
        {
            if (IsConnectionDoomed)
            {  // doomed means we can't do anything else...
                if (transactionRequest == TransactionRequest.Rollback
                 || transactionRequest == TransactionRequest.IfRollback)
                {
                    return;
                }
                throw SQL.ConnectionDoomed();
            }

            if (transactionRequest == TransactionRequest.Commit
             || transactionRequest == TransactionRequest.Rollback
             || transactionRequest == TransactionRequest.IfRollback)
            {
                if (!Parser.MARSOn && Parser._physicalStateObj.BcpLock)
                {
                    throw SQL.ConnectionLockedForBcpEvent();
                }
            }

            string transactionName = name == null ? string.Empty : name;

            ExecuteTransaction2005(transactionRequest, transactionName, iso, internalTransaction, isDelegateControlRequest);
        }


        internal void ExecuteTransaction2005(
                    TransactionRequest transactionRequest,
                    string transactionName,
                    System.Data.IsolationLevel iso,
                    SqlInternalTransaction internalTransaction,
                    bool isDelegateControlRequest)
        {
            TdsEnums.TransactionManagerRequestType requestType = TdsEnums.TransactionManagerRequestType.Begin;
            TdsEnums.TransactionManagerIsolationLevel isoLevel = TdsEnums.TransactionManagerIsolationLevel.ReadCommitted;

            switch (iso)
            {
                case System.Data.IsolationLevel.Unspecified:
                    isoLevel = TdsEnums.TransactionManagerIsolationLevel.Unspecified;
                    break;
                case System.Data.IsolationLevel.ReadCommitted:
                    isoLevel = TdsEnums.TransactionManagerIsolationLevel.ReadCommitted;
                    break;
                case System.Data.IsolationLevel.ReadUncommitted:
                    isoLevel = TdsEnums.TransactionManagerIsolationLevel.ReadUncommitted;
                    break;
                case System.Data.IsolationLevel.RepeatableRead:
                    isoLevel = TdsEnums.TransactionManagerIsolationLevel.RepeatableRead;
                    break;
                case System.Data.IsolationLevel.Serializable:
                    isoLevel = TdsEnums.TransactionManagerIsolationLevel.Serializable;
                    break;
                case System.Data.IsolationLevel.Snapshot:
                    isoLevel = TdsEnums.TransactionManagerIsolationLevel.Snapshot;
                    break;
                case System.Data.IsolationLevel.Chaos:
                    throw SQL.NotSupportedIsolationLevel(iso);
                default:
                    throw ADP.InvalidIsolationLevel(iso);
            }

            TdsParserStateObject stateObj = _parser._physicalStateObj;
            TdsParser parser = _parser;
            bool mustPutSession = false;
            bool releaseConnectionLock = false;

            Debug.Assert(!ThreadHasParserLockForClose || _parserLock.ThreadMayHaveLock(), "Thread claims to have parser lock, but lock is not taken");
            if (!ThreadHasParserLockForClose)
            {
                _parserLock.Wait(canReleaseFromAnyThread: false);
                ThreadHasParserLockForClose = true;   // In case of error, let the connection know that we already own the parser lock
                releaseConnectionLock = true;
            }
            try
            {
                switch (transactionRequest)
                {
                    case TransactionRequest.Begin:
                        requestType = TdsEnums.TransactionManagerRequestType.Begin;
                        break;
                    case TransactionRequest.Promote:
                        requestType = TdsEnums.TransactionManagerRequestType.Promote;
                        break;
                    case TransactionRequest.Commit:
                        requestType = TdsEnums.TransactionManagerRequestType.Commit;
                        break;
                    case TransactionRequest.IfRollback:
                    // Map IfRollback to Rollback since with 2005 and beyond we should never need
                    // the if since the server will inform us when transactions have completed
                    // as a result of an error on the server.
                    case TransactionRequest.Rollback:
                        requestType = TdsEnums.TransactionManagerRequestType.Rollback;
                        break;
                    case TransactionRequest.Save:
                        requestType = TdsEnums.TransactionManagerRequestType.Save;
                        break;
                    default:
                        Debug.Fail("Unknown transaction type");
                        break;
                }

                // only restore if connection lock has been taken within the function
                if (internalTransaction != null && internalTransaction.RestoreBrokenConnection && releaseConnectionLock)
                {
                    Task reconnectTask = internalTransaction.Parent.Connection.ValidateAndReconnect(() =>
                    {
                        ThreadHasParserLockForClose = false;
                        _parserLock.Release();
                        releaseConnectionLock = false;
                    }, ADP.InfiniteConnectionTimeout);
                    if (reconnectTask != null)
                    {
                        AsyncHelper.WaitForCompletion(reconnectTask, ADP.InfiniteConnectionTimeout); // there is no specific timeout for BeginTransaction, uses ConnectTimeout
                        internalTransaction.ConnectionHasBeenRestored = true;
                        return;
                    }
                }



                // SQLBUDT #20010853 - Promote, Commit and Rollback requests for
                // delegated transactions often happen while there is an open result
                // set, so we need to handle them by using a different MARS session,
                // otherwise we'll write on the physical state objects while someone
                // else is using it.  When we don't have MARS enabled, we need to
                // lock the physical state object to syncronize it's use at least
                // until we increment the open results count.  Once it's been
                // incremented the delegated transaction requests will fail, so they
                // won't stomp on anything.
                //
                // We need to keep this lock through the duration of the TM reqeuest
                // so that we won't hijack a different request's data stream and a
                // different request won't hijack ours, so we have a lock here on
                // an object that the ExecTMReq will also lock, but since we're on
                // the same thread, the lock is a no-op.

                if (internalTransaction != null && internalTransaction.IsDelegated)
                {
                    if (_parser.MARSOn)
                    {
                        stateObj = _parser.GetSession(this);
                        mustPutSession = true;
                    }
                    if (internalTransaction.OpenResultsCount != 0)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.ExecuteTransaction2005|DATA|CATCH> {0}, Connection is marked to be doomed when closed. Transaction ended with OpenResultsCount {1} > 0, MARSOn {2}",
                                                               ObjectID,
                                                               internalTransaction.OpenResultsCount,
                                                               _parser.MARSOn);
                        throw SQL.CannotCompleteDelegatedTransactionWithOpenResults(this, _parser.MARSOn);
                    }
                }

                // SQLBU #406778 - _parser may be nulled out during TdsExecuteTrannsactionManagerRequest.
                //  Only use local variable after this call.
                _parser.TdsExecuteTransactionManagerRequest(null, requestType, transactionName, isoLevel,
                    ConnectionOptions.ConnectTimeout, internalTransaction, stateObj, isDelegateControlRequest);
            }
            finally
            {
                if (mustPutSession)
                {
                    parser.PutSession(stateObj);
                }

                if (releaseConnectionLock)
                {
                    ThreadHasParserLockForClose = false;
                    _parserLock.Release();
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // DISTRIBUTED TRANSACTION METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        override internal void DelegatedTransactionEnded()
        {
            // TODO: I don't like the way that this works, but I don't want to rototill the entire pooler to avoid this call.
            base.DelegatedTransactionEnded();
        }

        override protected byte[] GetDTCAddress()
        {
            byte[] dtcAddress = _parser.GetDTCAddress(ConnectionOptions.ConnectTimeout, _parser.GetSession(this));
            Debug.Assert(dtcAddress != null, "null dtcAddress?");
            return dtcAddress;
        }

        override protected void PropagateTransactionCookie(byte[] cookie)
        {
            _parser.PropagateDistributedTransaction(cookie, ConnectionOptions.ConnectTimeout, _parser._physicalStateObj);
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // LOGIN-RELATED METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        private void CompleteLogin(bool enlistOK)
        {
            _parser.Run(RunBehavior.UntilDone, null, null, null, _parser._physicalStateObj);

            if (_routingInfo == null)
            {
                // ROR should not affect state of connection recovery
                if (_federatedAuthenticationRequested && !_federatedAuthenticationAcknowledged)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.CompleteLogin|ERR> {0}, Server did not acknowledge the federated authentication request", ObjectID);
                    throw SQL.ParsingError(ParsingErrorState.FedAuthNotAcknowledged);
                }
                if (_federatedAuthenticationInfoRequested && !_federatedAuthenticationInfoReceived)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.CompleteLogin|ERR> {0}, Server never sent the requested federated authentication info", ObjectID);
                    throw SQL.ParsingError(ParsingErrorState.FedAuthInfoNotReceived);
                }

                if (!_sessionRecoveryAcknowledged)
                {
                    _currentSessionData = null;
                    if (_recoverySessionData != null)
                    {
                        throw SQL.CR_NoCRAckAtReconnection(this);
                    }
                }
                if (_currentSessionData != null && _recoverySessionData == null)
                {
                    _currentSessionData._initialDatabase = CurrentDatabase;
                    _currentSessionData._initialCollation = _currentSessionData._collation;
                    _currentSessionData._initialLanguage = _currentLanguage;
                }
                bool isEncrypted = (_parser.EncryptionOptions & EncryptionOptions.OPTIONS_MASK) == EncryptionOptions.ON;
                if (_recoverySessionData != null)
                {
                    if (_recoverySessionData._encrypted != isEncrypted)
                    {
                        throw SQL.CR_EncryptionChanged(this);
                    }
                }
                if (_currentSessionData != null)
                {
                    _currentSessionData._encrypted = isEncrypted;
                }
                _recoverySessionData = null;
            }

            Debug.Assert(SniContext.Snix_Login == Parser._physicalStateObj.SniContext, $"SniContext should be Snix_Login; actual Value: {Parser._physicalStateObj.SniContext}");
            _parser._physicalStateObj.SniContext = SniContext.Snix_EnableMars;
            _parser.EnableMars();

            _fConnectionOpen = true; // mark connection as open
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.CompleteLogin|ADV> Post-Login Phase: Server connection obtained.");

            // for non-pooled connections, enlist in a distributed transaction
            // if present - and user specified to enlist
            if (enlistOK && ConnectionOptions.Enlist && _routingInfo == null)
            {
                _parser._physicalStateObj.SniContext = SniContext.Snix_AutoEnlist;
                Transaction tx = ADP.GetCurrentTransaction();
                Enlist(tx);
            }
            _parser._physicalStateObj.SniContext = SniContext.Snix_Login;
        }

        private void Login(ServerInfo server, TimeoutTimer timeout, string newPassword, SecureString newSecurePassword, SqlConnectionEncryptOption encrypt)
        {
            // create a new login record
            SqlLogin login = new SqlLogin();

            // gather all the settings the user set in the connection string or
            // properties and do the login
            CurrentDatabase = server.ResolvedDatabaseName;
            _currentPacketSize = ConnectionOptions.PacketSize;
            _currentLanguage = ConnectionOptions.CurrentLanguage;

            int timeoutInSeconds = 0;

            // If a timeout tick value is specified, compute the timeout based
            // upon the amount of time left in seconds.
            if (!timeout.IsInfinite)
            {
                long t = timeout.MillisecondsRemaining / 1000;
                if (t == 0 && LocalAppContextSwitches.UseMinimumLoginTimeout)
                {
                    // Take 1 as the minimum value, since 0 is treated as an infinite timeout
                    // to allow 1 second more for login to complete, since it should take only a few milliseconds.
                    t = 1;
                }

                if (int.MaxValue > t)
                {
                    timeoutInSeconds = (int)t;
                }
            }

            login.authentication = ConnectionOptions.Authentication;
            login.timeout = timeoutInSeconds;
            login.userInstance = ConnectionOptions.UserInstance;
            login.hostName = ConnectionOptions.ObtainWorkstationId();
            login.userName = ConnectionOptions.UserID;
            login.password = ConnectionOptions.Password;
            login.applicationName = ConnectionOptions.ApplicationName;

            login.language = _currentLanguage;
            if (!login.userInstance)
            { // Do not send attachdbfilename or database to SSE primary instance
                login.database = CurrentDatabase;
                ;
                login.attachDBFilename = ConnectionOptions.AttachDBFilename;
            }

            // VSTS#795621 - Ensure ServerName is Sent During TdsLogin To Enable Sql Azure Connectivity.
            // Using server.UserServerName (versus ConnectionOptions.DataSource) since TdsLogin requires
            // serverName to always be non-null.
            login.serverName = server.UserServerName;

            login.useReplication = ConnectionOptions.Replication;
            login.useSSPI = ConnectionOptions.IntegratedSecurity  // Treat AD Integrated like Windows integrated when against a non-FedAuth endpoint
                                     || (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && !_fedAuthRequired);
            login.packetSize = _currentPacketSize;
            login.newPassword = newPassword;
            login.readOnlyIntent = ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly;
            login.credential = _credential;
            if (newSecurePassword != null)
            {
                login.newSecurePassword = newSecurePassword;
            }

            TdsEnums.FeatureExtension requestedFeatures = TdsEnums.FeatureExtension.None;
            if (ConnectionOptions.ConnectRetryCount > 0)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.SessionRecovery;
                _sessionRecoveryRequested = true;
            }

            // If the workflow being used is Active Directory Authentication and server's prelogin response
            // for FEDAUTHREQUIRED option indicates Federated Authentication is required, we have to insert FedAuth Feature Extension
            // in Login7, indicating the intent to use Active Directory Authentication for SQL Server.
            #pragma warning disable 0618
            if (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
            #pragma warning restore 0618
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity
                // Since AD Integrated may be acting like Windows integrated, additionally check _fedAuthRequired
                || (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && _fedAuthRequired)
                || _accessTokenCallback != null)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.FedAuth;
                _federatedAuthenticationInfoRequested = true;
                _fedAuthFeatureExtensionData =
                    new FederatedAuthenticationFeatureExtensionData
                    {
                        libraryType = TdsEnums.FedAuthLibrary.MSAL,
                        authentication = ConnectionOptions.Authentication,
                        fedAuthRequiredPreLoginResponse = _fedAuthRequired
                    };
            }

            if (_accessTokenInBytes != null)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.FedAuth;
                _fedAuthFeatureExtensionData = new FederatedAuthenticationFeatureExtensionData
                {
                    libraryType = TdsEnums.FedAuthLibrary.SecurityToken,
                    fedAuthRequiredPreLoginResponse = _fedAuthRequired,
                    accessToken = _accessTokenInBytes
                };
                // No need any further info from the server for token based authentication. So set _federatedAuthenticationRequested to true
                _federatedAuthenticationRequested = true;
            }

            // The TCE, DATACLASSIFICATION and GLOBALTRANSACTIONS, UTF8 support feature are implicitly requested
            requestedFeatures |= TdsEnums.FeatureExtension.Tce | TdsEnums.FeatureExtension.DataClassification | TdsEnums.FeatureExtension.GlobalTransactions;

            requestedFeatures |= TdsEnums.FeatureExtension.UTF8Support;

            // The AzureSQLSupport feature is implicitly set for ReadOnly login
            if (ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.AzureSQLSupport;
            }

            // The SQLDNSCaching feature is implicitly set
            requestedFeatures |= TdsEnums.FeatureExtension.SQLDNSCaching;

            requestedFeatures |= TdsEnums.FeatureExtension.JsonSupport;

            requestedFeatures |= TdsEnums.FeatureExtension.VectorSupport;

            _parser.TdsLogin(login, requestedFeatures, _recoverySessionData, _fedAuthFeatureExtensionData, encrypt);
        }

        private void LoginFailure()
        {
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.LoginFailure|RES|CPOOL> {0}", ObjectID);

            // If the parser was allocated and we failed, then we must have failed on
            // either the Connect or Login, either way we should call Disconnect.
            // Disconnect can be called if the connection is already closed - becomes
            // no-op, so no issues there.
            if (_parser != null)
            {

                _parser.Disconnect();
            }
            // TODO: Need a performance counter for Failed Connections
        }

        private void OpenLoginEnlist(TimeoutTimer timeout, SqlConnectionString connectionOptions, SqlCredential credential,
                    string newPassword, SecureString newSecurePassword, bool redirectedUserInstance)
        {
            bool useFailoverPartner; // should we use primary or secondary first
            ServerInfo dataSource = new ServerInfo(connectionOptions);
            string failoverPartner;

            if (PoolGroupProviderInfo != null)
            {
                useFailoverPartner = PoolGroupProviderInfo.UseFailoverPartner;
                failoverPartner = PoolGroupProviderInfo.FailoverPartner;
            }
            else
            {
                // Only ChangePassword or SSE User Instance comes through this code path.
                useFailoverPartner = false;
                failoverPartner = ConnectionOptions.FailoverPartner;
            }

            _timeoutErrorInternal.SetInternalSourceType(useFailoverPartner ? SqlConnectionInternalSourceType.Failover : SqlConnectionInternalSourceType.Principle);

            bool hasFailoverPartner = !ADP.IsEmpty(failoverPartner);

            // Open the connection and Login
            try
            {
                _timeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.PreLoginBegin);
                if (hasFailoverPartner)
                {
                    _timeoutErrorInternal.SetFailoverScenario(true); // this is a failover scenario
                    LoginWithFailover(
                                useFailoverPartner,
                                dataSource,
                                failoverPartner,
                                newPassword,
                                newSecurePassword,
                                redirectedUserInstance,
                                connectionOptions,
                                credential,
                                timeout);
                }
                else
                {
                    _timeoutErrorInternal.SetFailoverScenario(false); // not a failover scenario
                    LoginNoFailover(dataSource, 
                            newPassword, 
                            newSecurePassword, 
                            redirectedUserInstance,
                            connectionOptions, 
                            credential, 
                            timeout);
                }

                if (!IsAzureSQLConnection)
                {
                    // If not a connection to Azure SQL, Readonly with FailoverPartner is not supported
                    if (ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly)
                    {
                        if (!string.IsNullOrEmpty(ConnectionOptions.FailoverPartner))
                        {
                            throw SQL.ROR_FailoverNotSupportedConnString();
                        }

                        if (ServerProvidedFailOverPartner != null)
                        {
                            throw SQL.ROR_FailoverNotSupportedServer(this);
                        }
                    }
                }
                _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.PostLogin);
            }
            catch (Exception e)
            {
                // UNDONE - should not be catching all exceptions!!!
                if (ADP.IsCatchableExceptionType(e))
                {
                    LoginFailure();
                }
                throw;
            }
            _timeoutErrorInternal.SetAllCompleteMarker();

#if DEBUG
            _parser._physicalStateObj.InvalidateDebugOnlyCopyOfSniContext();
#endif
        }

        // Is the given Sql error one that should prevent retrying
        //   to connect.
        private bool IsDoNotRetryConnectError(SqlException exc)
        {

            return (TdsEnums.LOGON_FAILED == exc.Number) // actual logon failed, i.e. bad password
                || (TdsEnums.PASSWORD_EXPIRED == exc.Number) // actual logon failed, i.e. password isExpired
                || (TdsEnums.IMPERSONATION_FAILED == exc.Number)  // Insuficient privelege for named pipe, among others
                || exc._doNotReconnect; // Exception explicitly supressed reconnection attempts
        }

        // Attempt to login to a host that does not have a failover partner
        //
        //  Will repeatedly attempt to connect, but back off between each attempt so as not to clog the network.
        //  Back off period increases for first few failures: 100ms, 200ms, 400ms, 800ms, then 1000ms for subsequent attempts
        //
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        //  DEVNOTE: The logic in this method is paralleled by the logic in LoginWithFailover.
        //           Changes to either one should be examined to see if they need to be reflected in the other
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        private void LoginNoFailover(ServerInfo serverInfo, string newPassword, SecureString newSecurePassword, bool redirectedUserInstance,
                    SqlConnectionString connectionOptions, SqlCredential credential, TimeoutTimer timeout)
        {

            Debug.Assert(object.ReferenceEquals(connectionOptions, this.ConnectionOptions), "ConnectionOptions argument and property must be the same"); // consider removing the argument
            int routingAttempts = 0;
            ServerInfo originalServerInfo = serverInfo; // serverInfo may end up pointing to new object due to routing, original object is used to set CurrentDatasource

            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.LoginNoFailover|ADV> {0}, host={1}", ObjectID, serverInfo.UserServerName);

            int sleepInterval = 100;  //milliseconds to sleep (back off) between attempts.

            ResolveExtendedServerName(serverInfo, !redirectedUserInstance, connectionOptions);

            Boolean disableTnir = ShouldDisableTnir(connectionOptions);

            long timeoutUnitInterval = 0;

            Boolean isParallel = connectionOptions.MultiSubnetFailover || (connectionOptions.TransparentNetworkIPResolution && !disableTnir);


            if (isParallel)
            {
                float failoverTimeoutStep = connectionOptions.MultiSubnetFailover ? ADP.FailoverTimeoutStep : ADP.FailoverTimeoutStepForTnir;
                // Determine unit interval
                if (timeout.IsInfinite)
                {
                    timeoutUnitInterval = checked((long)(failoverTimeoutStep * (1000L * ADP.DefaultConnectionTimeout)));
                }
                else
                {
                    timeoutUnitInterval = checked((long)(failoverTimeoutStep * timeout.MillisecondsRemaining));
                }
            }
            // Only three ways out of this loop:
            //  1) Successfully connected
            //  2) Parser threw exception while main timer was expired
            //  3) Parser threw logon failure-related exception
            //  4) Parser threw exception in post-initial connect code,
            //      such as pre-login handshake or during actual logon. (parser state != Closed)
            //
            //  Of these methods, only #1 exits normally. This preserves the call stack on the exception
            //  back into the parser for the error cases.
            int attemptNumber = 0;
            TimeoutTimer intervalTimer = null;

            TimeoutTimer attemptOneLoginTimeout = timeout;
            while (true)
            {

                Boolean isFirstTransparentAttempt = connectionOptions.TransparentNetworkIPResolution && !disableTnir && attemptNumber == 1;

                if (isParallel)
                {
                    int multiplier = ++attemptNumber;

                    if (connectionOptions.TransparentNetworkIPResolution)
                    {
                        // While connecting using TNIR the timeout multiplier should be increased to allow steps of 1,2,4 instead of 1,2,3.
                        // This will allow half the time out for the last connection attempt in case of Tnir.
                        multiplier = 1 << (attemptNumber - 1);
                    }
                    // Set timeout for this attempt, but don't exceed original timer
                    long nextTimeoutInterval = checked(timeoutUnitInterval * multiplier);
                    long milliseconds = timeout.MillisecondsRemaining;

                    // If it is the first attempt at TNIR connection, then allow at least 500 ms for timeout. With the current failover step of 0.125
                    // and Connection Time of < 4000 ms, the first attempt can be lower than 500 ms.
                    if (isFirstTransparentAttempt)
                    {
                        nextTimeoutInterval = Math.Max(ADP.MinimumTimeoutForTnirMs, nextTimeoutInterval);
                    }
                    if (nextTimeoutInterval > milliseconds)
                    {
                        nextTimeoutInterval = milliseconds;
                    }
                    intervalTimer = TimeoutTimer.StartMillisecondsTimeout(nextTimeoutInterval);
                }

                // Re-allocate parser each time to make sure state is known
                // RFC 50002652 - if parser was created by previous attempt, dispose it to properly close the socket, if created
                if (_parser != null)
                    _parser.Disconnect();

                _parser = new TdsParser(ConnectionOptions.MARS, ConnectionOptions.Asynchronous);
                Debug.Assert(SniContext.Undefined == Parser._physicalStateObj.SniContext, string.Format((IFormatProvider)null, "SniContext should be Undefined; actual Value: {0}", Parser._physicalStateObj.SniContext));

                try
                {
                    // UNDONE: ITEM12001110 (DB Mirroring Reconnect) Old behavior of not truly honoring timeout presevered
                    //  for non-failover, non-MSF scenarios to avoid breaking changes as part of a QFE.  Consider fixing timeout
                    //  handling in next full release and removing ignoreSniOpenTimeout parameter.

                    if (isParallel)
                    {
                        attemptOneLoginTimeout = intervalTimer;
                    }

                    AttemptOneLogin(serverInfo,
                                        newPassword,
                                        newSecurePassword,
                                        attemptOneLoginTimeout,
                                        isFirstTransparentAttempt: isFirstTransparentAttempt,
                                        disableTnir: disableTnir);

                    if (connectionOptions.MultiSubnetFailover && ServerProvidedFailOverPartner != null)
                    {
                        // connection succeeded: trigger exception if server sends failover partner and MultiSubnetFailover is used.
                        throw SQL.MultiSubnetFailoverWithFailoverPartner(serverProvidedFailoverPartner: true, internalConnection: this);
                    }

                    if (_routingInfo != null)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.LoginNoFailover> Routed to {0}", serverInfo.ExtendedServerName);

                        if (routingAttempts > _maxNumberOfRedirectRoute)
                        {
                            throw SQL.ROR_RecursiveRoutingNotSupported(this, _maxNumberOfRedirectRoute);
                        }

                        if (timeout.IsExpired)
                        {
                            throw SQL.ROR_TimeoutAfterRoutingInfo(this);
                        }

                        serverInfo = new ServerInfo(ConnectionOptions, _routingInfo, serverInfo.ResolvedServerName, serverInfo.ServerSPN);
                        _timeoutErrorInternal.SetInternalSourceType(SqlConnectionInternalSourceType.RoutingDestination);
                        _originalClientConnectionId = _clientConnectionId;
                        _routingDestination = serverInfo.UserServerName;

                        // restore properties that could be changed by the environment tokens
                        _currentPacketSize = ConnectionOptions.PacketSize;
                        _currentLanguage = _originalLanguage = ConnectionOptions.CurrentLanguage;
                        CurrentDatabase = _originalDatabase = ConnectionOptions.InitialCatalog;
                        _currentFailoverPartner = null;
                        _instanceName = string.Empty;

                        routingAttempts++;

                        continue; // repeat the loop, but skip code reserved for failed connections (after the catch)
                    }
                    else
                    {
                        break; // leave the while loop -- we've successfully connected
                    }
                }
                catch (SqlException sqlex)
                {
                    if (AttemptRetryADAuthWithTimeoutError(sqlex, connectionOptions, timeout))
                    {
                        continue;
                    }

                    if (_parser == null
                        || TdsParserState.Closed != _parser.State
                        || IsDoNotRetryConnectError(sqlex)
                        || timeout.IsExpired)
                    {       // no more time to try again
                        throw;  // Caller will call LoginFailure()
                    }

                    // Check sleep interval to make sure we won't exceed the timeout
                    //  Do this in the catch block so we can re-throw the current exception
                    if (timeout.MillisecondsRemaining <= sleepInterval)
                    {
                        throw;
                    }

                    // TODO: Stash parser away somewhere so we can examine it's state during debugging
                }

                // We only get here when we failed to connect, but are going to re-try

                // Switch to failover logic if the server provided a partner
                if (ServerProvidedFailOverPartner != null)
                {
                    if (connectionOptions.MultiSubnetFailover)
                    {
                        // connection failed: do not allow failover to server-provided failover partner if MultiSubnetFailover is set
                        throw SQL.MultiSubnetFailoverWithFailoverPartner(serverProvidedFailoverPartner: true, internalConnection: this);
                    }
                    Debug.Assert(ConnectionOptions.ApplicationIntent != ApplicationIntent.ReadOnly, "FAILOVER+AppIntent=RO: Should already fail (at LOGSHIPNODE in OnEnvChange)");

                    _timeoutErrorInternal.ResetAndRestartPhase();
                    _timeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.PreLoginBegin);
                    _timeoutErrorInternal.SetInternalSourceType(SqlConnectionInternalSourceType.Failover);
                    _timeoutErrorInternal.SetFailoverScenario(true); // this is a failover scenario
                    LoginWithFailover(
                                true,   // start by using failover partner, since we already failed to connect to the primary
                                serverInfo,
                                ServerProvidedFailOverPartner,
                                newPassword,
                                newSecurePassword,
                                redirectedUserInstance,
                                connectionOptions,
                                credential,
                                timeout);
                    return; // LoginWithFailover successfully connected and handled entire connection setup
                }

                // Sleep for a bit to prevent clogging the network with requests,
                //  then update sleep interval for next iteration (max 1 second interval)
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.LoginNoFailover|ADV> {0}, sleeping {1}[milisec]", ObjectID, sleepInterval);

                Thread.Sleep(sleepInterval);
                sleepInterval = (sleepInterval < 500) ? sleepInterval * 2 : 1000;
            }
            _activeDirectoryAuthTimeoutRetryHelper.State = ActiveDirectoryAuthenticationTimeoutRetryState.HasLoggedIn;

            if (PoolGroupProviderInfo != null)
            {
                // We must wait for CompleteLogin to finish for to have the
                // env change from the server to know its designated failover
                // partner; save this information in _currentFailoverPartner.
                PoolGroupProviderInfo.FailoverCheck(false, connectionOptions, ServerProvidedFailOverPartner);
            }
            CurrentDataSource = originalServerInfo.UserServerName;
        }

        private bool ShouldDisableTnir(SqlConnectionString connectionOptions)
        {
            Boolean isAzureEndPoint = ADP.IsAzureSqlServerEndpoint(connectionOptions.DataSource);

            Boolean isFedAuthEnabled = this._accessTokenInBytes != null ||
                                       #pragma warning disable 0618
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword ||
                                       #pragma warning restore 0618
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated ||
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive ||
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal ||
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow ||
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity ||
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI ||
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault ||
                                       connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;

            // Check if the user had explicitly specified the TNIR option in the connection string or the connection string builder.
            // If the user has specified the option in the connection string explicitly, then we shouldn't disable TNIR.
            bool isTnirExplicitlySpecifiedInConnectionOptions = connectionOptions.Parsetable.ContainsKey(SqlConnectionString.KEY.TransparentNetworkIPResolution);

            return isTnirExplicitlySpecifiedInConnectionOptions ? false : (isAzureEndPoint || isFedAuthEnabled);
        }

        // With possible MFA support in all AD auth providers, the duration for acquiring a token can be unpredictable.
        // If a timeout error (client or server) happened, we silently retry if a cached token exists from a previous auth attempt (see GetFedAuthToken)
        private bool AttemptRetryADAuthWithTimeoutError(SqlException sqlex, SqlConnectionString connectionOptions, TimeoutTimer timeout)
        {
            if (!_activeDirectoryAuthTimeoutRetryHelper.CanRetryWithSqlException(sqlex))
            {
                return false;
            }
            // Reset client-side timeout.
            timeout.Reset();
            // When server timeout, the auth context key was already created. Clean it up here.
            _dbConnectionPoolAuthenticationContextKey = null;
            // When server timeouts, connection is doomed. Reset here to allow reconnect.
            UnDoomThisConnection();
            // Change retry state so it only retries once for timeout error.
            _activeDirectoryAuthTimeoutRetryHelper.State = ActiveDirectoryAuthenticationTimeoutRetryState.Retrying;
            return true;
        }

        // Attempt to login to a host that has a failover partner
        //
        // Connection & timeout sequence is
        //      First target, timeout = interval * 1
        //      second target, timeout = interval * 1
        //      sleep for 100ms
        //      First target, timeout = interval * 2
        //      Second target, timeout = interval * 2
        //      sleep for 200ms
        //      First Target, timeout = interval * 3
        //      etc.
        //
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        //  DEVNOTE: The logic in this method is paralleled by the logic in LoginNoFailover.
        //           Changes to either one should be examined to see if they need to be reflected in the other
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        private void LoginWithFailover(
                bool useFailoverHost,
                ServerInfo primaryServerInfo,
                string failoverHost,
                string newPassword,
                SecureString newSecurePassword,
                bool redirectedUserInstance,
                SqlConnectionString connectionOptions,
                SqlCredential credential,
                TimeoutTimer timeout
            )
        {

            Debug.Assert(!connectionOptions.MultiSubnetFailover, "MultiSubnetFailover should not be set if failover partner is used");
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.LoginWithFailover|ADV> {0}, useFailover={1}[bool], primary={2}, failover={3}", ObjectID, useFailoverHost, primaryServerInfo.UserServerName, failoverHost);

            int sleepInterval = 100;  //milliseconds to sleep (back off) between attempts.
            long timeoutUnitInterval;

            string protocol = ConnectionOptions.NetworkLibrary;
            ServerInfo failoverServerInfo = new ServerInfo(connectionOptions, failoverHost, connectionOptions.FailoverPartnerSPN);

            ResolveExtendedServerName(primaryServerInfo, !redirectedUserInstance, connectionOptions);
            if (ServerProvidedFailOverPartner == null)
            {// No point in resolving the failover partner when we're going to override it below
             // Don't resolve aliases if failover == primary // UNDONE: WHY?  Previous code in TdsParser.Connect did this, but the reason is not clear
                ResolveExtendedServerName(failoverServerInfo, !redirectedUserInstance && failoverHost != primaryServerInfo.UserServerName, connectionOptions);
            }

            // Determine unit interval
            if (timeout.IsInfinite)
            {
                timeoutUnitInterval = checked((long)(ADP.FailoverTimeoutStep * ADP.TimerFromSeconds(ADP.DefaultConnectionTimeout)));
            }
            else
            {
                timeoutUnitInterval = checked((long)(ADP.FailoverTimeoutStep * timeout.MillisecondsRemaining));
            }

            // Initialize loop variables
            bool failoverDemandDone = false; // have we demanded for partner information yet (as necessary)?
            int attemptNumber = 0;

            // Only three ways out of this loop:
            //  1) Successfully connected
            //  2) Parser threw exception while main timer was expired
            //  3) Parser threw logon failure-related exception (LOGON_FAILED, PASSWORD_EXPIRED, etc)
            //
            //  Of these methods, only #1 exits normally. This preserves the call stack on the exception
            //  back into the parser for the error cases.
            while (true)
            {
                // Set timeout for this attempt, but don't exceed original timer
                long nextTimeoutInterval = checked(timeoutUnitInterval * ((attemptNumber / 2) + 1));
                long milliseconds = timeout.MillisecondsRemaining;
                if (nextTimeoutInterval > milliseconds)
                {
                    nextTimeoutInterval = milliseconds;
                }

                TimeoutTimer intervalTimer = TimeoutTimer.StartMillisecondsTimeout(nextTimeoutInterval);

                // Re-allocate parser each time to make sure state is known
                // RFC 50002652 - if parser was created by previous attempt, dispose it to properly close the socket, if created
                if (_parser != null)
                {
                    _parser.Disconnect();
                }

                _parser = new TdsParser(ConnectionOptions.MARS, ConnectionOptions.Asynchronous);
                Debug.Assert(SniContext.Undefined == Parser._physicalStateObj.SniContext, string.Format((IFormatProvider)null, "SniContext should be Undefined; actual Value: {0}", Parser._physicalStateObj.SniContext));

                ServerInfo currentServerInfo;
                if (useFailoverHost)
                {
                    if (!failoverDemandDone)
                    {
                        FailoverPermissionDemand();
                        failoverDemandDone = true;
                    }

                    // Primary server may give us a different failover partner than the connection string indicates.  Update it
                    if (ServerProvidedFailOverPartner != null && failoverServerInfo.ResolvedServerName != ServerProvidedFailOverPartner)
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.LoginWithFailover|ADV> {0}, new failover partner={1}", ObjectID, ServerProvidedFailOverPartner);
                        failoverServerInfo.SetDerivedNames(protocol, ServerProvidedFailOverPartner);
                    }
                    currentServerInfo = failoverServerInfo;
                    _timeoutErrorInternal.SetInternalSourceType(SqlConnectionInternalSourceType.Failover);
                }
                else
                {
                    currentServerInfo = primaryServerInfo;
                    _timeoutErrorInternal.SetInternalSourceType(SqlConnectionInternalSourceType.Principle);
                }

                try
                {
                    // Attempt login.  Use timerInterval for attempt timeout unless infinite timeout was requested.
                    AttemptOneLogin(
                            currentServerInfo,
                            newPassword,
                            newSecurePassword,
                            intervalTimer,
                            withFailover: true
                            );

                    int routingAttempts = 0;
                    while (_routingInfo != null)
                    {
                        if (routingAttempts > _maxNumberOfRedirectRoute)
                        {
                            throw SQL.ROR_RecursiveRoutingNotSupported(this, _maxNumberOfRedirectRoute);
                        }
                        routingAttempts++;

                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.LoginWithFailover> Routed to {0}", _routingInfo.ServerName);

                        if (_parser != null)
                        {
                            _parser.Disconnect();
                        }

                        _parser = new TdsParser(ConnectionOptions.MARS, ConnectionOptions.Asynchronous);
                        Debug.Assert(SniContext.Undefined == Parser._physicalStateObj.SniContext, $"SniContext should be Undefined; actual Value: {Parser._physicalStateObj.SniContext}");

                        currentServerInfo = new ServerInfo(ConnectionOptions, _routingInfo, currentServerInfo.ResolvedServerName, currentServerInfo.ServerSPN);
                        _timeoutErrorInternal.SetInternalSourceType(SqlConnectionInternalSourceType.RoutingDestination);
                        _originalClientConnectionId = _clientConnectionId;
                        _routingDestination = currentServerInfo.UserServerName;

                        // restore properties that could be changed by the environment tokens
                        _currentPacketSize = ConnectionOptions.PacketSize;
                        _currentLanguage = _originalLanguage = ConnectionOptions.CurrentLanguage;
                        CurrentDatabase = _originalDatabase = ConnectionOptions.InitialCatalog;
                        _currentFailoverPartner = null;
                        _instanceName = string.Empty;

                        AttemptOneLogin(
                                currentServerInfo,
                                newPassword,
                                newSecurePassword,
                                intervalTimer,
                                withFailover: true
                                );
                    }

                    break; // leave the while loop -- we've successfully connected
                }
                catch (SqlException sqlex)
                {
                    if (AttemptRetryADAuthWithTimeoutError(sqlex, connectionOptions, timeout))
                    {
                        continue;
                    }

                    if (IsDoNotRetryConnectError(sqlex)
                        || timeout.IsExpired)
                    {       // no more time to try again
                        throw;  // Caller will call LoginFailure()
                    }

                    if (!ADP.IsAzureSqlServerEndpoint(connectionOptions.DataSource) && IsConnectionDoomed)
                    {
                        throw;
                    }

                    if (1 == attemptNumber % 2)
                    {
                        // Check sleep interval to make sure we won't exceed the original timeout
                        //  Do this in the catch block so we can re-throw the current exception
                        if (timeout.MillisecondsRemaining <= sleepInterval)
                        {
                            throw;
                        }
                    }

                    // TODO: Stash parser away somewhere so we can examine it's state during debugging
                }

                // We only get here when we failed to connect, but are going to re-try

                // After trying to connect to both servers fails, sleep for a bit to prevent clogging
                //  the network with requests, then update sleep interval for next iteration (max 1 second interval)
                if (1 == attemptNumber % 2)
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.LoginWithFailover|ADV> {0}, sleeping {1}[milisec]", ObjectID, sleepInterval);
                    Thread.Sleep(sleepInterval);
                    sleepInterval = (sleepInterval < 500) ? sleepInterval * 2 : 1000;
                }

                // Update attempt number and target host
                attemptNumber++;
                useFailoverHost = !useFailoverHost;
            }

            // If we get here, connection/login succeeded!  Just a few more checks & record-keeping
            _activeDirectoryAuthTimeoutRetryHelper.State = ActiveDirectoryAuthenticationTimeoutRetryState.HasLoggedIn;

            // if connected to failover host, but said host doesn't have DbMirroring set up, throw an error
            if (useFailoverHost && ServerProvidedFailOverPartner == null)
            {
                throw SQL.InvalidPartnerConfiguration(failoverHost, CurrentDatabase);
            }

            if (PoolGroupProviderInfo != null)
            {
                // We must wait for CompleteLogin to finish for to have the
                // env change from the server to know its designated failover
                // partner; save this information in _currentFailoverPartner.
                PoolGroupProviderInfo.FailoverCheck(useFailoverHost, connectionOptions, ServerProvidedFailOverPartner);
            }
            CurrentDataSource = (useFailoverHost ? failoverHost : primaryServerInfo.UserServerName);
        }

        private void ResolveExtendedServerName(ServerInfo serverInfo, bool aliasLookup, SqlConnectionString options)
        {
            if (serverInfo.ExtendedServerName == null)
            {
                string host = serverInfo.UserServerName;
                string protocol = serverInfo.UserProtocol;

                if (aliasLookup)
                { // We skip this for UserInstances...
                  // Perform registry lookup to see if host is an alias.  It will appropriately set host and protocol, if an Alias.
                  // Check if it was already resolved, during CR reconnection _currentSessionData values will be copied from
                  // _reconnectSessonData of the previous connection
                    if (_currentSessionData != null && !string.IsNullOrEmpty(host))
                    {
                        Tuple<string, string> hostPortPair;
                        if (_currentSessionData._resolvedAliases.TryGetValue(host, out hostPortPair))
                        {
                            host = hostPortPair.Item1;
                            protocol = hostPortPair.Item2;
                        }
                        else
                        {
                            TdsParserStaticMethods.AliasRegistryLookup(ref host, ref protocol);
                            _currentSessionData._resolvedAliases.Add(serverInfo.UserServerName, new Tuple<string, string>(host, protocol));
                        }
                    }
                    else
                    {
                        TdsParserStaticMethods.AliasRegistryLookup(ref host, ref protocol);
                    }

                    //TODO: fix local host enforcement with datadirectory and failover
                    if (options.EnforceLocalHost)
                    {
                        // verify LocalHost for |DataDirectory| usage
                        SqlConnectionString.VerifyLocalHostAndFixup(ref host, true, true /*fix-up to "."*/);
                    }
                }

                serverInfo.SetDerivedNames(protocol, host);
            }
        }

        // Common code path for making one attempt to establish a connection and log in to server.
        private void AttemptOneLogin(ServerInfo serverInfo, string newPassword, SecureString newSecurePassword, TimeoutTimer timeout, bool withFailover = false, bool isFirstTransparentAttempt = true, bool disableTnir = false)
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.AttemptOneLogin|ADV> {0}, timeout={1}[msec], server={2}", ObjectID, timeout.MillisecondsRemaining, serverInfo.ExtendedServerName);

            _routingInfo = null; // forget routing information

            _parser._physicalStateObj.SniContext = SniContext.Snix_Connect;

            _parser.Connect(serverInfo,
                            this,
                            timeout,
                            ConnectionOptions,
                            withFailover,
                            isFirstTransparentAttempt,
                            disableTnir);

            _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake);
            _timeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.LoginBegin);

            _parser._physicalStateObj.SniContext = SniContext.Snix_Login;
            this.Login(serverInfo, timeout, newPassword, newSecurePassword, ConnectionOptions.Encrypt);

            _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth);
            _timeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.PostLogin);

            CompleteLogin(!ConnectionOptions.Pooling);

            _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.PostLogin);
        }


        internal void FailoverPermissionDemand()
        {
            if (PoolGroupProviderInfo != null)
            {
                PoolGroupProviderInfo.FailoverPermissionDemand();
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // PREPARED COMMAND METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        protected override bool ObtainAdditionalLocksForClose()
        {
            bool obtainParserLock = !ThreadHasParserLockForClose;
            Debug.Assert(obtainParserLock || _parserLock.ThreadMayHaveLock(), "Thread claims to have lock, but lock is not taken");
            if (obtainParserLock)
            {
                _parserLock.Wait(canReleaseFromAnyThread: false);
                ThreadHasParserLockForClose = true;
            }
            return obtainParserLock;
        }

        protected override void ReleaseAdditionalLocksForClose(bool lockToken)
        {
            if (lockToken)
            {
                ThreadHasParserLockForClose = false;
                _parserLock.Release();
            }
        }

        // called by SqlConnection.RepairConnection which is a relatevly expensive way of repair inner connection
        // prior to execution of request, used from EnlistTransaction, EnlistDistributedTransaction and ChangeDatabase
        internal bool GetSessionAndReconnectIfNeeded(SqlConnection parent, int timeout = 0)
        {

            Debug.Assert(!ThreadHasParserLockForClose, "Cannot call this method if caller has parser lock");
            if (ThreadHasParserLockForClose)
            {
                return false; // we cannot restore if we cannot release lock
            }

            _parserLock.Wait(canReleaseFromAnyThread: false);
            ThreadHasParserLockForClose = true;   // In case of error, let the connection know that we already own the parser lock
            bool releaseConnectionLock = true;

            try
            {
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    Task reconnectTask = parent.ValidateAndReconnect(() =>
                    {
                        ThreadHasParserLockForClose = false;
                        _parserLock.Release();
                        releaseConnectionLock = false;
                    }, timeout);
                    if (reconnectTask != null)
                    {
                        AsyncHelper.WaitForCompletion(reconnectTask, timeout);
                        return true;
                    }
                    return false;
                }
                catch (System.OutOfMemoryException)
                {
                    DoomThisConnection();
                    throw;
                }
                catch (System.StackOverflowException)
                {
                    DoomThisConnection();
                    throw;
                }
                catch (System.Threading.ThreadAbortException)
                {
                    DoomThisConnection();
                    throw;
                }
            }
            finally
            {
                if (releaseConnectionLock)
                {
                    ThreadHasParserLockForClose = false;
                    _parserLock.Release();
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // PARSER CALLBACKS
        ////////////////////////////////////////////////////////////////////////////////////////

        internal void BreakConnection()
        {
            SqlConnection connection = Connection;
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.BreakConnection|RES|CPOOL> {0}, Breaking connection.", ObjectID);
            DoomThisConnection();   // Mark connection as unusable, so it will be destroyed

            if (connection != null)
            {
                connection.Close();
            }
        }

        internal bool IgnoreEnvChange
        { // true if we are only draining environment change tokens, used by TdsParser
            get
            {
                return _routingInfo != null; // connection was routed, ignore rest of env change
            }
        }

        internal void OnEnvChange(SqlEnvChange rec)
        {
            Debug.Assert(!IgnoreEnvChange, "This function should not be called if IgnoreEnvChange is set!");
            switch (rec._type)
            {
                case TdsEnums.ENV_DATABASE:
                    // If connection is not open and recovery is not in progresss, store the server value as the original.
                    if (!_fConnectionOpen && _recoverySessionData == null)
                    {
                        _originalDatabase = rec._newValue;
                    }

                    CurrentDatabase = rec._newValue;
                    break;

                case TdsEnums.ENV_LANG:
                    // If connection is not open and recovery is not in progresss, store the server value as the original.
                    if (!_fConnectionOpen && _recoverySessionData == null)
                    {
                        _originalLanguage = rec._newValue;
                    }

                    _currentLanguage = rec._newValue; // TODO: finish this.
                    break;

                case TdsEnums.ENV_PACKETSIZE:
                    _currentPacketSize = int.Parse(rec._newValue, CultureInfo.InvariantCulture);
                    break;

                case TdsEnums.ENV_COLLATION:
                    if (_currentSessionData != null)
                    {
                        _currentSessionData._collation = rec._newCollation;
                    }
                    break;

                case TdsEnums.ENV_CHARSET:
                case TdsEnums.ENV_LOCALEID:
                case TdsEnums.ENV_COMPFLAGS:
                case TdsEnums.ENV_BEGINTRAN:
                case TdsEnums.ENV_COMMITTRAN:
                case TdsEnums.ENV_ROLLBACKTRAN:
                case TdsEnums.ENV_ENLISTDTC:
                case TdsEnums.ENV_DEFECTDTC:
                    // only used on parser
                    break;

                case TdsEnums.ENV_LOGSHIPNODE:
                    _currentFailoverPartner = rec._newValue;
                    break;

                case TdsEnums.ENV_PROMOTETRANSACTION:
                    byte[] dtcToken;
                    if (rec._newBinRented)
                    {
                        dtcToken = new byte[rec._newLength];
                        Buffer.BlockCopy(rec._newBinValue, 0, dtcToken, 0, dtcToken.Length);
                    }
                    else
                    {
                        dtcToken = rec._newBinValue;
                        rec._newBinValue = null;
                    }
                    PromotedDTCToken = dtcToken;
                    break;

                case TdsEnums.ENV_TRANSACTIONENDED:
                    break;

                case TdsEnums.ENV_TRANSACTIONMANAGERADDRESS:
                    // For now we skip these 2005 only env change notifications
                    break;

                case TdsEnums.ENV_SPRESETCONNECTIONACK:
                    // connection is being reset
                    if (_currentSessionData != null)
                    {
                        _currentSessionData.Reset();
                    }
                    break;

                case TdsEnums.ENV_USERINSTANCE:
                    _instanceName = rec._newValue;
                    break;

                case TdsEnums.ENV_ROUTING:
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnEnvChange|ADV> {0}, Received routing info", ObjectID);
                    if (string.IsNullOrEmpty(rec._newRoutingInfo.ServerName) || rec._newRoutingInfo.Protocol != 0 || rec._newRoutingInfo.Port == 0)
                    {
                        throw SQL.ROR_InvalidRoutingInfo(this);
                    }
                    _routingInfo = rec._newRoutingInfo;
                    break;

                default:
                    Debug.Fail("Missed token in EnvChange!");
                    break;
            }
        }

        internal void OnLoginAck(SqlLoginAck rec)
        {
            _loginAck = rec;
            // UNDONE:  throw an error if this is not 7.0 or 7.1[5].
            if (_recoverySessionData != null)
            {
                if (_recoverySessionData._tdsVersion != rec.tdsVersion)
                {
                    throw SQL.CR_TDSVersionNotPreserved(this);
                }
            }
            if (_currentSessionData != null)
            {
                _currentSessionData._tdsVersion = rec.tdsVersion;
            }
        }

        /// <summary>
        /// Generates (if appropriate) and sends a Federated Authentication Access token to the server, using the Federated Authentication Info.
        /// </summary>
        /// <param name="fedAuthInfo">Federated Authentication Info.</param>
        internal void OnFedAuthInfo(SqlFedAuthInfo fedAuthInfo)
        {
            Debug.Assert((ConnectionOptions._hasUserIdKeyword && ConnectionOptions._hasPasswordKeyword)
                         || _credential != null
                         || _accessTokenCallback != null
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                         || (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && _fedAuthRequired),
                         "Credentials aren't provided for calling MSAL");
            Debug.Assert(fedAuthInfo != null, "info should not be null.");
            Debug.Assert(_dbConnectionPoolAuthenticationContextKey == null, "_dbConnectionPoolAuthenticationContextKey should be null.");
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFedAuthInfo> {0}, Generating federated authentication token", ObjectID);
            DbConnectionPoolAuthenticationContext dbConnectionPoolAuthenticationContext = null;

            // We want to refresh the token without taking a lock on the context, allowed when the access token is expiring within the next 10 mins.
            bool attemptRefreshTokenUnLocked = false;

            // We want to refresh the token, if taking the lock on the authentication context is successful.
            bool attemptRefreshTokenLocked = false;

            if (_dbConnectionPool != null)
            {
                Debug.Assert(_dbConnectionPool.AuthenticationContexts != null);

                // Construct the dbAuthenticationContextKey with information from FedAuthInfo and store for later use, when inserting in to the token cache.
                _dbConnectionPoolAuthenticationContextKey = new DbConnectionPoolAuthenticationContextKey(fedAuthInfo.stsurl, fedAuthInfo.spn);

                // Try to retrieve the authentication context from the pool, if one does exist for this key.
                if (_dbConnectionPool.AuthenticationContexts.TryGetValue(_dbConnectionPoolAuthenticationContextKey, out dbConnectionPoolAuthenticationContext))
                {
                    Debug.Assert(dbConnectionPoolAuthenticationContext != null, "dbConnectionPoolAuthenticationContext should not be null.");

                    // The timespan between UTCNow and the token expiry.
                    TimeSpan contextValidity = dbConnectionPoolAuthenticationContext.ExpirationTime.Subtract(DateTime.UtcNow);

                    // If the authentication context is expiring within next 10 minutes, lets just re-create a token for this connection attempt.
                    // And on successful login, try to update the cache with the new token.
                    if (contextValidity <= _dbAuthenticationContextUnLockedRefreshTimeSpan)
                    {
                        if (SqlClientEventSource.Log.IsTraceEnabled())
                        {
                            SqlClientEventSource.Log.TraceEvent("<sc.SqlInternalConnectionTds.OnFedAuthInfo> {0}, " +
                                "The expiration time is less than 10 mins, so trying to get new access token regardless of if an other thread is also trying to update it." +
                                "The expiration time is {1}. Current Time is {2}.", ObjectID, dbConnectionPoolAuthenticationContext.ExpirationTime.ToLongTimeString(), DateTime.UtcNow.ToLongTimeString());
                        }
                        attemptRefreshTokenUnLocked = true;
                    }

#if DEBUG
                    // Checking if any failpoints are enabled.
                    else if (_forceExpiryUnLocked)
                    {
                        attemptRefreshTokenUnLocked = true;
                    }
                    else if (_forceExpiryLocked)
                    {
                        attemptRefreshTokenLocked = TryGetFedAuthTokenLocked(fedAuthInfo, dbConnectionPoolAuthenticationContext, out _fedAuthToken);
                    }
#endif

                    // If the token is expiring within the next 45 mins, try to fetch a new token, if there is no thread already doing it.
                    // If a thread is already doing the refresh, just use the existing token in the cache and proceed.
                    else if (contextValidity <= _dbAuthenticationContextLockedRefreshTimeSpan)
                    {
                        if (SqlClientEventSource.Log.IsAdvancedTraceOn())
                        {
                            SqlClientEventSource.Log.AdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFedAuthInfo|ADV> {0}, " +
                                "The authentication context needs a refresh.The expiration time is {1}. " +
                                "Current Time is {2}.", ObjectID, dbConnectionPoolAuthenticationContext.ExpirationTime.ToLongTimeString(), DateTime.UtcNow.ToLongTimeString());
                        }

                        // Call the function which tries to acquire a lock over the authentication context before trying to update.
                        // If the lock could not be obtained, it will return false, without attempting to fetch a new token.
                        attemptRefreshTokenLocked = TryGetFedAuthTokenLocked(fedAuthInfo, dbConnectionPoolAuthenticationContext, out _fedAuthToken);

                        // If TryGetFedAuthTokenLocked returns true, it means lock was obtained and _fedAuthToken should not be null.
                        // If there was an exception in retrieving the new token, TryGetFedAuthTokenLocked should have thrown, so we won't be here.
                        Debug.Assert(!attemptRefreshTokenLocked || _fedAuthToken != null, "Either Lock should not have been obtained or _fedAuthToken should not be null.");
                        Debug.Assert(!attemptRefreshTokenLocked || _newDbConnectionPoolAuthenticationContext != null, "Either Lock should not have been obtained or _newDbConnectionPoolAuthenticationContext should not be null.");

                        // Indicate in Bid Trace that we are successful with the update.
                        if (attemptRefreshTokenLocked)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFedAuthInfo> {0}, The attempt to get a new access token succeeded under the locked mode.", ObjectID);
                        }
                    }
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFedAuthInfo> {0}, Found an authentication context in the cache that does not need a refresh at this time. Re-using the cached token.", ObjectID);
                }
            }

            // dbConnectionPoolAuthenticationContext will be null if either this is the first connection attempt in the pool or pooling is disabled.
            if (dbConnectionPoolAuthenticationContext == null || attemptRefreshTokenUnLocked)
            {
                // Get the Federated Authentication Token.
                _fedAuthToken = GetFedAuthToken(fedAuthInfo);
                Debug.Assert(_fedAuthToken != null, "_fedAuthToken should not be null.");

                if (_dbConnectionPool != null)
                {
                    // GetFedAuthToken should have updated _newDbConnectionPoolAuthenticationContext.
                    Debug.Assert(_newDbConnectionPoolAuthenticationContext != null, "_newDbConnectionPoolAuthenticationContext should not be null.");

                    if (_newDbConnectionPoolAuthenticationContext != null)
                    {
                         _dbConnectionPool.AuthenticationContexts.TryAdd(_dbConnectionPoolAuthenticationContextKey, _newDbConnectionPoolAuthenticationContext);
                    }
                }
            }
            else if (!attemptRefreshTokenLocked)
            {
                Debug.Assert(dbConnectionPoolAuthenticationContext != null, "dbConnectionPoolAuthenticationContext should not be null.");
                Debug.Assert(_fedAuthToken == null, "_fedAuthToken should be null in this case.");
                Debug.Assert(_newDbConnectionPoolAuthenticationContext == null, "_newDbConnectionPoolAuthenticationContext should be null.");

                _fedAuthToken = new SqlFedAuthToken();

                // If the code flow is here, then we are re-using the context from the cache for this connection attempt and not
                // generating a new access token on this thread.
                _fedAuthToken.accessToken = dbConnectionPoolAuthenticationContext.AccessToken;
                _fedAuthToken.expirationFileTime = dbConnectionPoolAuthenticationContext.ExpirationTime.ToFileTime();
            }

            Debug.Assert(_fedAuthToken != null && _fedAuthToken.accessToken != null, "_fedAuthToken and _fedAuthToken.accessToken cannot be null.");
            _parser.SendFedAuthToken(_fedAuthToken);
        }

        /// <summary>
        /// Tries to acquire a lock on the authentication context. If successful in acquiring the lock, gets a new token and assigns it in the out parameter. Else returns false.
        /// </summary>
        /// <param name="fedAuthInfo">Federated Authentication Info</param>
        /// <param name="dbConnectionPoolAuthenticationContext">Authentication Context cached in the connection pool.</param>
        /// <param name="fedAuthToken">Out parameter, carrying the token if we acquired a lock and got the token.</param>
        /// <returns></returns>
        internal bool TryGetFedAuthTokenLocked(SqlFedAuthInfo fedAuthInfo, DbConnectionPoolAuthenticationContext dbConnectionPoolAuthenticationContext, out SqlFedAuthToken fedAuthToken)
        {

            Debug.Assert(fedAuthInfo != null, "fedAuthInfo should not be null.");
            Debug.Assert(dbConnectionPoolAuthenticationContext != null, "dbConnectionPoolAuthenticationContext should not be null.");

            fedAuthToken = null;

            // Variable which indicates if we did indeed manage to acquire the lock on the authentication context, to try update it.
            bool authenticationContextLocked = false;

            // Prepare CER to ensure the lock on authentication context is released.
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                // Try to obtain a lock on the context. If acquired, this thread got the opportunity to update.
                // Else some other thread is already updating it, so just proceed forward with the existing token in the cache.
                if (dbConnectionPoolAuthenticationContext.LockToUpdate())
                {
                    if (SqlClientEventSource.Log.IsTraceEnabled())
                    {
                        SqlClientEventSource.Log.TraceEvent("<sc.SqlInternalConnectionTds.TryGetFedAuthTokenLocked> {0}, " +
                        "Acquired the lock to update the authentication context.The expiration time is {1}. " +
                        "Current Time is {2}.", ObjectID, dbConnectionPoolAuthenticationContext.ExpirationTime.ToLongTimeString(), DateTime.UtcNow.ToLongTimeString());
                    }
                    authenticationContextLocked = true;
                }
                else
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.TryGetFedAuthTokenLocked> {0}, Refreshing the context is already in progress by another thread.", ObjectID);
                }

                if (authenticationContextLocked)
                {
                    // Get the Federated Authentication Token.
                    fedAuthToken = GetFedAuthToken(fedAuthInfo);
                    Debug.Assert(fedAuthToken != null, "fedAuthToken should not be null.");
                }
            }
            finally
            {
                if (authenticationContextLocked)
                {
                    // Release the lock we took on the authentication context, even if we have not yet updated the cache with the new context. Login process can fail at several places after this step and so there is no guarantee that the new context will make it to the cache. So we shouldn't miss resetting the flag. With the reset, at-least another thread may have a chance to update it.
                    dbConnectionPoolAuthenticationContext.ReleaseLockToUpdate();
                }
            }

            return authenticationContextLocked;
        }

        /// <summary>
        /// Get the Federated Authentication Token.
        /// </summary>
        /// <param name="fedAuthInfo">Information obtained from server as Federated Authentication Info.</param>
        /// <returns>SqlFedAuthToken</returns>
        internal SqlFedAuthToken GetFedAuthToken(SqlFedAuthInfo fedAuthInfo)
        {

            Debug.Assert(fedAuthInfo != null, "fedAuthInfo should not be null.");

            // No:of milliseconds to sleep for the inital back off.
            int sleepInterval = 100;

            // No:of attempts, for tracing purposes, if we underwent retries.
            int numberOfAttempts = 0;

            // Object that will be returned to the caller, containing all required data about the token.
            _fedAuthToken = new SqlFedAuthToken();

            // Username to use in error messages.
            string username = null;

            SqlAuthenticationProvider authProvider = SqlAuthenticationProvider.GetProvider(ConnectionOptions.Authentication);
            if (authProvider == null && _accessTokenCallback == null)
                throw SQL.CannotFindAuthProvider(ConnectionOptions.Authentication.ToString());

            // retry getting access token once if MsalException.error_code is unknown_error.
            // extra logic to deal with HTTP 429 (Retry after).
            while (numberOfAttempts <= 1 && sleepInterval <= _timeout.MillisecondsRemaining)
            {
                numberOfAttempts++;
                try
                {
                    var authParamsBuilder = new SqlAuthenticationParameters.Builder(
                        authenticationMethod: ConnectionOptions.Authentication,
                        resource: fedAuthInfo.spn,
                        authority: fedAuthInfo.stsurl,
                        serverName: ConnectionOptions.DataSource,
                        databaseName: ConnectionOptions.InitialCatalog)
                        .WithConnectionId(_clientConnectionId)
                        .WithConnectionTimeout(ConnectionOptions.ConnectTimeout);
                    switch (ConnectionOptions.Authentication)
                    {
                        case SqlAuthenticationMethod.ActiveDirectoryIntegrated:
                            username = TdsEnums.NTAUTHORITYANONYMOUSLOGON;
                            if (_activeDirectoryAuthTimeoutRetryHelper.State == ActiveDirectoryAuthenticationTimeoutRetryState.Retrying)
                            {
                                _fedAuthToken = _activeDirectoryAuthTimeoutRetryHelper.CachedToken;
                            }
                            else
                            {
                                // We use Task.Run here in all places to execute task synchronously in the same context.
                                // Fixes block-over-async deadlock possibilities https://github.com/dotnet/SqlClient/issues/1209
                                _fedAuthToken = Task.Run(async () => await authProvider.AcquireTokenAsync(authParamsBuilder)).GetAwaiter().GetResult().ToSqlFedAuthToken();
                                _activeDirectoryAuthTimeoutRetryHelper.CachedToken = _fedAuthToken;
                            }
                            break;
                        case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                        case SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow:
                        case SqlAuthenticationMethod.ActiveDirectoryManagedIdentity:
                        case SqlAuthenticationMethod.ActiveDirectoryMSI:
                        case SqlAuthenticationMethod.ActiveDirectoryDefault:
                        case SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity:
                            if (_activeDirectoryAuthTimeoutRetryHelper.State == ActiveDirectoryAuthenticationTimeoutRetryState.Retrying)
                            {
                                _fedAuthToken = _activeDirectoryAuthTimeoutRetryHelper.CachedToken;
                            }
                            else
                            {
                                authParamsBuilder.WithUserId(ConnectionOptions.UserID);
                                _fedAuthToken = Task.Run(async () => await authProvider.AcquireTokenAsync(authParamsBuilder)).GetAwaiter().GetResult().ToSqlFedAuthToken();
                                _activeDirectoryAuthTimeoutRetryHelper.CachedToken = _fedAuthToken;
                            }
                            break;
                        #pragma warning disable 0618
                        case SqlAuthenticationMethod.ActiveDirectoryPassword:
                        #pragma warning restore 0618
                        case SqlAuthenticationMethod.ActiveDirectoryServicePrincipal:
                            if (_activeDirectoryAuthTimeoutRetryHelper.State == ActiveDirectoryAuthenticationTimeoutRetryState.Retrying)
                            {
                                _fedAuthToken = _activeDirectoryAuthTimeoutRetryHelper.CachedToken;
                            }
                            else
                            {
                                if (_credential != null)
                                {
                                    username = _credential.UserId;
                                    authParamsBuilder.WithUserId(username).WithPassword(_credential.Password);
                                    _fedAuthToken = Task.Run(async () => await authProvider.AcquireTokenAsync(authParamsBuilder)).GetAwaiter().GetResult().ToSqlFedAuthToken();
                                }
                                else
                                {
                                    username = ConnectionOptions.UserID;
                                    authParamsBuilder.WithUserId(username).WithPassword(ConnectionOptions.Password);
                                    _fedAuthToken = Task.Run(async () => await authProvider.AcquireTokenAsync(authParamsBuilder)).GetAwaiter().GetResult().ToSqlFedAuthToken();
                                }
                                _activeDirectoryAuthTimeoutRetryHelper.CachedToken = _fedAuthToken;
                            }
                            break;
                        default:
                            if (_accessTokenCallback == null)
                            {
                                throw SQL.UnsupportedAuthenticationSpecified(ConnectionOptions.Authentication);
                            }

                            if (_activeDirectoryAuthTimeoutRetryHelper.State == ActiveDirectoryAuthenticationTimeoutRetryState.Retrying)
                            {
                                _fedAuthToken = _activeDirectoryAuthTimeoutRetryHelper.CachedToken;
                            }
                            else
                            {
                                if (_credential != null)
                                {
                                    username = _credential.UserId;
                                    authParamsBuilder.WithUserId(username).WithPassword(_credential.Password);
                                }
                                else
                                {
                                    authParamsBuilder.WithUserId(ConnectionOptions.UserID);
                                    authParamsBuilder.WithPassword(ConnectionOptions.Password);
                                }
                                SqlAuthenticationParameters parameters = authParamsBuilder;
                                CancellationTokenSource cts = new();
                                // Use Connection timeout value to cancel token acquire request after certain period of time.(int)
                                if (_timeout.MillisecondsRemaining < Int32.MaxValue)
                                {
                                    cts.CancelAfter((int)_timeout.MillisecondsRemaining);
                                }
                                _fedAuthToken = Task.Run(async () => await _accessTokenCallback(parameters, cts.Token)).GetAwaiter().GetResult().ToSqlFedAuthToken();
                                _activeDirectoryAuthTimeoutRetryHelper.CachedToken = _fedAuthToken;
                            }
                            break;
                    }

                    Debug.Assert(_fedAuthToken.accessToken != null, "AccessToken should not be null.");
#if DEBUG
                    if (_forceMsalRetry)
                    {
                        // 3399614468 is 0xCAA20004L just for testing.
                        throw new MsalServiceException(MsalError.UnknownError, "Force retry in GetFedAuthToken");
                    }
#endif
                    // Break out of the retry loop in successful case.
                    break;
                }
                // Deal with Msal service exceptions first, retry if 429 received.
                catch (MsalServiceException serviceException)
                {
                    if (serviceException.StatusCode == MsalHttpRetryStatusCode)
                    {
                        RetryConditionHeaderValue retryAfter = serviceException.Headers.RetryAfter;
                        if (retryAfter.Delta.HasValue)
                        {
                            sleepInterval = retryAfter.Delta.Value.Milliseconds;
                        }
                        else if (retryAfter.Date.HasValue)
                        {
                            sleepInterval = Convert.ToInt32(retryAfter.Date.Value.Offset.TotalMilliseconds);
                        }

                        // if there's enough time to retry before timeout, then retry, otherwise break out the retry loop.
                        if (sleepInterval < _timeout.MillisecondsRemaining)
                        {
                            Thread.Sleep(sleepInterval);
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.GetFedAuthToken.MsalServiceException error:> Timeout: {0}", serviceException.ErrorCode);
                            throw SQL.ActiveDirectoryTokenRetrievingTimeout(Enum.GetName(typeof(SqlAuthenticationMethod), ConnectionOptions.Authentication), serviceException.ErrorCode, serviceException);
                        }
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.GetFedAuthToken.MsalServiceException error:> {0}", serviceException.ErrorCode);
                        throw ADP.CreateSqlException(serviceException, ConnectionOptions, this, username);
                    }
                }
                // Deal with normal MsalExceptions.
                catch (MsalException msalException)
                {
                    if (MsalError.UnknownError != msalException.ErrorCode
                        || _timeout.IsExpired
                        || _timeout.MillisecondsRemaining <= sleepInterval)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.GetFedAuthToken.MSALException error:> {0}", msalException.ErrorCode);

                        throw ADP.CreateSqlException(msalException, ConnectionOptions, this, username);
                    }

                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.GetFedAuthToken|ADV> {0}, sleeping {1}[Milliseconds]", ObjectID, sleepInterval);
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.GetFedAuthToken|ADV> {0}, remaining {1}[Milliseconds]", ObjectID, _timeout.MillisecondsRemaining);

                    Thread.Sleep(sleepInterval);
                    sleepInterval *= 2;
                }
                // All other exceptions from MSAL/Azure Identity APIs
                catch (Exception e)
                {
                    throw SqlException.CreateException(new() { new(0, (byte)0x00, (byte)TdsEnums.FATAL_ERROR_CLASS, ConnectionOptions.DataSource, e.Message, ActiveDirectoryAuthentication.MSALGetAccessTokenFunctionName, 0) }, "", this, e);
                }
            }

            Debug.Assert(_fedAuthToken != null, "fedAuthToken should not be null.");
            Debug.Assert(_fedAuthToken.accessToken != null && _fedAuthToken.accessToken.Length > 0, "fedAuthToken.accessToken should not be null or empty.");

            // Store the newly generated token in _newDbConnectionPoolAuthenticationContext, only if using pooling.
            if (_dbConnectionPool != null)
            {
                DateTime expirationTime = DateTime.FromFileTimeUtc(_fedAuthToken.expirationFileTime);
                _newDbConnectionPoolAuthenticationContext = new DbConnectionPoolAuthenticationContext(_fedAuthToken.accessToken, expirationTime);
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.GetFedAuthToken> {0}, Finished generating federated authentication token.", ObjectID);
            return _fedAuthToken;
        }

        internal void OnFeatureExtAck(int featureId, byte[] data)
        {
            if (_routingInfo != null)
            {
                if (TdsEnums.FEATUREEXT_SQLDNSCACHING != featureId)
                {
                    return;
                }
            }

            switch (featureId)
            {
                case TdsEnums.FEATUREEXT_SRECOVERY:
                    {
                        // Session recovery not requested
                        if (!_sessionRecoveryRequested)
                        {
                            throw SQL.ParsingErrorFeatureId(ParsingErrorState.UnrequestedFeatureAckReceived, featureId);
                        }
                        _sessionRecoveryAcknowledged = true;

#if DEBUG
                        foreach (var s in _currentSessionData._delta)
                        {
                            Debug.Assert(s == null, "Delta should be null at this point");
                        }
#endif
                        Debug.Assert(_currentSessionData._unrecoverableStatesCount == 0, "Unrecoverable states count should be 0");

                        int i = 0;
                        while (i < data.Length)
                        {
                            byte stateId = data[i];
                            i++;
                            int len;
                            byte bLen = data[i];
                            i++;
                            if (bLen == 0xFF)
                            {
                                len = BitConverter.ToInt32(data, i);
                                i += 4;
                            }
                            else
                            {
                                len = bLen;
                            }
                            byte[] stateData = new byte[len];
                            Buffer.BlockCopy(data, i, stateData, 0, len);
                            i += len;
                            if (_recoverySessionData == null)
                            {
                                _currentSessionData._initialState[stateId] = stateData;
                            }
                            else
                            {
                                _currentSessionData._delta[stateId] = new SessionStateRecord { _data = stateData, _dataLength = len, _recoverable = true, _version = 0 };
                                _currentSessionData._deltaDirty = true;
                            }
                        }
                        break;
                    }
                case TdsEnums.FEATUREEXT_FEDAUTH:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for federated authentication", ObjectID);

                        if (!_federatedAuthenticationRequested)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Did not request federated authentication", ObjectID);
                            throw SQL.ParsingErrorFeatureId(ParsingErrorState.UnrequestedFeatureAckReceived, featureId);
                        }

                        Debug.Assert(_fedAuthFeatureExtensionData != null, "_fedAuthFeatureExtensionData must not be null when _federatedAuthenticationRequested == true");

                        switch (_fedAuthFeatureExtensionData.libraryType)
                        {
                            case TdsEnums.FedAuthLibrary.MSAL:
                            case TdsEnums.FedAuthLibrary.SecurityToken:
                                // The server shouldn't have sent any additional data with the ack (like a nonce)
                                if (data.Length != 0)
                                {
                                    SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Federated authentication feature extension ack for MSAL and Security Token includes extra data", ObjectID);
                                    throw SQL.ParsingError(ParsingErrorState.FedAuthFeatureAckContainsExtraData);
                                }
                                break;

                            default:
                                Debug.Fail("Unknown _fedAuthLibrary type");

                                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Attempting to use unknown federated authentication library", ObjectID);
                                throw SQL.ParsingErrorLibraryType(ParsingErrorState.FedAuthFeatureAckUnknownLibraryType, (int)_fedAuthFeatureExtensionData.libraryType);
                        }
                        _federatedAuthenticationAcknowledged = true;

                        // If a new authentication context was used as part of this login attempt, try to update the new context in the cache, i.e.dbConnectionPool.AuthenticationContexts.
                        // ChooseAuthenticationContextToUpdate will take care that only the context which has more validity will remain in the cache, based on the Update logic.
                        if (_newDbConnectionPoolAuthenticationContext != null)
                        {
                            Debug.Assert(_dbConnectionPool != null, "_dbConnectionPool should not be null when _newDbConnectionPoolAuthenticationContext != null.");

                            DbConnectionPoolAuthenticationContext newAuthenticationContextInCacheAfterAddOrUpdate = _dbConnectionPool.AuthenticationContexts.AddOrUpdate(_dbConnectionPoolAuthenticationContextKey, _newDbConnectionPoolAuthenticationContext,
                                                                                 (key, oldValue) => DbConnectionPoolAuthenticationContext.ChooseAuthenticationContextToUpdate(oldValue, _newDbConnectionPoolAuthenticationContext));

                            Debug.Assert(newAuthenticationContextInCacheAfterAddOrUpdate != null, "newAuthenticationContextInCacheAfterAddOrUpdate should not be null.");
#if DEBUG
                            // For debug purposes, assert and trace if we ended up updating the cache with the new one or some other thread's context won the expiration race.
                            if (newAuthenticationContextInCacheAfterAddOrUpdate == _newDbConnectionPoolAuthenticationContext)
                            {
                                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Updated the new dbAuthenticationContext in the _dbConnectionPool.AuthenticationContexts.", ObjectID);
                            }
                            else
                            {
                                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, AddOrUpdate attempted on _dbConnectionPool.AuthenticationContexts, but it did not update the new value.", ObjectID);
                            }
#endif
                        }

                        break;
                    }
                case TdsEnums.FEATUREEXT_TCE:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for TCE", ObjectID);
                        if (data.Length < 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown version number for TCE", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.TceUnknownVersion);
                        }

                        byte supportedTceVersion = data[0];
                        if (0 == supportedTceVersion || supportedTceVersion > TdsEnums.MAX_SUPPORTED_TCE_VERSION)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Invalid version number for TCE", ObjectID);
                            throw SQL.ParsingErrorValue(ParsingErrorState.TceInvalidVersion, supportedTceVersion);
                        }

                        _tceVersionSupported = supportedTceVersion;
                        Debug.Assert(_tceVersionSupported <= TdsEnums.MAX_SUPPORTED_TCE_VERSION, "Client support TCE version 2");
                        _parser.IsColumnEncryptionSupported = true;
                        _parser.TceVersionSupported = _tceVersionSupported;
                        _parser.AreEnclaveRetriesSupported = _tceVersionSupported == 3;

                        if (data.Length > 1)
                        {
                            _parser.EnclaveType = Encoding.Unicode.GetString(data, 2, (data.Length - 2));
                        }

                        break;
                    }

                case TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for GlobalTransactions", ObjectID);

                        if (data.Length < 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown version number for GlobalTransactions", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }

                        IsGlobalTransaction = true;
                        if (1 == data[0])
                        {
                            IsGlobalTransactionsEnabledForServer = true;
                        }
                        break;
                    }

                case TdsEnums.FEATUREEXT_AZURESQLSUPPORT:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for AzureSQLSupport", ObjectID);

                        if (data.Length < 1)
                        {
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }

                        IsAzureSQLConnection = true;

                        //  Bit 0 for RO/FP support
                        if ((data[0] & 1) == 1 && SqlClientEventSource.Log.IsTraceEnabled())
                        {
                            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, FailoverPartner enabled with Readonly intent for AzureSQL DB", ObjectID);
                        }
                        break;
                    }

                case TdsEnums.FEATUREEXT_DATACLASSIFICATION:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for DATACLASSIFICATION", ObjectID);

                        if (data.Length < 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown token for DATACLASSIFICATION", ObjectID);

                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }
                        byte supportedDataClassificationVersion = data[0];
                        if ((0 == supportedDataClassificationVersion) || (supportedDataClassificationVersion > TdsEnums.DATA_CLASSIFICATION_VERSION_MAX_SUPPORTED))
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Invalid version number for DATACLASSIFICATION", ObjectID);
                            throw SQL.ParsingErrorValue(ParsingErrorState.DataClassificationInvalidVersion, supportedDataClassificationVersion);
                        }

                        if (data.Length != 2)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown token for DATACLASSIFICATION", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }
                        byte enabled = data[1];
                        _parser.DataClassificationVersion = (enabled == 0) ? TdsEnums.DATA_CLASSIFICATION_NOT_ENABLED : supportedDataClassificationVersion;
                        break;
                    }

                case TdsEnums.FEATUREEXT_UTF8SUPPORT:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for UTF8 support", ObjectID);

                        if (data.Length < 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown value for UTF8 support", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }
                        break;
                    }

                case TdsEnums.FEATUREEXT_SQLDNSCACHING:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for SQLDNSCACHING", ObjectID);

                        if (data.Length < 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown token for SQLDNSCACHING", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }

                        if (1 == data[0])
                        {
                            IsSQLDNSCachingSupported = true;
                            _cleanSQLDNSCaching = false;

                            if (_routingInfo != null)
                            {
                                IsDNSCachingBeforeRedirectSupported = true;
                            }
                        }
                        else
                        {
                            // we receive the IsSupported whose value is 0
                            IsSQLDNSCachingSupported = false;
                            _cleanSQLDNSCaching = true;
                        }

                        // need to add more steps for phrase 2
                        // get IPv4 + IPv6 + Port number
                        // not put them in the DNS cache at this point but need to store them somewhere

                        // generate pendingSQLDNSObject and turn on IsSQLDNSRetryEnabled flag

                        break;
                    }

                case TdsEnums.FEATUREEXT_JSONSUPPORT:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for JSONSUPPORT", ObjectID);
                        if (data.Length != 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown token for JSONSUPPORT", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }
                        byte jsonSupportVersion = data[0];
                        if (jsonSupportVersion == 0 || jsonSupportVersion > TdsEnums.MAX_SUPPORTED_JSON_VERSION)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Invalid version number for JSONSUPPORT", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }
                        IsJsonSupportEnabled = true;
                        break;
                    }

                case TdsEnums.FEATUREEXT_VECTORSUPPORT:
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for VECTORSUPPORT", ObjectID);
                        if (data.Length != 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Unknown token for VECTORSUPPORT", ObjectID);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }
                        byte vectorSupportVersion = data[0];
                        if (vectorSupportVersion == 0 || vectorSupportVersion > TdsEnums.MAX_SUPPORTED_VECTOR_VERSION)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ERR> {0}, Invalid version number {1} for VECTORSUPPORT, Max supported version is {2}", ObjectID, vectorSupportVersion, TdsEnums.MAX_SUPPORTED_VECTOR_VERSION);
                            throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                        }
                        IsVectorSupportEnabled = true;
                        break;
                    }

                default:
                    {
                        // Unknown feature ack
                        throw SQL.ParsingErrorFeatureId(ParsingErrorState.UnknownFeatureAck, featureId);
                    }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // Helper methods for Locks
        ////////////////////////////////////////////////////////////////////////////////////////

        // Indicates if the current thread claims to hold the parser lock
        internal bool ThreadHasParserLockForClose
        {
            get
            {
                return _threadIdOwningParserLock == Thread.CurrentThread.ManagedThreadId;
            }
            set
            {
                Debug.Assert(_parserLock.ThreadMayHaveLock(), "Should not modify ThreadHasParserLockForClose without taking the lock first");
                Debug.Assert(_threadIdOwningParserLock == -1 || _threadIdOwningParserLock == Thread.CurrentThread.ManagedThreadId, "Another thread already claims to own the parser lock");

                if (value)
                {
                    // If setting to true, then the thread owning the lock is the current thread
                    _threadIdOwningParserLock = Thread.CurrentThread.ManagedThreadId;
                }
                else if (_threadIdOwningParserLock == Thread.CurrentThread.ManagedThreadId)
                {
                    // If setting to false and currently owns the lock, then no-one owns the lock
                    _threadIdOwningParserLock = -1;
                }
                // else This thread didn't own the parser lock and doesn't claim to own it, so do nothing
            }
        }

        internal override bool TryReplaceConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, TaskCompletionSource<DbConnectionInternal> retry, DbConnectionOptions userOptions)
        {
            return base.TryOpenConnectionInternal(outerConnection, connectionFactory, retry, userOptions);
        }
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
                return m_userServerName;
            }
            private set
            {
                m_userServerName = value;
            }
        }
        private string m_userServerName;

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

            UserProtocol = userOptions.NetworkLibrary;
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
            if (!ADP.IsEmpty(protocol))
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
