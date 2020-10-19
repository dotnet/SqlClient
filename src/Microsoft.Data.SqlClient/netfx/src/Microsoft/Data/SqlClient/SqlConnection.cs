// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Server;

[assembly: InternalsVisibleTo("System.Data.DataSetExtensions, PublicKey=" + Microsoft.Data.SqlClient.AssemblyRef.EcmaPublicKeyFull)] // DevDiv Bugs 92166
namespace Microsoft.Data.SqlClient
{
    using System.Diagnostics.Tracing;
    using Microsoft.Data.Common;

    /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/SqlConnection/*' />
    [DefaultEvent("InfoMessage")]
    public sealed partial class SqlConnection : DbConnection, ICloneable
    {

        internal bool ForceNewConnection
        {
            get;
            set;
        }

        internal bool _suppressStateChangeForReconnection = false; // Do not use for anything else ! Value will be overwritten by CR process

        static private readonly object EventInfoMessage = new object();

        // System column encryption key store providers are added by default
        static private readonly Dictionary<string, SqlColumnEncryptionKeyStoreProvider> _SystemColumnEncryptionKeyStoreProviders
            = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
        {
            {SqlColumnEncryptionCertificateStoreProvider.ProviderName, new SqlColumnEncryptionCertificateStoreProvider()},
            {SqlColumnEncryptionCngProvider.ProviderName, new SqlColumnEncryptionCngProvider()},
            {SqlColumnEncryptionCspProvider.ProviderName, new SqlColumnEncryptionCspProvider()}
        };

        /// <summary>
        /// Custom provider list should be provided by the user. We shallow copy the user supplied dictionary into a ReadOnlyDictionary.
        /// Custom provider list can only supplied once per application.
        /// </summary>
        static private ReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> _CustomColumnEncryptionKeyStoreProviders;

        // Lock to control setting of _CustomColumnEncryptionKeyStoreProviders
        static private readonly Object _CustomColumnEncryptionKeyProvidersLock = new Object();

        /// <summary>
        /// Dictionary object holding trusted key paths for various SQL Servers.
        /// Key to the dictionary is a SQL Server Name
        /// IList contains a list of trusted key paths.
        /// </summary>
        static private readonly ConcurrentDictionary<string, IList<string>> _ColumnEncryptionTrustedMasterKeyPaths
            = new ConcurrentDictionary<string, IList<string>>(concurrencyLevel: 4 * Environment.ProcessorCount /* default value in ConcurrentDictionary*/,
                                                            capacity: 1,
                                                            comparer: StringComparer.OrdinalIgnoreCase);

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionTrustedMasterKeyPaths/*' />
        [
        DefaultValue(null),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.TCE_SqlConnection_TrustedColumnMasterKeyPaths),
        ]
        static public IDictionary<string, IList<string>> ColumnEncryptionTrustedMasterKeyPaths
        {
            get
            {
                return _ColumnEncryptionTrustedMasterKeyPaths;
            }
        }

        /// <summary>
        /// Defines whether query metadata caching is enabled.
        /// </summary>
        static private bool _ColumnEncryptionQueryMetadataCacheEnabled = true;

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionQueryMetadataCacheEnabled/*' />
        [
        DefaultValue(null),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.TCE_SqlConnection_ColumnEncryptionQueryMetadataCacheEnabled),
        ]
        static public bool ColumnEncryptionQueryMetadataCacheEnabled
        {
            get
            {
                return _ColumnEncryptionQueryMetadataCacheEnabled;
            }
            set
            {
                _ColumnEncryptionQueryMetadataCacheEnabled = value;
            }
        }

        /// <summary>
        /// Defines whether query metadata caching is enabled.
        /// </summary>
        static private TimeSpan _ColumnEncryptionKeyCacheTtl = TimeSpan.FromHours(2);

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ColumnEncryptionKeyCacheTtl/*' />
        [
        DefaultValue(null),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.TCE_SqlConnection_ColumnEncryptionKeyCacheTtl),
        ]
        static public TimeSpan ColumnEncryptionKeyCacheTtl
        {
            get
            {
                return _ColumnEncryptionKeyCacheTtl;
            }
            set
            {
                _ColumnEncryptionKeyCacheTtl = value;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RegisterColumnEncryptionKeyStoreProviders/*' />
        static public void RegisterColumnEncryptionKeyStoreProviders(IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders)
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

        private SqlDebugContext _sdc;   // SQL Debugging support

        private bool _AsyncCommandInProgress;

        // SQLStatistics support
        internal SqlStatistics _statistics;
        private bool _collectstats;

        private bool _fireInfoMessageEventOnUserErrors; // False by default

        // root task associated with current async invocation
        Tuple<TaskCompletionSource<DbConnectionInternal>, Task> _currentCompletion;

        private SqlCredential _credential; // SQL authentication password stored in SecureString
        private string _connectionString;
        private int _connectRetryCount;

        private string _accessToken; // Access Token to be used for token based authententication

        // connection resiliency
        private object _reconnectLock = new object();
        internal Task _currentReconnectionTask;
        private Task _asyncWaitingForReconnection; // current async task waiting for reconnection in non-MARS connections
        private Guid _originalConnectionId = Guid.Empty;
        private CancellationTokenSource _reconnectionCancellationSource;
        internal SessionData _recoverySessionData;
        internal WindowsIdentity _lastIdentity;
        internal WindowsIdentity _impersonateIdentity;
        private int _reconnectCount;
        private ServerCertificateValidationCallback _serverCertificateValidationCallback;
        private ClientCertificateRetrievalCallback _clientCertificateRetrievalCallback;
        private SqlClientOriginalNetworkAddressInfo _originalNetworkAddressInfo;

        // Transient Fault handling flag. This is needed to convey to the downstream mechanism of connection establishment, if Transient Fault handling should be used or not
        // The downstream handling of Connection open is the same for idle connection resiliency. Currently we want to apply transient fault handling only to the connections opened
        // using SqlConnection.Open() method.
        internal bool _applyTransientFaultHandling = false;

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ctorConnectionString/*' />
        public SqlConnection(string connectionString) : this(connectionString, null)
        {
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ctorConnectionStringCredential/*' />
        public SqlConnection(string connectionString, SqlCredential credential) : this()
        {
            ConnectionString = connectionString;    // setting connection string first so that ConnectionOption is available
            if (credential != null)
            {
                // The following checks are necessary as setting Credential property will call CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential
                //  CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential it will throw InvalidOperationException rather than Arguemtn exception
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

                if (UsesContextConnection(connectionOptions))
                {
                    throw ADP.InvalidMixedArgumentOfSecureCredentialAndContextConnection();
                }

                if (UsesActiveDirectoryIntegrated(connectionOptions))
                {
                    throw SQL.SettingCredentialWithIntegratedArgument();
                }

                if (UsesActiveDirectoryInteractive(connectionOptions))
                {
                    throw SQL.SettingCredentialWithInteractiveArgument();
                }

                if (UsesActiveDirectoryDeviceCodeFlow(connectionOptions))
                {
                    throw SQL.SettingCredentialWithDeviceFlowArgument();
                }

                if (UsesActiveDirectoryManagedIdentity(connectionOptions))
                {
                    throw SQL.SettingCredentialWithManagedIdentityArgument(DbConnectionStringBuilderUtil.ActiveDirectoryManagedIdentityString);
                }

                if (UsesActiveDirectoryMSI(connectionOptions))
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
        { // Clone
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
            _serverCertificateValidationCallback = connection._serverCertificateValidationCallback;
            _clientCertificateRetrievalCallback = connection._clientCertificateRetrievalCallback;
            _originalNetworkAddressInfo = connection._originalNetworkAddressInfo;
            CacheConnectionStringProperties();
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/StatisticsEnabled/*' />
        // used to start/stop collection of statistics data and do verify the current state
        //
        // devnote: start/stop should not performed using a property since it requires execution of code
        //
        // start statistics
        //  set the internal flag (_statisticsEnabled) to true.
        //  Create a new SqlStatistics object if not already there.
        //  connect the parser to the object.
        //  if there is no parser at this time we need to connect it after creation.
        [
        DefaultValue(false),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_StatisticsEnabled),
        ]
        public bool StatisticsEnabled
        {
            get
            {
                return (_collectstats);
            }
            set
            {
                if (IsContextConnection)
                {
                    if (value)
                    {
                        throw SQL.NotAvailableOnContextConnection();
                    }
                }
                else
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
                    this._collectstats = value;
                }
            }
        }

        internal bool AsyncCommandInProgress
        {
            get
            {
                return (_AsyncCommandInProgress);
            }
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            set
            {
                _AsyncCommandInProgress = value;
            }
        }

        internal bool IsContextConnection
        {
            get
            {
                SqlConnectionString opt = (SqlConnectionString)ConnectionOptions;
                return UsesContextConnection(opt);
            }
        }

        /// <summary>
        /// Is this connection using column encryption ?
        /// </summary>
        internal bool IsColumnEncryptionSettingEnabled
        {
            get
            {
                SqlConnectionString opt = (SqlConnectionString)ConnectionOptions;
                return opt != null ? opt.ColumnEncryptionSetting == SqlConnectionColumnEncryptionSetting.Enabled : false;
            }
        }

        /// <summary>
        /// Get enclave attestation url to be used with enclave based Always Encrypted
        /// </summary>
        internal string EnclaveAttestationUrl
        {
            get
            {
                SqlConnectionString opt = (SqlConnectionString)ConnectionOptions;
                return opt.EnclaveAttestationUrl;
            }
        }

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

        // Is this connection is a Context Connection?
        private bool UsesContextConnection(SqlConnectionString opt)
        {
            return opt != null && opt.ContextConnection;
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

        // Does this connection uses Integrated Security?
        private bool UsesIntegratedSecurity(SqlConnectionString opt)
        {
            return opt != null && opt.IntegratedSecurity;
        }

        // Does this connection uses old style of clear userID or Password in connection string?
        private bool UsesClearUserIdOrPassword(SqlConnectionString opt)
        {
            bool result = false;
            if (null != opt)
            {
                result = (!ADP.IsEmpty(opt.UserID) || !ADP.IsEmpty(opt.Password));
            }
            return result;
        }

        private bool UsesCertificate(SqlConnectionString opt)
        {
            return opt != null && opt.UsesCertificate;
        }

        internal SqlConnectionString.TransactionBindingEnum TransactionBinding
        {
            get
            {
                return ((SqlConnectionString)ConnectionOptions).TransactionBinding;
            }
        }

        internal SqlConnectionString.TypeSystem TypeSystem
        {
            get
            {
                return ((SqlConnectionString)ConnectionOptions).TypeSystemVersion;
            }
        }

        internal Version TypeSystemAssemblyVersion
        {
            get
            {
                return ((SqlConnectionString)ConnectionOptions).TypeSystemAssemblyVersion;
            }
        }

        internal PoolBlockingPeriod PoolBlockingPeriod
        {
            get
            {
                return ((SqlConnectionString)ConnectionOptions).PoolBlockingPeriod;
            }
        }

        internal int ConnectRetryInterval
        {
            get
            {
                return ((SqlConnectionString)ConnectionOptions).ConnectRetryInterval;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/DbProviderFactory/*' />
        override protected DbProviderFactory DbProviderFactory
        {
            get
            {
                return SqlClientFactory.Instance;
            }
        }

        // AccessToken: To be used for token based authentication
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/AccessToken/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_AccessToken),
        ]
        public string AccessToken
        {
            get
            {
                string result = _accessToken;
                // When a connection is connecting or is ever opened, make AccessToken available only if "Persist Security Info" is set to true
                // otherwise, return null
                SqlConnectionString connectionOptions = (SqlConnectionString)UserConnectionOptions;
                if (InnerConnection.ShouldHidePassword && connectionOptions != null && !connectionOptions.PersistSecurityInfo)
                {
                    result = null;
                }

                return result;
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

                _accessToken = value;
                // Need to call ConnectionString_Set to do proper pool group check
                ConnectionString_Set(new SqlConnectionPoolKey(_connectionString, _credential, _accessToken, _serverCertificateValidationCallback, _clientCertificateRetrievalCallback, _originalNetworkAddressInfo));
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CommandTimeout/*' />
        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_ConnectionTimeout),
        ]
        public int CommandTimeout
        {
            get
            {
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                return ((null != constr) ? constr.CommandTimeout : SqlConnectionString.DEFAULT.Command_Timeout);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ConnectionString/*' />
        [
        DefaultValue(""),
#pragma warning disable 618 // ignore obsolete warning about RecommendedAsConfigurable to use SettingsBindableAttribute
        RecommendedAsConfigurable(true),
#pragma warning restore 618
        SettingsBindableAttribute(true),
        RefreshProperties(RefreshProperties.All),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        Editor("Microsoft.VSDesigner.Data.SQL.Design.SqlConnectionStringEditor, " + AssemblyRef.MicrosoftVSDesigner, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_ConnectionString),
        ]
        override public string ConnectionString
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
                        // ActiveDirectoryDeviceCodeFlow | ActiveDirectoryManagedIdentity/ActiveDirectoryMSI. Since a different error string is used
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
                ConnectionString_Set(new SqlConnectionPoolKey(value, _credential, _accessToken, _serverCertificateValidationCallback, _clientCertificateRetrievalCallback, _originalNetworkAddressInfo));
                _connectionString = value;  // Change _connectionString value only after value is validated
                CacheConnectionStringProperties();
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ConnectionTimeout/*' />
        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_ConnectionTimeout),
        ]
        override public int ConnectionTimeout
        {
            get
            {
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                return ((null != constr) ? constr.ConnectTimeout : SqlConnectionString.DEFAULT.Connect_Timeout);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Database/*' />
        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_Database),
        ]
        override public string Database
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
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/DataSource/*' />
        [
        Browsable(true),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_DataSource),
        ]
        override public string DataSource
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/PacketSize/*' />
        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_PacketSize),
        ]
        public int PacketSize
        {
            // if the connection is open, we need to ask the inner connection what it's
            // current packet size is because it may have gotten changed, otherwise we
            // can just return what the connection string had.
            get
            {
                if (IsContextConnection)
                {
                    throw SQL.NotAvailableOnContextConnection();
                }

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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClientConnectionId/*' />
        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_ClientConnectionId),
        ]
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ServerVersion/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_ServerVersion),
        ]
        override public string ServerVersion
        {
            get
            {
                return GetOpenConnection().ServerVersion;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ServerProcessId/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescription(StringsHelper.ResourceNames.SqlConnection_ServerProcessId),
        ]
        public int ServerProcessId
        {
            get => State.Equals(ConnectionState.Open) | State.Equals(ConnectionState.Executing) | State.Equals(ConnectionState.Fetching) ?
                GetOpenTdsConnection().ServerProcessId : 0;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/State/*' />
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnection_State),
        ]
        override public ConnectionState State
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
            get
            {
                return _statistics;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/WorkstationId/*' />
        [
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Data),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_WorkstationId),
        ]
        public string WorkstationId
        {
            get
            {
                if (IsContextConnection)
                {
                    throw SQL.NotAvailableOnContextConnection();
                }

                // If not supplied by the user, the default value is the MachineName
                // Note: In Longhorn you'll be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                string result = ((null != constr) ? constr.WorkstationId : null);
                if (null == result)
                {
                    // getting machine name requires Environment.Permission
                    // user must have that permission in order to retrieve this
                    result = Environment.MachineName;
                }
                return result;
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Credential/*' />
        // SqlCredential: Pair User Id and password in SecureString which are to be used for SQL authentication
        [
        Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
        ResDescriptionAttribute(StringsHelper.ResourceNames.SqlConnection_Credential),
        ]
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
                    throw ADP.OpenConnectionPropertySet("Credential", InnerConnection.State);
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
                ConnectionString_Set(new SqlConnectionPoolKey(_connectionString, _credential, _accessToken, _serverCertificateValidationCallback, _clientCertificateRetrievalCallback, _originalNetworkAddressInfo));
            }
        }

        // CheckAndThrowOnInvalidCombinationOfConnectionStringAndSqlCredential: check if the usage of credential has any conflict
        //  with the keys used in connection string
        //  If there is any conflict, it throws InvalidOperationException
        //  This is to be used setter of ConnectionString and Credential properties
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

            if (UsesContextConnection(connectionOptions))
            {
                throw ADP.InvalidMixedArgumentOfSecureCredentialAndContextConnection();
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

            if (UsesContextConnection(connectionOptions))
            {
                throw ADP.InvalidMixedUsageOfAccessTokenAndContextConnection();
            }

            if (UsesAuthentication(connectionOptions))
            {
                throw ADP.InvalidMixedUsageOfAccessTokenAndAuthentication();
            }

            // Check if the usage of AccessToken has the conflict with credential
            if (_credential != null)
            {
                throw ADP.InvalidMixedUsageOfAccessTokenAndCredential();
            }
        }

        //
        // PUBLIC EVENTS
        //

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/InfoMessage/*' />
        [
        ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_InfoMessage),
        ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnection_InfoMessage),
        ]
        public event SqlInfoMessageEventHandler InfoMessage
        {
            add
            {
                Events.AddHandler(EventInfoMessage, value);
            }
            remove
            {
                Events.RemoveHandler(EventInfoMessage, value);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/FireInfoMessageEventOnUserErrors/*' />
        public bool FireInfoMessageEventOnUserErrors
        {
            get
            {
                return _fireInfoMessageEventOnUserErrors;
            }
            set
            {
                _fireInfoMessageEventOnUserErrors = value;
            }
        }


#if ADONET_CERT_AUTH
         public ServerCertificateValidationCallback ServerCertificateValidationCallback {
            get {
                return _serverCertificateValidationCallback;
            }
            set {
                _serverCertificateValidationCallback = value;
                ConnectionString_Set(new SqlConnectionPoolKey(_connectionString, _credential, _accessToken, _serverCertificateValidationCallback, _clientCertificateRetrievalCallback, _originalNetworkAddressInfo));
            }
        }

        // The exceptions from client certificate callback are not rethrown and instead an SSL
        // exchange fails with CRYPT_E_NOT_FOUND = 0x80092004
        public ClientCertificateRetrievalCallback ClientCertificateRetrievalCallback {
            get {
                return _clientCertificateRetrievalCallback;
            }
            set {
                _clientCertificateRetrievalCallback = value;
                ConnectionString_Set(new SqlConnectionPoolKey(_connectionString, _credential, _accessToken, _serverCertificateValidationCallback, _clientCertificateRetrievalCallback, _originalNetworkAddressInfo));
            }
        }
#endif

#if ADONET_ORIGINAL_CLIENT_ADDRESS

        public SqlClientOriginalNetworkAddressInfo OriginalNetworkAddressInfo {
            get {
                return _originalNetworkAddressInfo;
            }
            set {
                _originalNetworkAddressInfo = value;
                ConnectionString_Set(new SqlConnectionPoolKey(_connectionString, _credential, _accessToken, _serverCertificateValidationCallback, _clientCertificateRetrievalCallback, _originalNetworkAddressInfo));
            }
        }

#endif

        // Approx. number of times that the internal connection has been reconnected
        internal int ReconnectCount
        {
            get
            {
                return _reconnectCount;
            }
        }

        //
        // PUBLIC METHODS
        //
        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransaction2/*' />
        new public SqlTransaction BeginTransaction()
        {
            // this is just a delegate. The actual method tracks executiontime
            return BeginTransaction(IsolationLevel.Unspecified, null);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionIso/*' />
        new public SqlTransaction BeginTransaction(IsolationLevel iso)
        {
            // this is just a delegate. The actual method tracks executiontime
            return BeginTransaction(iso, null);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionTransactionName/*' />
        public SqlTransaction BeginTransaction(string transactionName)
        {
            // Use transaction names only on the outermost pair of nested
            // BEGIN...COMMIT or BEGIN...ROLLBACK statements.  Transaction names
            // are ignored for nested BEGIN's.  The only way to rollback a nested
            // transaction is to have a save point from a SAVE TRANSACTION call.
            return BeginTransaction(IsolationLevel.Unspecified, transactionName);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginDbTransaction/*' />
        // suppress this message - we cannot use SafeHandle here. Also, see notes in the code (VSTFDEVDIV# 560355)
        [SuppressMessage("Microsoft.Reliability", "CA2004:RemoveCallsToGCKeepAlive")]
        override protected DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<prov.SqlConnection.BeginDbTransaction|API> {0}, isolationLevel={1}", ObjectID, (int)isolationLevel);
            try
            {

                DbTransaction transaction = BeginTransaction(isolationLevel);

                // VSTFDEVDIV# 560355 - InnerConnection doesn't maintain a ref on the outer connection (this) and
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/BeginTransactionIsoTransactionName/*' />
        public SqlTransaction BeginTransaction(IsolationLevel iso, string transactionName)
        {
            WaitForPendingReconnection();
            SqlStatistics statistics = null;
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.BeginTransaction|API> {0}, iso={1}, transactionName='{2}'", ObjectID, (int)iso, transactionName);

            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                // NOTE: we used to throw an exception if the transaction name was empty
                // (see MDAC 50292) but that was incorrect because we have a BeginTransaction
                // method that doesn't have a transactionName argument.
                SqlTransaction transaction;
                bool isFirstAttempt = true;
                do
                {
                    transaction = GetOpenConnection().BeginSqlTransaction(iso, transactionName, isFirstAttempt); // do not reconnect twice
                    Debug.Assert(isFirstAttempt || !transaction.InternalTransaction.ConnectionHasBeenRestored, "Restored connection on non-first attempt");
                    isFirstAttempt = false;
                } while (transaction.InternalTransaction.ConnectionHasBeenRestored);


                // SQLBU 503873  The GetOpenConnection line above doesn't keep a ref on the outer connection (this),
                //  and it could be collected before the inner connection can hook it to the transaction, resulting in
                //  a transaction with a null connection property.  Use GC.KeepAlive to ensure this doesn't happen.
                GC.KeepAlive(this);

                return transaction;
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
                SqlStatistics.StopTimer(statistics);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangeDatabase/*' />
        override public void ChangeDatabase(string database)
        {
            SqlStatistics statistics = null;
            RepairInnerConnection();
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.ChangeDatabase|API|Correlation> ObjectID{0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);
            TdsParser bestEffortCleanupTarget = null;
            RuntimeHelpers.PrepareConstrainedRegions();

            try
            {
#if DEBUG
                TdsParser.ReliabilitySection tdsReliabilitySection = new TdsParser.ReliabilitySection();

                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    tdsReliabilitySection.Start();
#else
                {
#endif //DEBUG
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(this);
                    statistics = SqlStatistics.StartTimer(Statistics);
                    InnerConnection.ChangeDatabase(database);
                }
#if DEBUG
                finally
                {
                    tdsReliabilitySection.Stop();
                }
#endif //DEBUG
            }
            catch (System.OutOfMemoryException e)
            {
                Abort(e);
                throw;
            }
            catch (System.StackOverflowException e)
            {
                Abort(e);
                throw;
            }
            catch (System.Threading.ThreadAbortException e)
            {
                Abort(e);
                SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearAllPools/*' />
        static public void ClearAllPools()
        {
            (new SqlClientPermission(PermissionState.Unrestricted)).Demand();
            SqlConnectionFactory.SingletonInstance.ClearAllPools();
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ClearPool/*' />
        static public void ClearPool(SqlConnection connection)
        {
            ADP.CheckArgumentNull(connection, "connection");

            DbConnectionOptions connectionOptions = connection.UserConnectionOptions;
            if (null != connectionOptions)
            {
                connectionOptions.DemandPermission();
                if (connection.IsContextConnection)
                {
                    throw SQL.NotAvailableOnContextConnection();
                }
                SqlConnectionFactory.SingletonInstance.ClearPool(connection);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/System.ICloneable.Clone/*' />
        object ICloneable.Clone()
        {
            SqlConnection clone = new SqlConnection(this);
            return clone;
        }

        void CloseInnerConnection()
        {
            // CloseConnection() now handles the lock

            // The SqlInternalConnectionTds is set to OpenBusy during close, once this happens the cast below will fail and
            // the command will no longer be cancelable.  It might be desirable to be able to cancel the close opperation, but this is
            // outside of the scope of Whidbey RTM.  See (SqlCommand::Cancel) for other lock.
            _originalConnectionId = ClientConnectionId;
            InnerConnection.CloseConnection(this, ConnectionFactory);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Close/*' />
        override public void Close()
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.Close|API> {0}", ObjectID);
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.Close|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);

            try
            {
                SqlStatistics statistics = null;

                TdsParser bestEffortCleanupTarget = null;
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
#if DEBUG
                    TdsParser.ReliabilitySection tdsReliabilitySection = new TdsParser.ReliabilitySection();

                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                        tdsReliabilitySection.Start();
#else
                    {
#endif //DEBUG
                        bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(this);
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
#if DEBUG
                    finally
                    {
                        tdsReliabilitySection.Stop();
                    }
#endif //DEBUG
                }
                catch (System.OutOfMemoryException e)
                {
                    Abort(e);
                    throw;
                }
                catch (System.StackOverflowException e)
                {
                    Abort(e);
                    throw;
                }
                catch (System.Threading.ThreadAbortException e)
                {
                    Abort(e);
                    SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                    throw;
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                    //dispose windows identity once connection is closed.
                    if (_lastIdentity != null)
                    {
                        _lastIdentity.Dispose();
                    }
                }
            }
            finally
            {
                SqlDebugContext sdc = _sdc;
                _sdc = null;

                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);

                if (sdc != null)
                {
                    sdc.Dispose();
                }
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/CreateCommand/*' />
        new public SqlCommand CreateCommand()
        {
            return new SqlCommand(null, this);
        }

        private void DisposeMe(bool disposing)
        { // MDAC 65459
            // clear credential and AccessToken here rather than in IDisposable.Dispose as these are specific to SqlConnection only
            //  IDisposable.Dispose is generated code from a template and used by other providers as well
            _credential = null;
            _accessToken = null;

            if (!disposing)
            {
                // DevDiv2 Bug 457934:SQLConnection leaks when not disposed
                // http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/457934
                // For non-pooled connections we need to make sure that if the SqlConnection was not closed, then we release the GCHandle on the stateObject to allow it to be GCed
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/EnlistDistributedTransaction/*' />
        public void EnlistDistributedTransaction(System.EnterpriseServices.ITransaction transaction)
        {
            if (IsContextConnection)
            {
                throw SQL.NotAvailableOnContextConnection();
            }

            EnlistDistributedTransactionHelper(transaction);
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/Open/*' />
        override public void Open()
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
                if (StatisticsEnabled)
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

                SqlStatistics statistics = null;
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    if (!TryOpen(null, overrides))
                    {
                        throw ADP.InternalError(ADP.InternalErrorCode.SynchronousConnectReturnedPending);
                    }
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
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
                        _impersonateIdentity = _lastIdentity;
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
                            _impersonateIdentity = null;
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
                                            runningReconnect = Task.Run(() => ReconnectAsync(timeout));
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

        // this is straightforward, but expensive method to do connection resiliency - it take locks and all prepartions as for TDS request
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

        void CancelOpenAndWait()
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/OpenAsync/*' />
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            long scopeID = SqlClientEventSource.Log.TryPoolerScopeEnterEvent("<sc.SqlConnection.OpenAsync|API> {0}", ObjectID);
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.OpenAsync|API|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, ActivityCorrelator.Current);

            try
            {
                if (StatisticsEnabled)
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

                SqlStatistics statistics = null;
                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    System.Transactions.Transaction transaction = ADP.GetCurrentTransaction();
                    TaskCompletionSource<DbConnectionInternal> completion = new TaskCompletionSource<DbConnectionInternal>(transaction);
                    TaskCompletionSource<object> result = new TaskCompletionSource<object>();

                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.SetCanceled();
                        return result.Task;
                    }

                    if (IsContextConnection)
                    {
                        // Async not supported on Context Connections
                        result.SetException(ADP.ExceptionWithStackTrace(SQL.NotAvailableOnContextConnection()));
                        return result.Task;
                    }

                    bool completed;

                    try
                    {
                        completed = TryOpen(completion);
                    }
                    catch (Exception e)
                    {
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
                            registration = cancellationToken.Register(() => completion.TrySetCanceled());
                        }
                        OpenAsyncRetry retry = new OpenAsyncRetry(this, completion, result, registration);
                        _currentCompletion = new Tuple<TaskCompletionSource<DbConnectionInternal>, Task>(completion, result.Task);
                        completion.Task.ContinueWith(retry.Retry, TaskScheduler.Default);
                        return result.Task;
                    }

                    return result.Task;
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

        private class OpenAsyncRetry
        {
            SqlConnection _parent;
            TaskCompletionSource<DbConnectionInternal> _retry;
            TaskCompletionSource<object> _result;
            CancellationTokenRegistration _registration;

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
                    RuntimeHelpers.PrepareConstrainedRegions();
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

        private bool TryOpen(TaskCompletionSource<DbConnectionInternal> retry, SqlConnectionOverrides overrides = SqlConnectionOverrides.None)
        {
            SqlConnectionString connectionOptions = (SqlConnectionString)ConnectionOptions;

            bool result = false;

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

            if (_impersonateIdentity != null)
            {
                using (WindowsIdentity identity = DbConnectionPoolIdentity.GetCurrentWindowsIdentity())
                {
                    if (_impersonateIdentity.User == identity.User)
                    {
                        result = TryOpenInner(retry);
                    }
                    else
                    {
                        using (WindowsImpersonationContext context = _impersonateIdentity.Impersonate())
                        {
                            result = TryOpenInner(retry);
                        }
                    }
                }
            }
            else
            {
                if (this.UsesIntegratedSecurity(connectionOptions) || this.UsesCertificate(connectionOptions) || this.UsesActiveDirectoryIntegrated(connectionOptions))
                {
                    _lastIdentity = DbConnectionPoolIdentity.GetCurrentWindowsIdentity();
                }
                else
                {
                    _lastIdentity = null;
                }
                result = TryOpenInner(retry);
            }

            // Set future transient fault handling based on connection options
            _applyTransientFaultHandling = (retry == null && connectionOptions != null && connectionOptions.ConnectRetryCount > 0);

            return result;
        }

        private bool TryOpenInner(TaskCompletionSource<DbConnectionInternal> retry)
        {
            TdsParser bestEffortCleanupTarget = null;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
#if DEBUG
                TdsParser.ReliabilitySection tdsReliabilitySection = new TdsParser.ReliabilitySection();

                RuntimeHelpers.PrepareConstrainedRegions();
                try
                {
                    tdsReliabilitySection.Start();
#else
                {
#endif //DEBUG
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
                    // does not require GC.KeepAlive(this) because of OnStateChange

                    // GetBestEffortCleanup must happen AFTER OpenConnection to get the correct target.
                    bestEffortCleanupTarget = SqlInternalConnection.GetBestEffortCleanupTarget(this);

                    var tdsInnerConnection = (InnerConnection as SqlInternalConnectionTds);
                    if (tdsInnerConnection == null)
                    {
                        SqlInternalConnectionSmi innerConnection = (InnerConnection as SqlInternalConnectionSmi);
                        innerConnection.AutomaticEnlistment();
                    }
                    else
                    {
                        Debug.Assert(tdsInnerConnection.Parser != null, "Where's the parser?");

                        if (!tdsInnerConnection.ConnectionOptions.Pooling)
                        {
                            // For non-pooled connections, we need to make sure that the finalizer does actually run to avoid leaking SNI handles
                            GC.ReRegisterForFinalize(this);
                        }

                        if (StatisticsEnabled)
                        {
                            ADP.TimerCurrent(out _statistics._openTimestamp);
                            tdsInnerConnection.Parser.Statistics = _statistics;
                        }
                        else
                        {
                            tdsInnerConnection.Parser.Statistics = null;
                            _statistics = null; // in case of previous Open/Close/reset_CollectStats sequence
                        }
                        CompleteOpen();
                    }
                }
#if DEBUG
                finally
                {
                    tdsReliabilitySection.Stop();
                }
#endif //DEBUG
            }
            catch (System.OutOfMemoryException e)
            {
                Abort(e);
                throw;
            }
            catch (System.StackOverflowException e)
            {
                Abort(e);
                throw;
            }
            catch (System.Threading.ThreadAbortException e)
            {
                Abort(e);
                SqlInternalConnection.BestEffortCleanup(bestEffortCleanupTarget);
                throw;
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
                return GetOpenConnection().HasLocalTransaction;
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
                return GetOpenConnection().HasLocalTransactionFromAPI;
            }
        }

        internal bool IsShiloh
        {
            get
            {
                if (_currentReconnectionTask != null)
                { // holds true even if task is completed
                    return true; // if CR is enabled, connection, if established, will be Katmai+
                }
                return GetOpenConnection().IsShiloh;
            }
        }

        internal bool IsYukonOrNewer
        {
            get
            {
                if (_currentReconnectionTask != null)
                { // holds true even if task is completed
                    return true; // if CR is enabled, connection, if established, will be Katmai+
                }
                return GetOpenConnection().IsYukonOrNewer;
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
                return GetOpenConnection().IsKatmaiOrNewer;
            }
        }

        internal TdsParser Parser
        {
            get
            {
                SqlInternalConnectionTds tdsConnection = (GetOpenConnection() as SqlInternalConnectionTds);
                if (null == tdsConnection)
                {
                    throw SQL.NotAvailableOnContextConnection();
                }
                return tdsConnection.Parser;
            }
        }

        internal bool Asynchronous
        {
            get
            {
                SqlConnectionString constr = (SqlConnectionString)ConnectionOptions;
                return ((null != constr) ? constr.Asynchronous : SqlConnectionString.DEFAULT.Asynchronous);
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
            SqlInternalConnection innerConnection = GetOpenConnection(method);
            innerConnection.ValidateConnectionForExecute(command);
        }

        // Surround name in brackets and then escape any end bracket to protect against SQL Injection.
        // NOTE: if the user escapes it themselves it will not work, but this was the case in V1 as well
        // as native OleDb and Odbc.
        static internal string FixupDatabaseTransactionName(string name)
        {
            if (!ADP.IsEmpty(name))
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

            // Bug fix - MDAC 49022 - connection open after failure...  Problem was parser was passing
            // Open as a state - because the parser's connection to the netlib was open.  We would
            // then set the connection state to the parser's state - which is not correct.  The only
            // time the connection state should change to what is passed in to this function is if
            // the parser is broken, then we should be closed.  Changed to passing in
            // TdsParserState, not ConnectionState.

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

        // SxS: using Debugger.IsAttached
        // TODO: review this code for SxS issues (VSDD 540765)
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        private void CompleteOpen()
        {
            Debug.Assert(ConnectionState.Open == State, "CompleteOpen not open");
            // be sure to mark as open so SqlDebugCheck can issue Query

            // check to see if we need to hook up sql-debugging if a debugger is attached
            // We only need this check for Shiloh and earlier servers.
            if (!GetOpenConnection().IsYukonOrNewer &&
                    System.Diagnostics.Debugger.IsAttached)
            {
                bool debugCheck = false;
                try
                {
                    new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand(); // MDAC 66682, 69017
                    debugCheck = true;
                }
                catch (SecurityException e)
                {
                    ADP.TraceExceptionWithoutRethrow(e);
                }

                if (debugCheck)
                {
                    // if we don't have Unmanaged code permission, don't check for debugging
                    // but let the connection be opened while under the debugger
                    CheckSQLDebugOnConnect();
                }
            }
        }

        internal SqlInternalConnection GetOpenConnection()
        {
            SqlInternalConnection innerConnection = (InnerConnection as SqlInternalConnection);
            if (null == innerConnection)
            {
                throw ADP.ClosedConnectionError();
            }
            return innerConnection;
        }

        internal SqlInternalConnection GetOpenConnection(string method)
        {
            DbConnectionInternal innerConnection = InnerConnection;
            SqlInternalConnection innerSqlConnection = (innerConnection as SqlInternalConnection);
            if (null == innerSqlConnection)
            {
                throw ADP.OpenConnectionRequired(method, innerConnection.State);
            }
            return innerSqlConnection;
        }

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

            Debug.Assert(null != imevent, "null SqlInfoMessageEventArgs");
            var imeventValue = (null != imevent) ? imevent.Message : "";
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlConnection.OnInfoMessage|API|INFO> {0}, Message='{1}'", ObjectID, imeventValue);
            SqlInfoMessageEventHandler handler = (SqlInfoMessageEventHandler)Events[EventInfoMessage];

            if (null != handler)
            {
                notified = true;
                try
                {
                    handler(this, imevent);
                }
                catch (Exception e)
                { // MDAC 53175
                    if (!ADP.IsCatchableOrSecurityExceptionType(e))
                    {
                        throw;
                    }

                    ADP.TraceExceptionWithoutRethrow(e);
                }
            }
            else
            {
                notified = false;
            }
        }

        //
        // SQL DEBUGGING SUPPORT
        //

        // this only happens once per connection
        // SxS: using named file mapping APIs
        // TODO: review this code for SxS issues (VSDD 540765)
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        private void CheckSQLDebugOnConnect()
        {
            IntPtr hFileMap;
            uint pid = (uint)SafeNativeMethods.GetCurrentProcessId();

            string mapFileName;

            // If Win2k or later, prepend "Global\\" to enable this to work through TerminalServices.
            if (ADP.IsPlatformNT5)
            {
                mapFileName = "Global\\" + TdsEnums.SDCI_MAPFILENAME;
            }
            else
            {
                mapFileName = TdsEnums.SDCI_MAPFILENAME;
            }

            mapFileName = mapFileName + pid.ToString(CultureInfo.InvariantCulture);

            hFileMap = NativeMethods.OpenFileMappingA(0x4/*FILE_MAP_READ*/, false, mapFileName);

            if (ADP.PtrZero != hFileMap)
            {
                IntPtr pMemMap = NativeMethods.MapViewOfFile(hFileMap, 0x4/*FILE_MAP_READ*/, 0, 0, IntPtr.Zero);
                if (ADP.PtrZero != pMemMap)
                {
                    SqlDebugContext sdc = new SqlDebugContext();
                    sdc.hMemMap = hFileMap;
                    sdc.pMemMap = pMemMap;
                    sdc.pid = pid;

                    // optimization: if we only have to refresh memory-mapped data at connection open time
                    // optimization: then call here instead of in CheckSQLDebug() which gets called
                    // optimization: at command execution time
                    // RefreshMemoryMappedData(sdc);

                    // delaying setting out global state until after we issue this first SQLDebug command so that
                    // we don't reentrantly call into CheckSQLDebug
                    CheckSQLDebug(sdc);
                    // now set our global state
                    _sdc = sdc;
                }
            }
        }

        // This overload is called by the Command object when executing stored procedures.  Note that
        // if SQLDebug has never been called, it is a noop.
        internal void CheckSQLDebug()
        {
            if (null != _sdc)
                CheckSQLDebug(_sdc);
        }

        // SxS: using GetCurrentThreadId
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)] // MDAC 66682, 69017
        private void CheckSQLDebug(SqlDebugContext sdc)
        {
            // check to see if debugging has been activated
            Debug.Assert(null != sdc, "SQL Debug: invalid null debugging context!");

#pragma warning disable 618
            uint tid = (uint)AppDomain.GetCurrentThreadId();    // Sql Debugging doesn't need fiber support;
#pragma warning restore 618
            RefreshMemoryMappedData(sdc);

            // UNDONE: do I need to remap the contents of pMemMap each time I call into here?
            // UNDONE: current behavior is to only marshal the contents of the memory-mapped file
            // UNDONE: at connection open time.

            // If we get here, the debugger must be hooked up.
            if (!sdc.active)
            {
                if (sdc.fOption/*TdsEnums.SQLDEBUG_ON*/)
                {
                    // turn on
                    sdc.active = true;
                    sdc.tid = tid;
                    try
                    {
                        IssueSQLDebug(TdsEnums.SQLDEBUG_ON, sdc.machineName, sdc.pid, sdc.dbgpid, sdc.sdiDllName, sdc.data);
                        sdc.tid = 0; // reset so that the first successful time through, we notify the server of the context switch
                    }
                    catch
                    {
                        sdc.active = false;
                        throw;
                    }
                }
            }

            // be sure to pick up thread context switch, especially the first time through
            if (sdc.active)
            {
                if (!sdc.fOption/*TdsEnums.SQLDEBUG_OFF*/)
                {
                    // turn off and free the memory
                    sdc.Dispose();
                    // okay if we throw out here, no state to clean up
                    IssueSQLDebug(TdsEnums.SQLDEBUG_OFF, null, 0, 0, null, null);
                }
                else
                {
                    // notify server of context change
                    if (sdc.tid != tid)
                    {
                        sdc.tid = tid;
                        try
                        {
                            IssueSQLDebug(TdsEnums.SQLDEBUG_CONTEXT, null, sdc.pid, sdc.tid, null, null);
                        }
                        catch
                        {
                            sdc.tid = 0;
                            throw;
                        }
                    }
                }
            }
        }

        private void IssueSQLDebug(uint option, string machineName, uint pid, uint id, string sdiDllName, byte[] data)
        {

            if (GetOpenConnection().IsYukonOrNewer)
            {
                // TODO: When Yukon actually supports debugging, we need to modify this method to do the right thing(tm)
                return;
            }

            // CONSIDER: we could cache three commands, one for each mode {on, off, context switch}
            // CONSIDER: but debugging is not the performant case so save space instead amd rebuild each time
            SqlCommand c = new SqlCommand(TdsEnums.SP_SDIDEBUG, this);
            c.CommandType = CommandType.StoredProcedure;

            // context param
            SqlParameter p = new SqlParameter(null, SqlDbType.VarChar, TdsEnums.SQLDEBUG_MODE_NAMES[option].Length);
            p.Value = TdsEnums.SQLDEBUG_MODE_NAMES[option];
            c.Parameters.Add(p);

            if (option == TdsEnums.SQLDEBUG_ON)
            {
                // debug dll name
                p = new SqlParameter(null, SqlDbType.VarChar, sdiDllName.Length);
                p.Value = sdiDllName;
                c.Parameters.Add(p);
                // debug machine name
                p = new SqlParameter(null, SqlDbType.VarChar, machineName.Length);
                p.Value = machineName;
                c.Parameters.Add(p);
            }

            if (option != TdsEnums.SQLDEBUG_OFF)
            {
                // client pid
                p = new SqlParameter(null, SqlDbType.Int);
                p.Value = pid;
                c.Parameters.Add(p);
                // dbgpid or tid
                p = new SqlParameter(null, SqlDbType.Int);
                p.Value = id;
                c.Parameters.Add(p);
            }

            if (option == TdsEnums.SQLDEBUG_ON)
            {
                // debug data
                p = new SqlParameter(null, SqlDbType.VarBinary, (null != data) ? data.Length : 0);
                p.Value = data;
                c.Parameters.Add(p);
            }

            c.ExecuteNonQuery();
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringNewPassword/*' />
        public static void ChangePassword(string connectionString, string newPassword)
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.ChangePassword|API>");
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.ChangePassword|API|Correlation> ActivityID {0}", ActivityCorrelator.Current);

            try
            {
                if (ADP.IsEmpty(connectionString))
                {
                    throw SQL.ChangePasswordArgumentMissing("connectionString");
                }
                if (ADP.IsEmpty(newPassword))
                {
                    throw SQL.ChangePasswordArgumentMissing("newPassword");
                }
                if (TdsEnums.MAXLEN_NEWPASSWORD < newPassword.Length)
                {
                    throw ADP.InvalidArgumentLength("newPassword", TdsEnums.MAXLEN_NEWPASSWORD);
                }

                SqlConnectionPoolKey key = new SqlConnectionPoolKey(connectionString, credential: null, accessToken: null, serverCertificateValidationCallback: null, clientCertificateRetrievalCallback: null, originalNetworkAddressInfo: null);

                SqlConnectionString connectionOptions = SqlConnectionFactory.FindSqlConnectionOptions(key);
                if (connectionOptions.IntegratedSecurity || connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
                {
                    throw SQL.ChangePasswordConflictsWithSSPI();
                }
                if (!ADP.IsEmpty(connectionOptions.AttachDBFilename))
                {
                    throw SQL.ChangePasswordUseOfUnallowedKey(SqlConnectionString.KEY.AttachDBFilename);
                }
                if (connectionOptions.ContextConnection)
                {
                    throw SQL.ChangePasswordUseOfUnallowedKey(SqlConnectionString.KEY.Context_Connection);
                }

                System.Security.PermissionSet permissionSet = connectionOptions.CreatePermissionSet();
                permissionSet.Demand();

                ChangePassword(connectionString, connectionOptions, null, newPassword, null);
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='/docs/members[@name="SqlConnection"]/ChangePasswordConnectionStringCredentialNewSecurePassword/*' />
        public static void ChangePassword(string connectionString, SqlCredential credential, SecureString newSecurePassword)
        {
            long scopeID = SqlClientEventSource.Log.TryScopeEnterEvent("<sc.SqlConnection.ChangePassword|API>");
            SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlConnection.ChangePassword|API|Correlation> ActivityID {0}", ActivityCorrelator.Current);

            try
            {
                if (ADP.IsEmpty(connectionString))
                {
                    throw SQL.ChangePasswordArgumentMissing("connectionString");
                }

                // check credential; not necessary to check the length of password in credential as the check is done by SqlCredential class
                if (credential == null)
                {
                    throw SQL.ChangePasswordArgumentMissing("credential");
                }

                if (newSecurePassword == null || newSecurePassword.Length == 0)
                {
                    throw SQL.ChangePasswordArgumentMissing("newSecurePassword");
                }

                if (!newSecurePassword.IsReadOnly())
                {
                    throw ADP.MustBeReadOnly("newSecurePassword");
                }

                if (TdsEnums.MAXLEN_NEWPASSWORD < newSecurePassword.Length)
                {
                    throw ADP.InvalidArgumentLength("newSecurePassword", TdsEnums.MAXLEN_NEWPASSWORD);
                }

                SqlConnectionPoolKey key = new SqlConnectionPoolKey(connectionString, credential, accessToken: null, serverCertificateValidationCallback: null, clientCertificateRetrievalCallback: null, originalNetworkAddressInfo: null);

                SqlConnectionString connectionOptions = SqlConnectionFactory.FindSqlConnectionOptions(key);

                // Check for incompatible connection string value with SqlCredential
                if (!ADP.IsEmpty(connectionOptions.UserID) || !ADP.IsEmpty(connectionOptions.Password))
                {
                    throw ADP.InvalidMixedArgumentOfSecureAndClearCredential();
                }

                if (connectionOptions.IntegratedSecurity || connectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated)
                {
                    throw SQL.ChangePasswordConflictsWithSSPI();
                }

                if (!ADP.IsEmpty(connectionOptions.AttachDBFilename))
                {
                    throw SQL.ChangePasswordUseOfUnallowedKey(SqlConnectionString.KEY.AttachDBFilename);
                }

                if (connectionOptions.ContextConnection)
                {
                    throw SQL.ChangePasswordUseOfUnallowedKey(SqlConnectionString.KEY.Context_Connection);
                }

                System.Security.PermissionSet permissionSet = connectionOptions.CreatePermissionSet();
                permissionSet.Demand();

                ChangePassword(connectionString, connectionOptions, credential, null, newSecurePassword);
            }
            finally
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(scopeID);
            }
        }

        private static void ChangePassword(string connectionString, SqlConnectionString connectionOptions, SqlCredential credential, string newPassword, SecureString newSecurePassword)
        {
            // note: This is the only case where we directly construt the internal connection, passing in the new password.
            // Normally we would simply create a regular connectoin and open it but there is no other way to pass the
            // new password down to the constructor. Also it would have an unwanted impact on the connection pool
            //
            using (SqlInternalConnectionTds con = new SqlInternalConnectionTds(null, connectionOptions, credential, null, newPassword, newSecurePassword, false, null, null, null, null))
            {
                if (!con.IsYukonOrNewer)
                {
                    throw SQL.ChangePasswordRequiresYukon();
                }
            }
            SqlConnectionPoolKey key = new SqlConnectionPoolKey(connectionString, credential, accessToken: null, serverCertificateValidationCallback: null, clientCertificateRetrievalCallback: null, originalNetworkAddressInfo: null);

            SqlConnectionFactory.SingletonInstance.ClearPool(key);
        }

        internal void RegisterForConnectionCloseNotification<T>(ref Task<T> outerTask, object value, int tag)
        {
            // Connection exists,  schedule removal, will be added to ref collection after calling ValidateAndReconnect
            outerTask = outerTask.ContinueWith(task =>
            {
                RemoveWeakReference(value);
                return task;
            }, TaskScheduler.Default).Unwrap();
        }

        // updates our context with any changes made to the memory-mapped data by an external process
        static private void RefreshMemoryMappedData(SqlDebugContext sdc)
        {
            Debug.Assert(ADP.PtrZero != sdc.pMemMap, "SQL Debug: invalid null value for pMemMap!");
            // copy memory mapped file contents into managed types
            MEMMAP memMap = (MEMMAP)Marshal.PtrToStructure(sdc.pMemMap, typeof(MEMMAP));
            sdc.dbgpid = memMap.dbgpid;
            sdc.fOption = (memMap.fOption == 1) ? true : false;
            // xlate ansi byte[] -> managed strings
            Encoding cp = System.Text.Encoding.GetEncoding(TdsEnums.DEFAULT_ENGLISH_CODE_PAGE_VALUE);
            sdc.machineName = cp.GetString(memMap.rgbMachineName, 0, memMap.rgbMachineName.Length);
            sdc.sdiDllName = cp.GetString(memMap.rgbDllName, 0, memMap.rgbDllName.Length);
            // just get data reference
            sdc.data = memMap.rgbData;
        }

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/ResetStatistics/*' />
        public void ResetStatistics()
        {
            if (IsContextConnection)
            {
                throw SQL.NotAvailableOnContextConnection();
            }

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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RetrieveStatistics/*' />
        public IDictionary RetrieveStatistics()
        {
            if (IsContextConnection)
            {
                throw SQL.NotAvailableOnContextConnection();
            }

            if (null != Statistics)
            {
                UpdateStatistics();
                return Statistics.GetHashtable();
            }
            else
            {
                return new SqlStatistics().GetHashtable();
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

        /// <include file='..\..\..\..\..\..\..\doc\snippets\Microsoft.Data.SqlClient\SqlConnection.xml' path='docs/members[@name="SqlConnection"]/RetrieveInternalInfo/*' />
        public IDictionary<string, object> RetrieveInternalInfo()
        {
            IDictionary<string, object> internalDictionary = new Dictionary<string, object>();

            internalDictionary.Add("SQLDNSCachingSupportedState", SQLDNSCachingSupportedState);
            internalDictionary.Add("SQLDNSCachingSupportedStateBeforeRedirect", SQLDNSCachingSupportedStateBeforeRedirect);

            return internalDictionary;
        }

        //
        // UDT SUPPORT
        //

        private Assembly ResolveTypeAssembly(AssemblyName asmRef, bool throwOnError)
        {
            Debug.Assert(TypeSystemAssemblyVersion != null, "TypeSystemAssembly should be set !");
            if (string.Compare(asmRef.Name, "Microsoft.SqlServer.Types", StringComparison.OrdinalIgnoreCase) == 0)
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

        // TODO - move UDT code to separate file.
        internal void CheckGetExtendedUDTInfo(SqlMetaDataPriv metaData, bool fThrow)
        {
            if (metaData.udt?.Type == null)
            { // If null, we have not obtained extended info.
                Debug.Assert(!ADP.IsEmpty(metaData.udt?.AssemblyQualifiedName), "Unexpected state on GetUDTInfo");
                // Parameter throwOnError determines whether exception from Assembly.Load is thrown.
                metaData.udt.Type =
                    Type.GetType(typeName: metaData.udt.AssemblyQualifiedName, assemblyResolver: asmRef => ResolveTypeAssembly(asmRef, fThrow), typeResolver: null, throwOnError: fThrow);

                if (fThrow && metaData.udt.Type == null)
                {
                    // TODO - BUG - UNDONE - Fix before Whidbey RTM - better message!
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
                o = t.InvokeMember("Null", BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Static, null, null, new Object[] { }, CultureInfo.InvariantCulture);
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
            int maxSize = 0;
            return GetBytes(o, out format, out maxSize);
        }

        internal byte[] GetBytes(object o, out Format format, out int maxSize)
        {
            SqlUdtInfo attr = AssemblyCache.GetInfoFromType(o.GetType());
            maxSize = attr.MaxByteSize;
            format = attr.SerializationFormat;

            if (maxSize < -1 || maxSize >= UInt16.MaxValue)
            { // Do we need this?  Is this the right place?
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
    } // SqlConnection

    // TODO: This really belongs in it's own source file...
    //
    // This is a private interface for the SQL Debugger
    // You must not change the guid for this coclass
    // or the iid for the ISQLDebug interface
    //
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlDebugging.xml' path='docs/members[@name="SQLDebugging"]/SQLDebugging/*'/>
    [
    ComVisible(true),
    ClassInterface(ClassInterfaceType.None),
    Guid("afef65ad-4577-447a-a148-83acadd3d4b9"),
    ]
    [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Name = "FullTrust")]
    public sealed class SQLDebugging : ISQLDebug
    {

        // Security stuff
        const int STANDARD_RIGHTS_REQUIRED = (0x000F0000);
        const int DELETE = (0x00010000);
        const int READ_CONTROL = (0x00020000);
        const int WRITE_DAC = (0x00040000);
        const int WRITE_OWNER = (0x00080000);
        const int SYNCHRONIZE = (0x00100000);
        const int FILE_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0x000001FF);
        const uint GENERIC_READ = (0x80000000);
        const uint GENERIC_WRITE = (0x40000000);
        const uint GENERIC_EXECUTE = (0x20000000);
        const uint GENERIC_ALL = (0x10000000);

        const int SECURITY_DESCRIPTOR_REVISION = (1);
        const int ACL_REVISION = (2);

        const int SECURITY_AUTHENTICATED_USER_RID = (0x0000000B);
        const int SECURITY_LOCAL_SYSTEM_RID = (0x00000012);
        const int SECURITY_BUILTIN_DOMAIN_RID = (0x00000020);
        const int SECURITY_WORLD_RID = (0x00000000);
        const byte SECURITY_NT_AUTHORITY = 5;
        const int DOMAIN_GROUP_RID_ADMINS = (0x00000200);
        const int DOMAIN_ALIAS_RID_ADMINS = (0x00000220);

        const int sizeofSECURITY_ATTRIBUTES = 12; // sizeof(SECURITY_ATTRIBUTES);
        const int sizeofSECURITY_DESCRIPTOR = 20; // sizeof(SECURITY_DESCRIPTOR);
        const int sizeofACCESS_ALLOWED_ACE = 12; // sizeof(ACCESS_ALLOWED_ACE);
        const int sizeofACCESS_DENIED_ACE = 12; // sizeof(ACCESS_DENIED_ACE);
        const int sizeofSID_IDENTIFIER_AUTHORITY = 6; // sizeof(SID_IDENTIFIER_AUTHORITY)
        const int sizeofACL = 8; // sizeof(ACL);

        private IntPtr CreateSD(ref IntPtr pDacl)
        {
            IntPtr pSecurityDescriptor = IntPtr.Zero;
            IntPtr pUserSid = IntPtr.Zero;
            IntPtr pAdminSid = IntPtr.Zero;
            IntPtr pNtAuthority = IntPtr.Zero;
            int cbAcl = 0;
            bool status = false;

            pNtAuthority = Marshal.AllocHGlobal(sizeofSID_IDENTIFIER_AUTHORITY);
            if (pNtAuthority == IntPtr.Zero)
                goto cleanup;
            Marshal.WriteInt32(pNtAuthority, 0, 0);
            Marshal.WriteByte(pNtAuthority, 4, 0);
            Marshal.WriteByte(pNtAuthority, 5, SECURITY_NT_AUTHORITY);

            status =
            NativeMethods.AllocateAndInitializeSid(
            pNtAuthority,
            (byte)1,
            SECURITY_AUTHENTICATED_USER_RID,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            ref pUserSid);

            if (!status || pUserSid == IntPtr.Zero)
            {
                goto cleanup;
            }
            status =
            NativeMethods.AllocateAndInitializeSid(
            pNtAuthority,
            (byte)2,
            SECURITY_BUILTIN_DOMAIN_RID,
            DOMAIN_ALIAS_RID_ADMINS,
            0,
            0,
            0,
            0,
            0,
            0,
            ref pAdminSid);

            if (!status || pAdminSid == IntPtr.Zero)
            {
                goto cleanup;
            }
            status = false;
            pSecurityDescriptor = Marshal.AllocHGlobal(sizeofSECURITY_DESCRIPTOR);
            if (pSecurityDescriptor == IntPtr.Zero)
            {
                goto cleanup;
            }
            for (int i = 0; i < sizeofSECURITY_DESCRIPTOR; i++)
                Marshal.WriteByte(pSecurityDescriptor, i, (byte)0);
            cbAcl = sizeofACL
            + (2 * (sizeofACCESS_ALLOWED_ACE))
            + sizeofACCESS_DENIED_ACE
            + NativeMethods.GetLengthSid(pUserSid)
            + NativeMethods.GetLengthSid(pAdminSid);

            pDacl = Marshal.AllocHGlobal(cbAcl);
            if (pDacl == IntPtr.Zero)
            {
                goto cleanup;
            }
            // rights must be added in a certain order.  Namely, deny access first, then add access
            if (NativeMethods.InitializeAcl(pDacl, cbAcl, ACL_REVISION))
                if (NativeMethods.AddAccessDeniedAce(pDacl, ACL_REVISION, WRITE_DAC, pUserSid))
                    if (NativeMethods.AddAccessAllowedAce(pDacl, ACL_REVISION, GENERIC_READ, pUserSid))
                        if (NativeMethods.AddAccessAllowedAce(pDacl, ACL_REVISION, GENERIC_ALL, pAdminSid))
                            if (NativeMethods.InitializeSecurityDescriptor(pSecurityDescriptor, SECURITY_DESCRIPTOR_REVISION))
                                if (NativeMethods.SetSecurityDescriptorDacl(pSecurityDescriptor, true, pDacl, false))
                                {
                                    status = true;
                                }

                            cleanup:
            if (pNtAuthority != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pNtAuthority);
            }
            if (pAdminSid != IntPtr.Zero)
                NativeMethods.FreeSid(pAdminSid);
            if (pUserSid != IntPtr.Zero)
                NativeMethods.FreeSid(pUserSid);
            if (status)
                return pSecurityDescriptor;
            else
            {
                if (pSecurityDescriptor != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pSecurityDescriptor);
                }
            }
            return IntPtr.Zero;
        }

        // SxS: using file mapping API (CreateFileMapping)
        // TODO: review this code for SxS issues (VSDD 540765)
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        bool ISQLDebug.SQLDebug(int dwpidDebugger, int dwpidDebuggee, [MarshalAs(UnmanagedType.LPStr)] string pszMachineName,
        [MarshalAs(UnmanagedType.LPStr)] string pszSDIDLLName, int dwOption, int cbData, byte[] rgbData)
        {
            bool result = false;
            IntPtr hFileMap = IntPtr.Zero;
            IntPtr pMemMap = IntPtr.Zero;
            IntPtr pSecurityDescriptor = IntPtr.Zero;
            IntPtr pSecurityAttributes = IntPtr.Zero;
            IntPtr pDacl = IntPtr.Zero;

            // validate the structure
            if (null == pszMachineName || null == pszSDIDLLName)
                return false;

            if (pszMachineName.Length > TdsEnums.SDCI_MAX_MACHINENAME ||
            pszSDIDLLName.Length > TdsEnums.SDCI_MAX_DLLNAME)
                return false;

            // note that these are ansi strings
            Encoding cp = System.Text.Encoding.GetEncoding(TdsEnums.DEFAULT_ENGLISH_CODE_PAGE_VALUE);
            byte[] rgbMachineName = cp.GetBytes(pszMachineName);
            byte[] rgbSDIDLLName = cp.GetBytes(pszSDIDLLName);

            if (null != rgbData && cbData > TdsEnums.SDCI_MAX_DATA)
                return false;

            string mapFileName;

            // If Win2k or later, prepend "Global\\" to enable this to work through TerminalServices.
            if (ADP.IsPlatformNT5)
            {
                mapFileName = "Global\\" + TdsEnums.SDCI_MAPFILENAME;
            }
            else
            {
                mapFileName = TdsEnums.SDCI_MAPFILENAME;
            }

            mapFileName = mapFileName + dwpidDebuggee.ToString(CultureInfo.InvariantCulture);

            // Create Security Descriptor
            pSecurityDescriptor = CreateSD(ref pDacl);
            pSecurityAttributes = Marshal.AllocHGlobal(sizeofSECURITY_ATTRIBUTES);
            if ((pSecurityDescriptor == IntPtr.Zero) || (pSecurityAttributes == IntPtr.Zero))
                return false;

            Marshal.WriteInt32(pSecurityAttributes, 0, sizeofSECURITY_ATTRIBUTES); // nLength = sizeof(SECURITY_ATTRIBUTES)
            Marshal.WriteIntPtr(pSecurityAttributes, 4, pSecurityDescriptor); // lpSecurityDescriptor = pSecurityDescriptor
            Marshal.WriteInt32(pSecurityAttributes, 8, 0); // bInheritHandle = FALSE
            hFileMap = NativeMethods.CreateFileMappingA(
            ADP.InvalidPtr/*INVALID_HANDLE_VALUE*/,
            pSecurityAttributes,
            0x4/*PAGE_READWRITE*/,
            0,
            Marshal.SizeOf(typeof(MEMMAP)),
            mapFileName);

            if (IntPtr.Zero == hFileMap)
            {
                goto cleanup;
            }


            pMemMap = NativeMethods.MapViewOfFile(hFileMap, 0x6/*FILE_MAP_READ|FILE_MAP_WRITE*/, 0, 0, IntPtr.Zero);

            if (IntPtr.Zero == pMemMap)
            {
                goto cleanup;
            }

            // copy data to memory-mapped file
            // layout of MEMMAP structure is:
            // uint dbgpid
            // uint fOption
            // byte[32] machineName
            // byte[16] sdiDllName
            // uint dbData
            // byte[255] vData
            int offset = 0;
            Marshal.WriteInt32(pMemMap, offset, (int)dwpidDebugger);
            offset += 4;
            Marshal.WriteInt32(pMemMap, offset, (int)dwOption);
            offset += 4;
            Marshal.Copy(rgbMachineName, 0, ADP.IntPtrOffset(pMemMap, offset), rgbMachineName.Length);
            offset += TdsEnums.SDCI_MAX_MACHINENAME;
            Marshal.Copy(rgbSDIDLLName, 0, ADP.IntPtrOffset(pMemMap, offset), rgbSDIDLLName.Length);
            offset += TdsEnums.SDCI_MAX_DLLNAME;
            Marshal.WriteInt32(pMemMap, offset, (int)cbData);
            offset += 4;
            if (null != rgbData)
            {
                Marshal.Copy(rgbData, 0, ADP.IntPtrOffset(pMemMap, offset), (int)cbData);
            }
            NativeMethods.UnmapViewOfFile(pMemMap);
            result = true;
        cleanup:
            if (result == false)
            {
                if (hFileMap != IntPtr.Zero)
                    NativeMethods.CloseHandle(hFileMap);
            }
            if (pSecurityAttributes != IntPtr.Zero)
                Marshal.FreeHGlobal(pSecurityAttributes);
            if (pSecurityDescriptor != IntPtr.Zero)
                Marshal.FreeHGlobal(pSecurityDescriptor);
            if (pDacl != IntPtr.Zero)
                Marshal.FreeHGlobal(pDacl);
            return result;
        }
    }

    // this is a private interface to com+ users
    // do not change this guid
    [
    ComImport,
    ComVisible(true),
    Guid("6cb925bf-c3c0-45b3-9f44-5dd67c7b7fe8"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
    BestFitMapping(false, ThrowOnUnmappableChar = true),
    ]
    interface ISQLDebug
    {

        [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.LinkDemand, Name = "FullTrust")]
        bool SQLDebug(
        int dwpidDebugger,
        int dwpidDebuggee,
        [MarshalAs(UnmanagedType.LPStr)] string pszMachineName,
        [MarshalAs(UnmanagedType.LPStr)] string pszSDIDLLName,
        int dwOption,
        int cbData,
        byte[] rgbData);
    }

    sealed class SqlDebugContext : IDisposable
    {
        // context data
        internal uint pid = 0;
        internal uint tid = 0;
        internal bool active = false;
        // memory-mapped data
        internal IntPtr pMemMap = ADP.PtrZero;
        internal IntPtr hMemMap = ADP.PtrZero;
        internal uint dbgpid = 0;
        internal bool fOption = false;
        internal string machineName = null;
        internal string sdiDllName = null;
        internal byte[] data = null;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // using CloseHandle and UnmapViewOfFile - no exposure
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Nothing to do here
                ;
            }
            if (pMemMap != IntPtr.Zero)
            {
                NativeMethods.UnmapViewOfFile(pMemMap);
                pMemMap = IntPtr.Zero;
            }
            if (hMemMap != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(hMemMap);
                hMemMap = IntPtr.Zero;
            }
            active = false;
        }

        ~SqlDebugContext()
        {
            Dispose(false);
        }

    }

    // native interop memory mapped structure for sdi debugging
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = 1)]
    internal struct MEMMAP
    {
        [MarshalAs(UnmanagedType.U4)]
        internal uint dbgpid; // id of debugger
        [MarshalAs(UnmanagedType.U4)]
        internal uint fOption; // 1 - start debugging, 0 - stop debugging
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal byte[] rgbMachineName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal byte[] rgbDllName;
        [MarshalAs(UnmanagedType.U4)]
        internal uint cbData;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 255)]
        internal byte[] rgbData;
    }
} // Microsoft.Data.SqlClient namespace
