// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.SqlClient.LocalDb;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlConnectionString : DbConnectionOptions
    {
        // instances of this class are intended to be immutable, i.e readonly
        // used by pooling classes so it is much easier to verify correctness
        // when not worried about the class being modified during execution

        // @TODO: Remove in favor DbConnectionStringSynonyms
        // Constant for the number of duplicate options in the connection string
        private static class SYNONYM
        {
            // ip address preference
            internal const string IPADDRESSPREFERENCE = DbConnectionStringSynonyms.IPADDRESSPREFERENCE;
            //application intent
            internal const string APPLICATIONINTENT = DbConnectionStringSynonyms.APPLICATIONINTENT;
            // application name
            internal const string APP = DbConnectionStringSynonyms.APP;
            // attachDBFilename
            internal const string EXTENDED_PROPERTIES = DbConnectionStringSynonyms.EXTENDEDPROPERTIES;
            internal const string INITIAL_FILE_NAME = DbConnectionStringSynonyms.INITIALFILENAME;
            // connect timeout
            internal const string CONNECTION_TIMEOUT = DbConnectionStringSynonyms.CONNECTIONTIMEOUT;
            internal const string TIMEOUT = DbConnectionStringSynonyms.TIMEOUT;
            // current language
            internal const string LANGUAGE = DbConnectionStringSynonyms.LANGUAGE;
            // data source
            internal const string ADDR = DbConnectionStringSynonyms.ADDR;
            internal const string ADDRESS = DbConnectionStringSynonyms.ADDRESS;
            internal const string SERVER = DbConnectionStringSynonyms.SERVER;
            internal const string NETWORK_ADDRESS = DbConnectionStringSynonyms.NETWORKADDRESS;
            // host name in certificate
            internal const string HOSTNAMEINCERTIFICATE = DbConnectionStringSynonyms.HOSTNAMEINCERTIFICATE;
            // server certificate
            internal const string SERVERCERTIFICATE = DbConnectionStringSynonyms.SERVERCERTIFICATE;
            // initial catalog
            internal const string DATABASE = DbConnectionStringSynonyms.DATABASE;
            // integrated security
            internal const string TRUSTED_CONNECTION = DbConnectionStringSynonyms.TRUSTEDCONNECTION;
            //connect retry count
            internal const string CONNECTRETRYCOUNT = DbConnectionStringSynonyms.CONNECTRETRYCOUNT;
            //connect retry interval
            internal const string CONNECTRETRYINTERVAL = DbConnectionStringSynonyms.CONNECTRETRYINTERVAL;
            // load balance timeout
            internal const string Connection_Lifetime = DbConnectionStringSynonyms.ConnectionLifetime;
            // multiple active result sets
            internal const string MULTIPLEACTIVERESULTSETS = DbConnectionStringSynonyms.MULTIPLEACTIVERESULTSETS;
            // multi subnet failover
            internal const string MULTISUBNETFAILOVER = DbConnectionStringSynonyms.MULTISUBNETFAILOVER;
            // network library
            internal const string NET = DbConnectionStringSynonyms.NET;
            internal const string NETWORK = DbConnectionStringSynonyms.NETWORK;
            // pool blocking period
            internal const string POOLBLOCKINGPERIOD = DbConnectionStringSynonyms.POOLBLOCKINGPERIOD;
            // password
            internal const string Pwd = DbConnectionStringSynonyms.Pwd;
            // persist security info
            internal const string PERSISTSECURITYINFO = DbConnectionStringSynonyms.PERSISTSECURITYINFO;
            // trust server certificate
            internal const string TRUSTSERVERCERTIFICATE = DbConnectionStringSynonyms.TRUSTSERVERCERTIFICATE;
            // user id
            internal const string UID = DbConnectionStringSynonyms.UID;
            internal const string User = DbConnectionStringSynonyms.User;
            // workstation id
            internal const string WSID = DbConnectionStringSynonyms.WSID;
            // server SPNs
            internal const string ServerSPN = DbConnectionStringSynonyms.ServerSPN;
            internal const string FailoverPartnerSPN = DbConnectionStringSynonyms.FailoverPartnerSPN;

#if NETFRAMEWORK
            internal const string TRANSPARENTNETWORKIPRESOLUTION = DbConnectionStringSynonyms.TRANSPARENTNETWORKIPRESOLUTION;
#endif

            // make sure to update SynonymCount value below when adding or removing synonyms
        }

        internal enum TypeSystem
        {
            Latest = 2008,
            SQLServer2000 = 2000,
            SQLServer2005 = 2005,
            SQLServer2008 = 2008,
            SQLServer2012 = 2012,
        }

        internal static class TYPESYSTEMVERSION
        {
            internal const string Latest = "Latest";
            internal const string SQL_Server_2000 = "SQL Server 2000";
            internal const string SQL_Server_2005 = "SQL Server 2005";
            internal const string SQL_Server_2008 = "SQL Server 2008";
            internal const string SQL_Server_2012 = "SQL Server 2012";
        }

        internal enum TransactionBindingEnum
        {
            ImplicitUnbind,
            ExplicitUnbind
        }

        internal static class TRANSACTIONBINDING
        {
            internal const string ImplicitUnbind = "Implicit Unbind";
            internal const string ExplicitUnbind = "Explicit Unbind";
        }

#if NETFRAMEWORK
        internal const int SynonymCount = 33;
#else
        internal const int SynonymCount = 30;
#endif // NETFRAMEWORK

        private static Dictionary<string, string> s_sqlClientSynonyms;

        private readonly bool _integratedSecurity;

        private readonly SqlConnectionEncryptOption _encrypt;
        private readonly bool _trustServerCertificate;
        private readonly bool _enlist;
        private readonly bool _mars;
        private readonly bool _persistSecurityInfo;
        private readonly PoolBlockingPeriod _poolBlockingPeriod;
        private readonly bool _pooling;
        private readonly bool _replication;
        private readonly bool _userInstance;
        private readonly bool _multiSubnetFailover;
        private readonly SqlAuthenticationMethod _authType;
        private readonly SqlConnectionColumnEncryptionSetting _columnEncryptionSetting;
        private readonly string _enclaveAttestationUrl;
        private readonly SqlConnectionAttestationProtocol _attestationProtocol;
        private readonly SqlConnectionIPAddressPreference _ipAddressPreference;

        private readonly int _commandTimeout;
        private readonly int _connectTimeout;
        private readonly int _loadBalanceTimeout;
        private readonly int _maxPoolSize;
        private readonly int _minPoolSize;
        private readonly int _packetSize;
        private readonly int _connectRetryCount;
        private readonly int _connectRetryInterval;

        private readonly ApplicationIntent _applicationIntent;
        private readonly string _applicationName;
        private readonly string _attachDBFileName;
        private readonly bool _contextConnection;
        private readonly string _currentLanguage;
        private readonly string _dataSource;
        private readonly string _localDBInstance; // created based on datasource, set to NULL if datasource is not LocalDB
        private readonly string _failoverPartner;
        private readonly string _initialCatalog;
        private readonly string _password;
        private readonly string _userID;
        private readonly string _hostNameInCertificate;
        private readonly string _serverCertificate;
        private readonly string _serverSPN;
        private readonly string _failoverPartnerSPN;

        private readonly string _workstationId;

        private readonly TransactionBindingEnum _transactionBinding;

        private readonly TypeSystem _typeSystemVersion;
        private readonly Version _typeSystemAssemblyVersion;
        private static readonly Version s_constTypeSystemAsmVersion10 = new("10.0.0.0");
        private static readonly Version s_constTypeSystemAsmVersion11 = new("11.0.0.0");

        private readonly string _expandedAttachDBFilename; // expanded during construction so that CreatePermissionSet & Expand are consistent

        // SxS: reading Software\\Microsoft\\MSSQLServer\\Client\\SuperSocketNetLib\Encrypt value from registry
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        internal SqlConnectionString(string connectionString): base(connectionString, GetParseSynonyms())
        {
#if !NETFRAMEWORK
            ThrowUnsupportedIfKeywordSet(DbConnectionStringKeywords.ConnectionReset);

            // Network Library has its own special error message
            if (ContainsKey(DbConnectionStringKeywords.NetworkLibrary))
            {
                throw SQL.NetworkLibraryKeywordNotSupported();
            }
#endif

            _integratedSecurity = ConvertValueToIntegratedSecurity();
            _poolBlockingPeriod = ConvertValueToPoolBlockingPeriod();
            _encrypt = ConvertValueToSqlConnectionEncrypt();
            _enlist = ConvertValueToBoolean(DbConnectionStringKeywords.Enlist, DbConnectionStringDefaults.Enlist);
            _mars = ConvertValueToBoolean(DbConnectionStringKeywords.MultipleActiveResultSets, DbConnectionStringDefaults.MultipleActiveResultSets);
            _persistSecurityInfo = ConvertValueToBoolean(DbConnectionStringKeywords.PersistSecurityInfo, DbConnectionStringDefaults.PersistSecurityInfo);
            _pooling = ConvertValueToBoolean(DbConnectionStringKeywords.Pooling, DbConnectionStringDefaults.Pooling);
            _replication = ConvertValueToBoolean(DbConnectionStringKeywords.Replication, DbConnectionStringDefaults.Replication);
            _userInstance = ConvertValueToBoolean(DbConnectionStringKeywords.UserInstance, DbConnectionStringDefaults.UserInstance);
            _multiSubnetFailover = ConvertValueToBoolean(DbConnectionStringKeywords.MultiSubnetFailover, DbConnectionStringDefaults.MultiSubnetFailover);

            _commandTimeout = ConvertValueToInt32(DbConnectionStringKeywords.CommandTimeout, DbConnectionStringDefaults.CommandTimeout);
            _connectTimeout = ConvertValueToInt32(DbConnectionStringKeywords.ConnectTimeout, DbConnectionStringDefaults.ConnectTimeout);
            _loadBalanceTimeout = ConvertValueToInt32(DbConnectionStringKeywords.LoadBalanceTimeout, DbConnectionStringDefaults.LoadBalanceTimeout);
            _maxPoolSize = ConvertValueToInt32(DbConnectionStringKeywords.MaxPoolSize, DbConnectionStringDefaults.MaxPoolSize);
            _minPoolSize = ConvertValueToInt32(DbConnectionStringKeywords.MinPoolSize, DbConnectionStringDefaults.MinPoolSize);
            _packetSize = ConvertValueToInt32(DbConnectionStringKeywords.PacketSize, DbConnectionStringDefaults.PacketSize);
            _connectRetryCount = ConvertValueToInt32(DbConnectionStringKeywords.ConnectRetryCount, DbConnectionStringDefaults.ConnectRetryCount);
            _connectRetryInterval = ConvertValueToInt32(DbConnectionStringKeywords.ConnectRetryInterval, DbConnectionStringDefaults.ConnectRetryInterval);

            _applicationIntent = ConvertValueToApplicationIntent();
            _applicationName = ConvertValueToString(DbConnectionStringKeywords.ApplicationName, DbConnectionStringDefaults.ApplicationName);
            _attachDBFileName = ConvertValueToString(DbConnectionStringKeywords.AttachDbFilename, DbConnectionStringDefaults.AttachDbFilename);
            _contextConnection = ConvertValueToBoolean(DbConnectionStringKeywords.ContextConnection, DbConnectionStringDefaults.ContextConnection);
            _currentLanguage = ConvertValueToString(DbConnectionStringKeywords.CurrentLanguage, DbConnectionStringDefaults.CurrentLanguage);
            _dataSource = ConvertValueToString(DbConnectionStringKeywords.DataSource, DbConnectionStringDefaults.DataSource);
            _localDBInstance = LocalDbApi.GetLocalDbInstanceNameFromServerName(_dataSource);
            _failoverPartner = ConvertValueToString(DbConnectionStringKeywords.FailoverPartner, DbConnectionStringDefaults.FailoverPartner);
            _initialCatalog = ConvertValueToString(DbConnectionStringKeywords.InitialCatalog, DbConnectionStringDefaults.InitialCatalog);
            _password = ConvertValueToString(DbConnectionStringKeywords.Password, DbConnectionStringDefaults.Password);
            _trustServerCertificate = ConvertValueToBoolean(DbConnectionStringKeywords.TrustServerCertificate, DbConnectionStringDefaults.TrustServerCertificate);
            _authType = ConvertValueToAuthenticationType();
            _columnEncryptionSetting = ConvertValueToColumnEncryptionSetting();
            _enclaveAttestationUrl = ConvertValueToString(DbConnectionStringKeywords.EnclaveAttestationUrl, DbConnectionStringDefaults.EnclaveAttestationUrl);
            _attestationProtocol = ConvertValueToAttestationProtocol();
            _ipAddressPreference = ConvertValueToIPAddressPreference();
            _hostNameInCertificate = ConvertValueToString(DbConnectionStringKeywords.HostNameInCertificate, DbConnectionStringDefaults.HostNameInCertificate);
            _serverCertificate = ConvertValueToString(DbConnectionStringKeywords.ServerCertificate, DbConnectionStringDefaults.ServerCertificate);
            _serverSPN = ConvertValueToString(DbConnectionStringKeywords.ServerSpn, DbConnectionStringDefaults.ServerSpn);
            _failoverPartnerSPN = ConvertValueToString(DbConnectionStringKeywords.FailoverPartnerSpn, DbConnectionStringDefaults.FailoverPartnerSpn);

            // Temporary string - this value is stored internally as an enum.
            string typeSystemVersionString = ConvertValueToString(DbConnectionStringKeywords.TypeSystemVersion, null);
            string transactionBindingString = ConvertValueToString(DbConnectionStringKeywords.TransactionBinding, null);

            _userID = ConvertValueToString(DbConnectionStringKeywords.UserId, DbConnectionStringDefaults.UserId);
            _workstationId = ConvertValueToString(DbConnectionStringKeywords.WorkstationId, null);

            if (_contextConnection)
            {
                throw SQL.ContextConnectionIsUnsupported();
            }

            if (_loadBalanceTimeout < 0)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.LoadBalanceTimeout);
            }

            if (_connectTimeout < 0)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ConnectTimeout);
            }

            if (_commandTimeout < 0)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.CommandTimeout);
            }

            if (_maxPoolSize < 1)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.MaxPoolSize);
            }

            if (_minPoolSize < 0)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.MinPoolSize);
            }
            if (_maxPoolSize < _minPoolSize)
            {
                throw ADP.InvalidMinMaxPoolSizeValues();
            }

            if ((_packetSize < TdsEnums.MIN_PACKET_SIZE) || (TdsEnums.MAX_PACKET_SIZE < _packetSize))
            {
                throw SQL.InvalidPacketSizeValue();
            }

