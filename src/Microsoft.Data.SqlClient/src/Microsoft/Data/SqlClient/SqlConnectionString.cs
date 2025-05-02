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

        // @TODO: Remove this in favor of using DbConnectionStringDefaults??
        internal static class DEFAULT
        {
            internal const ApplicationIntent ApplicationIntent = DbConnectionStringDefaults.ApplicationIntent;
            internal const string Application_Name = DbConnectionStringDefaults.ApplicationName;
            internal const string AttachDBFilename = DbConnectionStringDefaults.AttachDBFilename;
            internal const int Command_Timeout = DbConnectionStringDefaults.CommandTimeout;
            internal const int Connect_Timeout = DbConnectionStringDefaults.ConnectTimeout;
            internal const string Current_Language = DbConnectionStringDefaults.CurrentLanguage;
            internal const string Data_Source = DbConnectionStringDefaults.DataSource;
            internal static readonly SqlConnectionEncryptOption Encrypt = DbConnectionStringDefaults.Encrypt;
            internal const string HostNameInCertificate = DbConnectionStringDefaults.HostNameInCertificate;
            internal const string ServerCertificate = DbConnectionStringDefaults.ServerCertificate;
            internal const bool Enlist = DbConnectionStringDefaults.Enlist;
            internal const string FailoverPartner = DbConnectionStringDefaults.FailoverPartner;
            internal const string Initial_Catalog = DbConnectionStringDefaults.InitialCatalog;
            internal const bool Integrated_Security = DbConnectionStringDefaults.IntegratedSecurity;
            internal const int Load_Balance_Timeout = DbConnectionStringDefaults.LoadBalanceTimeout;
            internal const bool MARS = DbConnectionStringDefaults.MultipleActiveResultSets;
            internal const int Max_Pool_Size = DbConnectionStringDefaults.MaxPoolSize;
            internal const int Min_Pool_Size = DbConnectionStringDefaults.MinPoolSize;
            internal const bool MultiSubnetFailover = DbConnectionStringDefaults.MultiSubnetFailover;
            internal const int Packet_Size = DbConnectionStringDefaults.PacketSize;
            internal const string Password = DbConnectionStringDefaults.Password;
            internal const bool Persist_Security_Info = DbConnectionStringDefaults.PersistSecurityInfo;
            internal const PoolBlockingPeriod PoolBlockingPeriod = DbConnectionStringDefaults.PoolBlockingPeriod;
            internal const bool Pooling = DbConnectionStringDefaults.Pooling;
            internal const bool TrustServerCertificate = DbConnectionStringDefaults.TrustServerCertificate;
            internal const string Type_System_Version = DbConnectionStringDefaults.TypeSystemVersion;
            internal const string User_ID = DbConnectionStringDefaults.UserID;
            internal const bool User_Instance = DbConnectionStringDefaults.UserInstance;
            internal const bool Replication = DbConnectionStringDefaults.Replication;
            internal const int Connect_Retry_Count = DbConnectionStringDefaults.ConnectRetryCount;
            internal const int Connect_Retry_Interval = DbConnectionStringDefaults.ConnectRetryInterval;
            internal const string EnclaveAttestationUrl = DbConnectionStringDefaults.EnclaveAttestationUrl;
            internal const SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting = DbConnectionStringDefaults.ColumnEncryptionSetting;
            internal static readonly SqlAuthenticationMethod Authentication = DbConnectionStringDefaults.Authentication;
            internal static readonly SqlConnectionAttestationProtocol AttestationProtocol = DbConnectionStringDefaults.AttestationProtocol;
            internal static readonly SqlConnectionIPAddressPreference IpAddressPreference = DbConnectionStringDefaults.IPAddressPreference;
            internal const string ServerSPN = DbConnectionStringDefaults.ServerSPN;
            internal const string FailoverPartnerSPN = DbConnectionStringDefaults.FailoverPartnerSPN;
            internal const bool Context_Connection = DbConnectionStringDefaults.ContextConnection;
#if NETFRAMEWORK
            internal static readonly bool TransparentNetworkIPResolution = DbConnectionStringDefaults.TransparentNetworkIPResolution;
            internal const bool Connection_Reset = DbConnectionStringDefaults.ConnectionReset;
            internal const string Network_Library = DbConnectionStringDefaults.NetworkLibrary;
#endif // NETFRAMEWORK
        }

        // @TODO: Remove in favor of DbConnectionStringKeywords
        // SqlConnection ConnectionString Options
        internal static class KEY
        {
            internal const string ApplicationIntent = DbConnectionStringKeywords.ApplicationIntent;
            internal const string Application_Name = DbConnectionStringKeywords.ApplicationName;
            internal const string AttachDBFilename = DbConnectionStringKeywords.AttachDBFilename;
            internal const string PoolBlockingPeriod = DbConnectionStringKeywords.PoolBlockingPeriod;
            internal const string ColumnEncryptionSetting = DbConnectionStringKeywords.ColumnEncryptionSetting;
            internal const string EnclaveAttestationUrl = DbConnectionStringKeywords.EnclaveAttestationUrl;
            internal const string AttestationProtocol = DbConnectionStringKeywords.AttestationProtocol;
            internal const string IPAddressPreference = DbConnectionStringKeywords.IPAddressPreference;

            internal const string Command_Timeout = DbConnectionStringKeywords.CommandTimeout;
            internal const string Connect_Timeout = DbConnectionStringKeywords.ConnectTimeout;
            internal const string Connection_Reset = DbConnectionStringKeywords.ConnectionReset;
            internal const string Context_Connection = DbConnectionStringKeywords.ContextConnection;
            internal const string Current_Language = DbConnectionStringKeywords.CurrentLanguage;
            internal const string Data_Source = DbConnectionStringKeywords.DataSource;

            // Encrypt related
            internal const string Encrypt = DbConnectionStringKeywords.Encrypt;
            internal const string HostNameInCertificate = DbConnectionStringKeywords.HostNameInCertificate;
            internal const string ServerCertificate = DbConnectionStringKeywords.ServerCertificate;

            internal const string Enlist = DbConnectionStringKeywords.Enlist;
            internal const string FailoverPartner = DbConnectionStringKeywords.FailoverPartner;
            internal const string Initial_Catalog = DbConnectionStringKeywords.InitialCatalog;
            internal const string Integrated_Security = DbConnectionStringKeywords.IntegratedSecurity;
            internal const string Load_Balance_Timeout = DbConnectionStringKeywords.LoadBalanceTimeout;
            internal const string MARS = DbConnectionStringKeywords.MultipleActiveResultSets;
            internal const string Max_Pool_Size = DbConnectionStringKeywords.MaxPoolSize;
            internal const string Min_Pool_Size = DbConnectionStringKeywords.MinPoolSize;
            internal const string MultiSubnetFailover = DbConnectionStringKeywords.MultiSubnetFailover;
            internal const string Network_Library = DbConnectionStringKeywords.NetworkLibrary;
            internal const string Packet_Size = DbConnectionStringKeywords.PacketSize;
            internal const string Password = DbConnectionStringKeywords.Password;
            internal const string Persist_Security_Info = DbConnectionStringKeywords.PersistSecurityInfo;
            internal const string Pooling = DbConnectionStringKeywords.Pooling;
            internal const string TransactionBinding = DbConnectionStringKeywords.TransactionBinding;
            internal const string TrustServerCertificate = DbConnectionStringKeywords.TrustServerCertificate;
            internal const string Type_System_Version = DbConnectionStringKeywords.TypeSystemVersion;
            internal const string User_ID = DbConnectionStringKeywords.UserID;
            internal const string User_Instance = DbConnectionStringKeywords.UserInstance;
            internal const string Workstation_Id = DbConnectionStringKeywords.WorkstationID;
            internal const string Replication = DbConnectionStringKeywords.Replication;
            internal const string Connect_Retry_Count = DbConnectionStringKeywords.ConnectRetryCount;
            internal const string Connect_Retry_Interval = DbConnectionStringKeywords.ConnectRetryInterval;
            internal const string Authentication = DbConnectionStringKeywords.Authentication;
            internal const string Server_SPN = DbConnectionStringKeywords.ServerSPN;
            internal const string Failover_Partner_SPN = DbConnectionStringKeywords.FailoverPartnerSPN;
#if NETFRAMEWORK
            internal const string TransparentNetworkIPResolution = DbConnectionStringKeywords.TransparentNetworkIPResolution;
#endif // NETFRAMEWORK
        }

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
        internal SqlConnectionString(string connectionString) : base(connectionString, GetParseSynonyms())
        {
#if !NETFRAMEWORK
            ThrowUnsupportedIfKeywordSet(KEY.Connection_Reset);

            // Network Library has its own special error message
            if (ContainsKey(KEY.Network_Library))
            {
                throw SQL.NetworkLibraryKeywordNotSupported();
            }
#endif

            _integratedSecurity = ConvertValueToIntegratedSecurity();
            _poolBlockingPeriod = ConvertValueToPoolBlockingPeriod();
            _encrypt = ConvertValueToSqlConnectionEncrypt();
            _enlist = ConvertValueToBoolean(KEY.Enlist, DEFAULT.Enlist);
            _mars = ConvertValueToBoolean(KEY.MARS, DEFAULT.MARS);
            _persistSecurityInfo = ConvertValueToBoolean(KEY.Persist_Security_Info, DEFAULT.Persist_Security_Info);
            _pooling = ConvertValueToBoolean(KEY.Pooling, DEFAULT.Pooling);
            _replication = ConvertValueToBoolean(KEY.Replication, DEFAULT.Replication);
            _userInstance = ConvertValueToBoolean(KEY.User_Instance, DEFAULT.User_Instance);
            _multiSubnetFailover = ConvertValueToBoolean(KEY.MultiSubnetFailover, DEFAULT.MultiSubnetFailover);

            _commandTimeout = ConvertValueToInt32(KEY.Command_Timeout, DEFAULT.Command_Timeout);
            _connectTimeout = ConvertValueToInt32(KEY.Connect_Timeout, DEFAULT.Connect_Timeout);
            _loadBalanceTimeout = ConvertValueToInt32(KEY.Load_Balance_Timeout, DEFAULT.Load_Balance_Timeout);
            _maxPoolSize = ConvertValueToInt32(KEY.Max_Pool_Size, DEFAULT.Max_Pool_Size);
            _minPoolSize = ConvertValueToInt32(KEY.Min_Pool_Size, DEFAULT.Min_Pool_Size);
            _packetSize = ConvertValueToInt32(KEY.Packet_Size, DEFAULT.Packet_Size);
            _connectRetryCount = ConvertValueToInt32(KEY.Connect_Retry_Count, DEFAULT.Connect_Retry_Count);
            _connectRetryInterval = ConvertValueToInt32(KEY.Connect_Retry_Interval, DEFAULT.Connect_Retry_Interval);

            _applicationIntent = ConvertValueToApplicationIntent();
            _applicationName = ConvertValueToString(KEY.Application_Name, DEFAULT.Application_Name);
            _attachDBFileName = ConvertValueToString(KEY.AttachDBFilename, DEFAULT.AttachDBFilename);
            _contextConnection = ConvertValueToBoolean(KEY.Context_Connection, DEFAULT.Context_Connection);
            _currentLanguage = ConvertValueToString(KEY.Current_Language, DEFAULT.Current_Language);
            _dataSource = ConvertValueToString(KEY.Data_Source, DEFAULT.Data_Source);
            _localDBInstance = LocalDbApi.GetLocalDbInstanceNameFromServerName(_dataSource);
            _failoverPartner = ConvertValueToString(KEY.FailoverPartner, DEFAULT.FailoverPartner);
            _initialCatalog = ConvertValueToString(KEY.Initial_Catalog, DEFAULT.Initial_Catalog);
            _password = ConvertValueToString(KEY.Password, DEFAULT.Password);
            _trustServerCertificate = ConvertValueToBoolean(KEY.TrustServerCertificate, DEFAULT.TrustServerCertificate);
            _authType = ConvertValueToAuthenticationType();
            _columnEncryptionSetting = ConvertValueToColumnEncryptionSetting();
            _enclaveAttestationUrl = ConvertValueToString(KEY.EnclaveAttestationUrl, DEFAULT.EnclaveAttestationUrl);
            _attestationProtocol = ConvertValueToAttestationProtocol();
            _ipAddressPreference = ConvertValueToIPAddressPreference();
            _hostNameInCertificate = ConvertValueToString(KEY.HostNameInCertificate, DEFAULT.HostNameInCertificate);
            _serverCertificate = ConvertValueToString(KEY.ServerCertificate, DEFAULT.ServerCertificate);
            _serverSPN = ConvertValueToString(KEY.Server_SPN, DEFAULT.ServerSPN);
            _failoverPartnerSPN = ConvertValueToString(KEY.Failover_Partner_SPN, DEFAULT.FailoverPartnerSPN);

            // Temporary string - this value is stored internally as an enum.
            string typeSystemVersionString = ConvertValueToString(KEY.Type_System_Version, null);
            string transactionBindingString = ConvertValueToString(KEY.TransactionBinding, null);

            _userID = ConvertValueToString(KEY.User_ID, DEFAULT.User_ID);
            _workstationId = ConvertValueToString(KEY.Workstation_Id, null);

            if (_contextConnection)
            {
                throw SQL.ContextConnectionIsUnsupported();
            }

            if (_loadBalanceTimeout < 0)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Load_Balance_Timeout);
            }

            if (_connectTimeout < 0)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Connect_Timeout);
            }

            if (_commandTimeout < 0)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Command_Timeout);
            }

            if (_maxPoolSize < 1)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Max_Pool_Size);
            }

            if (_minPoolSize < 0)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Min_Pool_Size);
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
            _connectionReset = ConvertValueToBoolean(KEY.Connection_Reset, DEFAULT.Connection_Reset);
            _transparentNetworkIPResolution = ConvertValueToBoolean(KEY.TransparentNetworkIPResolution, DEFAULT.TransparentNetworkIPResolution);
            _networkLibrary = ConvertValueToString(KEY.Network_Library, null);

            if (_networkLibrary != null)
            { // MDAC 83525
                string networkLibrary = _networkLibrary.Trim().ToLower(CultureInfo.InvariantCulture);
                Dictionary<string, string> netlib = NetlibMapping();
                if (!netlib.ContainsKey(networkLibrary))
                {
                    throw ADP.InvalidConnectionOptionValue(KEY.Network_Library);
                }
                _networkLibrary = netlib[networkLibrary];
            }
            else
            {
                _networkLibrary = DEFAULT.Network_Library;
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

            ValidateValueLength(_applicationName, TdsEnums.MAXLEN_APPNAME, KEY.Application_Name);
            ValidateValueLength(_currentLanguage, TdsEnums.MAXLEN_LANGUAGE, KEY.Current_Language);
            ValidateValueLength(_dataSource, TdsEnums.MAXLEN_SERVERNAME, KEY.Data_Source);
            ValidateValueLength(_failoverPartner, TdsEnums.MAXLEN_SERVERNAME, KEY.FailoverPartner);
            ValidateValueLength(_initialCatalog, TdsEnums.MAXLEN_DATABASE, KEY.Initial_Catalog);
            ValidateValueLength(_password, TdsEnums.MAXLEN_CLIENTSECRET, KEY.Password);
            ValidateValueLength(_userID, TdsEnums.MAXLEN_CLIENTID, KEY.User_ID);
            if (_workstationId != null)
            {
                ValidateValueLength(_workstationId, TdsEnums.MAXLEN_HOSTNAME, KEY.Workstation_Id);
            }

            if (!string.Equals(DEFAULT.FailoverPartner, _failoverPartner, StringComparison.OrdinalIgnoreCase))
            {
                // fail-over partner is set

                if (_multiSubnetFailover)
                {
                    throw SQL.MultiSubnetFailoverWithFailoverPartner(serverProvidedFailoverPartner: false, internalConnection: null);
                }

                if (string.Equals(DEFAULT.Initial_Catalog, _initialCatalog, StringComparison.OrdinalIgnoreCase))
                {
                    throw ADP.MissingConnectionOptionValue(KEY.FailoverPartner, KEY.Initial_Catalog);
                }
            }

            // expand during construction so that CreatePermissionSet and Expand are consistent
#if NETFRAMEWORK
            string datadir = null;
            _expandedAttachDBFilename = ExpandDataDirectory(KEY.AttachDBFilename, _attachDBFileName, ref datadir);
#else
            _expandedAttachDBFilename = ExpandDataDirectory(KEY.AttachDBFilename, _attachDBFileName);
#endif // NETFRAMEWORK
            if (_expandedAttachDBFilename != null)
            {
                if (0 <= _expandedAttachDBFilename.IndexOf('|'))
                {
                    throw ADP.InvalidConnectionOptionValue(KEY.AttachDBFilename);
                }
                ValidateValueLength(_expandedAttachDBFilename, TdsEnums.MAXLEN_ATTACHDBFILE, KEY.AttachDBFilename);
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
                throw ADP.InvalidConnectionOptionValue(KEY.AttachDBFilename);
            }
            else
            {
                ValidateValueLength(_attachDBFileName, TdsEnums.MAXLEN_ATTACHDBFILE, KEY.AttachDBFilename);
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
                throw ADP.InvalidConnectionOptionValue(KEY.Type_System_Version);
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
                throw ADP.InvalidConnectionOptionValue(KEY.TransactionBinding);
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
            ValidateValueLength(_dataSource, TdsEnums.MAXLEN_SERVERNAME, KEY.Data_Source);
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
                return ExpandKeyword(KEY.AttachDBFilename, _expandedAttachDBFilename);
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
                    { KEY.ApplicationIntent, KEY.ApplicationIntent },
                    { KEY.Application_Name, KEY.Application_Name },
                    { KEY.AttachDBFilename, KEY.AttachDBFilename },
                    { KEY.AttestationProtocol, KEY.AttestationProtocol},
                    { KEY.Authentication, KEY.Authentication },
                    { KEY.ColumnEncryptionSetting, KEY.ColumnEncryptionSetting },
                    { KEY.Command_Timeout, KEY.Command_Timeout },
                    { KEY.Connect_Retry_Count, KEY.Connect_Retry_Count },
                    { KEY.Connect_Retry_Interval, KEY.Connect_Retry_Interval },
                    { KEY.Connect_Timeout, KEY.Connect_Timeout },
                    { KEY.Context_Connection, KEY.Context_Connection },
                    { KEY.Current_Language, KEY.Current_Language },
                    { KEY.Data_Source, KEY.Data_Source },
                    { KEY.EnclaveAttestationUrl, KEY.EnclaveAttestationUrl },
                    { KEY.Encrypt, KEY.Encrypt },
                    { KEY.Enlist, KEY.Enlist },
                    { KEY.FailoverPartner, KEY.FailoverPartner },
                    { KEY.Failover_Partner_SPN, KEY.Failover_Partner_SPN },
                    { KEY.HostNameInCertificate, KEY.HostNameInCertificate },
                    { KEY.ServerCertificate, KEY.ServerCertificate},
                    { KEY.Initial_Catalog, KEY.Initial_Catalog },
                    { KEY.Integrated_Security, KEY.Integrated_Security },
                    { KEY.IPAddressPreference, KEY.IPAddressPreference },
                    { KEY.Load_Balance_Timeout, KEY.Load_Balance_Timeout },
                    { KEY.MARS, KEY.MARS },
                    { KEY.Max_Pool_Size, KEY.Max_Pool_Size },
                    { KEY.Min_Pool_Size, KEY.Min_Pool_Size },
                    { KEY.MultiSubnetFailover, KEY.MultiSubnetFailover },
                    { KEY.Packet_Size, KEY.Packet_Size },
                    { KEY.Password, KEY.Password },
                    { KEY.Persist_Security_Info, KEY.Persist_Security_Info },
                    { KEY.Pooling, KEY.Pooling },
                    { KEY.PoolBlockingPeriod, KEY.PoolBlockingPeriod },
                    { KEY.Replication, KEY.Replication },
                    { KEY.Server_SPN, KEY.Server_SPN },
                    { KEY.TrustServerCertificate, KEY.TrustServerCertificate },
                    { KEY.TransactionBinding, KEY.TransactionBinding },
                    { KEY.Type_System_Version, KEY.Type_System_Version },
                    { KEY.User_ID, KEY.User_ID },
                    { KEY.User_Instance, KEY.User_Instance },
                    { KEY.Workstation_Id, KEY.Workstation_Id },

                    { SYNONYM.IPADDRESSPREFERENCE, KEY.IPAddressPreference },
                    { SYNONYM.APP, KEY.Application_Name },
                    { SYNONYM.APPLICATIONINTENT, KEY.ApplicationIntent },
                    { SYNONYM.EXTENDED_PROPERTIES, KEY.AttachDBFilename },
                    { SYNONYM.HOSTNAMEINCERTIFICATE, KEY.HostNameInCertificate },
                    { SYNONYM.SERVERCERTIFICATE, KEY.ServerCertificate},
                    { SYNONYM.INITIAL_FILE_NAME, KEY.AttachDBFilename },
                    { SYNONYM.CONNECTRETRYCOUNT, KEY.Connect_Retry_Count },
                    { SYNONYM.CONNECTRETRYINTERVAL, KEY.Connect_Retry_Interval },
                    { SYNONYM.CONNECTION_TIMEOUT, KEY.Connect_Timeout },
                    { SYNONYM.TIMEOUT, KEY.Connect_Timeout },
                    { SYNONYM.LANGUAGE, KEY.Current_Language },
                    { SYNONYM.ADDR, KEY.Data_Source },
                    { SYNONYM.ADDRESS, KEY.Data_Source },
                    { SYNONYM.MULTIPLEACTIVERESULTSETS, KEY.MARS },
                    { SYNONYM.MULTISUBNETFAILOVER, KEY.MultiSubnetFailover },
                    { SYNONYM.NETWORK_ADDRESS, KEY.Data_Source },
                    { SYNONYM.POOLBLOCKINGPERIOD, KEY.PoolBlockingPeriod},
                    { SYNONYM.SERVER, KEY.Data_Source },
                    { SYNONYM.DATABASE, KEY.Initial_Catalog },
                    { SYNONYM.TRUSTED_CONNECTION, KEY.Integrated_Security },
                    { SYNONYM.TRUSTSERVERCERTIFICATE, KEY.TrustServerCertificate },
                    { SYNONYM.Connection_Lifetime, KEY.Load_Balance_Timeout },
                    { SYNONYM.Pwd, KEY.Password },
                    { SYNONYM.PERSISTSECURITYINFO, KEY.Persist_Security_Info },
                    { SYNONYM.UID, KEY.User_ID },
                    { SYNONYM.User, KEY.User_ID },
                    { SYNONYM.WSID, KEY.Workstation_Id },
                    { SYNONYM.ServerSPN, KEY.Server_SPN },
                    { SYNONYM.FailoverPartnerSPN, KEY.Failover_Partner_SPN },
#if NETFRAMEWORK
                    { KEY.Connection_Reset, KEY.Connection_Reset },
                    { KEY.Network_Library, KEY.Network_Library },
                    { KEY.TransparentNetworkIPResolution, KEY.TransparentNetworkIPResolution },
                    { SYNONYM.NET, KEY.Network_Library },
                    { SYNONYM.NETWORK, KEY.Network_Library },
                    { SYNONYM.TRANSPARENTNETWORKIPRESOLUTION, KEY.TransparentNetworkIPResolution },
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
                ValidateValueLength(result, TdsEnums.MAXLEN_HOSTNAME, KEY.Workstation_Id);
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
                            throw ADP.InvalidConnectionOptionValue(KEY.AttachDBFilename);
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
            if (!TryGetParsetableValue(KEY.ApplicationIntent, out string value))
            {
                return DEFAULT.ApplicationIntent;
            }

            // when wrong value is used in the connection string provided to SqlConnection.ConnectionString or c-tor,
            // wrap Format and Overflow exceptions with Argument one, to be consistent with rest of the keyword types (like int and bool)
            try
            {
                return DbConnectionStringUtilities.ConvertToApplicationIntent(KEY.ApplicationIntent, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.ApplicationIntent, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.ApplicationIntent, e);
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
            if (!TryGetParsetableValue(KEY.Authentication, out string value))
            {
                return DEFAULT.Authentication;
            }

            try
            {
                return DbConnectionStringUtilities.ConvertToAuthenticationType(KEY.Authentication, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Authentication, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Authentication, e);
            }
        }

        /// <summary>
        /// Convert the value to SqlConnectionColumnEncryptionSetting.
        /// </summary>
        /// <returns></returns>
        internal SqlConnectionColumnEncryptionSetting ConvertValueToColumnEncryptionSetting()
        {
            if (!TryGetParsetableValue(KEY.ColumnEncryptionSetting, out string value))
            {
                return DEFAULT.ColumnEncryptionSetting;
            }

            try
            {
                return DbConnectionStringUtilities.ConvertToColumnEncryptionSetting(KEY.ColumnEncryptionSetting, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.ColumnEncryptionSetting, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.ColumnEncryptionSetting, e);
            }
        }

        /// <summary>
        /// Convert the value to SqlConnectionAttestationProtocol
        /// </summary>
        /// <returns></returns>
        internal SqlConnectionAttestationProtocol ConvertValueToAttestationProtocol()
        {
            if (!TryGetParsetableValue(KEY.AttestationProtocol, out string value))
            {
                return DEFAULT.AttestationProtocol;
            }

            try
            {
                return AttestationProtocolUtilities.ConvertToAttestationProtocol(KEY.AttestationProtocol, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.AttestationProtocol, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.AttestationProtocol, e);
            }
        }

        /// <summary>
        /// Convert the value to SqlConnectionIPAddressPreference
        /// </summary>
        /// <returns></returns>
        internal SqlConnectionIPAddressPreference ConvertValueToIPAddressPreference()
        {
            if (!TryGetParsetableValue(KEY.IPAddressPreference, out string value))
            {
                return DEFAULT.IpAddressPreference;
            }

            try
            {
                return IpAddressPreferenceUtilities.ConvertToIPAddressPreference(KEY.IPAddressPreference, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.IPAddressPreference, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.IPAddressPreference, e);
            }
        }

        internal PoolBlockingPeriod ConvertValueToPoolBlockingPeriod()
        {
            if (!TryGetParsetableValue(KEY.PoolBlockingPeriod, out string value))
            {
                return DEFAULT.PoolBlockingPeriod;
            }

            try
            {
                return PoolBlockingUtilities.ConvertToPoolBlockingPeriod(KEY.PoolBlockingPeriod, value);
            }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.PoolBlockingPeriod, e);
            }
        }

        internal SqlConnectionEncryptOption ConvertValueToSqlConnectionEncrypt()
        {
            if (!TryGetParsetableValue(KEY.Encrypt, out string value))
            {
                return DEFAULT.Encrypt;
            }

            try
            {
                return AttestationProtocolUtilities.ConvertToSqlConnectionEncryptOption(KEY.Encrypt, value);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Encrypt, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(KEY.Encrypt, e);
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
