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
using System.Runtime.CompilerServices;
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
                {
                    unrecoverableCount++;
                }
            }
            Debug.Assert(unrecoverableCount == _unrecoverableStatesCount, "Unrecoverable count does not match");
        }
    }

    internal sealed partial class SqlInternalConnectionTds : SqlInternalConnection, IDisposable
    {
        // FOR SYNCHRONIZATION IN TdsParser
        // How to use these locks:
        // 1. Whenever writing to the connection (with the exception of Cancellation) the _parserLock MUST be taken
        // 2. _parserLock will also be taken during close (to prevent closing in the middle of a write)
        // 3. Whenever you have the _parserLock and are calling a method that would cause the connection to close if it failed (with the exception of any writing method), you MUST set ThreadHasParserLockForClose to true
        //      * This is to prevent the connection deadlocking with itself (since you already have the _parserLock, and Closing the connection will attempt to re-take that lock)
        //      * It is safe to set ThreadHasParserLockForClose to true when writing as well, but it is unnecessary
        //      * If you have a method that takes _parserLock, it is a good idea check ThreadHasParserLockForClose first (if you don't expect _parserLock to be taken by something higher on the stack, then you should at least assert that it is false)
        // 4. ThreadHasParserLockForClose is thread-specific - this means that you must set it to false before returning a Task, and set it back to true in the continuation
        // 5. ThreadHasParserLockForClose should only be modified if you currently own the _parserLock
        // 6. Reading ThreadHasParserLockForClose is thread-safe
        internal class SyncAsyncLock
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

        // although the new password is generally not used it must be passed to the ctor
        // the new Login7 packet will always write out the new password (or a length of zero and no bytes if not present)
        //
        internal SqlInternalConnectionTds(
                DbConnectionPoolIdentity identity,
                SqlConnectionString connectionOptions,
                SqlCredential credential,
                DbConnectionPoolGroupProviderInfo providerInfo,
                string newPassword,
                SecureString newSecurePassword,
                bool redirectedUserInstance,
                SqlConnectionString userConnectionOptions = null, // NOTE: userConnectionOptions may be different to connectionOptions if the connection string has been expanded (see SqlConnectionString.Expand)
                SessionData reconnectSessionData = null,
                bool applyTransientFaultHandling = false,
                string accessToken = null,
                IDbConnectionPool pool = null,
                Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> accessTokenCallback = null,
                SspiContextProvider sspiContextProvider = null) 
            : base(connectionOptions)
        {
#if DEBUG
            if (reconnectSessionData != null)
            {
                reconnectSessionData._debugReconnectDataApplied = true;
            }
            try
            {
                // use this to help validate this object is only created after the following permission has been previously demanded in the current codepath
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

            if (accessToken != null)
            {
                _accessTokenInBytes = System.Text.Encoding.Unicode.GetBytes(accessToken);
            }

            _accessTokenCallback = accessTokenCallback;
            _sspiContextProvider = sspiContextProvider;

            _activeDirectoryAuthTimeoutRetryHelper = new ActiveDirectoryAuthenticationTimeoutRetryHelper();

            _identity = identity;
            Debug.Assert(newSecurePassword != null || newPassword != null, "cannot have both new secure change password and string based change password to be null");
            Debug.Assert(credential == null || (string.IsNullOrEmpty(connectionOptions.UserID) && string.IsNullOrEmpty(connectionOptions.Password)), "cannot mix the new secure password system and the connection string based password");
            Debug.Assert(credential == null || !connectionOptions.IntegratedSecurity, "Cannot use SqlCredential and Integrated Security");

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

            try
            {
                _timeout = TimeoutTimer.StartSecondsTimeout(connectionOptions.ConnectTimeout);

                // If transient fault handling is enabled then we can retry the login up to the ConnectRetryCount.
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
            // @TODO: CER Exception Handling was removed here (see GH#3581)
            finally
            {
                ThreadHasParserLockForClose = false;
                _parserLock.Release();
            }
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.ctor|ADV> {0}, constructed new TDS internal connection", ObjectID);
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // LOGIN-RELATED METHODS
        ////////////////////////////////////////////////////////////////////////////////////////

        // Common code path for making one attempt to establish a connection and log in to server.
        private void AttemptOneLogin(ServerInfo serverInfo,
                                    string newPassword,
                                    SecureString newSecurePassword,
                                    TimeoutTimer timeout,
                                    bool withFailover = false,
                                    bool isFirstTransparentAttempt = true,
                                    bool disableTnir = false)
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.AttemptOneLogin|ADV> {0}, timeout={1}[msec], server={2}", ObjectID, timeout.MillisecondsRemaining, serverInfo.ExtendedServerName);
            RoutingInfo = null; // forget routing information

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

        // called by SqlConnection.RepairConnection which is a relatively expensive way of repair inner connection
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
                // @TODO: CER Exception Handling was removed here (see GH#3581)
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

        internal void OnEnvChange(SqlEnvChange rec)
        {
            Debug.Assert(!IgnoreEnvChange, "This function should not be called if IgnoreEnvChange is set!");
            switch (rec._type)
            {
                case TdsEnums.ENV_DATABASE:
                    // If connection is not open and recovery is not in progress, store the server value as the original.
                    if (!_fConnectionOpen && _recoverySessionData == null)
                    {
                        _originalDatabase = rec._newValue;
                    }

                    CurrentDatabase = rec._newValue;
                    break;

                case TdsEnums.ENV_LANG:
                    // If connection is not open and recovery is not in progress, store the server value as the original.
                    if (!_fConnectionOpen && _recoverySessionData == null)
                    {
                        _originalLanguage = rec._newValue;
                    }

                    _currentLanguage = rec._newValue;
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
                    ServerProvidedFailoverPartner = rec._newValue;
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
                    PromotedDtcToken = dtcToken;
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
                    RoutingInfo = rec._newRoutingInfo;
                    break;

                default:
                    Debug.Fail("Missed token in EnvChange!");
                    break;
            }
        }

        internal void OnLoginAck(SqlLoginAck rec)
        {
            _loginAck = rec;
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
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault
                         || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity
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

                        // Indicate in EventSource Trace that we are successful with the update.
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
            {
                throw SQL.CannotFindAuthProvider(ConnectionOptions.Authentication.ToString());
            }

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
                        #pragma warning disable 0618 // Type or member is obsolete
                        case SqlAuthenticationMethod.ActiveDirectoryPassword:
                        #pragma warning restore 0618 // Type or member is obsolete
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
                    if (MsalError.UnknownError != msalException.ErrorCode || _timeout.IsExpired || _timeout.MillisecondsRemaining <= sleepInterval)
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
                    throw SqlException.CreateException(
                        new()
                        {
                            new(
                                0,
                                (byte)0x00,
                                (byte)TdsEnums.FATAL_ERROR_CLASS,
                                ConnectionOptions.DataSource,
                                e.Message,
                                ActiveDirectoryAuthentication.MSALGetAccessTokenFunctionName,
                                0)
                        },
                        "",
                        this,
                        e);
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
            if (RoutingInfo != null && featureId != TdsEnums.FEATUREEXT_SQLDNSCACHING)
            {
                return;
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
                            // Extract the type of enclave being used by the server.
                            _parser.EnclaveType = Encoding.Unicode.GetString(data, 2, (data.Length - 2));
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

                        IsAzureSqlConnection = true;

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

                            if (RoutingInfo != null)
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

                        // need to add more steps for phase 2
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
                case TdsEnums.FEATUREEXT_USERAGENT:
                    {
                        // TODO: Verify that the server sends an acknowledgment (Ack)
                        // using this log message in the future.

                        // This Ack from the server is unexpected and is ignored completely.
                        // According to the TDS specification, an Ack is not defined/expected
                        // for this scenario. We handle it only for completeness
                        // and to support testing.
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for USERAGENTSUPPORT (ignored)", ObjectID);
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

        internal override bool TryReplaceConnection(
            DbConnection outerConnection,
            SqlConnectionFactory connectionFactory,
            TaskCompletionSource<DbConnectionInternal> retry,
            DbConnectionOptions userOptions)
        {
            return TryOpenConnectionInternal(outerConnection, connectionFactory, retry, userOptions);
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
