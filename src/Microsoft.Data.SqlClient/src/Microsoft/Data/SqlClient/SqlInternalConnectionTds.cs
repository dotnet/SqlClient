// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;

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

        internal void IncrementAsyncCount()
        {
            Interlocked.Increment(ref _asyncCommandCount);
        }

        internal override bool IsConnectionAlive(bool throwOnException) =>
            _parser._physicalStateObj.IsConnectionAlive(throwOnException);

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

        protected override void PropagateTransactionCookie(byte[] cookie)
        {
            _parser.PropagateDistributedTransaction(
                cookie,
                ConnectionOptions.ConnectTimeout,
                _parser._physicalStateObj);
        }

        #endregion

        #region Private Methods

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

        #endregion
    }
}
