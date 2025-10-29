// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
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

        #region Protected Methods

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

        #endregion

        #region Private Methods

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

        #endregion
    }
}