#if NETFRAMEWORK
            // SQLPT 41700: Ignore ResetConnection=False (still validate the keyword/value)
            _connectionReset = ConvertValueToBoolean(DbConnectionStringKeywords.ConnectionReset, DbConnectionStringDefaults.ConnectionReset);
            _transparentNetworkIPResolution = ConvertValueToBoolean(DbConnectionStringKeywords.TransparentNetworkIpResolution, DbConnectionStringDefaults.TransparentNetworkIpResolution);
            _networkLibrary = ConvertValueToString(DbConnectionStringKeywords.NetworkLibrary, null);

            if (_networkLibrary != null)
            { // MDAC 83525
                string networkLibrary = _networkLibrary.Trim().ToLower(CultureInfo.InvariantCulture);
                Dictionary<string, string> netlib = NetlibMapping();
                if (!netlib.ContainsKey(networkLibrary))
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.NetworkLibrary);
                }
                _networkLibrary = netlib[networkLibrary];
            }
            else
            {
                _networkLibrary = DbConnectionStringDefaults.NetworkLibrary;
            }
#endif // NETFRAMEWORK

            if (_encrypt == SqlConnectionEncryptOption.Optional)
            {    // Support legacy registry encryption settings
                const string folder = "Software\\Microsoft\\MSSQLServer\\Client\\SuperSocketNetLib";
                const string value = "Encrypt";

                object obj = ADP.LocalMachineRegistryValue(folder, value);
                if ((obj is int iObj) && (iObj == 1))
                {         // If the registry key exists
                    _encrypt = SqlConnectionEncryptOption.Mandatory;
                }
            }

            ValidateValueLength(_applicationName, TdsEnums.MAXLEN_APPNAME, DbConnectionStringKeywords.ApplicationName);
            ValidateValueLength(_currentLanguage, TdsEnums.MAXLEN_LANGUAGE, DbConnectionStringKeywords.CurrentLanguage);
            ValidateValueLength(_dataSource, TdsEnums.MAXLEN_SERVERNAME, DbConnectionStringKeywords.DataSource);
            ValidateValueLength(_failoverPartner, TdsEnums.MAXLEN_SERVERNAME, DbConnectionStringKeywords.FailoverPartner);
            ValidateValueLength(_initialCatalog, TdsEnums.MAXLEN_DATABASE, DbConnectionStringKeywords.InitialCatalog);
            ValidateValueLength(_password, TdsEnums.MAXLEN_CLIENTSECRET, DbConnectionStringKeywords.Password);
            ValidateValueLength(_userID, TdsEnums.MAXLEN_CLIENTID, DbConnectionStringKeywords.UserId);
            if (_workstationId != null)
            {
                ValidateValueLength(_workstationId, TdsEnums.MAXLEN_HOSTNAME, DbConnectionStringKeywords.WorkstationId);
            }

            if (!string.Equals(DbConnectionStringDefaults.FailoverPartner, _failoverPartner, StringComparison.OrdinalIgnoreCase))
            {
                // fail-over partner is set

                if (_multiSubnetFailover)
                {
                    throw SQL.MultiSubnetFailoverWithFailoverPartner(serverProvidedFailoverPartner: false, internalConnection: null);
                }

                if (string.Equals(DbConnectionStringDefaults.InitialCatalog, _initialCatalog, StringComparison.OrdinalIgnoreCase))
                {
                    throw ADP.MissingConnectionOptionValue(DbConnectionStringKeywords.FailoverPartner, DbConnectionStringKeywords.InitialCatalog);
                }
            }

            // expand during construction so that CreatePermissionSet and Expand are consistent
            _expandedAttachDBFilename = ExpandDataDirectory(DbConnectionStringKeywords.AttachDbFilename, _attachDBFileName);
            if (_expandedAttachDBFilename != null)
            {
                if (0 <= _expandedAttachDBFilename.IndexOf('|'))
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.AttachDbFilename);
                }
                ValidateValueLength(_expandedAttachDBFilename, TdsEnums.MAXLEN_ATTACHDBFILE, DbConnectionStringKeywords.AttachDbFilename);
                if (_localDBInstance == null)
                {
                    // fail fast to verify LocalHost when using |DataDirectory|
                    // still must check again at connect time
                    string host = _dataSource;
#if NETFRAMEWORK
                    string protocol = _networkLibrary;
                    TdsParserStaticMethods.AliasRegistryLookup(ref host, ref protocol);
#endif
                    VerifyLocalHostAndFixup(ref host, true, false /*don't fix-up*/);
                }
            }
            else if (0 <= _attachDBFileName.IndexOf('|'))
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.AttachDbFilename);
            }
            else
            {
                ValidateValueLength(_attachDBFileName, TdsEnums.MAXLEN_ATTACHDBFILE, DbConnectionStringKeywords.AttachDbFilename);
            }
            _typeSystemAssemblyVersion = s_constTypeSystemAsmVersion10;

            if (_userInstance && !string.IsNullOrEmpty(_failoverPartner))
            {
                throw SQL.UserInstanceFailoverNotCompatible();
            }

            if (string.IsNullOrEmpty(typeSystemVersionString))
            {
                typeSystemVersionString = DbConnectionStringDefaults.TypeSystemVersion;
            }

            if (typeSystemVersionString.Equals(TYPESYSTEMVERSION.Latest, StringComparison.OrdinalIgnoreCase))
            {
                _typeSystemVersion = TypeSystem.Latest;
            }
            else if (typeSystemVersionString.Equals(TYPESYSTEMVERSION.SQL_Server_2000, StringComparison.OrdinalIgnoreCase))
            {
                _typeSystemVersion = TypeSystem.SQLServer2000;
            }
            else if (typeSystemVersionString.Equals(TYPESYSTEMVERSION.SQL_Server_2005, StringComparison.OrdinalIgnoreCase))
            {
                _typeSystemVersion = TypeSystem.SQLServer2005;
            }
            else if (typeSystemVersionString.Equals(TYPESYSTEMVERSION.SQL_Server_2008, StringComparison.OrdinalIgnoreCase))
            {
                _typeSystemVersion = TypeSystem.SQLServer2008;
            }
            else if (typeSystemVersionString.Equals(TYPESYSTEMVERSION.SQL_Server_2012, StringComparison.OrdinalIgnoreCase))
            {
                _typeSystemVersion = TypeSystem.SQLServer2012;
                _typeSystemAssemblyVersion = s_constTypeSystemAsmVersion11;
            }
            else
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.TypeSystemVersion);
            }

            if (string.IsNullOrEmpty(transactionBindingString))
            {
                transactionBindingString = DbConnectionStringDefaults.TransactionBinding;
            }

            if (transactionBindingString.Equals(TRANSACTIONBINDING.ImplicitUnbind, StringComparison.OrdinalIgnoreCase))
            {
                _transactionBinding = TransactionBindingEnum.ImplicitUnbind;
            }
            else if (transactionBindingString.Equals(TRANSACTIONBINDING.ExplicitUnbind, StringComparison.OrdinalIgnoreCase))
            {
                _transactionBinding = TransactionBindingEnum.ExplicitUnbind;
            }
            else
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.TransactionBinding);
            }

            if (_applicationIntent == ApplicationIntent.ReadOnly && !string.IsNullOrEmpty(_failoverPartner))
            {
                throw SQL.ROR_FailoverNotSupportedConnString();
            }

            if ((_connectRetryCount < 0) || (_connectRetryCount > 255))
            {
                throw ADP.InvalidConnectRetryCountValue();
            }

            if ((_connectRetryInterval < 1) || (_connectRetryInterval > 60))
            {
                throw ADP.InvalidConnectRetryIntervalValue();
            }

            if (Authentication != SqlAuthenticationMethod.NotSpecified && _integratedSecurity == true)
            {
                throw SQL.AuthenticationAndIntegratedSecurity();
            }

            if (Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated && _hasPasswordKeyword)
            {
                throw SQL.IntegratedWithPassword();
            }

            if (Authentication == SqlAuthenticationMethod.ActiveDirectoryInteractive && _hasPasswordKeyword)
            {
                throw SQL.InteractiveWithPassword();
            }

            if (Authentication == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow && (_hasUserIdKeyword || _hasPasswordKeyword))
            {
                throw SQL.DeviceFlowWithUsernamePassword();
            }

            if (Authentication == SqlAuthenticationMethod.ActiveDirectoryManagedIdentity && _hasPasswordKeyword)
            {
                throw SQL.NonInteractiveWithPassword(DbConnectionStringUtilities.ActiveDirectoryManagedIdentityString);
            }

            if (Authentication == SqlAuthenticationMethod.ActiveDirectoryMSI && _hasPasswordKeyword)
            {
                throw SQL.NonInteractiveWithPassword(DbConnectionStringUtilities.ActiveDirectoryMSIString);
            }

            if (Authentication == SqlAuthenticationMethod.ActiveDirectoryDefault && _hasPasswordKeyword)
            {
                throw SQL.NonInteractiveWithPassword(DbConnectionStringUtilities.ActiveDirectoryDefaultString);
            }

            if (Authentication == SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity && _hasPasswordKeyword)
            {
                throw SQL.NonInteractiveWithPassword(DbConnectionStringUtilities.ActiveDirectoryWorkloadIdentityString);
            }
        }

        // This c-tor is used to create SSE and user instance connection strings when user instance is set to true
        // BUG (VSTFDevDiv) 479687: Using TransactionScope with Linq2SQL against user instances fails with "connection has been broken" message
        internal SqlConnectionString(SqlConnectionString connectionOptions, string dataSource, bool userInstance, bool? setEnlistValue) : base(connectionOptions)
        {
            _integratedSecurity = connectionOptions._integratedSecurity;
            _encrypt = connectionOptions._encrypt;

            if (setEnlistValue.HasValue)
            {
                _enlist = setEnlistValue.Value;
            }
            else
            {
                _enlist = connectionOptions._enlist;
            }

            _mars = connectionOptions._mars;
            _persistSecurityInfo = connectionOptions._persistSecurityInfo;
            _pooling = connectionOptions._pooling;
            _replication = connectionOptions._replication;
            _userInstance = userInstance;
            _commandTimeout = connectionOptions._commandTimeout;
            _connectTimeout = connectionOptions._connectTimeout;
            _loadBalanceTimeout = connectionOptions._loadBalanceTimeout;
            _poolBlockingPeriod = connectionOptions._poolBlockingPeriod;
            _maxPoolSize = connectionOptions._maxPoolSize;
            _minPoolSize = connectionOptions._minPoolSize;
            _multiSubnetFailover = connectionOptions._multiSubnetFailover;
            _packetSize = connectionOptions._packetSize;
            _applicationName = connectionOptions._applicationName;
            _attachDBFileName = connectionOptions._attachDBFileName;
            _contextConnection = connectionOptions._contextConnection;
            _currentLanguage = connectionOptions._currentLanguage;
            _dataSource = dataSource;
            _localDBInstance = LocalDbApi.GetLocalDbInstanceNameFromServerName(_dataSource);
            _failoverPartner = connectionOptions._failoverPartner;
            _initialCatalog = connectionOptions._initialCatalog;
            _password = connectionOptions._password;
            _userID = connectionOptions._userID;
            _workstationId = connectionOptions._workstationId;
            _expandedAttachDBFilename = connectionOptions._expandedAttachDBFilename;
            _typeSystemVersion = connectionOptions._typeSystemVersion;
            _transactionBinding = connectionOptions._transactionBinding;
            _applicationIntent = connectionOptions._applicationIntent;
            _connectRetryCount = connectionOptions._connectRetryCount;
            _connectRetryInterval = connectionOptions._connectRetryInterval;
            _authType = connectionOptions._authType;
            _columnEncryptionSetting = connectionOptions._columnEncryptionSetting;
            _enclaveAttestationUrl = connectionOptions._enclaveAttestationUrl;
            _attestationProtocol = connectionOptions._attestationProtocol;
            _serverSPN = connectionOptions._serverSPN;
            _failoverPartnerSPN = connectionOptions._failoverPartnerSPN;
            _hostNameInCertificate = connectionOptions._hostNameInCertificate;
#if NETFRAMEWORK
            _connectionReset = connectionOptions._connectionReset;
            _transparentNetworkIPResolution = connectionOptions._transparentNetworkIPResolution;
            _networkLibrary = connectionOptions._networkLibrary;
            _typeSystemAssemblyVersion = connectionOptions._typeSystemAssemblyVersion;
#endif // NETFRAMEWORK
            ValidateValueLength(_dataSource, TdsEnums.MAXLEN_SERVERNAME, DbConnectionStringKeywords.DataSource);
        }

        internal bool IntegratedSecurity => _integratedSecurity;

        // We always initialize in Async mode so that both synchronous and asynchronous methods
        // will work.  In the future we can deprecate the keyword entirely.
        internal bool Asynchronous => true;
        // SQLPT 41700: Ignore ResetConnection=False, always reset the connection for security
        internal bool ConnectionReset => true;
        //        internal bool EnableUdtDownload => _enableUdtDownload;} }
        internal SqlConnectionEncryptOption Encrypt => _encrypt;
        internal string HostNameInCertificate => _hostNameInCertificate;
        internal bool TrustServerCertificate => _trustServerCertificate;
        public string ServerCertificate => _serverCertificate;
        internal bool Enlist => _enlist;
        internal bool MARS => _mars;
        internal bool MultiSubnetFailover => _multiSubnetFailover;
        internal SqlAuthenticationMethod Authentication => _authType;
        internal SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting => _columnEncryptionSetting;
        internal string EnclaveAttestationUrl => _enclaveAttestationUrl;
        internal SqlConnectionAttestationProtocol AttestationProtocol => _attestationProtocol;
        internal SqlConnectionIPAddressPreference IPAddressPreference => _ipAddressPreference;
        internal bool PersistSecurityInfo => _persistSecurityInfo;
        internal bool Pooling => _pooling;
        internal bool Replication => _replication;
        internal bool UserInstance => _userInstance;

        internal int CommandTimeout => _commandTimeout;
        internal int ConnectTimeout => _connectTimeout;
        internal int LoadBalanceTimeout => _loadBalanceTimeout;
        internal int MaxPoolSize => _maxPoolSize;
        internal int MinPoolSize => _minPoolSize;
        internal int PacketSize => _packetSize;
        internal int ConnectRetryCount => _connectRetryCount;
        internal int ConnectRetryInterval => _connectRetryInterval;

        internal ApplicationIntent ApplicationIntent => _applicationIntent;
        internal string ApplicationName => _applicationName;
        internal string AttachDBFilename => _attachDBFileName;
        internal string CurrentLanguage => _currentLanguage;
        internal string DataSource => _dataSource;
        internal string LocalDBInstance => _localDBInstance;
        internal string FailoverPartner => _failoverPartner;
        internal string InitialCatalog => _initialCatalog;
        internal string Password => _password;
        internal string UserID => _userID;
        internal string WorkstationId => _workstationId;
        internal PoolBlockingPeriod PoolBlockingPeriod => _poolBlockingPeriod;
        internal string ServerSPN => _serverSPN;
        internal string FailoverPartnerSPN => _failoverPartnerSPN;

        internal TypeSystem TypeSystemVersion => _typeSystemVersion;
        internal Version TypeSystemAssemblyVersion => _typeSystemAssemblyVersion;

        internal TransactionBindingEnum TransactionBinding => _transactionBinding;

        internal bool EnforceLocalHost
        {
            get
            {
                // so tdsparser.connect can determine if SqlConnection.UserConnectionOptions
                // needs to enforce local host after datasource alias lookup
                return _expandedAttachDBFilename != null && _localDBInstance == null;
            }
        }

        protected internal override string Expand()
        {
            if (_expandedAttachDBFilename != null)
            {
#if NETFRAMEWORK
                return ExpandKeyword(DbConnectionStringKeywords.AttachDbFilename, _expandedAttachDBFilename);
#else
                return ExpandAttachDbFileName(_expandedAttachDBFilename);
#endif
            }
            else
            {
                return base.Expand();
            }
        }

        private static bool CompareHostName(ref string host, string name, bool fixup)
        {
            // same computer name or same computer name + "\named instance"
            bool equal = false;

            if (host.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                if (fixup)
                {
                    host = ".";
                }
                equal = true;
            }
            else if (host.StartsWith(name + @"\", StringComparison.OrdinalIgnoreCase))
            {
                if (fixup)
                {
                    host = "." + host.Substring(name.Length);
                }
                equal = true;
            }
            return equal;
        }

        // This dictionary is meant to be read-only translation of parsed string
        // keywords/synonyms to a known keyword string.
        internal static Dictionary<string, string> GetParseSynonyms()
        {
            Dictionary<string, string> synonyms = s_sqlClientSynonyms;
            if (synonyms == null)
            {

                int count = SqlConnectionStringBuilder.KeywordsCount + SynonymCount;
                synonyms = new Dictionary<string, string>(count, StringComparer.OrdinalIgnoreCase)
                {
                    { DbConnectionStringKeywords.ApplicationIntent, DbConnectionStringKeywords.ApplicationIntent },
                    { DbConnectionStringKeywords.ApplicationName, DbConnectionStringKeywords.ApplicationName },
                    { DbConnectionStringKeywords.AttachDbFilename, DbConnectionStringKeywords.AttachDbFilename },
                    { DbConnectionStringKeywords.AttestationProtocol, DbConnectionStringKeywords.AttestationProtocol},
                    { DbConnectionStringKeywords.Authentication, DbConnectionStringKeywords.Authentication },
                    { DbConnectionStringKeywords.ColumnEncryptionSetting, DbConnectionStringKeywords.ColumnEncryptionSetting },
                    { DbConnectionStringKeywords.CommandTimeout, DbConnectionStringKeywords.CommandTimeout },
                    { DbConnectionStringKeywords.ConnectRetryCount, DbConnectionStringKeywords.ConnectRetryCount },
                    { DbConnectionStringKeywords.ConnectRetryInterval, DbConnectionStringKeywords.ConnectRetryInterval },
                    { DbConnectionStringKeywords.ConnectTimeout, DbConnectionStringKeywords.ConnectTimeout },
                    { DbConnectionStringKeywords.ContextConnection, DbConnectionStringKeywords.ContextConnection },
                    { DbConnectionStringKeywords.CurrentLanguage, DbConnectionStringKeywords.CurrentLanguage },
                    { DbConnectionStringKeywords.DataSource, DbConnectionStringKeywords.DataSource },
                    { DbConnectionStringKeywords.EnclaveAttestationUrl, DbConnectionStringKeywords.EnclaveAttestationUrl },
                    { DbConnectionStringKeywords.Encrypt, DbConnectionStringKeywords.Encrypt },
                    { DbConnectionStringKeywords.Enlist, DbConnectionStringKeywords.Enlist },
                    { DbConnectionStringKeywords.FailoverPartner, DbConnectionStringKeywords.FailoverPartner },
                    { DbConnectionStringKeywords.FailoverPartnerSpn, DbConnectionStringKeywords.FailoverPartnerSpn },
                    { DbConnectionStringKeywords.HostNameInCertificate, DbConnectionStringKeywords.HostNameInCertificate },
                    { DbConnectionStringKeywords.ServerCertificate, DbConnectionStringKeywords.ServerCertificate},
                    { DbConnectionStringKeywords.InitialCatalog, DbConnectionStringKeywords.InitialCatalog },
                    { DbConnectionStringKeywords.IntegratedSecurity, DbConnectionStringKeywords.IntegratedSecurity },
                    { DbConnectionStringKeywords.IpAddressPreference, DbConnectionStringKeywords.IpAddressPreference },
                    { DbConnectionStringKeywords.LoadBalanceTimeout, DbConnectionStringKeywords.LoadBalanceTimeout },
                    { DbConnectionStringKeywords.MultipleActiveResultSets, DbConnectionStringKeywords.MultipleActiveResultSets },
                    { DbConnectionStringKeywords.MaxPoolSize, DbConnectionStringKeywords.MaxPoolSize },
                    { DbConnectionStringKeywords.MinPoolSize, DbConnectionStringKeywords.MinPoolSize },
                    { DbConnectionStringKeywords.MultiSubnetFailover, DbConnectionStringKeywords.MultiSubnetFailover },
                    { DbConnectionStringKeywords.PacketSize, DbConnectionStringKeywords.PacketSize },
                    { DbConnectionStringKeywords.Password, DbConnectionStringKeywords.Password },
                    { DbConnectionStringKeywords.PersistSecurityInfo, DbConnectionStringKeywords.PersistSecurityInfo },
                    { DbConnectionStringKeywords.Pooling, DbConnectionStringKeywords.Pooling },
                    { DbConnectionStringKeywords.PoolBlockingPeriod, DbConnectionStringKeywords.PoolBlockingPeriod },
                    { DbConnectionStringKeywords.Replication, DbConnectionStringKeywords.Replication },
                    { DbConnectionStringKeywords.ServerSpn, DbConnectionStringKeywords.ServerSpn },
                    { DbConnectionStringKeywords.TrustServerCertificate, DbConnectionStringKeywords.TrustServerCertificate },
                    { DbConnectionStringKeywords.TransactionBinding, DbConnectionStringKeywords.TransactionBinding },
                    { DbConnectionStringKeywords.TypeSystemVersion, DbConnectionStringKeywords.TypeSystemVersion },
                    { DbConnectionStringKeywords.UserId, DbConnectionStringKeywords.UserId },
                    { DbConnectionStringKeywords.UserInstance, DbConnectionStringKeywords.UserInstance },
                    { DbConnectionStringKeywords.WorkstationId, DbConnectionStringKeywords.WorkstationId },

                    { SYNONYM.IPADDRESSPREFERENCE, DbConnectionStringKeywords.IpAddressPreference },
                    { SYNONYM.APP, DbConnectionStringKeywords.ApplicationName },
                    { SYNONYM.APPLICATIONINTENT, DbConnectionStringKeywords.ApplicationIntent },
                    { SYNONYM.EXTENDED_PROPERTIES, DbConnectionStringKeywords.AttachDbFilename },
                    { SYNONYM.HOSTNAMEINCERTIFICATE, DbConnectionStringKeywords.HostNameInCertificate },
                    { SYNONYM.SERVERCERTIFICATE, DbConnectionStringKeywords.ServerCertificate},
                    { SYNONYM.INITIAL_FILE_NAME, DbConnectionStringKeywords.AttachDbFilename },
                    { SYNONYM.CONNECTRETRYCOUNT, DbConnectionStringKeywords.ConnectRetryCount },
                    { SYNONYM.CONNECTRETRYINTERVAL, DbConnectionStringKeywords.ConnectRetryInterval },
                    { SYNONYM.CONNECTION_TIMEOUT, DbConnectionStringKeywords.ConnectTimeout },
                    { SYNONYM.TIMEOUT, DbConnectionStringKeywords.ConnectTimeout },
                    { SYNONYM.LANGUAGE, DbConnectionStringKeywords.CurrentLanguage },
                    { SYNONYM.ADDR, DbConnectionStringKeywords.DataSource },
                    { SYNONYM.ADDRESS, DbConnectionStringKeywords.DataSource },
                    { SYNONYM.MULTIPLEACTIVERESULTSETS, DbConnectionStringKeywords.MultipleActiveResultSets },
                    { SYNONYM.MULTISUBNETFAILOVER, DbConnectionStringKeywords.MultiSubnetFailover },
                    { SYNONYM.NETWORK_ADDRESS, DbConnectionStringKeywords.DataSource },
                    { SYNONYM.POOLBLOCKINGPERIOD, DbConnectionStringKeywords.PoolBlockingPeriod},
                    { SYNONYM.SERVER, DbConnectionStringKeywords.DataSource },
                    { SYNONYM.DATABASE, DbConnectionStringKeywords.InitialCatalog },
                    { SYNONYM.TRUSTED_CONNECTION, DbConnectionStringKeywords.IntegratedSecurity },
                    { SYNONYM.TRUSTSERVERCERTIFICATE, DbConnectionStringKeywords.TrustServerCertificate },
                    { SYNONYM.Connection_Lifetime, DbConnectionStringKeywords.LoadBalanceTimeout },
                    { SYNONYM.Pwd, DbConnectionStringKeywords.Password },
                    { SYNONYM.PERSISTSECURITYINFO, DbConnectionStringKeywords.PersistSecurityInfo },
                    { SYNONYM.UID, DbConnectionStringKeywords.UserId },
                    { SYNONYM.User, DbConnectionStringKeywords.UserId },
                    { SYNONYM.WSID, DbConnectionStringKeywords.WorkstationId },
                    { SYNONYM.ServerSPN, DbConnectionStringKeywords.ServerSpn },
                    { SYNONYM.FailoverPartnerSPN, DbConnectionStringKeywords.FailoverPartnerSpn },
#if NETFRAMEWORK
                    { DbConnectionStringKeywords.ConnectionReset, DbConnectionStringKeywords.ConnectionReset },
                    { DbConnectionStringKeywords.NetworkLibrary, DbConnectionStringKeywords.NetworkLibrary },
                    { DbConnectionStringKeywords.TransparentNetworkIpResolution, DbConnectionStringKeywords.TransparentNetworkIpResolution },
                    { SYNONYM.NET, DbConnectionStringKeywords.NetworkLibrary },
                    { SYNONYM.NETWORK, DbConnectionStringKeywords.NetworkLibrary },
                    { SYNONYM.TRANSPARENTNETWORKIPRESOLUTION, DbConnectionStringKeywords.TransparentNetworkIpResolution },
#endif // NETFRAMEWORK
                };
                Debug.Assert(synonyms.Count == count, $"incorrect initial ParseSynonyms size {count} v/s {synonyms.Count}");
                Interlocked.CompareExchange(ref s_sqlClientSynonyms, synonyms, null);
            }
            return synonyms;
        }

        internal string ObtainWorkstationId()
        {
            // If not supplied by the user, the default value is the MachineName
            // Note: In Longhorn you'll be able to rename a machine without
            // rebooting.  Therefore, don't cache this machine name.
            string result = WorkstationId;
            if (result == null)
            {
                // permission to obtain Environment.MachineName is Asserted
                // since permission to open the connection has been granted
                // the information is shared with the server, but not directly with the user
                result = ADP.MachineName();
                ValidateValueLength(result, TdsEnums.MAXLEN_HOSTNAME, DbConnectionStringKeywords.WorkstationId);
            }
            return result;
        }

        private void ValidateValueLength(string value, int limit, string key)
        {
            if (limit < value.Length)
            {
                throw ADP.InvalidConnectionOptionValueLength(key, limit);
            }
        }

        internal static void VerifyLocalHostAndFixup(ref string host, bool enforceLocalHost, bool fixup)
        {
            if (string.IsNullOrEmpty(host))
            {
                if (fixup)
                {
                    host = ".";
                }
            }
            else if (!CompareHostName(ref host, @".", fixup) &&
                     !CompareHostName(ref host, @"(local)", fixup))
            {
                // Fix-up completed in CompareHostName if return value true.
                string name = GetComputerNameDnsFullyQualified(); // i.e, machine.location.corp.company.com
                if (!CompareHostName(ref host, name, fixup))
                {
                    int separatorPos = name.IndexOf('.'); // to compare just 'machine' part
                    if ((separatorPos <= 0) || !CompareHostName(ref host, name.Substring(0, separatorPos), fixup))
                    {
                        if (enforceLocalHost)
                        {
                            throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.AttachDbFilename);
                        }
                    }
                }
            }
        }

        private static string GetComputerNameDnsFullyQualified()
        {
            try
            {
                var domainName = "." + IPGlobalProperties.GetIPGlobalProperties().DomainName;
                var hostName = Dns.GetHostName();
                if (domainName != "." && !hostName.EndsWith(domainName, StringComparison.Ordinal))
                    hostName += domainName;
                return hostName;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return Environment.MachineName;
            }
        }

        internal ApplicationIntent ConvertValueToApplicationIntent()
        {
            if (!TryGetParsetableValue(DbConnectionStringKeywords.ApplicationIntent, out string value))
            {
                return DbConnectionStringDefaults.ApplicationIntent;
            }

            // when wrong value is used in the connection string provided to SqlConnection.ConnectionString or c-tor,
            // wrap Format and Overflow exceptions with Argument one, to be consistent with rest of the keyword types (like int and bool)
            try
            {
                return DbConnectionStringUtilities.ConvertToApplicationIntent(DbConnectionStringKeywords.ApplicationIntent, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ApplicationIntent, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ApplicationIntent, e);
            }
            // ArgumentException and other types are raised as is (no wrapping)
        }

#if NET
        internal void ThrowUnsupportedIfKeywordSet(string keyword)
        {
            if (ContainsKey(keyword))
            {
                throw SQL.UnsupportedKeyword(keyword);
            }
        }
#endif

        internal SqlAuthenticationMethod ConvertValueToAuthenticationType()
        {
            if (!TryGetParsetableValue(DbConnectionStringKeywords.Authentication, out string value))
            {
                return DbConnectionStringDefaults.Authentication;
            }

            try
            {
                return DbConnectionStringUtilities.ConvertToAuthenticationType(DbConnectionStringKeywords.Authentication, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.Authentication, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.Authentication, e);
            }
        }

        /// <summary>
        /// Convert the value to SqlConnectionColumnEncryptionSetting.
        /// </summary>
        /// <returns></returns>
        internal SqlConnectionColumnEncryptionSetting ConvertValueToColumnEncryptionSetting()
        {
            if (!TryGetParsetableValue(DbConnectionStringKeywords.ColumnEncryptionSetting, out string value))
            {
                return DbConnectionStringDefaults.ColumnEncryptionSetting;
            }

            try
            {
                return DbConnectionStringUtilities.ConvertToColumnEncryptionSetting(DbConnectionStringKeywords.ColumnEncryptionSetting, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ColumnEncryptionSetting, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ColumnEncryptionSetting, e);
            }
        }

        /// <summary>
        /// Convert the value to SqlConnectionAttestationProtocol
        /// </summary>
        /// <returns></returns>
        internal SqlConnectionAttestationProtocol ConvertValueToAttestationProtocol()
        {
            if (!TryGetParsetableValue(DbConnectionStringKeywords.AttestationProtocol, out string value))
            {
                return DbConnectionStringDefaults.AttestationProtocol;
            }

            try
            {
                return AttestationProtocolUtilities.ConvertToAttestationProtocol(DbConnectionStringKeywords.AttestationProtocol, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.AttestationProtocol, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.AttestationProtocol, e);
            }
        }

        /// <summary>
        /// Convert the value to SqlConnectionIPAddressPreference
        /// </summary>
        /// <returns></returns>
        internal SqlConnectionIPAddressPreference ConvertValueToIPAddressPreference()
        {
            if (!TryGetParsetableValue(DbConnectionStringKeywords.IpAddressPreference, out string value))
            {
                return DbConnectionStringDefaults.IpAddressPreference;
            }

            try
            {
                return IpAddressPreferenceUtilities.ConvertToIPAddressPreference(DbConnectionStringKeywords.IpAddressPreference, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.IpAddressPreference, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.IpAddressPreference, e);
            }
        }

        internal PoolBlockingPeriod ConvertValueToPoolBlockingPeriod()
        {
            if (!TryGetParsetableValue(DbConnectionStringKeywords.PoolBlockingPeriod, out string value))
            {
                return DbConnectionStringDefaults.PoolBlockingPeriod;
            }

            try
            {
                return PoolBlockingUtilities.ConvertToPoolBlockingPeriod(DbConnectionStringKeywords.PoolBlockingPeriod, value);
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.PoolBlockingPeriod, e);
            }
        }

        internal SqlConnectionEncryptOption ConvertValueToSqlConnectionEncrypt()
        {
            if (!TryGetParsetableValue(DbConnectionStringKeywords.Encrypt, out string value))
            {
                return DbConnectionStringDefaults.Encrypt;
            }

            try
            {
                return AttestationProtocolUtilities.ConvertToSqlConnectionEncryptOption(DbConnectionStringKeywords.Encrypt, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.Encrypt, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.Encrypt, e);
            }
        }

        static internal Dictionary<string, string> NetlibMapping()
        {
            const int NetLibCount = 8;

            Dictionary<string, string> hash = s_netlibMapping;
            if (hash == null)
            {
                hash = new Dictionary<string, string>(NetLibCount)
                {
                    { NETLIB.TCPIP, TdsEnums.TCP },
                    { NETLIB.NamedPipes, TdsEnums.NP },
                    { NETLIB.Multiprotocol, TdsEnums.RPC },
                    { NETLIB.BanyanVines, TdsEnums.BV },
                    { NETLIB.AppleTalk, TdsEnums.ADSP },
                    { NETLIB.IPXSPX, TdsEnums.SPX },
                    { NETLIB.VIA, TdsEnums.VIA },
                    { NETLIB.SharedMemory, TdsEnums.LPC }
                };
                Debug.Assert(NetLibCount == hash.Count, "incorrect initial NetlibMapping size");
                s_netlibMapping = hash;
            }
            return hash;
        }
        static internal bool ValidProtocol(string protocol)
        {
            return protocol switch
            {
                TdsEnums.TCP or TdsEnums.NP or TdsEnums.VIA or TdsEnums.LPC => true,
                //              case TdsEnums.RPC  :  Invalid Protocols
                //              case TdsEnums.BV   :
                //              case TdsEnums.ADSP :
                //              case TdsEnums.SPX  :
                _ => false,
            };
        }

        // the following are all inserted as keys into the _netlibMapping hash
        internal static class NETLIB
        {
            internal const string AppleTalk = "dbmsadsn";
            internal const string BanyanVines = "dbmsvinn";
            internal const string IPXSPX = "dbmsspxn";
            internal const string Multiprotocol = "dbmsrpcn";
            internal const string NamedPipes = "dbnmpntw";
            internal const string SharedMemory = "dbmslpcn";
            internal const string TCPIP = "dbmssocn";
            internal const string VIA = "dbmsgnet";
        }

        private static Dictionary<string, string> s_netlibMapping;

#if NETFRAMEWORK
        protected internal override PermissionSet CreatePermissionSet()
        {
            PermissionSet permissionSet = new(PermissionState.None);
            permissionSet.AddPermission(new SqlClientPermission(this));
            return permissionSet;
        }

        private readonly bool _connectionReset;
        private readonly bool _transparentNetworkIPResolution;
        private readonly string _networkLibrary;

        internal bool TransparentNetworkIPResolution => _transparentNetworkIPResolution;
        internal string NetworkLibrary => _networkLibrary;

#endif // NETFRAMEWORK
    }
}
