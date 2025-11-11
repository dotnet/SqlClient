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
    internal partial class SqlInternalConnectionTds : SqlInternalConnection, IDisposable
    {
        #region Constants

        /// <summary>
        /// Maximum number of times the connection should be rerouted.
        /// </summary>
        // @TODO: Can be private?
        internal const int MaxNumberOfRedirectRoute = 10;

        /// <summary>
        /// Status code that indicates MSAL request should be retried.
        /// https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/retry-after#simple-retry-for-errors-with-http-error-codes-500-600
        /// </summary>
        // @TODO: Can be private?
        internal const int MsalHttpRetryStatusCode = 429;

        /// <summary>
        /// The timespan defining the amount of time the authentication context needs to be valid
        /// for at-least, to re-use the cached context, without making an attempt to refresh it. IF
        /// the context is expiring within the next 45 mins, then try to take a lock and refresh
        /// the context, if the lock is acquired.
        /// </summary>
        // @TODO: Rename to match naming conventions (s_camelCase or PascalCase)
        private static readonly TimeSpan _dbAuthenticationContextLockedRefreshTimeSpan =
            new TimeSpan(hours: 0, minutes: 45, seconds: 00);

        /// <summary>
        /// The timespan defining the minimum amount of time the authentication context needs to be
        /// valid for re-using the cached context. If the context is expiring within the next 10
        /// mins, then create a new context, irrespective of if another thread is trying to do the
        /// same.
        /// </summary>
        // @TODO: Rename to match naming conventions (s_camelCase or PascalCase)
        private static readonly TimeSpan _dbAuthenticationContextUnLockedRefreshTimeSpan = new TimeSpan(hours: 0, minutes: 10, seconds: 00);

        private static readonly HashSet<int> s_transientErrors =
        [
            // SQL Error Code: 4060
            // Cannot open database "%.*ls" requested by the login. The login failed.
            4060,

            // SQL Error Code: 10928
            // Resource ID: %d. The %s limit for the database is %d and has been reached.
            10928,

            // SQL Error Code: 10929
            // Resource ID: %d. The %s minimum guarantee is %d, maximum limit is %d and the current
            // usage for the database is %d. However, the server is currently too busy to support
            // requests greater than %d for this database.
            10929,

            // @TODO: Why aren't these included in netcore?
            #if NETFRAMEWORK
            40020,
            40143,
            40166,
            #endif

            // SQL Error Code: 40197
            // You will receive this error, when the service is down due to software or hardware
            // upgrades, hardware failures, or any other failover problems. The error code (%d)
            // embedded within the message of error 40197 provides additional information about the
            // kind of failure or failover that occurred. Some examples of the error codes are
            // embedded within the message of error 40197 are 40020, 40143, 40166, and 40540.
            40197,

            // The service is currently busy. Retry the request after 10 seconds. Incident ID: %ls.
            // Code: %d.
            40501,

            #if NETFRAMEWORK
            // @TODO: Why isn't this one included in netcore?
            // The service has encountered an error processing your request. Please try again.
            40540,
            #endif

            // Database '%.*ls' on server '%.*ls' is not currently available. Please retry the
            // connection later. If the problem persists, contact customer support, and provide
            // them the session tracing ID of '%.*ls'.
            40613,

            // Can not connect to the SQL pool since it is paused. Please resume the SQL pool and
            // try again.
            42108,

            // The SQL pool is warming up. Please try again.
            42109

            // @TODO: From netfx: Do federation errors deserve to be here ?
            // Note: Federation errors 10053 and 10054 might also deserve inclusion in your retry logic.
            // 10053
            // 10054
        ];

        #endregion

        #region Fields

        #region Debug/Test Behavior Overrides
        #if DEBUG
        /// <summary>
        /// This is a test hook to enable testing of the retry paths for MSAL get access token.
        /// </summary>
        /// <example>
        /// Type type = typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.SQLInternalConnectionTds");
        /// FieldInfo field = type.GetField("_forceMsalRetry", BindingFlags.NonPublic | BindingFlags.Static);
        /// if (field != null)
        /// {
        ///     field.SetValue(null, true);
        /// }
        /// </example>
        /// @TODO: For unit tests, it should not be necessary to do this via reflection.
        internal static bool _forceMsalRetry = false;

        /// <summary>
        /// This is a test hook to simulate a token expiring within the next 45 minutes.
        /// </summary>
        private static bool _forceExpiryLocked = false;

        /// <summary>
        /// This is a test hook to simulate a token expiring within the next 10 minutes.
        /// </summary>
        private static bool _forceExpiryUnLocked = false;
        #endif
        #endregion

        // @TODO: Should be private and accessed via internal property
        internal byte[] _accessTokenInBytes;

        // @TODO: Should be private and accessed via internal property
        // @TODO: Probably a good idea to introduce a delegate type
        internal readonly Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> _accessTokenCallback;

        // @TODO: Should be private and accessed via internal property
        // @TODO: Rename to match naming conventions
        internal bool _cleanSQLDNSCaching = false;

        internal Guid _clientConnectionId = Guid.Empty;

        /// <remarks>
        /// Internal for use from TdsParser only, other should use CurrentSessionData property that will fix database and language
        /// @TODO: No... all external usages should be via property.
        /// </remarks>
        internal SessionData _currentSessionData;

        // @TODO: Should be private and accessed via internal property
        internal bool _sessionRecoveryAcknowledged;

        // @TODO: Should be private and accessed via internal property
        // @TODO: Could these federated auth fields be contained in a single record/struct/object?
        internal bool _fedAuthRequired;

        // @TODO: Should be private and accessed via internal property
        internal bool _federatedAuthenticationAcknowledged;

        // @TODO: Should be private and accessed via internal property
        internal bool _federatedAuthenticationInfoReceived;

        /// <remarks>
        /// Keep this distinct from _federatedAuthenticationRequested, since some fedauth library types may not need more info
        /// </remarks>
        // @TODO: Should be private and accessed via internal property
        internal bool _federatedAuthenticationInfoRequested;

        // @TODO: Should be private and accessed via internal property
        internal bool _federatedAuthenticationRequested;

        /// <summary>
        /// Flag indicating whether JSON objects are supported by the server.
        /// </summary>
        // @TODO: Should be private and accessed via internal property
        internal bool IsJsonSupportEnabled = false;

        /// <summary>
        /// Flag indicating whether vector objects are supported by the server.
        /// </summary>
        // @TODO: Should be private and accessed via internal property
        internal bool IsVectorSupportEnabled = false;

        // @TODO: Should be private and accessed via internal property
        internal SQLDNSInfo pendingSQLDNSObject = null;

        // @TODO: Should be private and accessed via internal property
        internal readonly SspiContextProvider _sspiContextProvider;

        /// <summary>
        /// TCE flags supported by the server.
        /// </summary>
        // @TODO: Should be private and accessed via internal property
        internal byte _tceVersionSupported;

        private readonly ActiveDirectoryAuthenticationTimeoutRetryHelper _activeDirectoryAuthTimeoutRetryHelper;

        /// <summary>
        /// Number of async Begins minus number of async Ends.
        /// </summary>
        private int _asyncCommandCount;

        // @TODO: Rename for naming conventions (remove f prefix)
        private bool _fConnectionOpen = false;

        private SqlCredential _credential;

        private string _currentLanguage;

        private int _currentPacketSize;

        /// <summary>
        /// Pool this connection is associated with, if any.
        /// </summary>
        private IDbConnectionPool _dbConnectionPool;

        /// <summary>
        /// Ley of the authentication context, built from information found in the FedAuthInfoToken.
        /// </summary>
        private DbConnectionPoolAuthenticationContextKey _dbConnectionPoolAuthenticationContextKey;

        // @TODO: Rename to match naming conventions
        private bool _dnsCachingBeforeRedirect = false;

        private FederatedAuthenticationFeatureExtensionData _fedAuthFeatureExtensionData;

        private SqlFedAuthToken _fedAuthToken = null;

        /// <summary>
        /// Used to lookup info for notification matching Start().
        /// </summary>
        private DbConnectionPoolIdentity _identity;

        private string _instanceName = string.Empty;

        private SqlLoginAck _loginAck;

        /// <summary>
        /// This is used to preserve the authentication context object if we decide to cache it for
        /// subsequent connections in the same pool. This will finally end up in
        /// _dbConnectionPool.AuthenticationContexts, but only after 1 successful login to SQL
        /// Server using this context. This variable is to persist the context after we have
        /// generated it, but before we have successfully completed the login with this new
        /// context. If this connection attempt ended up re-using the existing context and not
        /// create a new one, this will be null (since the context is not new).
        /// </summary>
        private DbConnectionPoolAuthenticationContext _newDbConnectionPoolAuthenticationContext;

        private Guid _originalClientConnectionId = Guid.Empty;

        private string _originalDatabase;

        private string _originalLanguage;

        private TdsParser _parser;

        /// <remarks>
        /// Will only be null when called for ChangePassword, or creating SSE User Instance.
        /// </remarks>
        private readonly SqlConnectionPoolGroupProviderInfo _poolGroupProviderInfo;

        private SessionData _recoverySessionData;

        // @TODO: Rename to match naming conventions (remove f prefix)
        private bool _fResetConnection;

        private string _routingDestination = null;

        // @TODO: Rename to match naming conventions
        private bool _SQLDNSRetryEnabled = false;

        // @TODO: Rename to match naming conventions
        private bool _serverSupportsDNSCaching = false;

        private bool _sessionRecoveryRequested;

        private int _threadIdOwningParserLock = -1;

        // @TODO: Rename to indicate this has to do with routing
        private readonly TimeoutTimer _timeout;

        private SqlConnectionTimeoutErrorInternal _timeoutErrorInternal;

        #endregion

        #region Constructors

        /// <remarks>
        /// - Although the new password is generally not used it must be passed to the ctor. The
        ///   new Login7 packet will always write out the new password (or a length of zero and no
        ///   bytes if not present).
        /// - userConnectionOptions may be different to connectionOptions if the connection string
        ///   has been expanded (see SqlConnectionString.Expand)
        /// </remarks>
        // @TODO: We really really need simplify what we pass into this. All these optional parameters need to go!
        internal SqlInternalConnectionTds(
            DbConnectionPoolIdentity identity,
            SqlConnectionString connectionOptions,
            SqlCredential credential,
            DbConnectionPoolGroupProviderInfo providerInfo,
            string newPassword,
            SecureString newSecurePassword,
            bool redirectedUserInstance,
            SqlConnectionString userConnectionOptions = null,
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
                // use this to help validate this object is only created after the following
                // permission has been previously demanded in the current codepath
                if (userConnectionOptions != null)
                {
                    // As mentioned above, userConnectionOptions may be different to
                    // connectionOptions, so we need to demand on the correct connection string
                    userConnectionOptions.DemandPermission();
                }
                else
                {
                    connectionOptions.DemandPermission();
                }
            }
            catch (SecurityException)
            {
                Debug.Assert(false, "unexpected SecurityException for current codepath");
                throw;
            }
            #endif
            #endif

            Debug.Assert(reconnectSessionData == null || connectionOptions.ConnectRetryCount > 0,
                "Reconnect data supplied with CR turned off");

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
                _accessTokenInBytes = Encoding.Unicode.GetBytes(accessToken);
            }

            _accessTokenCallback = accessTokenCallback;
            _sspiContextProvider = sspiContextProvider;

            _activeDirectoryAuthTimeoutRetryHelper = new ActiveDirectoryAuthenticationTimeoutRetryHelper();

            _identity = identity;

            Debug.Assert(newSecurePassword != null || newPassword != null,
                "cannot have both new secure change password and string based change password to be null");
            Debug.Assert(credential == null || (string.IsNullOrEmpty(connectionOptions.UserID) &&
                                                string.IsNullOrEmpty(connectionOptions.Password)),
                "cannot mix the new secure password system and the connection string based password");

            Debug.Assert(credential == null || !connectionOptions.IntegratedSecurity,
                "Cannot use SqlCredential and Integrated Security");

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

            // In case of error, let ourselves know that we already own the parser lock
            ThreadHasParserLockForClose = true;

            try
            {
                _timeout = TimeoutTimer.StartSecondsTimeout(connectionOptions.ConnectTimeout);

                // If transient fault handling is enabled then we can retry the login up to the
                // ConnectRetryCount.
                int connectionEstablishCount = applyTransientFaultHandling
                    ? connectionOptions.ConnectRetryCount + 1
                    : 1;

                // Max value of transientRetryInterval is 60*1000 ms. The max value allowed for
                // ConnectRetryInterval is 60
                int transientRetryIntervalInMilliSeconds = connectionOptions.ConnectRetryInterval * 1000;
                for (int i = 0; i < connectionEstablishCount; i++)
                {
                    try
                    {
                        OpenLoginEnlist(
                            _timeout,
                            connectionOptions,
                            credential,
                            newPassword,
                            newSecurePassword,
                            redirectedUserInstance);
                        break;
                    }
                    catch (SqlException sqlex)
                    {
                        if (connectionEstablishCount == i + 1
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

            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                $"SqlInternalConnectionTds.ctor | ADV | " +
                $"Object ID {ObjectID}, " +
                $"constructed new TDS internal connection");
        }

        #endregion

        #region Properties

        // @TODO: Make internal
        public override string ServerVersion
        {
            get => $"{_loginAck.majorVersion:00}.{(short)_loginAck.minorVersion:00}.{_loginAck.buildNum:0000}";
        }

        internal override SqlInternalTransaction AvailableInternalTransaction
        {
            get => _parser._fResetConnection ? null : CurrentTransaction;
        }

        // @TODO: Make auto-property
        internal Guid ClientConnectionId
        {
            get => _clientConnectionId;
        }

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

        internal override SqlInternalTransaction CurrentTransaction
        {
            get => _parser.CurrentTransaction;
        }

        // @TODO: Make auto-property
        internal DbConnectionPoolIdentity Identity
        {
            get => _identity;
        }

        /// <summary>
        /// Returns <c>true</c> if we are only draining environment change tokens, used by
        /// <see cref="TdsParser"/>. This is the case when the connection has been re-routed.
        /// </summary>
        internal bool IgnoreEnvChange
        {
            get => RoutingInfo != null;
        }

        // @TODO: Make auto-property
        internal string InstanceName
        {
            get => _instanceName;
        }

        internal override bool Is2008OrNewer
        {
            get => _parser.Is2008OrNewer;
        }

        /// <summary>
        /// Validates if federated authentication is used, Access Token used by this connection is
        /// active for the value of 'accessTokenExpirationBufferTime'.
        /// </summary>
        internal override bool IsAccessTokenExpired
        {
            get => _federatedAuthenticationInfoRequested &&
                   DateTime.FromFileTimeUtc(_fedAuthToken.expirationFileTime) < DateTime.UtcNow.AddSeconds(accessTokenExpirationBufferTime);
        }

        /// <summary>
        /// Get or set if the control ring send redirect token and feature ext ack with true for DNSCaching
        /// </summary>
        /// @TODO: Make auto-property
        internal bool IsDNSCachingBeforeRedirectSupported
        {
            get => _dnsCachingBeforeRedirect;
            set => _dnsCachingBeforeRedirect = value;
        }

        internal override bool IsLockedForBulkCopy
        {
            get => !_parser.MARSOn && _parser._physicalStateObj.BcpLock;
        }

        /// <summary>
        /// Get or set if SQLDNSCaching is supported by the server.
        /// </summary>
        // @TODO: Make auto-property
        internal bool IsSQLDNSCachingSupported
        {
            get => _serverSupportsDNSCaching;
            set => _serverSupportsDNSCaching = value;
        }

        /// <summary>
        /// Get or set if we need retrying with IP received from FeatureExtAck.
        /// </summary>
        // @TODO: Make auto-property
        internal bool IsSQLDNSRetryEnabled
        {
            get => _SQLDNSRetryEnabled;
            set => _SQLDNSRetryEnabled = value;
        }

        // @TODO: Make auto-property
        internal Guid OriginalClientConnectionId
        {
            get => _originalClientConnectionId;
        }

        // @TODO: Make auto-property
        internal int PacketSize
        {
            get => _currentPacketSize;
        }

        // @TODO: Make auto-property
        internal TdsParser Parser
        {
            get => _parser;
        }

        internal override SqlInternalTransaction PendingTransaction
        {
            get => _parser.PendingTransaction;
        }

        // @TODO: Make auto-property
        internal SqlConnectionPoolGroupProviderInfo PoolGroupProviderInfo
        {
            get => _poolGroupProviderInfo;
        }

        // @TODO: Make auto-property
        internal string RoutingDestination
        {
            get => _routingDestination;
        }

        internal RoutingInfo RoutingInfo { get; private set; } = null;

        internal int ServerProcessId
        {
            get => _parser._physicalStateObj._spid;
        }

        internal string ServerProvidedFailoverPartner { get; set; }

        // @TODO: Make auto-property
        internal SqlConnectionTimeoutErrorInternal TimeoutErrorInternal
        {
            get => _timeoutErrorInternal;
        }

        /// <summary>
        /// Indicates if the current thread claims to hold the parser lock.
        /// </summary>
        internal bool ThreadHasParserLockForClose
        {
            // @TODO: Replace with Environment.CurrentManagedThreadId in netcore
            get => _threadIdOwningParserLock == Thread.CurrentThread.ManagedThreadId;
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

        // @TODO: Rename to be "IsReadyToPrepareTransaction"
        protected override bool ReadyToPrepareTransaction
        {
            // TODO: probably need to use a different method but that's a different bug
            get => FindLiveReader(null) is null; // Can't prepare with a live data reader...
        }

        /// <summary>
        /// Get boolean that specifies whether an enlisted transaction can be unbound from the
        /// connection when that transaction completes. This override always returns <c>false</c>.
        /// </summary>
        /// <remarks>
        /// The SqlInternalConnectionTds.CheckEnlistedTransactionBinding method handles implicit
        /// unbinding for disposed transactions.
        /// </remarks>
        protected override bool UnbindOnTransactionCompletion
        {
            get => false;
        }

        /// <summary>
        /// Returns buffer time allowed before access token expiry to continue using the access token.
        /// </summary>
        // @TODO: Rename to match naming convention
        private int accessTokenExpirationBufferTime
        {
            get => ConnectionOptions.ConnectTimeout == ADP.InfiniteConnectionTimeout ||
                   ConnectionOptions.ConnectTimeout >= ADP.MaxBufferAccessTokenExpiry
                ? ADP.MaxBufferAccessTokenExpiry
                : ConnectionOptions.ConnectTimeout;
        }

        #endregion

        #region Public and Internal Methods

        internal void BreakConnection()
        {
            SqlClientEventSource.Log.TryTraceEvent(
                $"SqlInternalConnectionTds.BreakConnection | RES | CPOOL " +
                $"Object ID {ObjectID}, " +
                $"Breaking connection.");

            DoomThisConnection();   // Mark connection as unusable, so it will be destroyed
            Connection?.Close();
        }

        /// <summary>
        /// Validate the enlisted transaction state, taking into consideration the ambient
        /// transaction and transaction unbinding mode. If there is no enlisted transaction, this
        /// method is a no-op.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method must be called while holding a lock on the SqlInternalConnection instance,
        /// to ensure we don't accidentally execute after the transaction has completed on a
        /// different thread, causing us to unwittingly execute in auto-commit mode.
        /// </para>
        /// <para>
        /// When using Explicit transaction unbinding, verify that the enlisted transaction is
        /// active and equal to the current ambient transaction.
        /// </para>
        /// <para>
        /// When using Implicit transaction unbinding, verify that the enlisted transaction is
        /// active. If it is not active, and the transaction object has been disposed, unbind from
        /// the transaction. If it is not active and not disposed, throw an exception.
        /// </para>
        /// </remarks>
        internal void CheckEnlistedTransactionBinding()
        {
            // If we are enlisted in a transaction, check that transaction is active.
            // When using explicit transaction unbinding, also verify that the enlisted transaction
            // is the current transaction.
            Transaction enlistedTransaction = EnlistedTransaction;

            if (enlistedTransaction != null)
            {
                if (ConnectionOptions.TransactionBinding is SqlConnectionString.TransactionBindingEnum.ExplicitUnbind)
                {
                    Transaction currentTransaction = Transaction.Current;
                    if (enlistedTransaction.TransactionInformation.Status != TransactionStatus.Active || !enlistedTransaction.Equals(currentTransaction))
                    {
                        throw ADP.TransactionConnectionMismatch();
                    }
                }
                else
                {
                    // Implicit transaction unbind
                    if (enlistedTransaction.TransactionInformation.Status != TransactionStatus.Active)
                    {
                        if (EnlistedTransactionDisposed)
                        {
                            DetachTransaction(enlistedTransaction, isExplicitlyReleasing: true);
                        }
                        else
                        {
                            throw ADP.TransactionCompletedButNotDisposed();
                        }
                    }
                }
            }
        }

        internal void DecrementAsyncCount()
        {
            Debug.Assert(_asyncCommandCount > 0);
            Interlocked.Decrement(ref _asyncCommandCount);
        }

        internal override void DisconnectTransaction(SqlInternalTransaction internalTransaction) =>
            _parser?.DisconnectTransaction(internalTransaction);

        // @TODO: Make internal by making the SqlInternalConnection implementation internal
        public override void Dispose()
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                $"SqlInternalConnectionTds.Dispose | ADV | " +
                $"Object ID {ObjectID} disposing");

            try
            {
                // Guard against multiple concurrent dispose calls -- Delegated Transactions might
                // cause this.
                TdsParser parser = Interlocked.Exchange(ref _parser, null);

                Debug.Assert(parser is not null && _fConnectionOpen || parser is null && !_fConnectionOpen,
                    "Unexpected state on dispose");

                parser?.Disconnect();
            }
            finally
            {
                // Close will always close, even if exception is thrown.
                // Remember to null out any object references.
                _loginAck = null;

                // Mark internal connection as closed
                _fConnectionOpen = false;
            }

            base.Dispose();
        }

        internal override void ExecuteTransaction(
            TransactionRequest transactionRequest,
            string name,
            System.Data.IsolationLevel iso,
            SqlInternalTransaction internalTransaction,
            bool isDelegateControlRequest)
        {
            if (IsConnectionDoomed)
            {
                // Doomed means we can't do anything else...
                if (transactionRequest is TransactionRequest.Rollback or
                                          TransactionRequest.IfRollback)
                {
                    return;
                }

                throw SQL.ConnectionDoomed();
            }

            if (transactionRequest is TransactionRequest.Commit or
                                      TransactionRequest.Rollback or
                                      TransactionRequest.IfRollback)
            {
                if (!Parser.MARSOn && Parser._physicalStateObj.BcpLock)
                {
                    throw SQL.ConnectionLockedForBcpEvent();
                }
            }

            string transactionName = name ?? string.Empty;

            ExecuteTransaction2005(transactionRequest, transactionName, iso, internalTransaction, isDelegateControlRequest);
        }

        /// <summary>
        /// Called by SqlConnection.RepairConnection which is a relatively expensive way of repair
        /// inner connection prior to execution of request, used from EnlistTransaction,
        /// EnlistDistributedTransaction and ChangeDatabase.
        /// </summary>
        internal bool GetSessionAndReconnectIfNeeded(SqlConnection parent, int timeout = 0) // @TODO: Return value is never used
        {
            Debug.Assert(!ThreadHasParserLockForClose, "Cannot call this method if caller has parser lock");

            if (ThreadHasParserLockForClose)
            {
                // We cannot restore if we cannot release lock
                return false;
            }

            _parserLock.Wait(canReleaseFromAnyThread: false);

            // In case of error, let the connection know that we already own the parser lock
            ThreadHasParserLockForClose = true;
            bool releaseConnectionLock = true;

            try
            {
                Task reconnectTask = parent.ValidateAndReconnect(
                    () =>
                    {
                        ThreadHasParserLockForClose = false;
                        _parserLock.Release();
                        releaseConnectionLock = false;
                    },
                    timeout);

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

        internal void IncrementAsyncCount()
        {
            Interlocked.Increment(ref _asyncCommandCount);
        }

        internal override bool IsConnectionAlive(bool throwOnException) =>
            _parser._physicalStateObj.IsConnectionAlive(throwOnException);

        // @TODO: It seems fishy to have an entire environment change processor in here. Maybe either have specialized callbacks for specific scenarios, or maybe have the connection register with the parser what env changes it can handle? Idk, just seems a bit weird to do low-level stuff up here.
        internal void OnEnvChange(SqlEnvChange rec)
        {
            Debug.Assert(!IgnoreEnvChange, "This function should not be called if IgnoreEnvChange is set!");

            switch (rec._type)
            {
                case TdsEnums.ENV_DATABASE:
                    // If connection is not open and recovery is not in progress, store the server
                    // value as the original.
                    if (!_fConnectionOpen && _recoverySessionData == null)
                    {
                        _originalDatabase = rec._newValue;
                    }

                    CurrentDatabase = rec._newValue;
                    break;

                case TdsEnums.ENV_LANG:
                    // If connection is not open and recovery is not in progress, store the server
                    // value as the original.
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
                    // Only used on parser
                    // @TODO: Well ... why do they have cases here? Why are they in the middle of everything? Why aren't other skipped cases handled here??
                    break;

                case TdsEnums.ENV_LOGSHIPNODE:
                    #if NET
                    if (ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly)
                    {
                        throw SQL.ROR_FailoverNotSupportedServer(this);
                    }
                    #endif

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
                    // For now, we skip these 2005 only env change notifications
                    break;

                case TdsEnums.ENV_SPRESETCONNECTIONACK:
                    // Connection is being reset
                    _currentSessionData?.Reset();
                    break;

                case TdsEnums.ENV_USERINSTANCE:
                    _instanceName = rec._newValue;
                    break;

                case TdsEnums.ENV_ROUTING:
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnEnvChange | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received routing info");

                    if (string.IsNullOrEmpty(rec._newRoutingInfo.ServerName) ||
                        rec._newRoutingInfo.Protocol != 0 ||
                        rec._newRoutingInfo.Port == 0)
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

        // @TODO: This feature is *far* too big, and has the same issues as the above OnEnvChange
        // @TODO: Consider individual callbacks for the supported features and perhaps an interface of feature callbacks. Or registering with the parser what features are handleable.
        // @TODO: This class should not do low-level parsing of data from the server.
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

                    Debug.Assert(_currentSessionData._unrecoverableStatesCount == 0,
                        "Unrecoverable states count should be 0");

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
                            _currentSessionData._delta[stateId] = new SessionStateRecord
                            {
                                _data = stateData, _dataLength = len, _recoverable = true, _version = 0
                            };
                            _currentSessionData._deltaDirty = true;
                        }
                    }

                    break;
                }

                case TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for GlobalTransactions");

                    if (data.Length < 1)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown version number for GlobalTransactions");

                        throw SQL.ParsingError();
                    }

                    IsGlobalTransaction = true;
                    if (data[0] == 0x01)
                    {
                        IsGlobalTransactionsEnabledForServer = true;
                    }

                    break;
                }

                case TdsEnums.FEATUREEXT_FEDAUTH:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {0}, " +
                        $"Received feature extension acknowledgement for federated authentication");

                    if (!_federatedAuthenticationRequested)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Did not request federated authentication");

                        throw SQL.ParsingErrorFeatureId(ParsingErrorState.UnrequestedFeatureAckReceived, featureId);
                    }

                    Debug.Assert(_fedAuthFeatureExtensionData != null,
                        "_fedAuthFeatureExtensionData must not be null when _federatedAuthenticationRequested == true");

                    switch (_fedAuthFeatureExtensionData.libraryType)
                    {
                        case TdsEnums.FedAuthLibrary.MSAL:
                        case TdsEnums.FedAuthLibrary.SecurityToken:
                            // The server shouldn't have sent any additional data with the ack (like a nonce)
                            if (data.Length != 0)
                            {
                                SqlClientEventSource.Log.TryTraceEvent(
                                    $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                                    $"Object ID {ObjectID}, " +
                                    $"Federated authentication feature extension ack for MSAL and Security Token includes extra data");

                                throw SQL.ParsingError(ParsingErrorState.FedAuthFeatureAckContainsExtraData);
                            }

                            break;

                        default:
                            SqlClientEventSource.Log.TryTraceEvent(
                                $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                                $"Object ID {ObjectID}, " +
                                $"Attempting to use unknown federated authentication library");

                            Debug.Fail("Unknown _fedAuthLibrary type");
                            throw SQL.ParsingErrorLibraryType(
                                ParsingErrorState.FedAuthFeatureAckUnknownLibraryType,
                                (int)_fedAuthFeatureExtensionData.libraryType);
                    }

                    _federatedAuthenticationAcknowledged = true;

                    // If a new authentication context was used as part of this login attempt, try
                    // to update the new context in the cache, i.e.
                    // dbConnectionPool.AuthenticationContexts. ChooseAuthenticationContextToUpdate
                    // will take care that only the context which has more validity will remain in
                    // the cache, based on the Update logic.
                    if (_newDbConnectionPoolAuthenticationContext != null)
                    {
                        Debug.Assert(_dbConnectionPool != null,
                            "_dbConnectionPool should not be null when _newDbConnectionPoolAuthenticationContext != null.");

                        DbConnectionPoolAuthenticationContext newAuthenticationContextInCacheAfterAddOrUpdate =
                            _dbConnectionPool.AuthenticationContexts.AddOrUpdate(
                                _dbConnectionPoolAuthenticationContextKey,
                                _newDbConnectionPoolAuthenticationContext,
                                (_, oldValue) =>
                                    DbConnectionPoolAuthenticationContext.ChooseAuthenticationContextToUpdate(
                                        oldValue,
                                        _newDbConnectionPoolAuthenticationContext));

                        Debug.Assert(newAuthenticationContextInCacheAfterAddOrUpdate != null,
                            "newAuthenticationContextInCacheAfterAddOrUpdate should not be null.");

                        #if DEBUG
                        // For debug purposes, assert and trace if we ended up updating the cache
                        // with the new one or some other thread's context won the expiration race.
                        if (newAuthenticationContextInCacheAfterAddOrUpdate == _newDbConnectionPoolAuthenticationContext)
                        {
                            SqlClientEventSource.Log.TryTraceEvent(
                                $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                                $"Object ID {ObjectID}, " +
                                $"Updated the new dbAuthenticationContext in the _dbConnectionPool.AuthenticationContexts.");
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryTraceEvent(
                                $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                                $"Object ID {ObjectID }, " +
                                $"AddOrUpdate attempted on _dbConnectionPool.AuthenticationContexts, but it did not update the new value.",
                                ObjectID);
                        }
                        #endif
                    }

                    break;
                }
                case TdsEnums.FEATUREEXT_TCE:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for TCE");

                    if (data.Length < 1)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown version number for TCE");

                        throw SQL.ParsingError(ParsingErrorState.TceUnknownVersion);
                    }

                    byte supportedTceVersion = data[0];
                    if (supportedTceVersion == 0 || supportedTceVersion > TdsEnums.MAX_SUPPORTED_TCE_VERSION)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Invalid version number for TCE");

                        throw SQL.ParsingErrorValue(ParsingErrorState.TceInvalidVersion, supportedTceVersion);
                    }

                    _tceVersionSupported = supportedTceVersion;

                    Debug.Assert(_tceVersionSupported <= TdsEnums.MAX_SUPPORTED_TCE_VERSION,
                        "Client support TCE version 2");

                    _parser.IsColumnEncryptionSupported = true;
                    _parser.TceVersionSupported = _tceVersionSupported;
                    _parser.AreEnclaveRetriesSupported = _tceVersionSupported == 3;

                    if (data.Length > 1)
                    {
                        // Extract the type of enclave being used by the server.
                        _parser.EnclaveType = Encoding.Unicode.GetString(data, 2, data.Length - 2);
                    }
                    break;
                }
                case TdsEnums.FEATUREEXT_AZURESQLSUPPORT:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for AzureSQLSupport");

                    if (data.Length < 1)
                    {
                        throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                    }

                    IsAzureSqlConnection = true;

                    // Bit 0 for RO/FP support
                    // @TODO: Add a constant somewhere for that
                    if ((data[0] & 1) == 1 && SqlClientEventSource.Log.IsTraceEnabled())
                    {
                        SqlClientEventSource.Log.TryAdvancedTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                            $"Object ID {ObjectID}, " +
                            $"FailoverPartner enabled with Readonly intent for AzureSQL DB");
                    }

                    break;
                }
                case TdsEnums.FEATUREEXT_DATACLASSIFICATION:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for DATACLASSIFICATION");

                    if (data.Length < 1)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown token for DATACLASSIFICATION");

                        throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                    }

                    byte supportedDataClassificationVersion = data[0];
                    if (supportedDataClassificationVersion == 0 ||
                        supportedDataClassificationVersion > TdsEnums.DATA_CLASSIFICATION_VERSION_MAX_SUPPORTED)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Invalid version number for DATACLASSIFICATION");

                        throw SQL.ParsingErrorValue(
                            ParsingErrorState.DataClassificationInvalidVersion,
                            supportedDataClassificationVersion);
                    }

                    if (data.Length != 2)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown token for DATACLASSIFICATION");

                        throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                    }

                    byte enabled = data[1];
                    _parser.DataClassificationVersion = enabled == 0
                        ? TdsEnums.DATA_CLASSIFICATION_NOT_ENABLED
                        : supportedDataClassificationVersion;

                    break;
                }

                case TdsEnums.FEATUREEXT_UTF8SUPPORT:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for UTF8 support");

                    if (data.Length < 1)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown value for UTF8 support", ObjectID);

                        throw SQL.ParsingError();
                    }
                    break;
                }
                case TdsEnums.FEATUREEXT_SQLDNSCACHING:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for SQLDNSCACHING");

                    if (data.Length < 1)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown token for SQLDNSCACHING");

                        throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                    }

                    if (data[0] == 1)
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

                    // TODO: need to add more steps for phase 2
                    // get IPv4 + IPv6 + Port number
                    // not put them in the DNS cache at this point but need to store them somewhere
                    // generate pendingSQLDNSObject and turn on IsSQLDNSRetryEnabled flag
                    break;
                }
                case TdsEnums.FEATUREEXT_JSONSUPPORT:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for JSONSUPPORT");

                    if (data.Length != 1)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown token for JSONSUPPORT");
                        
                        throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                    }

                    byte jsonSupportVersion = data[0];
                    if (jsonSupportVersion == 0 || jsonSupportVersion > TdsEnums.MAX_SUPPORTED_JSON_VERSION)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Invalid version number for JSONSUPPORT");

                        throw SQL.ParsingError();
                    }

                    IsJsonSupportEnabled = true;
                    break;
                }
                case TdsEnums.FEATUREEXT_VECTORSUPPORT:
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for VECTORSUPPORT");

                    if (data.Length != 1)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Unknown token for VECTORSUPPORT");

                        throw SQL.ParsingError(ParsingErrorState.CorruptedTdsStream);
                    }

                    byte vectorSupportVersion = data[0];
                    if (vectorSupportVersion == 0 || vectorSupportVersion > TdsEnums.MAX_SUPPORTED_VECTOR_VERSION)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFeatureExtAck | ERR | " +
                            $"Object ID {ObjectID}, " +
                            $"Invalid version number {vectorSupportVersion} for VECTORSUPPORT, " +
                            $"Max supported version is {TdsEnums.MAX_SUPPORTED_VECTOR_VERSION}");

                        throw SQL.ParsingError();
                    }

                    IsVectorSupportEnabled = true;

                    break;
                }
                case TdsEnums.FEATUREEXT_USERAGENT:
                {
                    // Unexpected ack from server but we ignore it entirely
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFeatureExtAck | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"Received feature extension acknowledgement for USERAGENTSUPPORT (ignored)");

                    break;
                }
                default:
                {
                    // Unknown feature ack
                    throw SQL.ParsingError();
                }
            }
        }

        /// <summary>
        /// Generates (if appropriate) and sends a Federated Authentication Access token to the
        /// server, using the Federated Authentication Info.
        /// </summary>
        /// <param name="fedAuthInfo">Federated Authentication Info.</param>
        internal void OnFedAuthInfo(SqlFedAuthInfo fedAuthInfo)
        {
            // @TODO: Seriously, put this into a hash set or give it a helper or something! We're gonna forget one in *one* spot and cause a big ol bug someday.
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
            Debug.Assert(_dbConnectionPoolAuthenticationContextKey == null,
                "_dbConnectionPoolAuthenticationContextKey should be null.");
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlInternalConnectionTds.OnFedAuthInfo> {0}, Generating federated authentication token", ObjectID);
            DbConnectionPoolAuthenticationContext dbConnectionPoolAuthenticationContext = null;

            // We want to refresh the token without taking a lock on the context, allowed when the
            // access token is expiring within the next 10 mins.
            bool attemptRefreshTokenUnLocked = false;

            // We want to refresh the token, if taking the lock on the authentication context is
            // successful.
            bool attemptRefreshTokenLocked = false;

            if (_dbConnectionPool != null)
            {
                Debug.Assert(_dbConnectionPool.AuthenticationContexts != null);

                // Construct the dbAuthenticationContextKey with information from FedAuthInfo and
                // store for later use, when inserting in to the token cache.
                _dbConnectionPoolAuthenticationContextKey = new DbConnectionPoolAuthenticationContextKey(
                    fedAuthInfo.stsurl,
                    fedAuthInfo.spn);

                // Try to retrieve the authentication context from the pool, if one does exist for
                // this key.
                if (_dbConnectionPool.AuthenticationContexts.TryGetValue(_dbConnectionPoolAuthenticationContextKey, out dbConnectionPoolAuthenticationContext))
                {
                    Debug.Assert(dbConnectionPoolAuthenticationContext != null,
                        "dbConnectionPoolAuthenticationContext should not be null.");

                    // The timespan between UTCNow and the token expiry.
                    TimeSpan contextValidity =
                        dbConnectionPoolAuthenticationContext.ExpirationTime.Subtract(DateTime.UtcNow);


                    if (contextValidity <= _dbAuthenticationContextUnLockedRefreshTimeSpan)
                    {
                        // If the authentication context is expiring within next 10 minutes, lets
                        // just re-create a token for this connection attempt. And on successful
                        // login, try to update the cache with the new token.
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.OnFedAuthInfo | " +
                            $"Object ID {ObjectID}, " +
                            $"The expiration time is less than 10 mins, trying to get new access " +
                            $"token regardless of if an other thread is also trying to update it. " +
                            $"The expiration time is {dbConnectionPoolAuthenticationContext.ExpirationTime:T}. " +
                            $"Current Time is {DateTime.UtcNow:T}.");
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
                        attemptRefreshTokenLocked = TryGetFedAuthTokenLocked(
                            fedAuthInfo,
                            dbConnectionPoolAuthenticationContext,
                            out _fedAuthToken);
                    }
                    #endif
                    else if (contextValidity <= _dbAuthenticationContextLockedRefreshTimeSpan)
                    {
                        // If the token is expiring within the next 45 mins, try to fetch a new
                        // token, if there is no thread already doing it. If a thread is already
                        // doing the refresh, just use the existing token in the cache and proceed.
                        SqlClientEventSource.Log.TryAdvancedTraceEvent(
                            $"SqlInternalConnectionTds.OnFedAuthInfo | ADV | " +
                            $"Object ID {ObjectID}, " +
                            $"The authentication context needs a refresh. " +
                            $"The expiration time is {dbConnectionPoolAuthenticationContext.ExpirationTime:T}. " +
                            $"Current Time is {DateTime.UtcNow:T}.");

                        // Call the function which tries to acquire a lock over the authentication
                        // context before trying to update. If the lock could not be obtained, it
                        // will return false, without attempting to fetch a new token.
                        attemptRefreshTokenLocked = TryGetFedAuthTokenLocked(
                            fedAuthInfo,
                            dbConnectionPoolAuthenticationContext,
                            out _fedAuthToken);

                        // If TryGetFedAuthTokenLocked returns true, it means lock was obtained and
                        // _fedAuthToken should not be null. If there was an exception in
                        // retrieving the new token, TryGetFedAuthTokenLocked should have thrown,
                        // so we won't be here.
                        Debug.Assert(!attemptRefreshTokenLocked || _fedAuthToken != null,
                            "Either Lock should not have been obtained or _fedAuthToken should not be null.");
                        Debug.Assert(!attemptRefreshTokenLocked || _newDbConnectionPoolAuthenticationContext != null,
                            "Either Lock should not have been obtained or _newDbConnectionPoolAuthenticationContext should not be null.");

                        // Indicate in EventSource Trace that we are successful with the update.
                        if (attemptRefreshTokenLocked)
                        {
                            SqlClientEventSource.Log.TryTraceEvent(
                                $"SqlInternalConnectionTds.OnFedAuthInfo | " +
                                $"Object ID {ObjectID}, " +
                                $"The attempt to get a new access token succeeded under the locked mode.");
                        }
                    }

                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.OnFedAuthInfo | " +
                        $"Object ID {ObjectID}, " +
                        $"Found an authentication context in the cache that does not need a refresh at this time. " +
                        $"Re-using the cached token.");
                }
            }

            // dbConnectionPoolAuthenticationContext will be null if either this is the first
            // connection attempt in the pool or pooling is disabled.
            if (dbConnectionPoolAuthenticationContext == null || attemptRefreshTokenUnLocked)
            {
                // Get the Federated Authentication Token.
                _fedAuthToken = GetFedAuthToken(fedAuthInfo);
                Debug.Assert(_fedAuthToken != null, "_fedAuthToken should not be null.");

                if (_dbConnectionPool != null)
                {
                    // GetFedAuthToken should have updated _newDbConnectionPoolAuthenticationContext.
                    Debug.Assert(_newDbConnectionPoolAuthenticationContext != null,
                        "_newDbConnectionPoolAuthenticationContext should not be null.");

                    if (_newDbConnectionPoolAuthenticationContext != null)
                    {
                        _dbConnectionPool.AuthenticationContexts.TryAdd(
                            _dbConnectionPoolAuthenticationContextKey,
                            _newDbConnectionPoolAuthenticationContext);
                    }
                }
            }
            else if (!attemptRefreshTokenLocked)
            {
                Debug.Assert(dbConnectionPoolAuthenticationContext != null,
                    "dbConnectionPoolAuthenticationContext should not be null.");
                Debug.Assert(_fedAuthToken == null, "_fedAuthToken should be null in this case.");
                Debug.Assert(_newDbConnectionPoolAuthenticationContext == null,
                    "_newDbConnectionPoolAuthenticationContext should be null.");

                // If the code flow is here, then we are re-using the context from the cache for
                // this connection attempt and not generating a new access token on this thread.
                _fedAuthToken = new SqlFedAuthToken
                {
                    accessToken = dbConnectionPoolAuthenticationContext.AccessToken,
                    expirationFileTime = dbConnectionPoolAuthenticationContext.ExpirationTime.ToFileTime()
                };
            }

            Debug.Assert(_fedAuthToken?.accessToken != null,
                "_fedAuthToken and _fedAuthToken.accessToken cannot be null.");

            _parser.SendFedAuthToken(_fedAuthToken);
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

        internal override bool TryReplaceConnection(
            DbConnection outerConnection,
            SqlConnectionFactory connectionFactory,
            TaskCompletionSource<DbConnectionInternal> retry,
            DbConnectionOptions userOptions)
        {
            return TryOpenConnectionInternal(outerConnection, connectionFactory, retry, userOptions);
        }

        internal override void ValidateConnectionForExecute(SqlCommand command)
        {
            TdsParser parser = _parser;
            if (parser == null || parser.State is TdsParserState.Broken or TdsParserState.Closed)
            {
                throw ADP.ClosedConnectionError();
            }
            else
            {
                SqlDataReader reader = null;
                if (parser.MARSOn)
                {
                    if (command != null)
                    {
                        // Command can't have datareader already associated with it
                        reader = FindLiveReader(command);
                    }
                }
                else
                {
                    // Single execution/datareader per connection
                    if (_asyncCommandCount > 0)
                    {
                        throw SQL.MARSUnsupportedOnConnection();
                    }

                    reader = FindLiveReader(null);
                }

                if (reader != null)
                {
                    // If MARS is on, then a datareader associated with the command exists or if
                    // MARS is off, then a datareader exists
                    throw ADP.OpenReaderExists(parser.MARSOn); // MDAC 66411
                }

                if (!parser.MARSOn && parser._physicalStateObj.HasPendingData)
                {
                    parser.DrainData(parser._physicalStateObj);
                }

                Debug.Assert(!parser._physicalStateObj.HasPendingData,
                    "Should not have a busy physicalStateObject at this point!");

                parser.RollbackOrphanedAPITransactions();
            }
        }

        #endregion

        #region Protected Methods

        protected override void Activate(Transaction transaction)
        {
            #if NETFRAMEWORK
            // Demand for unspecified failover pooled connections
            FailoverPermissionDemand();
            #endif

            // When we're required to automatically enlist in transactions and there is one we
            // enlist in it. On the other hand, if there isn't a transaction, and we are
            // currently enlisted in one, then we un-enlist from it.
            // Regardless of whether we're required to automatically enlist, when there is not a
            // current transaction, we cannot leave the connection enlisted in a transaction.
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

        // @TODO: Is this suppression still required
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")] // copied from Triaged.cs
        protected override void ChangeDatabaseInternal(string database)
        {
            // MDAC 73598 - add brackets around database
            database = SqlConnection.FixupDatabaseTransactionName(database); // @TODO: Should go to a utility method
            Task executeTask = _parser.TdsExecuteSQLBatch(
                $@"USE {database}",
                ConnectionOptions.ConnectTimeout,
                notificationRequest: null,
                _parser._physicalStateObj,
                sync: true);

            Debug.Assert(executeTask == null, "Shouldn't get a task when doing sync writes");

            _parser.Run(
                RunBehavior.UntilDone,
                cmdHandler: null,
                dataStream: null,
                bulkCopyHandler: null,
                _parser._physicalStateObj);
        }

        // @TODO: Rename to match guidelines
        protected override byte[] GetDTCAddress()
        {
            byte[] dtcAddress = _parser.GetDTCAddress(ConnectionOptions.ConnectTimeout, _parser.GetSession(this));

            Debug.Assert(dtcAddress != null, "null dtcAddress?");
            return dtcAddress;
        }

        protected override void InternalDeactivate()
        {
            // When we're deactivated, the user must have called End on all the async commands, or
            // we don't know that we're in a state that we can recover from. We doom the connection
            // in this case, to prevent odd cases when we go to the wire.
            if (_asyncCommandCount != 0)
            {
                DoomThisConnection();
            }

            // If we're deactivating with a delegated transaction, we should not be cleaning up the
            // parser just yet, that will cause our transaction to be rolled back and the
            // connection to be reset.  We'll get called again once the delegated transaction is
            // completed, and we can do it all then.
            if (!(IsTransactionRoot && Pool == null))
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

        protected override bool ObtainAdditionalLocksForClose()
        {
            bool obtainParserLock = !ThreadHasParserLockForClose;

            Debug.Assert(obtainParserLock || _parserLock.ThreadMayHaveLock(),
                "Thread claims to have lock, but lock is not taken");

            if (obtainParserLock)
            {
                _parserLock.Wait(canReleaseFromAnyThread: false);
                ThreadHasParserLockForClose = true;
            }

            return obtainParserLock;
        }

        protected override void PropagateTransactionCookie(byte[] cookie)
        {
            _parser.PropagateDistributedTransaction(
                cookie,
                ConnectionOptions.ConnectTimeout,
                _parser._physicalStateObj);
        }

        protected override void ReleaseAdditionalLocksForClose(bool lockToken)
        {
            if (lockToken)
            {
                ThreadHasParserLockForClose = false;
                _parserLock.Release();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Common code path for making one attempt to establish a connection and log in to server.
        /// </summary>
        // @TODO: This is gross - there is no good way to #if a multi-line method signature. Introduce a record/struct type the different values.
        private void AttemptOneLogin(
            ServerInfo serverInfo,
            string newPassword,
            SecureString newSecurePassword,
            TimeoutTimer timeout,
            bool withFailover = false

            #if NETFRAMEWORK
            ,
            bool isFirstTransparentAttempt = true,
            bool disableTnir = false
            #endif
        )
        {
            SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SqlInternalConnectionTds.AttemptOneLogin|ADV> {0}, timeout={1}[msec], server={2}", ObjectID, timeout.MillisecondsRemaining, serverInfo.ExtendedServerName);
            RoutingInfo = null; // forget routing information

            _parser._physicalStateObj.SniContext = SniContext.Snix_Connect;

            #if NETFRAMEWORK
            _parser.Connect(
                serverInfo,
                this,
                timeout,
                ConnectionOptions,
                withFailover,
                isFirstTransparentAttempt,
                disableTnir);
            #else
            _parser.Connect(
                serverInfo,
                this,
                timeout,
                ConnectionOptions,
                withFailover);
            #endif

            _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.ConsumePreLoginHandshake);
            _timeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.LoginBegin);

            _parser._physicalStateObj.SniContext = SniContext.Snix_Login;
            Login(serverInfo, timeout, newPassword, newSecurePassword, ConnectionOptions.Encrypt);

            _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.ProcessConnectionAuth);
            _timeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.PostLogin);

            CompleteLogin(!ConnectionOptions.Pooling);

            _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.PostLogin);
        }

        /// <summary>
        /// With possible MFA support in all AD auth providers, the duration for acquiring a token
        /// can be unpredictable. If a timeout error (client or server) happened, we silently retry
        /// if a cached token exists from a previous auth attempt (see GetFedAuthToken).
        /// </summary>
        // @TODO: Rename to meet naming conventions
        private bool AttemptRetryADAuthWithTimeoutError(
            SqlException sqlex,
            SqlConnectionString connectionOptions, // @TODO: this is not used
            TimeoutTimer timeout)
        {
            if (!_activeDirectoryAuthTimeoutRetryHelper.CanRetryWithSqlException(sqlex))
            {
                return false;
            }
            // Reset client-side timeout.
            timeout.Reset();

            // When server timeout, the auth context key was already created. Clean it up here.
            _dbConnectionPoolAuthenticationContextKey = null;

            // When server timeouts, connection is doomed. Reset here to allow reconnection.
            UnDoomThisConnection();

            // Change retry state so it only retries once for timeout error.
            _activeDirectoryAuthTimeoutRetryHelper.State = ActiveDirectoryAuthenticationTimeoutRetryState.Retrying;
            return true;
        }

        private void CompleteLogin(bool enlistOK) // @TODO: Rename as per guidelines
        {
            _parser.Run(
                RunBehavior.UntilDone,
                cmdHandler: null,
                dataStream: null,
                bulkCopyHandler: null,
                _parser._physicalStateObj);

            if (RoutingInfo == null)
            {
                // ROR should not affect state of connection recovery
                if (_federatedAuthenticationRequested && !_federatedAuthenticationAcknowledged)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"SqlInternalConnectionTds.CompleteLogin | ERR | " +
                        $"Object ID {ObjectID}, " +
                        $"Server did not acknowledge the federated authentication request");
                    throw SQL.ParsingError(ParsingErrorState.FedAuthNotAcknowledged);
                }

                if (_federatedAuthenticationInfoRequested && !_federatedAuthenticationInfoReceived)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"SqlInternalConnectionTds.CompleteLogin | ERR | " +
                        $"Object ID {ObjectID}, " +
                        $"Server never sent the requested federated authentication info");
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

            Debug.Assert(SniContext.Snix_Login == Parser._physicalStateObj.SniContext,
                $"SniContext should be Snix_Login; actual Value: {Parser._physicalStateObj.SniContext}");

            _parser._physicalStateObj.SniContext = SniContext.Snix_EnableMars;
            _parser.EnableMars();

            // Mark connection as open
            _fConnectionOpen = true;
            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                "SqlInternalConnectionTds.CompleteLogin | ADV | Post-Login Phase: Server connection obtained.");

            // For non-pooled connections, enlist in a distributed transaction if present - and
            // user specified to enlist
            if (enlistOK && ConnectionOptions.Enlist && RoutingInfo == null)
            {
                _parser._physicalStateObj.SniContext = SniContext.Snix_AutoEnlist;
                Transaction tx = ADP.GetCurrentTransaction();
                Enlist(tx);
            }

            _parser._physicalStateObj.SniContext = SniContext.Snix_Login;
        }

        // @TODO: Rename to ExecuteTransactionInternal ... we don't have multiple server version implementations of this
        private void ExecuteTransaction2005(
            TransactionRequest transactionRequest,
            string transactionName,
            System.Data.IsolationLevel iso, // @TODO: We have three different IsolationTypes, maybe we can indicate which one is which a bit better.
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

            Debug.Assert(!ThreadHasParserLockForClose || _parserLock.ThreadMayHaveLock(),
                "Thread claims to have parser lock, but lock is not taken");

            if (!ThreadHasParserLockForClose)
            {
                _parserLock.Wait(canReleaseFromAnyThread: false);

                // In case of error, let the connection know that we already own the parser lock
                ThreadHasParserLockForClose = true;
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
                        // Map IfRollback to Rollback since with 2005 and beyond we should never
                        // need the "if" since the server will inform us when transactions have
                        // completed as a result of an error on the server.
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
                    Task reconnectTask = internalTransaction.Parent.Connection.ValidateAndReconnect(
                        () =>
                        {
                            ThreadHasParserLockForClose = false;
                            _parserLock.Release();
                            releaseConnectionLock = false;
                        },
                        ADP.InfiniteConnectionTimeout);

                    if (reconnectTask != null)
                    {
                        // There is no specific timeout for BeginTransaction, uses ConnectTimeout
                        AsyncHelper.WaitForCompletion(reconnectTask, ADP.InfiniteConnectionTimeout);
                        internalTransaction.ConnectionHasBeenRestored = true;
                        return;
                    }
                }

                // Promote, Commit and Rollback requests for delegated transactions often happen
                // while there is an open result set, so we need to handle them by using a
                // different MARS session. Otherwise, we'll write on the physical state objects
                // while someone else is using it. When we don't have MARS enabled, we need to lock
                // the physical state object to synchronize its use, at least until we increment
                // the open results count. Once it's been incremented the delegated transaction
                // requests will fail, so they won't stomp on anything.

                // We need to keep this lock through the duration of the TM request so that we
                // won't hijack a different request's data stream and a different request won't
                // hijack ours, so we have a lock here on an object that the ExecTMReq will also
                // lock, but since we're on the same thread, the lock is a no-op.

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

                // _parser may be nulled out during TdsExecuteTransactionManagerRequest. Only use
                // local variable after this call.
                _parser.TdsExecuteTransactionManagerRequest(
                    buffer: null,
                    requestType,
                    transactionName,
                    isoLevel,
                    ConnectionOptions.ConnectTimeout,
                    internalTransaction,
                    stateObj,
                    isDelegateControlRequest);
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

        #if NETFRAMEWORK
        private void FailoverPermissionDemand() =>
            PoolGroupProviderInfo?.FailoverPermissionDemand();
        #endif

        /// <summary>
        /// Get the Federated Authentication Token.
        /// </summary>
        /// <param name="fedAuthInfo">Information obtained from server as Federated Authentication Info.</param>
        private SqlFedAuthToken GetFedAuthToken(SqlFedAuthInfo fedAuthInfo)
        {
            Debug.Assert(fedAuthInfo != null, "fedAuthInfo should not be null.");

            // Number of milliseconds to sleep for the initial back off.
            int sleepInterval = 100;

            // Number of attempts, for tracing purposes, if we underwent retries.
            int numberOfAttempts = 0;

            // Object that will be returned to the caller, containing all required data about the token.
            _fedAuthToken = new SqlFedAuthToken();

            // Username to use in error messages.
            string username = null;

            SqlAuthenticationProvider authProvider =
                SqlAuthenticationProvider.GetProvider(ConnectionOptions.Authentication);

            if (authProvider == null && _accessTokenCallback == null)
            {
                throw SQL.CannotFindAuthProvider(ConnectionOptions.Authentication.ToString());
            }

            // Retry getting access token once if MsalException.error_code is unknown_error.
            // extra logic to deal with HTTP 429 (Retry after).
            // @TODO: Can we pick one or the other?
            // @TODO: Wait ... are we counting up but only looping while the number of attempts is <=1 ? Huh?
            // @TODO: Can we consider using a for loop here since there's a fixed number of times to loop

            #if NET
            while (numberOfAttempts <= 1)
            #else
            while (numberOfAttempts <= 1 && sleepInterval <= _timeout.MillisecondsRemaining)
            #endif
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
                            #if NET
                            // In some scenarios for .NET Core, MSAL cannot detect the current user and needs it passed in
                            // for Integrated auth. Allow the user/application to pass it in to work around those scenarios.
                            if (!string.IsNullOrEmpty(ConnectionOptions.UserID))
                            {
                                username = ConnectionOptions.UserID;
                                authParamsBuilder.WithUserId(username);
                            }
                            else
                            {
                                username = TdsEnums.NTAUTHORITYANONYMOUSLOGON;
                            }
                            #else
                            username = TdsEnums.NTAUTHORITYANONYMOUSLOGON;
                            #endif

                            if (_activeDirectoryAuthTimeoutRetryHelper.State == ActiveDirectoryAuthenticationTimeoutRetryState.Retrying)
                            {
                                _fedAuthToken = _activeDirectoryAuthTimeoutRetryHelper.CachedToken;
                            }
                            else
                            {
                                // We use Task.Run here in all places to execute task synchronously
                                // in the same context. Fixes block-over-async deadlock possibilities
                                // https://github.com/dotnet/SqlClient/issues/1209
                                // @TODO: Verify that the wrapping/unwrapping is necessary.
                                _fedAuthToken = Task.Run(async () =>
                                        await authProvider.AcquireTokenAsync(authParamsBuilder))
                                            .GetAwaiter()
                                            .GetResult()
                                            .ToSqlFedAuthToken();
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
                                    _fedAuthToken = Task.Run(async () =>
                                        await authProvider.AcquireTokenAsync(authParamsBuilder))
                                            .GetAwaiter()
                                            .GetResult()
                                            .ToSqlFedAuthToken();
                                }
                                else
                                {
                                    username = ConnectionOptions.UserID;
                                    authParamsBuilder.WithUserId(username).WithPassword(ConnectionOptions.Password);
                                    _fedAuthToken = Task.Run(async () =>
                                        await authProvider.AcquireTokenAsync(authParamsBuilder))
                                            .GetAwaiter()
                                            .GetResult()
                                            .ToSqlFedAuthToken();
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

                                // Use Connection timeout value to cancel token acquire request
                                // after certain period of time.(int)
                                if (_timeout.MillisecondsRemaining < int.MaxValue)
                                {
                                    cts.CancelAfter((int)_timeout.MillisecondsRemaining);
                                }

                                _fedAuthToken = Task.Run(async () =>
                                    await _accessTokenCallback(parameters, cts.Token))
                                        .GetAwaiter()
                                        .GetResult()
                                        .ToSqlFedAuthToken();
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
                catch (MsalServiceException serviceException)
                {
                    // Deal with Msal service exceptions first, retry if 429 received.
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

                        // if there's enough time to retry before timeout, then retry, otherwise
                        // break out the retry loop.
                        if (sleepInterval < _timeout.MillisecondsRemaining)
                        {
                            Thread.Sleep(sleepInterval);
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryTraceEvent(
                                $"SqlInternalConnectionTds.GetFedAuthToken.MsalServiceException | ERR | " +
                                $"Timeout: {serviceException.ErrorCode}");

                            throw SQL.ActiveDirectoryTokenRetrievingTimeout(
                                Enum.GetName(typeof(SqlAuthenticationMethod), ConnectionOptions.Authentication),
                                serviceException.ErrorCode,
                                serviceException);
                        }
                    }
                    else
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.GetFedAuthToken.MsalServiceException | ERR | " +
                            $"{serviceException.ErrorCode}");

                        throw ADP.CreateSqlException(serviceException, ConnectionOptions, this, username);
                    }
                }
                catch (MsalException msalException)
                {
                    // Deal with normal MsalExceptions.
                    if (MsalError.UnknownError != msalException.ErrorCode ||
                        _timeout.IsExpired ||
                        _timeout.MillisecondsRemaining <= sleepInterval)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.GetFedAuthToken.MSALException | ERR | " +
                            $"{msalException.ErrorCode}");

                        throw ADP.CreateSqlException(msalException, ConnectionOptions, this, username);
                    }

                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.GetFedAuthToken | ADV | " +
                        $"Object ID: {ObjectID}, " +
                        $"sleeping {sleepInterval}ms");
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.GetFedAuthToken | ADV | " +
                        $"Object ID: {ObjectID}, " +
                        $"remaining {_timeout.MillisecondsRemaining}ms");

                    Thread.Sleep(sleepInterval);

                    sleepInterval *= 2;
                }
                // All other exceptions from MSAL/Azure Identity APIs
                catch (Exception e)
                {
                    SqlError error = new(
                        infoNumber: 0,
                        errorState: 0x00,
                        errorClass: TdsEnums.FATAL_ERROR_CLASS,
                        server: ConnectionOptions.DataSource,
                        errorMessage: e.Message,
                        procedure: ActiveDirectoryAuthentication.MSALGetAccessTokenFunctionName,
                        lineNumber: 0);

                    throw SqlException.CreateException(
                        error,
                        serverVersion: string.Empty,
                        internalConnection: this,
                        innerException: e);
                }
            }

            Debug.Assert(_fedAuthToken != null, "fedAuthToken should not be null.");
            Debug.Assert(_fedAuthToken.accessToken?.Length > 0,
                "fedAuthToken.accessToken should not be null or empty.");

            // Store the newly generated token in _newDbConnectionPoolAuthenticationContext, only if using pooling.
            if (_dbConnectionPool != null)
            {
                DateTime expirationTime = DateTime.FromFileTimeUtc(_fedAuthToken.expirationFileTime);
                _newDbConnectionPoolAuthenticationContext = new DbConnectionPoolAuthenticationContext(
                    _fedAuthToken.accessToken,
                    expirationTime);
            }

            SqlClientEventSource.Log.TryTraceEvent(
                $"SqlInternalConnectionTds.GetFedAuthToken | " +
                $"Object ID {ObjectID}, " +
                $"Finished generating federated authentication token.");

            return _fedAuthToken;
        }

        private void Login(
            ServerInfo server,
            TimeoutTimer timeout,
            string newPassword,
            SecureString newSecurePassword,
            SqlConnectionEncryptOption encrypt)
        {
            // create a new login record
            SqlLogin login = new SqlLogin();

            // Gather all the settings the user set in the connection string or properties and do
            // the login
            CurrentDatabase = server.ResolvedDatabaseName;
            _currentPacketSize = ConnectionOptions.PacketSize;
            _currentLanguage = ConnectionOptions.CurrentLanguage;

            int timeoutInSeconds = 0;

            // If a timeout tick value is specified, compute the timeout based upon the amount of
            // time left in seconds.
            if (!timeout.IsInfinite)
            {
                long t = timeout.MillisecondsRemaining / 1000;
                if (t == 0 && LocalAppContextSwitches.UseMinimumLoginTimeout)
                {
                    // Take 1 as the minimum value, since 0 is treated as an infinite timeout to
                    // allow 1 second more for login to complete, since it should take only a few
                    // milliseconds.
                    t = 1;
                }

                if (int.MaxValue > t)
                {
                    timeoutInSeconds = (int)t;
                }
            }

            // @TODO: How about we define all the easy ones in one block, then have the conditional ones below that
            login.authentication = ConnectionOptions.Authentication;
            login.timeout = timeoutInSeconds;
            login.userInstance = ConnectionOptions.UserInstance;
            login.hostName = ConnectionOptions.ObtainWorkstationId();
            login.userName = ConnectionOptions.UserID;
            login.password = ConnectionOptions.Password;
            login.applicationName = ConnectionOptions.ApplicationName;
            login.language = _currentLanguage;

            if (!login.userInstance)
            {
                // Do not send attachdbfilename or database to SSE primary instance
                login.database = CurrentDatabase;
                login.attachDBFilename = ConnectionOptions.AttachDBFilename;
            }

            // Ensure ServerName is sent during TdsLogin to enable SQL Azure connectivity.
            // Using server.UserServerName (versus ConnectionOptions.DataSource) since TdsLogin
            // requires serverName to always be non-null.
            login.serverName = server.UserServerName;

            login.useReplication = ConnectionOptions.Replication;

            // Treat AD Integrated like Windows integrated when against a non-FedAuth endpoint
            login.useSSPI = ConnectionOptions.IntegratedSecurity || (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && !_fedAuthRequired);

            login.packetSize = _currentPacketSize;
            login.newPassword = newPassword;
            login.readOnlyIntent = ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly;
            login.credential = _credential;

            if (newSecurePassword != null)
            {
                // @TODO: Isn't this already null?
                login.newSecurePassword = newSecurePassword;
            }

            TdsEnums.FeatureExtension requestedFeatures = TdsEnums.FeatureExtension.None;
            if (ConnectionOptions.ConnectRetryCount > 0)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.SessionRecovery;
                _sessionRecoveryRequested = true;
            }

            // If the workflow being used is Active Directory Authentication and server's prelogin
            // response for FEDAUTHREQUIRED option indicates Federated Authentication is required,
            // we have to insert FedAuth Feature Extension in Login7, indicating the intent to use
            // Active Directory Authentication for SQL Server.
            // @TODO: Rewrite using a static readonly hash set of federated auth types
            #pragma warning disable 0618 // Type or member is obsolete
            if (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword
            #pragma warning restore 0618 // Type or member is obsolete
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
                || ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
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

                // No need any further info from the server for token based authentication. So set
                // _federatedAuthenticationRequested to true
                _federatedAuthenticationRequested = true;
            }

            // The GLOBALTRANSACTIONS, DATACLASSIFICATION, TCE, and UTF8 support features are implicitly requested
            requestedFeatures |= TdsEnums.FeatureExtension.GlobalTransactions |
                                 TdsEnums.FeatureExtension.DataClassification |
                                 TdsEnums.FeatureExtension.Tce |
                                 TdsEnums.FeatureExtension.UTF8Support;

            // The AzureSQLSupport feature is implicitly set for ReadOnly login
            if (ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly)
            {
                requestedFeatures |= TdsEnums.FeatureExtension.AzureSQLSupport;
            }

            // The following features are implicitly set
            // @TODO: Request all the implicit features in one place (probably at the very top)
            requestedFeatures |= TdsEnums.FeatureExtension.SQLDNSCaching;
            requestedFeatures |= TdsEnums.FeatureExtension.JsonSupport;
            requestedFeatures |= TdsEnums.FeatureExtension.VectorSupport;
            requestedFeatures |= TdsEnums.FeatureExtension.UserAgent;

            _parser.TdsLogin(login, requestedFeatures, _recoverySessionData, _fedAuthFeatureExtensionData, encrypt);
        }

        private void LoginFailure()
        {
            SqlClientEventSource.Log.TryTraceEvent(
                $"SqlInternalConnectionTds.LoginFailure | RES | CPOOL | " +
                $"Object ID {ObjectID}");

            // If the parser was allocated, and we failed, then we must have failed on either the
            // Connect or Login, either way we should call Disconnect. Disconnect can be called if
            // the connection is already closed - becomes no-op, so no issues there.
            _parser?.Disconnect();
        }

        /// <summary>
        /// Attempt to log in to a host that does not have a failover partner.
        /// </summary>
        /// <remarks>
        /// Will repeatedly attempt to connect, but back off between each attempt so as not to clog
        /// the network. Back off period increases for first few failures: 100ms, 200ms, 400ms,
        /// 800ms, then 1000ms for subsequent attempts.
        ///
        /// The logic in this method is paralleled by the logic in LoginWithFailover. Changes to
        /// either one should be examined to see if they need to be reflected in the other.
        /// </remarks>
        // @TODO: If the code is parallel with LoginWithFailover ... surely there's a way to factor it out a bit
        private void LoginNoFailover(
            ServerInfo serverInfo,
            string newPassword,
            SecureString newSecurePassword,
            bool redirectedUserInstance,
            SqlConnectionString connectionOptions,
            SqlCredential credential,
            TimeoutTimer timeout)
        {
            Debug.Assert(ReferenceEquals(connectionOptions, ConnectionOptions),
                "ConnectionOptions argument and property must be the same");

            int routingAttempts = 0;

            // ServerInfo may end up pointing to new object due to routing, original object is used
            // to set CurrentDatasource
            ServerInfo originalServerInfo = serverInfo;

            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                $"SqlInternalConnectionTds.LoginNoFailover | ADV | " +
                $"Object ID {ObjectID}, " +
                $"Host={serverInfo.UserServerName}");

            // Milliseconds to sleep (back off) between attempts.
            int sleepInterval = 100;

            ResolveExtendedServerName(serverInfo, !redirectedUserInstance, connectionOptions);

            #if NET
            bool isParallel = connectionOptions.MultiSubnetFailover;
            #else
            bool disableTnir = ShouldDisableTnir(connectionOptions);
            bool isParallel = connectionOptions.MultiSubnetFailover ||
                              (connectionOptions.TransparentNetworkIPResolution && !disableTnir);
            #endif

            long timeoutUnitInterval = 0;
            if (isParallel)
            {
                // @TODO: Can we just use an int or a timespan or something? This math is annoying.
                #if NET
                float failoverTimeoutStep = ADP.FailoverTimeoutStep;
                #else
                float failoverTimeoutStep = connectionOptions.MultiSubnetFailover
                    ? ADP.FailoverTimeoutStep
                    : ADP.FailoverTimeoutStepForTnir;
                #endif

                // Determine unit interval
                timeoutUnitInterval = timeout.IsInfinite
                    ? checked((long)(failoverTimeoutStep * (1000L * ADP.DefaultConnectionTimeout)))
                    : checked((long)(failoverTimeoutStep * timeout.MillisecondsRemaining));
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

            // @TODO: Break down this while true loop, consider replacing with for, since it is a fixed number of tries
            while (true)
            {
                #if NETFRAMEWORK
                bool isFirstTransparentAttempt = connectionOptions.TransparentNetworkIPResolution &&
                                                 !disableTnir &&
                                                 attemptNumber == 1;
                #endif

                if (isParallel)
                {
                    int multiplier = ++attemptNumber;

                    #if NETFRAMEWORK
                    if (connectionOptions.TransparentNetworkIPResolution)
                    {
                        // While connecting using TNIR the timeout multiplier should be increased
                        // to allow steps of 1,2,4 instead of 1,2,3. This will allow half the
                        // timeout for the last connection attempt in case of TNIR.
                        multiplier = 1 << (attemptNumber - 1);
                    }
                    #endif

                    // Set timeout for this attempt, but don't exceed original timer
                    long nextTimeoutInterval = checked(timeoutUnitInterval * multiplier);
                    long milliseconds = timeout.MillisecondsRemaining;

                    #if NETFRAMEWORK
                    // If it is the first attempt at TNIR connection, then allow at least 500ms for
                    // timeout. With the current failover step of 0.125 and Connection Time of
                    // <4000ms, the first attempt can be lower than 500 ms.
                    if (isFirstTransparentAttempt)
                    {
                        nextTimeoutInterval = Math.Max(ADP.MinimumTimeoutForTnirMs, nextTimeoutInterval);
                    }
                    #endif

                    if (nextTimeoutInterval > milliseconds)
                    {
                        nextTimeoutInterval = milliseconds;
                    }
                    intervalTimer = TimeoutTimer.StartMillisecondsTimeout(nextTimeoutInterval);
                }

                // Re-allocate parser each time to make sure state is known.
                // If parser was created by previous attempt, dispose it to properly close the
                // socket, if created.
                _parser?.Disconnect();

                _parser = new TdsParser(ConnectionOptions.MARS, ConnectionOptions.Asynchronous);
                Debug.Assert(SniContext.Undefined == Parser._physicalStateObj.SniContext,
                    $"SniContext should be Undefined; actual Value: {Parser._physicalStateObj.SniContext}");

                try
                {
                    if (isParallel)
                    {
                        attemptOneLoginTimeout = intervalTimer;
                    }

                    #if NET
                    AttemptOneLogin(
                        serverInfo,
                        newPassword,
                        newSecurePassword,
                        attemptOneLoginTimeout);
                    #else
                    AttemptOneLogin(
                        serverInfo,
                        newPassword,
                        newSecurePassword,
                        attemptOneLoginTimeout,
                        isFirstTransparentAttempt: isFirstTransparentAttempt,
                        disableTnir: disableTnir);
                    #endif

                    if (connectionOptions.MultiSubnetFailover && ServerProvidedFailoverPartner != null)
                    {
                        // connection succeeded: trigger exception if server sends failover partner
                        // and MultiSubnetFailover is used
                        throw SQL.MultiSubnetFailoverWithFailoverPartner(
                            serverProvidedFailoverPartner: true,
                            internalConnection: this);
                    }

                    if (RoutingInfo != null)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.LoginNoFailover | " +
                            $"Routed to {serverInfo.ExtendedServerName}");

                        if (routingAttempts > MaxNumberOfRedirectRoute)
                        {
                            throw SQL.ROR_RecursiveRoutingNotSupported(this, MaxNumberOfRedirectRoute);
                        }

                        if (timeout.IsExpired)
                        {
                            throw SQL.ROR_TimeoutAfterRoutingInfo(this);
                        }

                        serverInfo = new ServerInfo(
                            ConnectionOptions,
                            RoutingInfo,
                            serverInfo.ResolvedServerName,
                            serverInfo.ServerSPN);
                        _timeoutErrorInternal.SetInternalSourceType(SqlConnectionInternalSourceType.RoutingDestination);
                        _originalClientConnectionId = _clientConnectionId;
                        _routingDestination = serverInfo.UserServerName;

                        // Restore properties that could be changed by the environment tokens
                        _currentPacketSize = ConnectionOptions.PacketSize;
                        _currentLanguage = _originalLanguage = ConnectionOptions.CurrentLanguage;
                        CurrentDatabase = _originalDatabase = ConnectionOptions.InitialCatalog;
                        ServerProvidedFailoverPartner = null;
                        _instanceName = string.Empty;

                        routingAttempts++;

                        // Repeat the loop, but skip code reserved for failed connections (after the catch)
                        continue;
                    }
                    else
                    {
                        // Leave the while loop -- we've successfully connected
                        break;
                    }
                }
                catch (SqlException sqlex)
                {
                    if (AttemptRetryADAuthWithTimeoutError(sqlex, connectionOptions, timeout))
                    {
                        continue;
                    }

                    // If state != closed, indicates that the parser encountered an error while
                    // processing the login response (e.g. an explicit error token). Transient
                    // network errors that impact connectivity will result in parser state being
                    // closed.
                    if (_parser?.State is not TdsParserState.Closed ||
                        IsDoNotRetryConnectError(sqlex) ||
                        timeout.IsExpired)
                    {
                        // No more time to try again
                        // Caller will call LoginFailure()
                        throw;
                    }

                    // Check sleep interval to make sure we won't exceed the timeout. Do this in
                    // the catch block so we can re-throw the current exception.
                    if (timeout.MillisecondsRemaining <= sleepInterval)
                    {
                        throw;
                    }
                }

                // We only get here when we failed to connect, but are going to re-try

                // Switch to failover logic if the server provided a partner
                if (ServerProvidedFailoverPartner != null)
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

                    // This is a failover scenario
                    _timeoutErrorInternal.SetFailoverScenario(true);

                    // Start by using failover partner, since we already failed to connect to the primary
                    LoginWithFailover(
                        useFailoverHost: true,
                        serverInfo,
                        ServerProvidedFailoverPartner,
                        newPassword,
                        newSecurePassword,
                        redirectedUserInstance,
                        connectionOptions,
                        credential,
                        timeout);

                    // LoginWithFailover successfully connected and handled entire connection setup
                    return;
                }

                // Sleep for a bit to prevent clogging the network with requests, then update sleep
                // interval for next iteration (max 1 second interval)
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    $"SqlInternalConnectionTds.LoginNoFailover | ADV " +
                    $"Object ID {ObjectID}, " +
                    $"Sleeping {sleepInterval}ms");

                Thread.Sleep(sleepInterval);

                sleepInterval = sleepInterval < 500
                    ? sleepInterval * 2
                    : 1000;
            }

            _activeDirectoryAuthTimeoutRetryHelper.State = ActiveDirectoryAuthenticationTimeoutRetryState.HasLoggedIn;

            if (PoolGroupProviderInfo != null)
            {
                // We must wait for CompleteLogin to finish for to have the env change from the
                // server to know its designated failover partner; save this information in
                // ServerProvidedFailoverPartner.

                // When ignoring server provided failover partner, we must pass in the original
                // failover partner from the connection string. Otherwise, the pool group's
                // failover partner designation will be updated to point to the server provided
                // value.
                string actualFailoverPartner = LocalAppContextSwitches.IgnoreServerProvidedFailoverPartner
                    ? string.Empty
                    : ServerProvidedFailoverPartner;

                PoolGroupProviderInfo.FailoverCheck(false, connectionOptions, actualFailoverPartner);
            }
            CurrentDataSource = originalServerInfo.UserServerName;
        }

        /// <summary>
        /// Attempt to log in to a host that has a failover partner.
        /// </summary>
        /// <remarks>
        /// Connection and timeout sequence is:
        /// - First target, timeout = interval * 1
        /// - second target, timeout = interval * 1
        /// - sleep for 100ms
        /// - First target, timeout = interval * 2
        /// - Second target, timeout = interval * 2
        /// - sleep for 200ms
        /// - First Target, timeout = interval * 3
        /// - etc.
        ///
        /// The logic in this method is paralleled by the logic in LoginNoFailover. Changes to
        /// either one should be examined to see if they need to be reflected in the other.
        /// </remarks>
        // @TODO: If it's so similar, then why don't we factor out some common code from it?
        private void LoginWithFailover(
            bool useFailoverHost,
            ServerInfo primaryServerInfo,
            string failoverHost,
            string newPassword,
            SecureString newSecurePassword,
            bool redirectedUserInstance,
            SqlConnectionString connectionOptions,
            SqlCredential credential, // @TODO: This isn't used anywhere
            TimeoutTimer timeout)
        {
            Debug.Assert(!connectionOptions.MultiSubnetFailover,
                "MultiSubnetFailover should not be set if failover partner is used");

            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                $"SqlInternalConnectionTds.LoginWithFailover | ADV | " +
                $"Object ID {ObjectID}, " +
                $"useFailover={useFailoverHost}, " +
                $"primary={primaryServerInfo.UserServerName}, " +
                $"failover={failoverHost}");

            #if NETFRAMEWORK
            string protocol = ConnectionOptions.NetworkLibrary;
            #endif

            ServerInfo failoverServerInfo = new ServerInfo(
                connectionOptions,
                failoverHost,
                connectionOptions.FailoverPartnerSPN);

            ResolveExtendedServerName(primaryServerInfo, !redirectedUserInstance, connectionOptions);
            if (ServerProvidedFailoverPartner == null)
            {
                ResolveExtendedServerName(
                    failoverServerInfo,
                    aliasLookup: !redirectedUserInstance && failoverHost != primaryServerInfo.UserServerName,
                    connectionOptions);
            }

            // Milliseconds to sleep (back off) between attempts.
            // @TODO: Rename to include units (or use TimeSpan!)
            int sleepInterval = 100;

            // Determine unit interval
            // @TODO: Use ints or timespans or something that doesn't requires floating point math
            long timeoutUnitInterval = timeout.IsInfinite
                ? checked((long)(ADP.FailoverTimeoutStep * ADP.TimerFromSeconds(ADP.DefaultConnectionTimeout)))
                : checked((long)(ADP.FailoverTimeoutStep * timeout.MillisecondsRemaining));

            // Initialize loop variables
            // Have we demanded for partner information yet (as necessary)?
            bool failoverDemandDone = false;
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

                // Re-allocate parser each time to make sure state is known. If parser was created
                // by previous attempt, dispose it to properly close the socket, if created.
                _parser?.Disconnect();
                _parser = new TdsParser(ConnectionOptions.MARS, ConnectionOptions.Asynchronous);

                Debug.Assert(SniContext.Undefined == Parser._physicalStateObj.SniContext,
                    $"SniContext should be Undefined; actual Value: {Parser._physicalStateObj.SniContext}");

                ServerInfo currentServerInfo;
                if (useFailoverHost)
                {
                    if (!failoverDemandDone)
                    {
                        #if NETFRAMEWORK
                        FailoverPermissionDemand();
                        #endif

                        failoverDemandDone = true;
                    }

                    // Primary server may give us a different failover partner than the connection
                    // string indicates. Update it only if we are respecting server-provided
                    // failover partner values.
                    if (ServerProvidedFailoverPartner != null && failoverServerInfo.ResolvedServerName != ServerProvidedFailoverPartner)
                    {
                        if (LocalAppContextSwitches.IgnoreServerProvidedFailoverPartner)
                        {
                            SqlClientEventSource.Log.TryTraceEvent(
                                $"SqlInternalConnectionTds.LoginWithFailover | ADV | " +
                                $"Object ID {ObjectID}, " +
                                $"Ignoring server provided failover partner '{ServerProvidedFailoverPartner}' " +
                                $"due to IgnoreServerProvidedFailoverPartner AppContext switch.");
                        }
                        else
                        {
                            SqlClientEventSource.Log.TryAdvancedTraceEvent(
                                $"SqlInternalConnectionTds.LoginWithFailover | ADV | " +
                                $"Object ID {ObjectID}, " +
                                $"new failover partner={ServerProvidedFailoverPartner}");

                            #if NET
                            failoverServerInfo.SetDerivedNames(string.Empty, ServerProvidedFailoverPartner);
                            #else
                            failoverServerInfo.SetDerivedNames(protocol, ServerProvidedFailoverPartner);
                            #endif
                        }
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
                    // Attempt login. Use timerInterval for attempt timeout unless infinite timeout
                    // was requested.
                    AttemptOneLogin(
                        currentServerInfo,
                        newPassword,
                        newSecurePassword,
                        intervalTimer,
                        withFailover: true);

                    int routingAttempts = 0;
                    while (RoutingInfo != null)
                    {
                        if (routingAttempts > MaxNumberOfRedirectRoute)
                        {
                            throw SQL.ROR_RecursiveRoutingNotSupported(this, MaxNumberOfRedirectRoute);
                        }
                        routingAttempts++;

                        SqlClientEventSource.Log.TryTraceEvent(
                            $"SqlInternalConnectionTds.LoginWithFailover | " +
                            $"Routed to {RoutingInfo.ServerName}", RoutingInfo.ServerName);

                        _parser?.Disconnect();
                        _parser = new TdsParser(ConnectionOptions.MARS, connectionOptions.Asynchronous);

                        Debug.Assert(SniContext.Undefined == Parser._physicalStateObj.SniContext,
                            $"SniContext should be Undefined; actual Value: {Parser._physicalStateObj.SniContext}");

                        currentServerInfo = new ServerInfo(
                            ConnectionOptions,
                            RoutingInfo,
                            currentServerInfo.ResolvedServerName,
                            currentServerInfo.ServerSPN);
                        _timeoutErrorInternal.SetInternalSourceType(SqlConnectionInternalSourceType.RoutingDestination);
                        _originalClientConnectionId = _clientConnectionId;
                        _routingDestination = currentServerInfo.UserServerName;

                        // Restore properties that could be changed by the environment tokens
                        _currentPacketSize = connectionOptions.PacketSize;
                        _currentLanguage = _originalLanguage = ConnectionOptions.CurrentLanguage;
                        CurrentDatabase = _originalDatabase = connectionOptions.InitialCatalog;
                        ServerProvidedFailoverPartner = null;
                        _instanceName = string.Empty;

                        AttemptOneLogin(
                            currentServerInfo,
                            newPassword,
                            newSecurePassword,
                            intervalTimer,
                            withFailover: true);
                    }

                    // Leave the while loop -- we've successfully connected
                    break;
                }
                catch (SqlException sqlex)
                {
                    if (AttemptRetryADAuthWithTimeoutError(sqlex, connectionOptions, timeout))
                    {
                        continue;
                    }

                    if (IsDoNotRetryConnectError(sqlex) || timeout.IsExpired)
                    {
                        // No more time to try again.
                        // Caller will call LoginFailure()
                        throw;
                    }

                    // TODO: It doesn't make sense to connect to an azure sql server instance with a failover partner
                    // specified. Azure SQL Server does not support failover partners. Other availability technologies
                    // like Failover Groups should be used instead.
                    if (!ADP.IsAzureSqlServerEndpoint(connectionOptions.DataSource) && IsConnectionDoomed)
                    {
                        throw;
                    }

                    if (attemptNumber % 2 == 1)
                    {
                        // Check sleep interval to make sure we won't exceed the original timeout.
                        // Do this in the catch block so we can re-throw the current exception
                        if (timeout.MillisecondsRemaining <= sleepInterval)
                        {
                            throw;
                        }
                    }
                }

                // We only get here when we failed to connect, but are going to re-try

                // After trying to connect to both servers fails, sleep for a bit to prevent
                // clogging the network with requests, then update sleep interval for next
                // iteration (max 1 second interval).
                if (attemptNumber % 2 == 1)
                {
                    SqlClientEventSource.Log.TryAdvancedTraceEvent(
                        $"SqlInternalConnectionTds.LoginWithFailover | ADV | " +
                        $"Object ID {ObjectID}, " +
                        $"sleeping {sleepInterval}ms");

                    Thread.Sleep(sleepInterval);

                    sleepInterval = sleepInterval < 500
                        ? sleepInterval * 2
                        : 1000;
                }

                // Update attempt number and target host
                attemptNumber++;
                useFailoverHost = !useFailoverHost;
            }

            // If we get here, connection/login succeeded!  Just a few more checks & record-keeping
            _activeDirectoryAuthTimeoutRetryHelper.State = ActiveDirectoryAuthenticationTimeoutRetryState.HasLoggedIn;

            // if connected to failover host, but said host doesn't have DbMirroring set up, throw an error
            if (useFailoverHost && ServerProvidedFailoverPartner == null)
            {
                throw SQL.InvalidPartnerConfiguration(failoverHost, CurrentDatabase);
            }

            if (PoolGroupProviderInfo != null)
            {
                // We must wait for CompleteLogin to finish for to have the env change from the
                // server to know its designated failover partner.

                // When ignoring server provided failover partner, we must pass in the original
                // failover partner from the connection string. Otherwise, the pool group's
                // failover partner designation will be updated to point to the server provided
                // value.
                string actualFailoverPartner = LocalAppContextSwitches.IgnoreServerProvidedFailoverPartner
                    ? failoverHost
                    : ServerProvidedFailoverPartner;

                PoolGroupProviderInfo.FailoverCheck(useFailoverHost, connectionOptions, actualFailoverPartner);
            }

            CurrentDataSource = useFailoverHost
                ? failoverHost
                : primaryServerInfo.UserServerName;
        }

        /// <summary>
        /// Is the given Sql error one that should prevent retrying to connect.
        /// </summary>
        // @TODO: Make static
        private bool IsDoNotRetryConnectError(SqlException exc)
        {
            bool errorNumberMatch = exc.Number is TdsEnums.LOGON_FAILED          // Actual login failed, ie bad password
                                               or TdsEnums.PASSWORD_EXPIRED      // Actual login failed, ie expired password
                                               or TdsEnums.IMPERSONATION_FAILED; // Insufficient privilege for named pipe, etc
            return errorNumberMatch || exc._doNotReconnect;
        }

        /// <summary>
        /// Returns <c>true</c> if the SQL error is transient, as per <see cref="s_transientErrors"/>.
        /// </summary>
        private bool IsTransientError(SqlException exc)
        {
            if (exc == null)
            {
                return false;
            }

            foreach (SqlError error in exc.Errors)
            {
                if (s_transientErrors.Contains(error.Number))
                {
                    // When server timeouts, connection is doomed. Reset here to allow reconnect.
                    UnDoomThisConnection();
                    return true;
                }
            }

            return false;
        }

        private void OpenLoginEnlist(
            TimeoutTimer timeout,
            SqlConnectionString connectionOptions,
            SqlCredential credential,
            string newPassword,
            SecureString newSecurePassword,
            bool redirectedUserInstance)
        {
            // Indicates whether we should use primary or secondary first
            bool useFailoverPartner;
            string failoverPartner;

            ServerInfo dataSource = new ServerInfo(connectionOptions);

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

            SqlConnectionInternalSourceType sourceType = useFailoverPartner
                ? SqlConnectionInternalSourceType.Failover
                : SqlConnectionInternalSourceType.Principle;
            _timeoutErrorInternal.SetInternalSourceType(sourceType);

            bool hasFailoverPartner = !string.IsNullOrEmpty(failoverPartner);

            try
            {
                // Open the connection and Login
                _timeoutErrorInternal.SetAndBeginPhase(SqlConnectionTimeoutErrorPhase.PreLoginBegin);
                if (hasFailoverPartner)
                {
                    // This is a failover scenario
                    _timeoutErrorInternal.SetFailoverScenario(true);
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
                    // This is *not* a failover scenario
                    _timeoutErrorInternal.SetFailoverScenario(false);
                    LoginNoFailover(
                        dataSource,
                        newPassword,
                        newSecurePassword,
                        redirectedUserInstance,
                        connectionOptions,
                        credential,
                        timeout);
                }

                if (!IsAzureSqlConnection)
                {
                    // If not a connection to Azure SQL, Readonly with FailoverPartner is not supported
                    if (ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly)
                    {
                        if (!string.IsNullOrEmpty(ConnectionOptions.FailoverPartner))
                        {
                            throw SQL.ROR_FailoverNotSupportedConnString();
                        }

                        if (ServerProvidedFailoverPartner != null)
                        {
                            throw SQL.ROR_FailoverNotSupportedServer(this);
                        }
                    }
                }

                _timeoutErrorInternal.EndPhase(SqlConnectionTimeoutErrorPhase.PostLogin);
            }
            catch (Exception e)
            {
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

        // @TODO: Is this suppression still required
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")] // copied from Triaged.cs
        private void ResetConnection()
        {
            // For implicit pooled connections, if connection reset behavior is specified, reset
            // the database and language properties back to default. It is important to do this on
            // activate so that the dictionary is correct before SqlConnection obtains a clone.

            Debug.Assert(!HasLocalTransactionFromAPI,
                "Upon ResetConnection SqlInternalConnectionTds has a currently ongoing local transaction.");
            Debug.Assert(!_parser._physicalStateObj.HasPendingData,
                "Upon ResetConnection SqlInternalConnectionTds has pending data.");

            if (_fResetConnection)
            {
                // Pooled connections that are enlisted in a transaction must have their transaction
                // preserved when resetting the connection state. Otherwise, future uses of the connection
                // from the pool will execute outside the transaction, in auto-commit mode.
                // https://github.com/dotnet/SqlClient/issues/2970
                _parser.PrepareResetConnection(EnlistedTransaction is not null && Pool is not null);

                // Reset dictionary values, since calling reset will not send us env_changes.
                CurrentDatabase = _originalDatabase;
                _currentLanguage = _originalLanguage;
            }
        }

        private void ResolveExtendedServerName(ServerInfo serverInfo, bool aliasLookup, SqlConnectionString options)
        {
            // @TODO: Invert to save on indentation
            if (serverInfo.ExtendedServerName == null)
            {
                string host = serverInfo.UserServerName;
                string protocol = serverInfo.UserProtocol;

                if (aliasLookup)
                {
                    // We skip this for UserInstances...
                    // Perform registry lookup to see if host is an alias. It will appropriately
                    // set host and protocol, if an Alias. Check if it was already resolved, during
                    // CR reconnection _currentSessionData values will be copied from
                    // _reconnectSessionData of the previous connection.
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
                            // @TODO: What are these refs doing here?? Just return the values!
                            TdsParserStaticMethods.AliasRegistryLookup(ref host, ref protocol);
                            _currentSessionData._resolvedAliases.Add(
                                serverInfo.UserServerName,
                                new Tuple<string, string>(host, protocol));
                        }
                    }
                    else
                    {
                        TdsParserStaticMethods.AliasRegistryLookup(ref host, ref protocol);
                    }

                    // TODO: fix local host enforcement with datadirectory and failover
                    if (options.EnforceLocalHost)
                    {
                        // Verify LocalHost for |DataDirectory| usage
                        SqlConnectionString.VerifyLocalHostAndFixup(
                            ref host,
                            enforceLocalHost: true,
                            fixup: true);
                    }
                }

                serverInfo.SetDerivedNames(protocol, host);
            }
        }

        #if NETFRAMEWORK
        private bool ShouldDisableTnir(SqlConnectionString connectionOptions)
        {
            bool isAzureEndPoint = ADP.IsAzureSqlServerEndpoint(connectionOptions.DataSource);

            // @TODO: Turn into a HashSet and just check the list instead of this MESS.
            bool isFedAuthEnabled = _accessTokenInBytes != null ||
                                    #pragma warning disable 0618 // Type or member is obsolete
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword ||
                                    #pragma warning restore 0618 // Type or member is obsolete
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated ||
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive ||
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal ||
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow ||
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity ||
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI ||
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault ||
                                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity;

            // Check if the user had explicitly specified the TNIR option in the connection string
            // or the connection string builder. If the user has specified the option in the
            // connection string explicitly, then we shouldn't disable TNIR.
            bool isTnirExplicitlySpecifiedInConnectionOptions = connectionOptions.Parsetable.ContainsKey(
                DbConnectionStringKeywords.TransparentNetworkIpResolution);

            return isTnirExplicitlySpecifiedInConnectionOptions ? false : (isAzureEndPoint || isFedAuthEnabled);
        }
        #endif

        /// <summary>
        /// Tries to acquire a lock on the authentication context. If successful in acquiring the
        /// lock, gets a new token and assigns it in the out parameter. Else returns false.
        /// </summary>
        /// <param name="fedAuthInfo">Federated Authentication Info</param>
        /// <param name="dbConnectionPoolAuthenticationContext">
        /// Authentication Context cached in the connection pool.
        /// </param>
        /// <param name="fedAuthToken">
        /// Out parameter, carrying the token if we acquired a lock and got the token.
        /// </param>
        private bool TryGetFedAuthTokenLocked(
            SqlFedAuthInfo fedAuthInfo,
            DbConnectionPoolAuthenticationContext dbConnectionPoolAuthenticationContext,
            out SqlFedAuthToken fedAuthToken)
        {

            Debug.Assert(fedAuthInfo != null, "fedAuthInfo should not be null.");
            Debug.Assert(dbConnectionPoolAuthenticationContext != null,
                "dbConnectionPoolAuthenticationContext should not be null.");

            fedAuthToken = null;

            // Variable which indicates if we did indeed manage to acquire the lock on the
            // authentication context, to try update it.
            bool authenticationContextLocked = false;

            try
            {
                // Try to obtain a lock on the context. If acquired, this thread got the
                // opportunity to update. Else some other thread is already updating it, so just
                // proceed forward with the existing token in the cache.
                if (dbConnectionPoolAuthenticationContext.LockToUpdate())
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"SqlInternalConnectionTds.TryGetFedAuthTokenLocked | " +
                        $"Object ID {ObjectID}, " +
                        $"Acquired the lock to update the authentication context. " +
                        $"The expiration time is {dbConnectionPoolAuthenticationContext.ExpirationTime:T}. " +
                        $"Current Time is {DateTime.UtcNow:T}.");

                    authenticationContextLocked = true;
                }
                else
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        $"SqlInternalConnectionTds.TryGetFedAuthTokenLocked | " +
                        $"Object ID {ObjectID}, " +
                        $"Refreshing the context is already in progress by another thread.");
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
                    // Release the lock we took on the authentication context, even if we have not
                    // yet updated the cache with the new context. Login process can fail at
                    // several places after this step and so there is no guarantee that the new
                    // context will make it to the cache. So we shouldn't miss resetting the flag.
                    // With the reset, at-least another thread may have a chance to update it.
                    dbConnectionPoolAuthenticationContext.ReleaseLockToUpdate();
                }
            }

            return authenticationContextLocked;
        }

        #endregion
    }
}
