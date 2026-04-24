// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security;
using System.Security.Permissions;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;
using Microsoft.Data.SqlClient.LocalDb;

namespace Microsoft.Data.SqlClient
{
    internal sealed partial class SqlConnectionOptions
    {
        // instances of this class are intended to be immutable, i.e readonly
        // used by pooling classes so it is much easier to verify correctness
        // when not worried about the class being modified during execution

        #region DbConnectionOptions fields

        private const string ConnectionStringValidKeyPattern = "^(?![;\\s])[^\\p{Cc}]+(?<!\\s)$"; // key not allowed to start with semi-colon or space or contain non-visible characters or end with space
        private const string ConnectionStringValidValuePattern = "^[^\u0000]*$";                    // value not allowed to contain embedded null
        private const string ConnectionStringQuoteValuePattern = "^[^\"'=;\\s\\p{Cc}]*$";           // generally do not quote the value if it matches the pattern
        private const string ConnectionStringQuoteOdbcValuePattern = "^\\{([^\\}\u0000]|\\}\\})*\\}$"; // do not quote odbc value if it matches this pattern
        internal const string DataDirectory = "|datadirectory|";

        private static readonly Regex s_connectionStringValidKeyRegex = new Regex(ConnectionStringValidKeyPattern, RegexOptions.Compiled);
        private static readonly Regex s_connectionStringValidValueRegex = new Regex(ConnectionStringValidValuePattern, RegexOptions.Compiled);
        private static readonly Regex s_connectionStringQuoteValueRegex = new Regex(ConnectionStringQuoteValuePattern, RegexOptions.Compiled);
        private static readonly Regex s_connectionStringQuoteOdbcValueRegex = new Regex(ConnectionStringQuoteOdbcValuePattern, RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        internal readonly bool _hasPasswordKeyword;
        internal readonly bool _hasUserIdKeyword;
        internal readonly NameValuePair _keyChain;

        private readonly string _usersConnectionString;
        private readonly Dictionary<string, string> _parsetable;

        internal Dictionary<string, string> Parsetable => _parsetable;
        public bool IsEmpty => _keyChain == null;

        #endregion

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

        private static readonly Dictionary<string, string> s_keywordMap =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

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

        #region Constructors

        /// <summary>
        /// Static constructor to do things that we can't do in a single line initialization.
        /// </summary>
        static SqlConnectionOptions()
        {
            // Add keywords and synonyms to the keyword map.
            // @TODO: About half of these synonyms are just the same as the keyword but with spaces
            //    removed. However, not all multiword keywords are supported without spaces. We
            //    should just add support for all keywords w/ or w/o spaces, then remove them from
            //    synonyms.
            AddKeywordToMap(DbConnectionStringKeywords.ApplicationIntent,
                            DbConnectionStringSynonyms.ApplicationIntent);
            AddKeywordToMap(DbConnectionStringKeywords.ApplicationName,
                            DbConnectionStringSynonyms.App);
            AddKeywordToMap(DbConnectionStringKeywords.AttachDbFilename,
                            DbConnectionStringSynonyms.ExtendedProperties,
                            DbConnectionStringSynonyms.InitialFileName);
            AddKeywordToMap(DbConnectionStringKeywords.AttestationProtocol);
            AddKeywordToMap(DbConnectionStringKeywords.Authentication);
            AddKeywordToMap(DbConnectionStringKeywords.ColumnEncryptionSetting,
                            DbConnectionStringSynonyms.ColumnEncryption);
            AddKeywordToMap(DbConnectionStringKeywords.CommandTimeout);
            AddKeywordToMap(DbConnectionStringKeywords.ConnectRetryCount,
                            DbConnectionStringSynonyms.ConnectRetryCount);
            AddKeywordToMap(DbConnectionStringKeywords.ConnectRetryInterval,
                            DbConnectionStringSynonyms.ConnectRetryInterval);
            AddKeywordToMap(DbConnectionStringKeywords.ConnectTimeout,
                            DbConnectionStringSynonyms.ConnectionTimeout,
                            DbConnectionStringSynonyms.ConnectTimeout,
                            DbConnectionStringSynonyms.Timeout);
            AddKeywordToMap(DbConnectionStringKeywords.ContextConnection);
            AddKeywordToMap(DbConnectionStringKeywords.CurrentLanguage,
                            DbConnectionStringSynonyms.Language);
            AddKeywordToMap(DbConnectionStringKeywords.DataSource,
                            DbConnectionStringSynonyms.Addr,
                            DbConnectionStringSynonyms.Address,
                            DbConnectionStringSynonyms.NetworkAddress,
                            DbConnectionStringSynonyms.Server);
            AddKeywordToMap(DbConnectionStringKeywords.EnclaveAttestationUrl);
            AddKeywordToMap(DbConnectionStringKeywords.Encrypt);
            AddKeywordToMap(DbConnectionStringKeywords.Enlist);
            AddKeywordToMap(DbConnectionStringKeywords.FailoverPartner,
                            DbConnectionStringSynonyms.FailoverPartner);
            AddKeywordToMap(DbConnectionStringKeywords.FailoverPartnerSpn,
                            DbConnectionStringSynonyms.FailoverPartnerSpn);
            AddKeywordToMap(DbConnectionStringKeywords.HostNameInCertificate,
                            DbConnectionStringSynonyms.HostNameInCertificate);
            AddKeywordToMap(DbConnectionStringKeywords.InitialCatalog,
                            DbConnectionStringSynonyms.Database);
            AddKeywordToMap(DbConnectionStringKeywords.IntegratedSecurity,
                            DbConnectionStringSynonyms.TrustedConnection);
            AddKeywordToMap(DbConnectionStringKeywords.IpAddressPreference,
                            DbConnectionStringSynonyms.IpAddressPreference);
            AddKeywordToMap(DbConnectionStringKeywords.LoadBalanceTimeout,
                            DbConnectionStringSynonyms.ConnectionLifetime);
            AddKeywordToMap(DbConnectionStringKeywords.MultipleActiveResultSets,
                            DbConnectionStringSynonyms.MultipleActiveResultSets);
            AddKeywordToMap(DbConnectionStringKeywords.MaxPoolSize);
            AddKeywordToMap(DbConnectionStringKeywords.MinPoolSize);
            AddKeywordToMap(DbConnectionStringKeywords.MultiSubnetFailover,
                            DbConnectionStringSynonyms.MultiSubnetFailover);
            AddKeywordToMap(DbConnectionStringKeywords.PacketSize,
                            DbConnectionStringSynonyms.PacketSize);
            AddKeywordToMap(DbConnectionStringKeywords.Password,
                            DbConnectionStringSynonyms.Pwd);
            AddKeywordToMap(DbConnectionStringKeywords.PersistSecurityInfo,
                            DbConnectionStringSynonyms.PersistSecurityInfo);
            AddKeywordToMap(DbConnectionStringKeywords.Pooling);
            AddKeywordToMap(DbConnectionStringKeywords.PoolBlockingPeriod,
                            DbConnectionStringSynonyms.PoolBlockingPeriod);
            AddKeywordToMap(DbConnectionStringKeywords.Replication);
            AddKeywordToMap(DbConnectionStringKeywords.ServerCertificate,
                            DbConnectionStringSynonyms.ServerCertificate);
            AddKeywordToMap(DbConnectionStringKeywords.ServerSpn,
                            DbConnectionStringSynonyms.ServerSpn);
            AddKeywordToMap(DbConnectionStringKeywords.TrustServerCertificate,
                            DbConnectionStringSynonyms.TrustServerCertificate);
            AddKeywordToMap(DbConnectionStringKeywords.TransactionBinding);
            AddKeywordToMap(DbConnectionStringKeywords.TypeSystemVersion);
            AddKeywordToMap(DbConnectionStringKeywords.UserId,
                            DbConnectionStringSynonyms.Uid,
                            DbConnectionStringSynonyms.User);
            AddKeywordToMap(DbConnectionStringKeywords.UserInstance);
            AddKeywordToMap(DbConnectionStringKeywords.WorkstationId,
                            DbConnectionStringSynonyms.WorkstationId,
                            DbConnectionStringSynonyms.WsId);

            #if NETFRAMEWORK
            AddKeywordToMap(DbConnectionStringKeywords.ConnectionReset);
            AddKeywordToMap(DbConnectionStringKeywords.NetworkLibrary,
                            DbConnectionStringSynonyms.Net,
                            DbConnectionStringSynonyms.Network);
            AddKeywordToMap(DbConnectionStringKeywords.TransparentNetworkIpResolution,
                            DbConnectionStringSynonyms.TransparentNetworkIpResolution);
            #endif
        }

        internal SqlConnectionOptions(string connectionString)
        {
            // Initialize parse table (inlined from DbConnectionOptions base constructor)
            _parsetable = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _usersConnectionString = connectionString ?? "";
            if (_usersConnectionString.Length > 0)
            {
                _keyChain = ParseInternal(_parsetable, _usersConnectionString, true, s_keywordMap, false);
                _hasPasswordKeyword = _parsetable.ContainsKey(DbConnectionStringKeywords.Password) ||
                                      _parsetable.ContainsKey(DbConnectionStringSynonyms.Pwd);
                _hasUserIdKeyword = _parsetable.ContainsKey(DbConnectionStringKeywords.UserId) ||
                                    _parsetable.ContainsKey(DbConnectionStringSynonyms.Uid);
            }
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
        internal SqlConnectionOptions(SqlConnectionOptions connectionOptions, string dataSource, bool userInstance, bool? setEnlistValue)
        {
            // Copy parse state (inlined from DbConnectionOptions copy constructor)
            _usersConnectionString = connectionOptions._usersConnectionString;
            _parsetable = connectionOptions._parsetable;
            _keyChain = connectionOptions._keyChain;
            _hasPasswordKeyword = connectionOptions._hasPasswordKeyword;
            _hasUserIdKeyword = connectionOptions._hasUserIdKeyword;
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

        #endregion

        internal bool IntegratedSecurity => _integratedSecurity;

        // @TODO: This is temporary until we can remove DbConnectionString (see SqlClientPermission)
        internal static IReadOnlyDictionary<string, string> KeywordMap => s_keywordMap;

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

        internal string Expand()
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
                return _usersConnectionString;
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
                {
                    hostName += domainName;
                }

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
        protected internal PermissionSet CreatePermissionSet()
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

        #region Private Methods

        private static void AddKeywordToMap(string keyword, params string[] synonyms)
        {
            // Add mapping of keyword to keyword
            s_keywordMap.Add(keyword, keyword);

            // Add mapping of synonyms to keyword
            foreach (string synonym in synonyms)
            {
                s_keywordMap.Add(synonym, keyword);
            }
        }

        #endregion

        #region DbConnectionOptions methods

        internal bool TryGetParsetableValue(string key, out string value) => _parsetable.TryGetValue(key, out value);

        // same as Boolean, but with SSPI thrown in as valid yes
        public bool ConvertValueToIntegratedSecurity()
        {
            return _parsetable.TryGetValue(DbConnectionStringKeywords.IntegratedSecurity, out string value) && value != null
                ? ConvertValueToIntegratedSecurityInternal(value)
                : false;
        }

        internal bool ConvertValueToIntegratedSecurityInternal(string stringValue)
        {
            if (CompareInsensitiveInvariant(stringValue, "sspi") || CompareInsensitiveInvariant(stringValue, "true") || CompareInsensitiveInvariant(stringValue, "yes"))
            {
                return true;
            }

            if (CompareInsensitiveInvariant(stringValue, "false") || CompareInsensitiveInvariant(stringValue, "no"))
            {
                return false;
            }

            string tmp = stringValue.Trim();  // Remove leading & trailing whitespace.
            if (CompareInsensitiveInvariant(tmp, "sspi") || CompareInsensitiveInvariant(tmp, "true") || CompareInsensitiveInvariant(tmp, "yes"))
            {
                return true;
            }

            if (CompareInsensitiveInvariant(tmp, "false") || CompareInsensitiveInvariant(tmp, "no"))
            {
                return false;
            }

            throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.IntegratedSecurity);
        }

        public int ConvertValueToInt32(string keyName, int defaultValue)
        {
            return _parsetable.TryGetValue(keyName, out string value) && value != null ?
                   ConvertToInt32Internal(keyName, value) :
                   defaultValue;
        }

        internal static int ConvertToInt32Internal(string keyname, string stringValue)
        {
            try
            {
                return int.Parse(stringValue, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            catch (FormatException e)
            {
                throw ADP.InvalidConnectionOptionValue(keyname, e);
            }
            catch (OverflowException e)
            {
                throw ADP.InvalidConnectionOptionValue(keyname, e);
            }
        }

        public string ConvertValueToString(string keyName, string defaultValue)
            => _parsetable.TryGetValue(keyName, out string value) && value != null ? value : defaultValue;

        public bool ContainsKey(string keyword) => _parsetable.ContainsKey(keyword);

        public string UsersConnectionString(bool hidePassword) => UsersConnectionString(hidePassword, false);

        internal string UsersConnectionStringForTrace() => UsersConnectionString(true, true);

        private string UsersConnectionString(bool hidePassword, bool forceHidePassword)
        {
            string connectionString = _usersConnectionString;
            if (_hasPasswordKeyword && (forceHidePassword || (hidePassword && !HasPersistablePassword)))
            {
                ReplacePasswordPwd(out connectionString, false);
            }
            return connectionString ?? string.Empty;
        }

        internal bool HasPersistablePassword => _hasPasswordKeyword
            ? ConvertValueToBoolean(DbConnectionStringKeywords.PersistSecurityInfo, DbConnectionStringDefaults.PersistSecurityInfo)
            : true; // no password means persistable password so we don't have to munge

        public bool ConvertValueToBoolean(string keyName, bool defaultValue)
        {
            string value;
            return _parsetable.TryGetValue(keyName, out value) ?
                ConvertValueToBooleanInternal(keyName, value) :
                defaultValue;
        }

        internal static bool ConvertValueToBooleanInternal(string keyName, string stringValue)
        {
            if (CompareInsensitiveInvariant(stringValue, "true") || CompareInsensitiveInvariant(stringValue, "yes"))
            {
                return true;
            }
            else if (CompareInsensitiveInvariant(stringValue, "false") || CompareInsensitiveInvariant(stringValue, "no"))
            {
                return false;
            }
            else
            {
                string tmp = stringValue.Trim();  // Remove leading & trailing whitespace.
                if (CompareInsensitiveInvariant(tmp, "true") || CompareInsensitiveInvariant(tmp, "yes"))
                {
                    return true;
                }
                else if (CompareInsensitiveInvariant(tmp, "false") || CompareInsensitiveInvariant(tmp, "no"))
                {
                    return false;
                }
                else
                {
                    throw ADP.InvalidConnectionOptionValue(keyName);
                }
            }
        }

        private static bool CompareInsensitiveInvariant(string strvalue, string strconst)
            => (0 == StringComparer.OrdinalIgnoreCase.Compare(strvalue, strconst));

        private static string GetKeyName(StringBuilder buffer)
        {
            int count = buffer.Length;
            while ((0 < count) && char.IsWhiteSpace(buffer[count - 1]))
            {
                count--; // trailing whitespace
            }
            return buffer.ToString(0, count).ToLower(CultureInfo.InvariantCulture);
        }

        private static string GetKeyValue(StringBuilder buffer, bool trimWhitespace)
        {
            int count = buffer.Length;
            int index = 0;
            if (trimWhitespace)
            {
                while ((index < count) && char.IsWhiteSpace(buffer[index]))
                {
                    index++; // leading whitespace
                }
                while ((0 < count) && char.IsWhiteSpace(buffer[count - 1]))
                {
                    count--; // trailing whitespace
                }
            }
            return buffer.ToString(index, count - index);
        }

        // transition states used for parsing
        private enum ParserState
        {
            NothingYet = 1,   //start point
            Key,
            KeyEqual,
            KeyEnd,
            UnquotedValue,
            DoubleQuoteValue,
            DoubleQuoteValueQuote,
            SingleQuoteValue,
            SingleQuoteValueQuote,
            BraceQuoteValue,
            BraceQuoteValueQuote,
            QuotedValueEnd,
            NullTermination,
        };

        // @TODO: This should probably be extracted into its own parser class
        internal static int GetKeyValuePair(string connectionString, int currentPosition, StringBuilder buffer, bool useOdbcRules, out string keyname, out string keyvalue)
        {
            int startposition = currentPosition;

            buffer.Length = 0;
            keyname = null;
            keyvalue = null;

            char currentChar = '\0';

            ParserState parserState = ParserState.NothingYet;
            int length = connectionString.Length;
            for (; currentPosition < length; ++currentPosition)
            {
                currentChar = connectionString[currentPosition];

                switch (parserState)
                {
                    case ParserState.NothingYet: // [\\s;]*
                        if ((';' == currentChar) || char.IsWhiteSpace(currentChar))
                        {
                            continue;
                        }
                        if ('\0' == currentChar)
                        { parserState = ParserState.NullTermination; continue; }
                        if (char.IsControl(currentChar))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        startposition = currentPosition;
                        if ('=' != currentChar)
                        {
                            parserState = ParserState.Key;
                            break;
                        }
                        else
                        {
                            parserState = ParserState.KeyEqual;
                            continue;
                        }

                    case ParserState.Key: // (?<key>([^=\\s\\p{Cc}]|\\s+[^=\\s\\p{Cc}]|\\s+==|==)+)
                        if ('=' == currentChar)
                        { parserState = ParserState.KeyEqual; continue; }
                        if (char.IsWhiteSpace(currentChar))
                        { break; }
                        if (char.IsControl(currentChar))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.KeyEqual: // \\s*=(?!=)\\s*
                        if (!useOdbcRules && '=' == currentChar)
                        { parserState = ParserState.Key; break; }
                        keyname = GetKeyName(buffer);
                        if (string.IsNullOrEmpty(keyname))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        buffer.Length = 0;
                        parserState = ParserState.KeyEnd;
                        goto case ParserState.KeyEnd;

                    case ParserState.KeyEnd:
                        if (char.IsWhiteSpace(currentChar))
                        { continue; }
                        if (useOdbcRules)
                        {
                            if ('{' == currentChar)
                            { parserState = ParserState.BraceQuoteValue; break; }
                        }
                        else
                        {
                            if ('\'' == currentChar)
                            { parserState = ParserState.SingleQuoteValue; continue; }
                            if ('"' == currentChar)
                            { parserState = ParserState.DoubleQuoteValue; continue; }
                        }
                        if (';' == currentChar)
                        { goto ParserExit; }
                        if ('\0' == currentChar)
                        { goto ParserExit; }
                        if (char.IsControl(currentChar))
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        parserState = ParserState.UnquotedValue;
                        break;

                    case ParserState.UnquotedValue: // "((?![\"'\\s])" + "([^;\\s\\p{Cc}]|\\s+[^;\\s\\p{Cc}])*" + "(?<![\"']))"
                        if (char.IsWhiteSpace(currentChar))
                        { break; }
                        if (char.IsControl(currentChar) || ';' == currentChar)
                        { goto ParserExit; }
                        break;

                    case ParserState.DoubleQuoteValue: // "(\"([^\"\u0000]|\"\")*\")"
                        if ('"' == currentChar)
                        { parserState = ParserState.DoubleQuoteValueQuote; continue; }
                        if ('\0' == currentChar)
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.DoubleQuoteValueQuote:
                        if ('"' == currentChar)
                        { parserState = ParserState.DoubleQuoteValue; break; }
                        keyvalue = GetKeyValue(buffer, false);
                        parserState = ParserState.QuotedValueEnd;
                        goto case ParserState.QuotedValueEnd;

                    case ParserState.SingleQuoteValue: // "('([^'\\u0000]|'')*')"
                        if ('\'' == currentChar)
                        { parserState = ParserState.SingleQuoteValueQuote; continue; }
                        if ('\0' == currentChar)
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.SingleQuoteValueQuote:
                        if ('\'' == currentChar)
                        { parserState = ParserState.SingleQuoteValue; break; }
                        keyvalue = GetKeyValue(buffer, false);
                        parserState = ParserState.QuotedValueEnd;
                        goto case ParserState.QuotedValueEnd;

                    case ParserState.BraceQuoteValue: // "(\\{([^\\}\\u0000]|\\}\\})*\\})"
                        if ('}' == currentChar)
                        { parserState = ParserState.BraceQuoteValueQuote; break; }
                        if ('\0' == currentChar)
                        { throw ADP.ConnectionStringSyntax(startposition); }
                        break;

                    case ParserState.BraceQuoteValueQuote:
                        if ('}' == currentChar)
                        { parserState = ParserState.BraceQuoteValue; break; }
                        keyvalue = GetKeyValue(buffer, false);
                        parserState = ParserState.QuotedValueEnd;
                        goto case ParserState.QuotedValueEnd;

                    case ParserState.QuotedValueEnd:
                        if (char.IsWhiteSpace(currentChar))
                        { continue; }
                        if (';' == currentChar)
                        { goto ParserExit; }
                        if ('\0' == currentChar)
                        { parserState = ParserState.NullTermination; continue; }
                        throw ADP.ConnectionStringSyntax(startposition);  // unbalanced single quote

                    case ParserState.NullTermination: // [\\s;\\u0000]*
                        if ('\0' == currentChar)
                        { continue; }
                        if (char.IsWhiteSpace(currentChar))
                        { continue; }
                        throw ADP.ConnectionStringSyntax(currentPosition);

                    default:
                        throw ADP.InternalError(ADP.InternalErrorCode.InvalidParserState1);
                }
                buffer.Append(currentChar);
            }
        ParserExit:
            switch (parserState)
            {
                case ParserState.Key:
                case ParserState.DoubleQuoteValue:
                case ParserState.SingleQuoteValue:
                case ParserState.BraceQuoteValue:
                    // keyword not found/unbalanced double/single quote
                    throw ADP.ConnectionStringSyntax(startposition);

                case ParserState.KeyEqual:
                    // equal sign at end of line
                    keyname = GetKeyName(buffer);
                    if (string.IsNullOrEmpty(keyname))
                    { throw ADP.ConnectionStringSyntax(startposition); }
                    break;

                case ParserState.UnquotedValue:
                    // unquoted value at end of line
                    keyvalue = GetKeyValue(buffer, true);

                    char tmpChar = keyvalue[keyvalue.Length - 1];
                    if (!useOdbcRules && (('\'' == tmpChar) || ('"' == tmpChar)))
                    {
                        throw ADP.ConnectionStringSyntax(startposition);    // unquoted value must not end in quote, except for odbc
                    }
                    break;

                case ParserState.DoubleQuoteValueQuote:
                case ParserState.SingleQuoteValueQuote:
                case ParserState.BraceQuoteValueQuote:
                case ParserState.QuotedValueEnd:
                    // quoted value at end of line
                    keyvalue = GetKeyValue(buffer, false);
                    break;

                case ParserState.NothingYet:
                case ParserState.KeyEnd:
                case ParserState.NullTermination:
                    // do nothing
                    break;

                default:
                    throw ADP.InternalError(ADP.InternalErrorCode.InvalidParserState2);
            }
            if ((';' == currentChar) && (currentPosition < connectionString.Length))
            {
                currentPosition++;
            }
            return currentPosition;
        }

        private static bool IsValueValidInternal(string keyvalue)
        {
            if (keyvalue != null)
            {
#if DEBUG
                bool compValue = s_connectionStringValidValueRegex.IsMatch(keyvalue);
                Debug.Assert((-1 == keyvalue.IndexOf('\u0000')) == compValue, "IsValueValid mismatch with regex");
#endif
                return (-1 == keyvalue.IndexOf('\u0000'));
            }
            return true;
        }

        private static bool IsKeyNameValid(string keyname)
        {
            if (keyname != null)
            {
#if DEBUG
                bool compValue = s_connectionStringValidKeyRegex.IsMatch(keyname);
                Debug.Assert(((0 < keyname.Length) && (';' != keyname[0]) && !char.IsWhiteSpace(keyname[0]) && (-1 == keyname.IndexOf('\u0000'))) == compValue, "IsValueValid mismatch with regex");
#endif
                return ((0 < keyname.Length) && (';' != keyname[0]) && !char.IsWhiteSpace(keyname[0]) && (-1 == keyname.IndexOf('\u0000')));
            }
            return false;
        }

        private static NameValuePair ParseInternal(
            Dictionary<string, string> parsetable,
            string connectionString,
            bool buildChain,
            IReadOnlyDictionary<string, string> synonyms,
            bool firstKey)
        {
            Debug.Assert(connectionString != null, "null connectionstring");
            StringBuilder buffer = new StringBuilder();
            NameValuePair localKeychain = null, keychain = null;

            #if DEBUG
            try
            {
            #endif
                int nextStartPosition = 0;
                int endPosition = connectionString.Length;
                while (nextStartPosition < endPosition)
                {
                    int startPosition = nextStartPosition;

                    string keyname, keyvalue;
                    nextStartPosition = GetKeyValuePair(connectionString, startPosition, buffer, firstKey, out keyname, out keyvalue);
                    if (string.IsNullOrEmpty(keyname))
                    {
                        // if (nextStartPosition != endPosition) { throw; }
                        break;
                    }

                    DebugTraceKeyValuePair(keyname, keyvalue, synonyms);
                    Debug.Assert(IsKeyNameValid(keyname), "ParseFailure, invalid keyname");
                    Debug.Assert(IsValueValidInternal(keyvalue), "parse failure, invalid keyvalue");

                    string realkeyname = (synonyms is not null) ?
                                         (synonyms.TryGetValue(keyname, out string synonym) ? synonym : null) :
                                          keyname;

                    if (!IsKeyNameValid(realkeyname))
                    {
                        throw ADP.KeywordNotSupported(keyname);
                    }
                    if (!firstKey || !parsetable.ContainsKey(realkeyname))
                    {
                        parsetable[realkeyname] = keyvalue; // last key-value pair wins (or first)
                    }

                    if (localKeychain != null)
                    {
                        localKeychain = localKeychain.Next = new NameValuePair(realkeyname, keyvalue, nextStartPosition - startPosition);
                    }
                    else if (buildChain)
                    { // first time only - don't contain modified chain from UDL file
                        keychain = localKeychain = new NameValuePair(realkeyname, keyvalue, nextStartPosition - startPosition);
                    }
                }
            #if DEBUG
            }
            catch (ArgumentException e)
            {
                ParseComparison(parsetable, connectionString, synonyms, firstKey, e);
                throw;
            }
            ParseComparison(parsetable, connectionString, synonyms, firstKey, null);
            #endif

            return keychain;
        }

        internal NameValuePair ReplacePasswordPwd(out string constr, bool fakePassword)
        {
            bool expanded = false;
            int copyPosition = 0;
            NameValuePair head = null, tail = null, next = null;
            StringBuilder builder = new StringBuilder(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; current != null; current = current.Next)
            {
                if (!CompareInsensitiveInvariant(DbConnectionStringKeywords.Password, current.Name) &&
                    !CompareInsensitiveInvariant(DbConnectionStringSynonyms.Pwd, current.Name))
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                    if (fakePassword)
                    {
                        next = new NameValuePair(current.Name, current.Value, current.Length);
                    }
                }
                else if (fakePassword)
                {
                    // replace user password/pwd value with *
                    const string equalstar = "=*;";
                    builder.Append(current.Name).Append(equalstar);
                    next = new NameValuePair(current.Name, "*", current.Name.Length + equalstar.Length);
                    expanded = true;
                }
                else
                {
                    // drop the password/pwd completely in returning for user
                    expanded = true;
                }

                if (fakePassword)
                {
                    if (tail != null)
                    {
                        tail = tail.Next = next;
                    }
                    else
                    {
                        tail = head = next;
                    }
                }
                copyPosition += current.Length;
            }
            Debug.Assert(expanded, "password/pwd was not removed");
            constr = builder.ToString();
            return head;
        }

        // SxS notes:
        // * this method queries "DataDirectory" value from the current AppDomain.
        //   This string is used for to replace "!DataDirectory!" values in the connection string, it is not considered as an "exposed resource".
        // * This method uses GetFullPath to validate that root path is valid, the result is not exposed out.
        internal static string ExpandDataDirectory(string keyword, string value)
        {
            string fullPath = null;
            if (value != null && value.StartsWith(DataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // find the replacement path
                object rootFolderObject = AppDomain.CurrentDomain.GetData("DataDirectory");
                var rootFolderPath = (rootFolderObject as string);
                if (rootFolderObject != null && rootFolderPath == null)
                {
                    throw ADP.InvalidDataDirectory();
                }

                if (string.IsNullOrEmpty(rootFolderPath))
                {
                    rootFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                }

                var fileName = value.Substring(DataDirectory.Length);

                if (Path.IsPathRooted(fileName))
                {
                    fileName = fileName.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                fullPath = Path.Combine(rootFolderPath, fileName);

                // verify root folder path is a real path without unexpected "..\"
                if (!Path.GetFullPath(fullPath).StartsWith(rootFolderPath, StringComparison.Ordinal))
                {
                    throw ADP.InvalidConnectionOptionValue(keyword);
                }
            }
            return fullPath;
        }

        #region Net methods

#if NET
        internal string ExpandAttachDbFileName(string replacementValue)
        {
            int copyPosition = 0;

            StringBuilder builder = new(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; current != null; current = current.Next)
            {
                if (string.Equals(current.Name, DbConnectionStringKeywords.AttachDbFilename, StringComparison.InvariantCultureIgnoreCase))
                {
                    builder.Append($"{current.Name}={replacementValue};");
                }
                else
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                }
                copyPosition += current.Length;
            }

            return builder.ToString();
        }
#endif

        #endregion

        #region NetFx methods

#if NETFRAMEWORK
        public string this[string keyword] => _parsetable[keyword];

        private PermissionSet _permissionset;

        internal void DemandPermission()
        {
            if (_permissionset is null)
            {
                _permissionset = CreatePermissionSet();
            }
            _permissionset.Demand();
        }

        internal bool HasBlankPassword
        {
            get
            {
                if (!ConvertValueToIntegratedSecurity())
                {
                    if (_parsetable.TryGetValue(DbConnectionStringKeywords.Password, out string value))
                    {
                        return string.IsNullOrEmpty(value);
                    }

                    if (_parsetable.TryGetValue(DbConnectionStringSynonyms.Pwd, out value))
                    {
                        return string.IsNullOrEmpty(value); // MDAC 83097
                    }

                    return (_parsetable.TryGetValue(DbConnectionStringKeywords.UserId, out value) && !string.IsNullOrEmpty(value)) ||
                           (_parsetable.TryGetValue(DbConnectionStringSynonyms.Uid, out value) && !string.IsNullOrEmpty(value));
                }
                return false;
            }
        }

        internal string ExpandKeyword(string keyword, string replacementValue)
        {
            // preserve duplicates, updated keyword value with replacement value
            // if keyword not specified, append to end of the string
            bool expanded = false;
            int copyPosition = 0;

            StringBuilder builder = new(_usersConnectionString.Length);
            for (NameValuePair current = _keyChain; current != null; current = current.Next)
            {
                if ((current.Name == keyword) && (current.Value == this[keyword]))
                {
                    // only replace the parse end-result value instead of all values
                    // so that when duplicate-keywords occur other original values remain in place
                    AppendKeyValuePairBuilder(builder, current.Name, replacementValue);
                    builder.Append(';');
                    expanded = true;
                }
                else
                {
                    builder.Append(_usersConnectionString, copyPosition, current.Length);
                }
                copyPosition += current.Length;
            }

            if (!expanded)
            {
                AppendKeyValuePairBuilder(builder, keyword, replacementValue);
            }
            return builder.ToString();
        }

        internal static void AppendKeyValuePairBuilder(StringBuilder builder, string keyName, string keyValue)
        {
            ADP.CheckArgumentNull(builder, nameof(builder));
            ADP.CheckArgumentLength(keyName, nameof(keyName));

            if (keyName == null || !s_connectionStringValidKeyRegex.IsMatch(keyName))
            {
                throw ADP.InvalidKeyname(keyName);
            }
            if (keyValue != null && !IsValueValidInternal(keyValue))
            {
                throw ADP.InvalidValue(keyName);
            }

            if ((0 < builder.Length) && (';' != builder[builder.Length - 1]))
            {
                builder.Append(";");
            }

            builder.Append(keyName.Replace("=", "=="));
            builder.Append("=");

            if (keyValue != null)
            { // else <keyword>=;
                if (s_connectionStringQuoteValueRegex.IsMatch(keyValue))
                {
                    // <value> -> <value>
                    builder.Append(keyValue);
                }
                else if ((-1 != keyValue.IndexOf('\"')) && (-1 == keyValue.IndexOf('\'')))
                {
                    // <val"ue> -> <'val"ue'>
                    builder.Append('\'');
                    builder.Append(keyValue);
                    builder.Append('\'');
                }
                else
                {
                    // <val'ue> -> <"val'ue">
                    // <=value> -> <"=value">
                    // <;value> -> <";value">
                    // < value> -> <" value">
                    // <va lue> -> <"va lue">
                    // <va'"lue> -> <"va'""lue">
                    builder.Append('\"');
                    builder.Append(keyValue.Replace("\"", "\"\""));
                    builder.Append('\"');
                }
            }
        }
#endif

        #endregion

        #endregion
    }
}
