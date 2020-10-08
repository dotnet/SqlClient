// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlConnection/*' />
    public sealed partial class SqlConnection : DbConnection, ICloneable
    {
        private enum CultureCheckState : uint
        {
            Unknown = 0,
            Standard = 1,
            Invariant = 2
        }

        private bool _AsyncCommandInProgress;

        // SQLStatistics support
        internal SqlStatistics _statistics;
        private bool _collectstats;

        private bool _fireInfoMessageEventOnUserErrors; // False by default

        // root task associated with current async invocation
        private Tuple<TaskCompletionSource<DbConnectionInternal>, Task> _currentCompletion;

        private SqlCredential _credential;
        private string _connectionString;
        private int _connectRetryCount;
        private string _accessToken; // Access Token to be used for token based authentication

        // connection resiliency
        private object _reconnectLock;
        internal Task _currentReconnectionTask;
        private Task _asyncWaitingForReconnection; // current async task waiting for reconnection in non-MARS connections
        private Guid _originalConnectionId = Guid.Empty;
        private CancellationTokenSource _reconnectionCancellationSource;
        internal SessionData _recoverySessionData;
        internal bool _suppressStateChangeForReconnection;
        private int _reconnectCount;

        // diagnostics listener
        private static readonly SqlDiagnosticListener s_diagnosticListener = new SqlDiagnosticListener(SqlClientDiagnosticListenerExtensions.DiagnosticListenerName);

        // Transient Fault handling flag. This is needed to convey to the downstream mechanism of connection establishment, if Transient Fault handling should be used or not
        // The downstream handling of Connection open is the same for idle connection resiliency. Currently we want to apply transient fault handling only to the connections opened
        // using SqlConnection.Open() method.
        internal bool _applyTransientFaultHandling = false;

        // System column encryption key store providers are added by default
        private static readonly Dictionary<string, SqlColumnEncryptionKeyStoreProvider> _SystemColumnEncryptionKeyStoreProviders
            = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
                {
                    {SqlColumnEncryptionCertificateStoreProvider.ProviderName, new SqlColumnEncryptionCertificateStoreProvider()},
                    {SqlColumnEncryptionCngProvider.ProviderName, new SqlColumnEncryptionCngProvider()},
                    {SqlColumnEncryptionCspProvider.ProviderName, new SqlColumnEncryptionCspProvider()}
                };

        // Lock to control setting of _CustomColumnEncryptionKeyStoreProviders
        private static readonly object _CustomColumnEncryptionKeyProvidersLock = new object();
        // status of invariant culture environment check
        private static CultureCheckState _cultureCheckState;

        /// <summary>
        /// Custom provider list should be provided by the user. We shallow copy the user supplied dictionary into a ReadOnlyDictionary.
        /// Custom provider list can only supplied once per application.
        /// </summary>
        private static ReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> _CustomColumnEncryptionKeyStoreProviders;

        /// <summary>
        /// Dictionary object holding trusted key paths for various SQL Servers.
        /// Key to the dictionary is a SQL Server Name
        /// IList contains a list of trusted key paths.
        /// </summary>
        private static readonly ConcurrentDictionary<string, IList<string>> _ColumnEncryptionTrustedMasterKeyPaths
            = new ConcurrentDictionary<string, IList<string>>(concurrencyLevel: 4 * Environment.ProcessorCount /* default value in ConcurrentDictionary*/,
                capacity: 1,
                comparer: StringComparer.OrdinalIgnoreCase);

        private static readonly Action<object> s_openAsyncCancel = OpenAsyncCancel;
        private static readonly Action<Task<object>, object> s_openAsyncComplete = OpenAsyncComplete;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionKeyCacheTtl/*' />
        public static TimeSpan ColumnEncryptionKeyCacheTtl { get; set; } = TimeSpan.FromHours(2);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionQueryMetadataCacheEnabled/*' />
        public static bool ColumnEncryptionQueryMetadataCacheEnabled { get; set; } = true;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionTrustedMasterKeyPaths/*' />
        public static IDictionary<string, IList<string>> ColumnEncryptionTrustedMasterKeyPaths => _ColumnEncryptionTrustedMasterKeyPaths;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ctorConnectionString/*' />
        public SqlConnection(string connectionString) : this()
        {
            ConnectionString = connectionString;    // setting connection string first so that ConnectionOption is available
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ctorConnectionStringCredential/*' />
        public SqlConnection(string connectionString, SqlCredential credential) : this()
        {
            ConnectionString = connectionString;
            if (credential != null)
            {
                // The following checks are necessary as setting Credential property will call CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential
                //  CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential it will throw InvalidOperationException rather than ArgumentException
                //  Need to call setter on Credential property rather than setting _credential directly as pool groups need to be checked
                SqlConnectionString connectionOptions = (SqlConnectionString)ConnectionOptions;
                if (UsesClearUserIdOrPassword(connectionOptions))
                {
                    throw ADP.InvalidMixedArgumentOfSecureAndClearCredential();
                }

                if (UsesIntegratedSecurity(connectionOptions))
                {
                    throw ADP.InvalidMixedArgumentOfSecureCredentialAndIntegratedSecurity();
                }
                else if (UsesActiveDirectoryIntegrated(connectionOptions))
                {
                    throw SQL.SettingCredentialWithIntegratedArgument();
                }
                else if (UsesActiveDirectoryInteractive(connectionOptions))
                {
                    throw SQL.SettingCredentialWithInteractiveArgument();
                }
                else if (UsesActiveDirectoryDeviceCodeFlow(connectionOptions))
                {
                    throw SQL.SettingCredentialWithDeviceFlowArgument();
                }
                else if (UsesActiveDirectoryManagedIdentity(connectionOptions))
                {
                    throw SQL.SettingCredentialWithManagedIdentityArgument(DbConnectionStringBuilderUtil.ActiveDirectoryManagedIdentityString);
                }
                else if (UsesActiveDirectoryMSI(connectionOptions))
                {
                    throw SQL.SettingCredentialWithManagedIdentityArgument(DbConnectionStringBuilderUtil.ActiveDirectoryMSIString);
                }

                Credential = credential;
            }
            // else
            //      credential == null:  we should not set "Credential" as this will do additional validation check and
            //      checking pool groups which is not necessary. All necessary operation is already done by calling "ConnectionString = connectionString"
        }

        private SqlConnection(SqlConnection connection)
        {
            GC.SuppressFinalize(this);
            CopyFrom(connection);
            _connectionString = connection._connectionString;
            if (connection._credential != null)
            {
                SecureString password = connection._credential.Password.Copy();
                password.MakeReadOnly();
                _credential = new SqlCredential(connection._credential.UserId, password);
            }

            _accessToken = connection._accessToken;
            CacheConnectionStringProperties();
        }

        /// <summary>
        /// This function walks through both system and custom column encryption key store providers and returns an object if found.
        /// </summary>
        /// <param name="providerName">Provider Name to be searched in System Provider diction and Custom provider dictionary.</param>
        /// <param name="columnKeyStoreProvider">If the provider is found, returns the corresponding SqlColumnEncryptionKeyStoreProvider instance.</param>
        /// <returns>true if the provider is found, else returns false</returns>
        static internal bool TryGetColumnEncryptionKeyStoreProvider(string providerName, out SqlColumnEncryptionKeyStoreProvider columnKeyStoreProvider)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(providerName), "Provider name is invalid");

            // Initialize the out parameter
            columnKeyStoreProvider = null;

            // Search in the sytem provider list.
            if (_SystemColumnEncryptionKeyStoreProviders.TryGetValue(providerName, out columnKeyStoreProvider))
            {
                return true;
            }

            lock (_CustomColumnEncryptionKeyProvidersLock)
            {
                // If custom provider is not set, then return false
                if (_CustomColumnEncryptionKeyStoreProviders == null)
                {
                    return false;
                }

                // Search in the custom provider list
                return _CustomColumnEncryptionKeyStoreProviders.TryGetValue(providerName, out columnKeyStoreProvider);
            }
        }

        /// <summary>
        /// This function returns a list of system provider dictionary currently supported by this driver.
        /// </summary>
        /// <returns>Combined list of provider names</returns>
        static internal List<string> GetColumnEncryptionSystemKeyStoreProviders()
        {
            HashSet<string> providerNames = new HashSet<string>(_SystemColumnEncryptionKeyStoreProviders.Keys);
            return providerNames.ToList();
        }

        /// <summary>
        /// This function returns a list of custom provider dictionary currently registered.
        /// </summary>
        /// <returns>Combined list of provider names</returns>
        static internal List<string> GetColumnEncryptionCustomKeyStoreProviders()
        {
            if (_CustomColumnEncryptionKeyStoreProviders != null)
            {
                HashSet<string> providerNames = new HashSet<string>(_CustomColumnEncryptionKeyStoreProviders.Keys);
                return providerNames.ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// Is this connection using column encryption ?
        /// </summary>
        internal bool IsColumnEncryptionSettingEnabled
        {
            get
            {
                SqlConnectionString opt = (SqlConnectionString)ConnectionOptions;
                return opt?.ColumnEncryptionSetting == SqlConnectionColumnEncryptionSetting.Enabled;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RegisterColumnEncryptionKeyStoreProviders/*' />
        public static void RegisterColumnEncryptionKeyStoreProviders(IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders)
        {

            // Return when the provided dictionary is null.
            if (customProviders == null)
            {
                throw SQL.NullCustomKeyStoreProviderDictionary();
            }

            // Validate that custom provider list doesn't contain any of system provider list
            foreach (string key in customProviders.Keys)
            {
                // Validate the provider name
                //
                // Check for null or empty
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw SQL.EmptyProviderName();
                }

                // Check if the name starts with MSSQL_, since this is reserved namespace for system providers.
                if (key.StartsWith(ADP.ColumnEncryptionSystemProviderNamePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw SQL.InvalidCustomKeyStoreProviderName(key, ADP.ColumnEncryptionSystemProviderNamePrefix);
                }

                // Validate the provider value
                if (customProviders[key] == null)
                {
                    throw SQL.NullProviderValue(key);
                }
            }

            lock (_CustomColumnEncryptionKeyProvidersLock)
            {
                // Provider list can only be set once
                if (_CustomColumnEncryptionKeyStoreProviders != null)
                {
                    throw SQL.CanOnlyCallOnce();
                }

                // Create a temporary dictionary and then add items from the provided dictionary.
                // Dictionary constructor does shallow copying by simply copying the provider name and provider reference pairs
                // in the provided customerProviders dictionary.
                Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customColumnEncryptionKeyStoreProviders =
                    new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(customProviders, StringComparer.OrdinalIgnoreCase);

                // Set the dictionary to the ReadOnly dictionary.
                _CustomColumnEncryptionKeyStoreProviders = new ReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>(customColumnEncryptionKeyStoreProviders);
            }
        }

        /// <summary>
        /// Get enclave attestation url to be used with enclave based Always Encrypted
        /// </summary>
        internal string EnclaveAttestationUrl => ((SqlConnectionString)ConnectionOptions).EnclaveAttestationUrl;

        /// <summary>
        /// Get attestation protocol
        /// </summary>
        internal SqlConnectionAttestationProtocol AttestationProtocol
        {
            get
            {
                SqlConnectionString opt = (SqlConnectionString)ConnectionOptions;
                return opt.AttestationProtocol;
            }
        }

        // This method will be called once connection string is set or changed.
        private void CacheConnectionStringProperties()
        {
            SqlConnectionString connString = ConnectionOptions as SqlConnectionString;
            if (connString != null)
            {
                _connectRetryCount = connString.ConnectRetryCount;
                // For Azure SQL connection, set _connectRetryCount to 2 instead of 1 will greatly improve recovery
                //   success rate
                if (_connectRetryCount == 1 && ADP.IsAzureSqlServerEndpoint(connString.DataSource))
                {
                    _connectRetryCount = 2;
                }
            }
        }

        //
        // PUBLIC PROPERTIES
        //

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/StatisticsEnabled/*' />
        // used to start/stop collection of statistics data and do verify the current state
        //
        // devnote: start/stop should not performed using a property since it requires execution of code
        //
        // start statistics
        //  set the internal flag (_statisticsEnabled) to true.
        //  Create a new SqlStatistics object if not already there.
        //  connect the parser to the object.
        //  if there is no parser at this time we need to connect it after creation.
        public bool StatisticsEnabled
        {
            get
            {
                return (_collectstats);
            }
            set
            {
                {
                    if (value)
                    {
                        // start
                        if (ConnectionState.Open == State)
                        {
                            if (null == _statistics)
                            {
                                _statistics = new SqlStatistics();
                                ADP.TimerCurrent(out _statistics._openTimestamp);
                            }
                            // set statistics on the parser
                            // update timestamp;
                            Debug.Assert(Parser != null, "Where's the parser?");
                            Parser.Statistics = _statistics;
                        }
                    }
                    else
                    {
                        // stop
                        if (null != _statistics)
                        {
                            if (ConnectionState.Open == State)
                            {
                                // remove statistics from parser
                                // update timestamp;
                                TdsParser parser = Parser;
                                Debug.Assert(parser != null, "Where's the parser?");
                                parser.Statistics = null;
                                ADP.TimerCurrent(out _statistics._closeTimestamp);
                            }
                        }
                    }
                    _collectstats = value;
                }
            }
        }

        internal bool AsyncCommandInProgress
        {
            get => _AsyncCommandInProgress;
            set => _AsyncCommandInProgress = value;
        }

        private bool UsesActiveDirectoryIntegrated(SqlConnectionString opt)
        {
            return opt != null && opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated;
        }

        private bool UsesActiveDirectoryInteractive(SqlConnectionString opt)
        {
            return opt != null && opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive;
        }

        private bool UsesActiveDirectoryDeviceCodeFlow(SqlConnectionString opt)
        {
            return opt != null && opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
        }

        private bool UsesActiveDirectoryManagedIdentity(SqlConnectionString opt)
        {
            return opt != null && opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;
        }

        private bool UsesActiveDirectoryMSI(SqlConnectionString opt)
        {
            return opt != null && opt.Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI;
        }

        private bool UsesAuthentication(SqlConnectionString opt)
        {
            return opt != null && opt.Authentication != SqlAuthenticationMethod.NotSpecified;
        }

        // Does this connection use Integrated Security?
        private bool UsesIntegratedSecurity(SqlConnectionString opt)
        {
            return opt != null && opt.IntegratedSecurity;
        }

        // Does this connection use old style of clear userID or Password in connection string?
        private bool UsesClearUserIdOrPassword(SqlConnectionString opt)
        {
            bool result = false;
            if (null != opt)
            {
                result = (!string.IsNullOrEmpty(opt.UserID) || !string.IsNullOrEmpty(opt.Password));
            }
            return result;
        }

        internal SqlConnectionString.TransactionBindingEnum TransactionBinding
        {
            get => ((SqlConnectionString)ConnectionOptions).TransactionBinding;
        }

        internal SqlConnectionString.TypeSystem TypeSystem
        {
            get => ((SqlConnectionString)ConnectionOptions).TypeSystemVersion;
        }

        internal Version TypeSystemAssemblyVersion
        {
            get => ((SqlConnectionString)ConnectionOptions).TypeSystemAssemblyVersion;
        }

        internal int ConnectRetryInterval
        {
            get => ((SqlConnectionString)ConnectionOptions).ConnectRetryInterval;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ConnectionString/*' />
        public override string ConnectionString
        {
            get
            {
                return ConnectionString_Get();
            }
            set
            {
                if (_credential != null || _accessToken != null)
                {
                    SqlConnectionString connectionOptions = new SqlConnectionString(value);
                    if (_credential != null)
                    {
                        // Check for Credential being used with Authentication=ActiveDirectoryIntegrated | ActiveDirectoryInteractive |
                        //  ActiveDirectoryDeviceCodeFlow | ActiveDirectoryManagedIdentity/ActiveDirectoryMSI. Since a different error string is used
                        // for this case in ConnectionString setter vs in Credential setter, check for this error case before calling
                        // CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential, which is common to both setters.
                        if (UsesActiveDirectoryIntegrated(connectionOptions))
                        {
                            throw SQL.SettingIntegratedWithCredential();
                        }
                        else if (UsesActiveDirectoryInteractive(connectionOptions))
                        {
                            throw SQL.SettingInteractiveWithCredential();
                        }
                        else if (UsesActiveDirectoryDeviceCodeFlow(connectionOptions))
                        {
                            throw SQL.SettingDeviceFlowWithCredential();
                        }
                        else if (UsesActiveDirectoryManagedIdentity(connectionOptions))
                        {
                            throw SQL.SettingManagedIdentityWithCredential(DbConnectionStringBuilderUtil.ActiveDirectoryManagedIdentityString);
                        }
                        else if (UsesActiveDirectoryMSI(connectionOptions))
                        {
                            throw SQL.SettingManagedIdentityWithCredential(DbConnectionStringBuilderUtil.ActiveDirectoryMSIString);
                        }

                        CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential(connectionOptions);
                    }
                    else if (_accessToken != null)
                    {
                        CheckAndThrowOnInvalidCombinationOfConnectionOptionAndAccessToken(connectionOptions);
                    }
                }
                ConnectionString_Set(new SqlConnectionPoolKey(value, _credential, _accessToken));
                _connectionString = value;  // Change _connectionString value only after value is validated
                CacheConnectionStringProperties();
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ConnectionTimeout/*' />
        public override int ConnectionTimeout
        {
            get
            {
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                return ((null != constr) ? constr.ConnectTimeout : SqlConnectionString.DEFAULT.Connect_Timeout);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CommandTimeout/*' />
        public int CommandTimeout
        {
            get
            {
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                return ((null != constr) ? constr.CommandTimeout : SqlConnectionString.DEFAULT.Command_Timeout);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/AccessToken/*' />
        // AccessToken: To be used for token based authentication
        public string AccessToken
        {
            get
            {
                string result = _accessToken;
                // When a connection is connecting or is ever opened, make AccessToken available only if "Persist Security Info" is set to true
                // otherwise, return null
                SqlConnectionString connectionOptions = (SqlConnectionString)UserConnectionOptions;
                return InnerConnection.ShouldHidePassword && connectionOptions != null && !connectionOptions.PersistSecurityInfo ? null : _accessToken;
            }
            set
            {
                // If a connection is connecting or is ever opened, AccessToken cannot be set
                if (!InnerConnection.AllowSetConnectionString)
                {
                    throw ADP.OpenConnectionPropertySet("AccessToken", InnerConnection.State);
                }

                if (value != null)
                {
                    // Check if the usage of AccessToken has any conflict with the keys used in connection string and credential
                    CheckAndThrowOnInvalidCombinationOfConnectionOptionAndAccessToken((SqlConnectionString)ConnectionOptions);
                }

                // Need to call ConnectionString_Set to do proper pool group check
                ConnectionString_Set(new SqlConnectionPoolKey(_connectionString, credential: _credential, accessToken: value));
                _accessToken = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Database/*' />
        public override string Database
        {
            // if the connection is open, we need to ask the inner connection what it's
            // current catalog is because it may have gotten changed, otherwise we can
            // just return what the connection string had.
            get
            {
                SqlInternalConnection innerConnection = (InnerConnection as SqlInternalConnection);
                string result;

                if (null != innerConnection)
                {
                    result = innerConnection.CurrentDatabase;
                }
                else
                {
                    SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                    result = ((null != constr) ? constr.InitialCatalog : SqlConnectionString.DEFAULT.Initial_Catalog);
                }
                return result;
            }
        }

        ///
        /// To indicate the IsSupported flag sent by the server for DNS Caching. This property is for internal testing only.
        ///
        internal string SQLDNSCachingSupportedState
        {
            get
            {
                SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);
                string result;

                if (null != innerConnection)
                {
                    result = innerConnection.IsSQLDNSCachingSupported ? "true" : "false";
                }
                else
                {
                    result = "innerConnection is null!";
                }

                return result;
            }
        }

        ///
        /// To indicate the IsSupported flag sent by the server for DNS Caching before redirection. This property is for internal testing only.
        ///
        internal string SQLDNSCachingSupportedStateBeforeRedirect
        {
            get
            {
                SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);
                string result;

                if (null != innerConnection)
                {
                    result = innerConnection.IsDNSCachingBeforeRedirectSupported ? "true" : "false";
                }
                else
                {
                    result = "innerConnection is null!";
                }

                return result;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/DataSource/*' />
        public override string DataSource
        {
            get
            {
                SqlInternalConnection innerConnection = (InnerConnection as SqlInternalConnection);
                string result;

                if (null != innerConnection)
                {
                    result = innerConnection.CurrentDataSource;
                }
                else
                {
                    SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                    result = ((null != constr) ? constr.DataSource : SqlConnectionString.DEFAULT.Data_Source);
                }
                return result;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/PacketSize/*' />
        public int PacketSize
        {
            // if the connection is open, we need to ask the inner connection what it's
            // current packet size is because it may have gotten changed, otherwise we
            // can just return what the connection string had.
            get
            {
                SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);
                int result;

                if (null != innerConnection)
                {
                    result = innerConnection.PacketSize;
                }
                else
                {
                    SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                    result = ((null != constr) ? constr.PacketSize : SqlConnectionString.DEFAULT.Packet_Size);
                }
                return result;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClientConnectionId/*' />
        public Guid ClientConnectionId
        {
            get
            {
                SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);

                if (null != innerConnection)
                {
                    return innerConnection.ClientConnectionId;
                }
                else
                {
                    Task reconnectTask = _currentReconnectionTask;
                    // Connection closed but previously open should return the correct ClientConnectionId
                    DbConnectionClosedPreviouslyOpened innerConnectionClosed = (InnerConnection as DbConnectionClosedPreviouslyOpened);
                    if ((reconnectTask != null && !reconnectTask.IsCompleted) || null != innerConnectionClosed)
                    {
                        return _originalConnectionId;
                    }
                    return Guid.Empty;
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ServerVersion/*' />
        public override string ServerVersion
        {
            get => GetOpenTdsConnection().ServerVersion;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ServerProcessId/*' />
        public int ServerProcessId
        {
            get => State.Equals(ConnectionState.Open) | State.Equals(ConnectionState.Executing) | State.Equals(ConnectionState.Fetching) ?
                GetOpenTdsConnection().ServerProcessId : 0;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/State/*' />
        public override ConnectionState State
        {
            get
            {
                Task reconnectTask = _currentReconnectionTask;
                if (reconnectTask != null && !reconnectTask.IsCompleted)
                {
                    return ConnectionState.Open;
                }
                return InnerConnection.State;
            }
        }


        internal SqlStatistics Statistics
        {
            get => _statistics;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/WorkstationId/*' />
        public string WorkstationId
        {
            get
            {
                // If not supplied by the user, the default value is the MachineName
                // Note: In Longhorn you'll be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                string result = constr?.WorkstationId ?? Environment.MachineName;
                return result;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Credential/*' />
        public SqlCredential Credential
        {
            get
            {
                SqlCredential result = _credential;

                // When a connection is connecting or is ever opened, make credential available only if "Persist Security Info" is set to true
                //  otherwise, return null
                SqlConnectionString connectionOptions = (SqlConnectionString)UserConnectionOptions;
                if (InnerConnection.ShouldHidePassword && connectionOptions != null && !connectionOptions.PersistSecurityInfo)
                {
                    result = null;
                }

                return result;
            }

            set
            {
                // If a connection is connecting or is ever opened, user id/password cannot be set
                if (!InnerConnection.AllowSetConnectionString)
                {
                    throw ADP.OpenConnectionPropertySet(nameof(Credential), InnerConnection.State);
                }

                // check if the usage of credential has any conflict with the keys used in connection string
                if (value != null)
                {
                    var connectionOptions = (SqlConnectionString)ConnectionOptions;
                    // Check for Credential being used with Authentication=ActiveDirectoryIntegrated | ActiveDirectoryInteractive |
                    // ActiveDirectoryDeviceCodeFlow | ActiveDirectoryManagedIdentity/ActiveDirectoryMSI. Since a different error string is used
                    // for this case in ConnectionString setter vs in Credential setter, check for this error case before calling
                    // CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential, which is common to both setters.
                    if (UsesActiveDirectoryIntegrated(connectionOptions))
                    {
                        throw SQL.SettingCredentialWithIntegratedInvalid();
                    }
                    else if (UsesActiveDirectoryInteractive(connectionOptions))
                    {
                        throw SQL.SettingCredentialWithInteractiveInvalid();
                    }
                    else if (UsesActiveDirectoryDeviceCodeFlow(connectionOptions))
                    {
                        throw SQL.SettingCredentialWithDeviceFlowInvalid();
                    }
                    else if (UsesActiveDirectoryManagedIdentity(connectionOptions))
                    {
                        throw SQL.SettingCredentialWithManagedIdentityInvalid(DbConnectionStringBuilderUtil.ActiveDirectoryManagedIdentityString);
                    }
                    else if (UsesActiveDirectoryMSI(connectionOptions))
                    {
                        throw SQL.SettingCredentialWithManagedIdentityInvalid(DbConnectionStringBuilderUtil.ActiveDirectoryMSIString);
                    }

                    CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential(connectionOptions);
                    if (_accessToken != null)
                    {
                        throw ADP.InvalidMixedUsageOfCredentialAndAccessToken();
                    }
                }

                _credential = value;

                // Need to call ConnectionString_Set to do proper pool group check
                ConnectionString_Set(new SqlConnectionPoolKey(_connectionString, _credential, accessToken: _accessToken));
            }
        }

        // CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential: check if the usage of credential has any conflict
        //  with the keys used in connection string
        //  If there is any conflict, it throws InvalidOperationException
        //  This is used in the setter of ConnectionString and Credential properties.
        private void CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential(SqlConnectionString connectionOptions)
        {
            if (UsesClearUserIdOrPassword(connectionOptions))
            {
                throw ADP.InvalidMixedUsageOfSecureAndClearCredential();
            }

            if (UsesIntegratedSecurity(connectionOptions))
            {
                throw ADP.InvalidMixedUsageOfSecureCredentialAndIntegratedSecurity();
            }
        }

        // CheckAndThrowOnInvalidCombinationOfConnectionOptionAndAccessToken: check if the usage of AccessToken has any conflict
        //  with the keys used in connection string and credential
        //  If there is any conflict, it throws InvalidOperationException
        //  This is to be used setter of ConnectionString and AccessToken properties
        private void CheckAndThrowOnInvalidCombinationOfConnectionOptionAndAccessToken(SqlConnectionString connectionOptions)
        {
            if (UsesClearUserIdOrPassword(connectionOptions))
            {
                throw ADP.InvalidMixedUsageOfAccessTokenAndUserIDPassword();
            }

            if (UsesIntegratedSecurity(connectionOptions))
            {
                throw ADP.InvalidMixedUsageOfAccessTokenAndIntegratedSecurity();
            }

            if (UsesAuthentication(connectionOptions))
            {
                throw ADP.InvalidMixedUsageOfAccessTokenAndAuthentication();
            }

            // Check if the usage of AccessToken has the conflict with credential
            if (_credential != null)
            {
                throw ADP.InvalidMixedUsageOfCredentialAndAccessToken();
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/DbProviderFactory/*' />
        protected override DbProviderFactory DbProviderFactory
        {
            get => SqlClientFactory.Instance;
        }

        // SqlCredential: Pair User Id and password in SecureString which are to be used for SQL authentication

        //
        // PUBLIC EVENTS
        //

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/InfoMessage/*' />
        public event SqlInfoMessageEventHandler InfoMessage;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/FireInfoMessageEventOnUserErrors/*' />
        public bool FireInfoMessageEventOnUserErrors
        {
            get => _fireInfoMessageEventOnUserErrors;
            set => _fireInfoMessageEventOnUserErrors = value;
        }

        // Approx. number of times that the internal connection has been reconnected
        internal int ReconnectCount
        {
            get => _reconnectCount;
        }

        internal bool ForceNewConnection { get; set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/OnStateChange/*' />
        protected override void OnStateChange(StateChangeEventArgs stateChange)
        {
            if (!_suppressStateChangeForReconnection)
            {
                base.OnStateChange(stateChange);
            }
        }

        //
        // PUBLIC METHODS
        //
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransaction2/*' />
        new public SqlTransaction BeginTransaction()
        {
            // this is just a delegate. The actual method tracks executiontime
            return BeginTransaction(System.Data.IsolationLevel.Unspecified, null);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionIso/*' />
        new public SqlTransaction BeginTransaction(System.Data.IsolationLevel iso)
        {
            // this is just a delegate. The actual method tracks executiontime
            return BeginTransaction(iso, null);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionTransactionName/*' />
        public SqlTransaction BeginTransaction(string transactionName)
        {
            // Use transaction names only on the outermost pair of nested
            // BEGIN...COMMIT or BEGIN...ROLLBACK statements.  Transaction names
            // are ignored for nested BEGIN's.  The only way to rollback a nested
            // transaction is to have a save point from a SAVE TRANSACTION call.
            return BeginTransaction(System.Data.IsolationLevel.Unspecified, transactionName);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginDbTransaction/*' />
        [SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive")]
        override protected DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<prov.SqlConnection.BeginDbTransaction|API> {0}, isolationLevel={1}", ObjectID, (int)isolationLevel);
            try
            {
                DbTransaction transaction = BeginTransaction(isolationLevel);

                //   InnerConnection doesn't maintain a ref on the outer connection (this) and
                //   subsequently leaves open the possibility that the outer connection could be GC'ed before the SqlTransaction
                //   is fully hooked up (leaving a DbTransaction with a null connection property). Ensure that this is reachable
                //   until the completion of BeginTransaction with KeepAlive
                GC.KeepAlive(this);

                return transaction;
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionIsoTransactionName/*' />
        public SqlTransaction BeginTransaction(System.Data.IsolationLevel iso, string transactionName)
        {
            WaitForPendingReconnection();
            SqlStatistics statistics = null;
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.BeginTransaction|API> {0}, iso={1}, transactionName='{2}'", ObjectID, (int)iso, transactionName);
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                SqlTransaction transaction;
                bool isFirstAttempt = true;
                do
                {
                    transaction = GetOpenTdsConnection().BeginSqlTransaction(iso, transactionName, isFirstAttempt); // do not reconnect twice
                    Debug.Assert(isFirstAttempt || !transaction.InternalTransaction.ConnectionHasBeenRestored, "Restored connection on non-first attempt");
                    isFirstAttempt = false;
                } while (transaction.InternalTransaction.ConnectionHasBeenRestored);


                //  The GetOpenConnection line above doesn't keep a ref on the outer connection (this),
                //  and it could be collected before the inner connection can hook it to the transaction, resulting in
                //  a transaction with a null connection property.  Use GC.KeepAlive to ensure this doesn't happen.
                GC.KeepAlive(this);

                return transaction;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangeDatabase/*' />
        public override void ChangeDatabase(string database)
        {
            SqlStatistics statistics = null;
            RepairInnerConnection();
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.ChangeDatabase|API|Correlation> ObjectID{0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                InnerConnection.ChangeDatabase(database);
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearAllPools/*' />
        public static void ClearAllPools()
        {
            SqlConnectionFactory.SingletonInstance.ClearAllPools();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearPool/*' />
        public static void ClearPool(SqlConnection connection)
        {
            ADP.CheckArgumentNull(connection, nameof(connection));

            DbConnectionOptions connectionOptions = connection.UserConnectionOptions;
            if (null != connectionOptions)
            {
                SqlConnectionFactory.SingletonInstance.ClearPool(connection);
            }
        }


        private void CloseInnerConnection()
        {
            // CloseConnection() now handles the lock

            // The SqlInternalConnectionTds is set to OpenBusy during close, once this happens the cast below will fail and
            // the command will no longer be cancelable.  It might be desirable to be able to cancel the close operation, but this is
            // outside of the scope of Whidbey RTM.  See (SqlCommand::Cancel) for other lock.
            _originalConnectionId = ClientConnectionId;
            InnerConnection.CloseConnection(this, ConnectionFactory);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Close/*' />
        public override void Close()
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.Close|API> {0}", ObjectID);
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.Close|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
            try
            {
                ConnectionState previousState = State;
                Guid operationId = default(Guid);
                Guid clientConnectionId = default(Guid);

                // during the call to Dispose() there is a redundant call to
                // Close(). because of this, the second time Close() is invoked the
                // connection is already in a closed state. this doesn't seem to be a
                // problem except for logging, as we'll get duplicate Before/After/Error
                // log entries
                if (previousState != ConnectionState.Closed)
                {
                    operationId = s_diagnosticListener.WriteConnectionCloseBefore(this);
                    // we want to cache the ClientConnectionId for After/Error logging, as when the connection
                    // is closed then we will lose this identifier
                    //
                    // note: caching this is only for diagnostics logging purposes
                    clientConnectionId = ClientConnectionId;
                }

                SqlStatistics statistics = null;

                Exception e = null;
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    Task reconnectTask = _currentReconnectionTask;
                    if (reconnectTask != null && !reconnectTask.IsCompleted)
                    {
                        CancellationTokenSource cts = _reconnectionCancellationSource;
                        if (cts != null)
                        {
                            cts.Cancel();
                        }
                        AsyncHelper.WaitForCompletion(reconnectTask, 0, null, rethrowExceptions: false); // we do not need to deal with possible exceptions in reconnection
                        if (State != ConnectionState.Open)
                        {// if we cancelled before the connection was opened
                            OnStateChange(DbConnectionInternal.StateChangeClosed);
                        }
                    }
                    CancelOpenAndWait();
                    CloseInnerConnection();
                    GC.SuppressFinalize(this);

                    if (null != Statistics)
                    {
                        ADP.TimerCurrent(out _statistics._closeTimestamp);
                    }
                }
                catch (Exception ex)
                {
                    e = ex;
                    throw;
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);

                    // we only want to log this if the previous state of the
                    // connection is open, as that's the valid use-case
                    if (previousState != ConnectionState.Closed)
                    {
                        if (e != null)
                        {
                            s_diagnosticListener.WriteConnectionCloseError(operationId, clientConnectionId, this, e);
                        }
                        else
                        {
                            s_diagnosticListener.WriteConnectionCloseAfter(operationId, clientConnectionId, this);
                        }
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateCommand/*' />
        new public SqlCommand CreateCommand()
        {
            return new SqlCommand(null, this);
        }

        private void DisposeMe(bool disposing)
        {
            _credential = null;
            _accessToken = null;

            if (!disposing)
            {
                // For non-pooled connections we need to make sure that if the SqlConnection was not closed,
                // then we release the GCHandle on the stateObject to allow it to be GCed
                // For pooled connections, we will rely on the pool reclaiming the connection
                var innerConnection = (InnerConnection as SqlInternalConnectionTds);
                if ((innerConnection != null) && (!innerConnection.ConnectionOptions.Pooling))
                {
                    var parser = innerConnection.Parser;
                    if ((parser != null) && (parser._physicalStateObj != null))
                    {
                        parser._physicalStateObj.DecrementPendingCallbacks(release: false);
                    }
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Open/*' />
        public override void Open()
        {
            Open(SqlConnectionOverrides.None);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/OpenWithOverrides/*' />
        public void Open(SqlConnectionOverrides overrides)
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.Open|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.Open|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
            try
            {
                Guid operationId = s_diagnosticListener.WriteConnectionOpenBefore(this);

                PrepareStatisticsForNewConnection();

                SqlStatistics statistics = null;

                Exception e = null;
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    if (!TryOpen(null, overrides))
                    {
                        throw ADP.InternalError(ADP.InternalErrorCode.SynchronousConnectReturnedPending);
                    }
                }
                catch (Exception ex)
                {
                    e = ex;
                    throw;
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);

                    if (e != null)
                    {
                        s_diagnosticListener.WriteConnectionOpenError(operationId, this, e);
                    }
                    else
                    {
                        s_diagnosticListener.WriteConnectionOpenAfter(operationId, this);
                    }
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        internal void RegisterWaitingForReconnect(Task waitingTask)
        {
            if (((SqlConnectionString)ConnectionOptions).MARS)
            {
                return;
            }
            Interlocked.CompareExchange(ref _asyncWaitingForReconnection, waitingTask, null);
            if (_asyncWaitingForReconnection != waitingTask)
            { // somebody else managed to register
                throw SQL.MARSUnsupportedOnConnection();
            }
        }

        private async Task ReconnectAsync(int timeout)
        {
            try
            {
                long commandTimeoutExpiration = 0;
                if (timeout > 0)
                {
                    commandTimeoutExpiration = ADP.TimerCurrent() + ADP.TimerFromSeconds(timeout);
                }
                CancellationTokenSource cts = new CancellationTokenSource();
                _reconnectionCancellationSource = cts;
                CancellationToken ctoken = cts.Token;
                int retryCount = _connectRetryCount; // take a snapshot: could be changed by modifying the connection string
                for (int attempt = 0; attempt < retryCount; attempt++)
                {
                    if (ctoken.IsCancellationRequested)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.ReconnectAsync|INFO> Original ClientConnectionID: {0} - reconnection cancelled.", _originalConnectionId);
                        return;
                    }
                    try
                    {
                        try
                        {
                            ForceNewConnection = true;
                            await OpenAsync(ctoken).ConfigureAwait(false);
                            // On success, increment the reconnect count - we don't really care if it rolls over since it is approx.
                            _reconnectCount = unchecked(_reconnectCount + 1);
#if DEBUG
                            Debug.Assert(_recoverySessionData._debugReconnectDataApplied, "Reconnect data was not applied !");
#endif
                        }
                        finally
                        {
                            ForceNewConnection = false;
                        }
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.ReconnectIfNeeded|INFO> Reconnection succeeded.  ClientConnectionID {0} -> {1}", _originalConnectionId, ClientConnectionId);
                        return;
                    }
                    catch (SqlException e)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.ReconnectAsyncINFO> Original ClientConnectionID {0} - reconnection attempt failed error {1}", _originalConnectionId, e.Message);
                        if (attempt == retryCount - 1)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.ReconnectAsync|INFO> Original ClientConnectionID {0} - give up reconnection", _originalConnectionId);
                            throw SQL.CR_AllAttemptsFailed(e, _originalConnectionId);
                        }
                        if (timeout > 0 && ADP.TimerRemaining(commandTimeoutExpiration) < ADP.TimerFromSeconds(ConnectRetryInterval))
                        {
                            throw SQL.CR_NextAttemptWillExceedQueryTimeout(e, _originalConnectionId);
                        }
                    }
                    await Task.Delay(1000 * ConnectRetryInterval, ctoken).ConfigureAwait(false);
                }
            }
            finally
            {
                _recoverySessionData = null;
                _suppressStateChangeForReconnection = false;
            }
            Debug.Fail("Should not reach this point");
        }

        internal Task ValidateAndReconnect(Action beforeDisconnect, int timeout)
        {
            Task runningReconnect = _currentReconnectionTask;
            // This loop in the end will return not completed reconnect task or null
            while (runningReconnect != null && runningReconnect.IsCompleted)
            {
                // clean current reconnect task (if it is the same one we checked
                Interlocked.CompareExchange<Task>(ref _currentReconnectionTask, null, runningReconnect);
                // make sure nobody started new task (if which case we did not clean it)
                runningReconnect = _currentReconnectionTask;
            }
            if (runningReconnect == null)
            {
                if (_connectRetryCount > 0)
                {
                    SqlInternalConnectionTds tdsConn = GetOpenTdsConnection();
                    if (tdsConn._sessionRecoveryAcknowledged)
                    {
                        TdsParserStateObject stateObj = tdsConn.Parser._physicalStateObj;
                        if (!stateObj.ValidateSNIConnection())
                        {
                            if (tdsConn.Parser._sessionPool != null)
                            {
                                if (tdsConn.Parser._sessionPool.ActiveSessionsCount > 0)
                                {
                                    // >1 MARS session
                                    if (beforeDisconnect != null)
                                    {
                                        beforeDisconnect();
                                    }
                                    OnError(SQL.CR_UnrecoverableClient(ClientConnectionId), true, null);
                                }
                            }
                            SessionData cData = tdsConn.CurrentSessionData;
                            cData.AssertUnrecoverableStateCountIsCorrect();
                            if (cData._unrecoverableStatesCount == 0)
                            {
                                bool callDisconnect = false;

                                if (_reconnectLock is null)
                                {
                                    Interlocked.CompareExchange(ref _reconnectLock, new object(), null);
                                }

                                lock (_reconnectLock)
                                {
                                    tdsConn.CheckEnlistedTransactionBinding();
                                    runningReconnect = _currentReconnectionTask; // double check after obtaining the lock
                                    if (runningReconnect == null)
                                    {
                                        if (cData._unrecoverableStatesCount == 0)
                                        {
                                            // could change since the first check, but now is stable since connection is know to be broken
                                            _originalConnectionId = ClientConnectionId;
                                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.ReconnectIfNeeded|INFO> Connection ClientConnectionID {0} is invalid, reconnecting", _originalConnectionId);
                                            _recoverySessionData = cData;
                                            if (beforeDisconnect != null)
                                            {
                                                beforeDisconnect();
                                            }
                                            try
                                            {
                                                _suppressStateChangeForReconnection = true;
                                                tdsConn.DoomThisConnection();
                                            }
                                            catch (SqlException)
                                            {
                                            }
                                            // use Task.Factory.StartNew with state overload instead of Task.Run to avoid anonymous closure context capture in method scope and avoid the unneeded allocation
                                            runningReconnect = Task.Factory.StartNew(state => ReconnectAsync((int)state), timeout, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
                                            // if current reconnect is not null, somebody already started reconnection task - some kind of race condition
                                            Debug.Assert(_currentReconnectionTask == null, "Duplicate reconnection tasks detected");
                                            _currentReconnectionTask = runningReconnect;
                                        }
                                    }
                                    else
                                    {
                                        callDisconnect = true;
                                    }
                                }
                                if (callDisconnect && beforeDisconnect != null)
                                {
                                    beforeDisconnect();
                                }
                            }
                            else
                            {
                                if (beforeDisconnect != null)
                                {
                                    beforeDisconnect();
                                }
                                OnError(SQL.CR_UnrecoverableServer(ClientConnectionId), true, null);
                            }
                        } // ValidateSNIConnection
                    } // sessionRecoverySupported
                } // connectRetryCount>0
            }
            else
            { // runningReconnect = null
                if (beforeDisconnect != null)
                {
                    beforeDisconnect();
                }
            }
            return runningReconnect;
        }

        // this is straightforward, but expensive method to do connection resiliency - it take locks and all preparations as for TDS request
        partial void RepairInnerConnection()
        {
            WaitForPendingReconnection();
            if (_connectRetryCount == 0)
            {
                return;
            }
            SqlInternalConnectionTds tdsConn = InnerConnection as SqlInternalConnectionTds;
            if (tdsConn != null)
            {
                tdsConn.ValidateConnectionForExecute(null);
                tdsConn.GetSessionAndReconnectIfNeeded((SqlConnection)this);
            }
        }

        private void WaitForPendingReconnection()
        {
            Task reconnectTask = _currentReconnectionTask;
            if (reconnectTask != null && !reconnectTask.IsCompleted)
            {
                AsyncHelper.WaitForCompletion(reconnectTask, 0, null, rethrowExceptions: false);
            }
        }

        private void CancelOpenAndWait()
        {
            // copy from member to avoid changes by background thread
            var completion = _currentCompletion;
            if (completion != null)
            {
                completion.Item1.TrySetCanceled();
                ((IAsyncResult)completion.Item2).AsyncWaitHandle.WaitOne();
            }
            Debug.Assert(_currentCompletion == null, "After waiting for an async call to complete, there should be no completion source");
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/OpenAsync/*' />
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            long scopeID = SqlClientEventSource.Log.TryPoolerScopeEnterEvent("<sc.SqlConnection.OpenAsync|API> {0}", ObjectID);
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.OpenAsync|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
            try
            {
                Guid operationId = s_diagnosticListener.WriteConnectionOpenBefore(this);

                PrepareStatisticsForNewConnection();

                SqlStatistics statistics = null;
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    System.Transactions.Transaction transaction = ADP.GetCurrentTransaction();
                    TaskCompletionSource<DbConnectionInternal> completion = new TaskCompletionSource<DbConnectionInternal>(transaction);
                    TaskCompletionSource<object> result = new TaskCompletionSource<object>(state: this);

                    if (s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterOpenConnection) ||
                        s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlErrorOpenConnection))
                    {
                        result.Task.ContinueWith(
                            continuationAction: s_openAsyncComplete,
                            state: operationId, // connection is passed in TaskCompletionSource async state
                            scheduler: TaskScheduler.Default
                        );
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.SetCanceled();
                        return result.Task;
                    }


                    bool completed;

                    try
                    {
                        completed = TryOpen(completion);
                    }
                    catch (Exception e)
                    {
                        s_diagnosticListener.WriteConnectionOpenError(operationId, this, e);
                        result.SetException(e);
                        return result.Task;
                    }

                    if (completed)
                    {
                        result.SetResult(null);
                    }
                    else
                    {
                        CancellationTokenRegistration registration = new CancellationTokenRegistration();
                        if (cancellationToken.CanBeCanceled)
                        {
                            registration = cancellationToken.Register(s_openAsyncCancel, completion);
                        }
                        OpenAsyncRetry retry = new OpenAsyncRetry(this, completion, result, registration);
                        _currentCompletion = new Tuple<TaskCompletionSource<DbConnectionInternal>, Task>(completion, result.Task);
                        completion.Task.ContinueWith(retry.Retry, TaskScheduler.Default);
                        return result.Task;
                    }

                    return result.Task;
                }
                catch (Exception ex)
                {
                    s_diagnosticListener.WriteConnectionOpenError(operationId, this, ex);
                    throw;
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                }
            }
            finally
            {
                SqlClientEventSource.Log.TryPoolerScopeLeaveEvent(scopeID);
            }
        }

        private static void OpenAsyncComplete(Task<object> task, object state)
        {
            Guid operationId = (Guid)state;
            SqlConnection connection = (SqlConnection)task.AsyncState;
            if (task.Exception != null)
            {
                s_diagnosticListener.WriteConnectionOpenError(operationId, connection, task.Exception);
            }
            else
            {
                s_diagnosticListener.WriteConnectionOpenAfter(operationId, connection);
            }
        }

        private static void OpenAsyncCancel(object state)
        {
            ((TaskCompletionSource<DbConnectionInternal>)state).TrySetCanceled();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/GetSchema2/*' />
        public override DataTable GetSchema()
        {
            return GetSchema(DbMetaDataCollectionNames.MetaDataCollections, null);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/GetSchemaCollectionName/*' />
        public override DataTable GetSchema(string collectionName)
        {
            return GetSchema(collectionName, null);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/GetSchemaCollectionNameRestrictionValues/*' />
        public override DataTable GetSchema(string collectionName, string[] restrictionValues)
        {
            return InnerConnection.GetSchema(ConnectionFactory, PoolGroup, this, collectionName, restrictionValues);
        }

        private class OpenAsyncRetry
        {
            private SqlConnection _parent;
            private TaskCompletionSource<DbConnectionInternal> _retry;
            private TaskCompletionSource<object> _result;
            private CancellationTokenRegistration _registration;

            public OpenAsyncRetry(SqlConnection parent, TaskCompletionSource<DbConnectionInternal> retry, TaskCompletionSource<object> result, CancellationTokenRegistration registration)
            {
                _parent = parent;
                _retry = retry;
                _result = result;
                _registration = registration;
            }

            internal void Retry(Task<DbConnectionInternal> retryTask)
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.OpenAsyncRetry|Info> {0}", _parent.ObjectID);
                _registration.Dispose();
                try
                {
                    SqlStatistics statistics = null;
                    try
                    {
                        statistics = SqlStatistics.StartTimer(_parent.Statistics);

                        if (retryTask.IsFaulted)
                        {
                            Exception e = retryTask.Exception.InnerException;
                            _parent.CloseInnerConnection();
                            _parent._currentCompletion = null;
                            _result.SetException(retryTask.Exception.InnerException);
                        }
                        else if (retryTask.IsCanceled)
                        {
                            _parent.CloseInnerConnection();
                            _parent._currentCompletion = null;
                            _result.SetCanceled();
                        }
                        else
                        {
                            bool result;
                            // protect continuation from races with close and cancel
                            lock (_parent.InnerConnection)
                            {
                                result = _parent.TryOpen(_retry);
                            }
                            if (result)
                            {
                                _parent._currentCompletion = null;
                                _result.SetResult(null);
                            }
                            else
                            {
                                _parent.CloseInnerConnection();
                                _parent._currentCompletion = null;
                                _result.SetException(ADP.ExceptionWithStackTrace(ADP.InternalError(ADP.InternalErrorCode.CompletedConnectReturnedPending)));
                            }
                        }
                    }
                    finally
                    {
                        SqlStatistics.StopTimer(statistics);
                    }
                }
                catch (Exception e)
                {
                    _parent.CloseInnerConnection();
                    _parent._currentCompletion = null;
                    _result.SetException(e);
                }
            }
        }

        private void PrepareStatisticsForNewConnection()
        {
            if (StatisticsEnabled ||
                s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterExecuteCommand) ||
                s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterOpenConnection))
            {
                if (null == _statistics)
                {
                    _statistics = new SqlStatistics();
                }
                else
                {
                    _statistics.ContinueOnNewConnection();
                }
            }
        }

        private bool TryOpen(TaskCompletionSource<DbConnectionInternal> retry, SqlConnectionOverrides overrides = SqlConnectionOverrides.None)
        {
            SqlConnectionString connectionOptions = (SqlConnectionString)ConnectionOptions;

            if (_cultureCheckState != CultureCheckState.Standard)
            {
                // .NET Core 2.0 and up supports a Globalization Invariant Mode to reduce the size of
                // required libraries for applications which don't need globalization support. SqlClient
                // requires those libraries for core functionality and will throw exceptions later if they
                // are not present. Throwing on open with a meaningful message helps identify the issue.
                if (_cultureCheckState == CultureCheckState.Unknown)
                {
                    _cultureCheckState = CultureInfo.GetCultureInfo("en-US").EnglishName.Contains("Invariant") ? CultureCheckState.Invariant : CultureCheckState.Standard;
                }
                if (_cultureCheckState == CultureCheckState.Invariant)
                {
                    throw SQL.GlobalizationInvariantModeNotSupported();
                }
            }

            _applyTransientFaultHandling = (!overrides.HasFlag(SqlConnectionOverrides.OpenWithoutRetry) && retry == null && connectionOptions != null && connectionOptions.ConnectRetryCount > 0);

            if (connectionOptions != null &&
                (connectionOptions.Authentication == SqlAuthenticationMethod.SqlPassword ||
                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryPassword ||
                    connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryServicePrincipal) &&
                (!connectionOptions.HasUserIdKeyword || !connectionOptions.HasPasswordKeyword) &&
                _credential == null)
            {
                throw SQL.CredentialsNotProvided(connectionOptions.Authentication);
            }

            if (ForceNewConnection)
            {
                if (!InnerConnection.TryReplaceConnection(this, ConnectionFactory, retry, UserConnectionOptions))
                {
                    return false;
                }
            }
            else
            {
                if (!InnerConnection.TryOpenConnection(this, ConnectionFactory, retry, UserConnectionOptions))
                {
                    return false;
                }
            }
            // does not require GC.KeepAlive(this) because of ReRegisterForFinalize below.

            // Set future transient fault handling based on connection options
            _applyTransientFaultHandling = (retry == null && connectionOptions != null && connectionOptions.ConnectRetryCount > 0);

            var tdsInnerConnection = (SqlInternalConnectionTds)InnerConnection;

            Debug.Assert(tdsInnerConnection.Parser != null, "Where's the parser?");

            if (!tdsInnerConnection.ConnectionOptions.Pooling)
            {
                // For non-pooled connections, we need to make sure that the finalizer does actually run to avoid leaking SNI handles
                GC.ReRegisterForFinalize(this);
            }

            // The _statistics can change with StatisticsEnabled. Copying to a local variable before checking for a null value.
            SqlStatistics statistics = _statistics;
            if (StatisticsEnabled ||
                (s_diagnosticListener.IsEnabled(SqlClientDiagnosticListenerExtensions.SqlAfterExecuteCommand) && statistics != null))
            {
                ADP.TimerCurrent(out _statistics._openTimestamp);
                tdsInnerConnection.Parser.Statistics = _statistics;
            }
            else
            {
                tdsInnerConnection.Parser.Statistics = null;
                _statistics = null; // in case of previous Open/Close/reset_CollectStats sequence
            }

            return true;
        }


        //
        // INTERNAL PROPERTIES
        //

        internal bool HasLocalTransaction
        {
            get
            {
                return GetOpenTdsConnection().HasLocalTransaction;
            }
        }

        internal bool HasLocalTransactionFromAPI
        {
            get
            {
                Task reconnectTask = _currentReconnectionTask;
                if (reconnectTask != null && !reconnectTask.IsCompleted)
                {
                    return false; //we will not go into reconnection if we are inside the transaction
                }
                return GetOpenTdsConnection().HasLocalTransactionFromAPI;
            }
        }


        internal bool IsKatmaiOrNewer
        {
            get
            {
                if (_currentReconnectionTask != null)
                { // holds true even if task is completed
                    return true; // if CR is enabled, connection, if established, will be Katmai+
                }
                return GetOpenTdsConnection().IsKatmaiOrNewer;
            }
        }

        internal TdsParser Parser
        {
            get
            {
                SqlInternalConnectionTds tdsConnection = GetOpenTdsConnection();
                return tdsConnection.Parser;
            }
        }


        //
        // INTERNAL METHODS
        //

        internal void ValidateConnectionForExecute(string method, SqlCommand command)
        {
            Task asyncWaitingForReconnection = _asyncWaitingForReconnection;
            if (asyncWaitingForReconnection != null)
            {
                if (!asyncWaitingForReconnection.IsCompleted)
                {
                    throw SQL.MARSUnsupportedOnConnection();
                }
                else
                {
                    Interlocked.CompareExchange(ref _asyncWaitingForReconnection, null, asyncWaitingForReconnection);
                }
            }
            if (_currentReconnectionTask != null)
            {
                Task currentReconnectionTask = _currentReconnectionTask;
                if (currentReconnectionTask != null && !currentReconnectionTask.IsCompleted)
                {
                    return; // execution will wait for this task later
                }
            }
            SqlInternalConnectionTds innerConnection = GetOpenTdsConnection(method);
            innerConnection.ValidateConnectionForExecute(command);
        }

        // Surround name in brackets and then escape any end bracket to protect against SQL Injection.
        // NOTE: if the user escapes it themselves it will not work, but this was the case in V1 as well
        // as native OleDb and Odbc.
        internal static string FixupDatabaseTransactionName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return SqlServerEscapeHelper.EscapeIdentifier(name);
            }
            else
            {
                return name;
            }
        }

        // If wrapCloseInAction is defined, then the action it defines will be run with the connection close action passed in as a parameter
        // The close action also supports being run asynchronously
        internal void OnError(SqlException exception, bool breakConnection, Action<Action> wrapCloseInAction)
        {
            Debug.Assert(exception != null && exception.Errors.Count != 0, "SqlConnection: OnError called with null or empty exception!");


            if (breakConnection && (ConnectionState.Open == State))
            {
                if (wrapCloseInAction != null)
                {
                    int capturedCloseCount = _closeCount;

                    Action closeAction = () =>
                    {
                        if (capturedCloseCount == _closeCount)
                        {
                            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.OnError|INFO> {0}, Connection broken.", ObjectID);
                            Close();
                        }
                    };

                    wrapCloseInAction(closeAction);
                }
                else
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.OnError|INFO> {0}, Connection broken.", ObjectID);
                    Close();
                }
            }

            if (exception.Class >= TdsEnums.MIN_ERROR_CLASS)
            {
                // It is an error, and should be thrown.  Class of TdsEnums.MIN_ERROR_CLASS or above is an error,
                // below TdsEnums.MIN_ERROR_CLASS denotes an info message.
                throw exception;
            }
            else
            {
                // If it is a class < TdsEnums.MIN_ERROR_CLASS, it is a warning collection - so pass to handler
                this.OnInfoMessage(new SqlInfoMessageEventArgs(exception));
            }
        }

        //
        // PRIVATE METHODS
        //


        internal SqlInternalConnectionTds GetOpenTdsConnection()
        {
            SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);
            if (null == innerConnection)
            {
                throw ADP.ClosedConnectionError();
            }
            return innerConnection;
        }

        internal SqlInternalConnectionTds GetOpenTdsConnection(string method)
        {
            SqlInternalConnectionTds innerConnection = (InnerConnection as SqlInternalConnectionTds);
            if (null == innerConnection)
            {
                throw ADP.OpenConnectionRequired(method, InnerConnection.State);
            }
            return innerConnection;
        }

        internal void OnInfoMessage(SqlInfoMessageEventArgs imevent)
        {
            bool notified;
            OnInfoMessage(imevent, out notified);
        }

        internal void OnInfoMessage(SqlInfoMessageEventArgs imevent, out bool notified)
        {
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.OnInfoMessage|API|INFO> {0}, Message='{1}'", ObjectID, imevent.Message);
            SqlInfoMessageEventHandler handler = InfoMessage;
            if (null != handler)
            {
                notified = true;
                try
                {
                    handler(this, imevent);
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableOrSecurityExceptionType(e))
                    {
                        throw;
                    }
                }
            }
            else
            {
                notified = false;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringNewPassword/*' />
        public static void ChangePassword(string connectionString, string newPassword)
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.ChangePassword|API>");
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.ChangePassword|API|Correlation> ActivityID {0}", ActivityCorrelator.Current);
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw SQL.ChangePasswordArgumentMissing(nameof(newPassword));
                }
                if (string.IsNullOrEmpty(newPassword))
                {
                    throw SQL.ChangePasswordArgumentMissing(nameof(newPassword));
                }
                if (TdsEnums.MAXLEN_NEWPASSWORD < newPassword.Length)
                {
                    throw ADP.InvalidArgumentLength(nameof(newPassword), TdsEnums.MAXLEN_NEWPASSWORD);
                }

                SqlConnectionPoolKey key = new SqlConnectionPoolKey(connectionString, credential: null, accessToken: null);

                SqlConnectionString connectionOptions = SqlConnectionFactory.FindSqlConnectionOptions(key);
                if (connectionOptions.IntegratedSecurity)
                {
                    throw SQL.ChangePasswordConflictsWithSSPI();
                }
                if (!string.IsNullOrEmpty(connectionOptions.AttachDBFilename))
                {
                    throw SQL.ChangePasswordUseOfUnallowedKey(SqlConnectionString.KEY.AttachDBFilename);
                }

                ChangePassword(connectionString, connectionOptions, null, newPassword, null);
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringCredentialNewSecurePassword/*' />
        public static void ChangePassword(string connectionString, SqlCredential credential, SecureString newSecurePassword)
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.ChangePassword|API>");
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.ChangePassword|API|Correlation> ActivityID {0}", ActivityCorrelator.Current);
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw SQL.ChangePasswordArgumentMissing(nameof(connectionString));
                }

                // check credential; not necessary to check the length of password in credential as the check is done by SqlCredential class
                if (credential == null)
                {
                    throw SQL.ChangePasswordArgumentMissing(nameof(credential));
                }

                if (newSecurePassword == null || newSecurePassword.Length == 0)
                {
                    throw SQL.ChangePasswordArgumentMissing(nameof(newSecurePassword));
                }

                if (!newSecurePassword.IsReadOnly())
                {
                    throw ADP.MustBeReadOnly(nameof(newSecurePassword));
                }

                if (TdsEnums.MAXLEN_NEWPASSWORD < newSecurePassword.Length)
                {
                    throw ADP.InvalidArgumentLength(nameof(newSecurePassword), TdsEnums.MAXLEN_NEWPASSWORD);
                }

                SqlConnectionPoolKey key = new SqlConnectionPoolKey(connectionString, credential, accessToken: null);

                SqlConnectionString connectionOptions = SqlConnectionFactory.FindSqlConnectionOptions(key);

                // Check for connection string values incompatible with SqlCredential
                if (!string.IsNullOrEmpty(connectionOptions.UserID) || !string.IsNullOrEmpty(connectionOptions.Password))
                {
                    throw ADP.InvalidMixedArgumentOfSecureAndClearCredential();
                }

                if (connectionOptions.IntegratedSecurity || connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
                {
                    throw SQL.ChangePasswordConflictsWithSSPI();
                }

                if (!string.IsNullOrEmpty(connectionOptions.AttachDBFilename))
                {
                    throw SQL.ChangePasswordUseOfUnallowedKey(SqlConnectionString.KEY.AttachDBFilename);
                }

                ChangePassword(connectionString, connectionOptions, credential, null, newSecurePassword);
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        private static void ChangePassword(string connectionString, SqlConnectionString connectionOptions, SqlCredential credential, string newPassword, SecureString newSecurePassword)
        {
            // note: This is the only case where we directly construct the internal connection, passing in the new password.
            // Normally we would simply create a regular connection and open it, but there is no other way to pass the
            // new password down to the constructor. This would have an unwanted impact on the connection pool.
            SqlInternalConnectionTds con = null;
            try
            {
                con = new SqlInternalConnectionTds(null, connectionOptions, credential, null, newPassword, newSecurePassword, false);
            }
            finally
            {
                if (con != null)
                    con.Dispose();
            }
            SqlConnectionPoolKey key = new SqlConnectionPoolKey(connectionString, credential, accessToken: null);

            SqlConnectionFactory.SingletonInstance.ClearPool(key);
        }

        //
        // SQL DEBUGGING SUPPORT
        //

        // this only happens once per connection
        // SxS: using named file mapping APIs

        internal Task<T> RegisterForConnectionCloseNotification<T>(Task<T> outerTask, object value, int tag)
        {
            // Connection exists,  schedule removal, will be added to ref collection after calling ValidateAndReconnect
            return outerTask.ContinueWith(
                continuationFunction: (task, state) =>
                {
                    Tuple<SqlConnection, object> parameters = (Tuple<SqlConnection, object>)state;
                    SqlConnection connection = parameters.Item1;
                    object obj = parameters.Item2;

                    connection.RemoveWeakReference(obj);
                    return task;
                },
                state: Tuple.Create(this, value),
                scheduler: TaskScheduler.Default
           ).Unwrap();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ResetStatistics/*' />
        public void ResetStatistics()
        {
            if (null != Statistics)
            {
                Statistics.Reset();
                if (ConnectionState.Open == State)
                {
                    // update timestamp;
                    ADP.TimerCurrent(out _statistics._openTimestamp);
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RetrieveStatistics/*' />
        public IDictionary RetrieveStatistics()
        {
            if (null != Statistics)
            {
                UpdateStatistics();
                return Statistics.GetDictionary();
            }
            else
            {
                return new SqlStatistics().GetDictionary();
            }
        }

        private void UpdateStatistics()
        {
            if (ConnectionState.Open == State)
            {
                // update timestamp
                ADP.TimerCurrent(out _statistics._closeTimestamp);
            }
            // delegate the rest of the work to the SqlStatistics class
            Statistics.UpdateStatistics();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RetrieveInternalInfo/*' />
        public IDictionary<string, object> RetrieveInternalInfo()
        {
            IDictionary<string, object> internalDictionary = new Dictionary<string, object>();

            internalDictionary.Add("SQLDNSCachingSupportedState", SQLDNSCachingSupportedState);
            internalDictionary.Add("SQLDNSCachingSupportedStateBeforeRedirect", SQLDNSCachingSupportedStateBeforeRedirect);

            return internalDictionary;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/System.ICloneable.Clone/*' />
        object ICloneable.Clone() => new SqlConnection(this);

        private void CopyFrom(SqlConnection connection)
        {
            ADP.CheckArgumentNull(connection, nameof(connection));
            _userConnectionOptions = connection.UserConnectionOptions;
            _poolGroup = connection.PoolGroup;

            if (DbConnectionClosedNeverOpened.SingletonInstance == connection._innerConnection)
            {
                _innerConnection = DbConnectionClosedNeverOpened.SingletonInstance;
            }
            else
            {
                _innerConnection = DbConnectionClosedPreviouslyOpened.SingletonInstance;
            }
        }

        // UDT SUPPORT
        private Assembly ResolveTypeAssembly(AssemblyName asmRef, bool throwOnError)
        {
            Debug.Assert(TypeSystemAssemblyVersion != null, "TypeSystemAssembly should be set !");
            if (string.Equals(asmRef.Name, "Microsoft.SqlServer.Types", StringComparison.OrdinalIgnoreCase))
            {
                if (asmRef.Version != TypeSystemAssemblyVersion && SqlClientEventSource.Log.IsTraceEnabled())
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.ResolveTypeAssembly> SQL CLR type version change: Server sent {0}, client will instantiate {1}", asmRef.Version, TypeSystemAssemblyVersion);
                }
                asmRef.Version = TypeSystemAssemblyVersion;
            }
            try
            {
                return Assembly.Load(asmRef);
            }
            catch (Exception e)
            {
                if (throwOnError || !ADP.IsCatchableExceptionType(e))
                {
                    throw;
                }
                else
                {
                    return null;
                };
            }
        }

        internal void CheckGetExtendedUDTInfo(SqlMetaDataPriv metaData, bool fThrow)
        {
            if (metaData.udt?.Type == null)
            { // If null, we have not obtained extended info.
                Debug.Assert(!string.IsNullOrEmpty(metaData.udt?.AssemblyQualifiedName), "Unexpected state on GetUDTInfo");
                // Parameter throwOnError determines whether exception from Assembly.Load is thrown.
                metaData.udt.Type =
                    Type.GetType(typeName: metaData.udt.AssemblyQualifiedName, assemblyResolver: asmRef => ResolveTypeAssembly(asmRef, fThrow), typeResolver: null, throwOnError: fThrow);

                if (fThrow && metaData.udt.Type == null)
                {
                    throw SQL.UDTUnexpectedResult(metaData.udt.AssemblyQualifiedName);
                }
            }
        }

        internal object GetUdtValue(object value, SqlMetaDataPriv metaData, bool returnDBNull)
        {
            if (returnDBNull && ADP.IsNull(value))
            {
                return DBNull.Value;
            }

            object o = null;

            // Since the serializer doesn't handle nulls...
            if (ADP.IsNull(value))
            {
                Type t = metaData.udt?.Type;
                Debug.Assert(t != null, "Unexpected null of udtType on GetUdtValue!");
                o = t.InvokeMember("Null", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Static, null, null, Array.Empty<object>(), CultureInfo.InvariantCulture);
                Debug.Assert(o != null);
                return o;
            }
            else
            {

                MemoryStream stm = new MemoryStream((byte[])value);

                o = SerializationHelperSql9.Deserialize(stm, metaData.udt?.Type);

                Debug.Assert(o != null, "object could NOT be created");
                return o;
            }
        }

        internal byte[] GetBytes(object o)
        {
            Format format = Format.Native;
            return GetBytes(o, out format, out int maxSize);
        }

        internal byte[] GetBytes(object o, out Format format, out int maxSize)
        {
            SqlUdtInfo attr = GetInfoFromType(o.GetType());
            maxSize = attr.MaxByteSize;
            format = attr.SerializationFormat;

            if (maxSize < -1 || maxSize >= ushort.MaxValue)
            {
                throw new InvalidOperationException(o.GetType() + ": invalid Size");
            }

            byte[] retval;

            using (MemoryStream stm = new MemoryStream(maxSize < 0 ? 0 : maxSize))
            {
                SerializationHelperSql9.Serialize(stm, o);
                retval = stm.ToArray();
            }
            return retval;
        }

        private SqlUdtInfo GetInfoFromType(Type t)
        {
            Debug.Assert(t != null, "Type object cant be NULL");
            Type orig = t;
            do
            {
                SqlUdtInfo attr = SqlUdtInfo.TryGetFromType(t);
                if (attr != null)
                {
                    return attr;
                }
                else
                {
                    t = t.BaseType;
                }
            }
            while (t != null);

            throw SQL.UDTInvalidSqlType(orig.AssemblyQualifiedName);
        }
    }
}
