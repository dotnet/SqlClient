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
            object providerInfo,
            string newPassword,
            SecureString newSecurePassword,
            bool redirectedUserInstance,
            SqlConnectionString userConnectionOptions = null, // NOTE: userConnectionOptions may be different to connectionOptions if the connection string has been expanded (see SqlConnectionString.Expand)
            SessionData reconnectSessionData = null,
            bool applyTransientFaultHandling = false,
            string accessToken = null,
            IDbConnectionPool pool = null,
            Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> accessTokenCallback = null,
            SspiContextProvider sspiContextProvider = null) : base(connectionOptions)
        {
#if DEBUG
            if (reconnectSessionData != null)
            {
                reconnectSessionData._debugReconnectDataApplied = true;
            }
#if NETFRAMEWORK
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
        // PARSER CALLBACKS
        ////////////////////////////////////////////////////////////////////////////////////////

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
                            throw SQL.ParsingError();
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
                            throw SQL.ParsingError();
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
                            throw SQL.ParsingError();
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
                            throw SQL.ParsingError();
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
                            throw SQL.ParsingError();
                        }
                        IsVectorSupportEnabled = true;
                        break;
                    }
                case TdsEnums.FEATUREEXT_USERAGENT:
                    {
                        // Unexpected ack from server but we ignore it entirely
                        SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.OnFeatureExtAck|ADV> {0}, Received feature extension acknowledgement for USERAGENTSUPPORT (ignored)", ObjectID);
                        break;
                    }

                default:
                    {
                        // Unknown feature ack
                        throw SQL.ParsingError();
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
