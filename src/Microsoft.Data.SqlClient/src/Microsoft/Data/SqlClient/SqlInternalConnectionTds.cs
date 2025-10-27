// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
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

        private SqlCredential _credential;

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

        private TdsParser _parser;

        /// <remarks>
        /// Will only be null when called for ChangePassword, or creating SSE User Instance.
        /// </remarks>
        private readonly SqlConnectionPoolGroupProviderInfo _poolGroupProviderInfo;

        private SessionData _recoverySessionData;

        // @TODO: Rename to match naming conventions
        private bool _SQLDNSRetryEnabled = false;

        // @TODO: Rename to match naming conventions
        private bool _serverSupportsDNSCaching = false;

        private bool _sessionRecoveryRequested;

        #endregion

        #region Properties

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
        /// Get or set if the control ring send redirect token and feature ext ack with true for DNSCaching
        /// </summary>
        /// @TODO: Make auto-property
        internal bool IsDNSCachingBeforeRedirectSupported
        {
            get => _dnsCachingBeforeRedirect;
            set => _dnsCachingBeforeRedirect = value;
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
    }
}
