// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/SqlConnectionStringBuilder/*' />
    [DefaultProperty(DbConnectionStringKeywords.DataSource)]
    [TypeConverter(typeof(SqlConnectionStringBuilderConverter))]
    public sealed class SqlConnectionStringBuilder : DbConnectionStringBuilder
    {
        /// <summary>
        /// specific ordering for ConnectionString output construction
        /// </summary>
        private enum Keywords
        {
            DataSource,
            FailoverPartner,
            AttachDBFilename,
            InitialCatalog,
            IntegratedSecurity,
            PersistSecurityInfo,
            UserID,
            Password,
            Enlist,
            Pooling,
            MinPoolSize,
            MaxPoolSize,
            PoolBlockingPeriod,
            MultipleActiveResultSets,
            Replication,
            ConnectTimeout,
            Encrypt,
            HostNameInCertificate,
            ServerCertificate,
            TrustServerCertificate,
            LoadBalanceTimeout,
            PacketSize,
            TypeSystemVersion,
            Authentication,
            ApplicationName,
            CurrentLanguage,
            WorkstationID,
            UserInstance,
            TransactionBinding,
            ApplicationIntent,
            MultiSubnetFailover,
            ConnectRetryCount,
            ConnectRetryInterval,
            ColumnEncryptionSetting,
            EnclaveAttestationUrl,
            AttestationProtocol,
            CommandTimeout,
            IPAddressPreference,
            ServerSPN,
            FailoverPartnerSPN,
            ContextConnection,
#if NETFRAMEWORK
            ConnectionReset,
            NetworkLibrary,
            TransparentNetworkIPResolution,
#endif
            // keep the KeywordsCount value last
            KeywordsCount
        }

        #region Fields
        internal const int KeywordsCount = (int)Keywords.KeywordsCount;

        private static readonly string[] s_validKeywords = CreateValidKeywords();
        private static readonly Dictionary<string, Keywords> s_keywords = CreateKeywordsDictionary();

        private ApplicationIntent _applicationIntent = DbConnectionStringDefaults.ApplicationIntent;
        private string _applicationName = DbConnectionStringDefaults.ApplicationName;
        private string _attachDBFilename = DbConnectionStringDefaults.AttachDBFilename;
        private string _currentLanguage = DbConnectionStringDefaults.CurrentLanguage;
        private string _dataSource = DbConnectionStringDefaults.DataSource;
        private string _failoverPartner = DbConnectionStringDefaults.FailoverPartner;
        private string _initialCatalog = DbConnectionStringDefaults.InitialCatalog;
        private string _password = DbConnectionStringDefaults.Password;
        private string _transactionBinding = DbConnectionStringDefaults.TransactionBinding;
        private string _typeSystemVersion = DbConnectionStringDefaults.TypeSystemVersion;
        private string _userID = DbConnectionStringDefaults.UserID;
        private string _workstationID = DbConnectionStringDefaults.WorkstationID;

        private int _commandTimeout = DbConnectionStringDefaults.CommandTimeout;
        private int _connectTimeout = DbConnectionStringDefaults.ConnectTimeout;
        private int _loadBalanceTimeout = DbConnectionStringDefaults.LoadBalanceTimeout;
        private int _maxPoolSize = DbConnectionStringDefaults.MaxPoolSize;
        private int _minPoolSize = DbConnectionStringDefaults.MinPoolSize;
        private int _packetSize = DbConnectionStringDefaults.PacketSize;
        private int _connectRetryCount = DbConnectionStringDefaults.ConnectRetryCount;
        private int _connectRetryInterval = DbConnectionStringDefaults.ConnectRetryInterval;
        private SqlConnectionEncryptOption _encrypt = DbConnectionStringDefaults.Encrypt;
        private string _hostNameInCertificate = DbConnectionStringDefaults.HostNameInCertificate;
        private string _serverCertificate = DbConnectionStringDefaults.ServerCertificate;
        private bool _trustServerCertificate = DbConnectionStringDefaults.TrustServerCertificate;
        private bool _enlist = DbConnectionStringDefaults.Enlist;
        private bool _integratedSecurity = DbConnectionStringDefaults.IntegratedSecurity;
        private bool _multipleActiveResultSets = DbConnectionStringDefaults.MultipleActiveResultSets;
        private bool _multiSubnetFailover = DbConnectionStringDefaults.MultiSubnetFailover;

        private bool _persistSecurityInfo = DbConnectionStringDefaults.PersistSecurityInfo;
        private PoolBlockingPeriod _poolBlockingPeriod = DbConnectionStringDefaults.PoolBlockingPeriod;
        private bool _pooling = DbConnectionStringDefaults.Pooling;
        private bool _replication = DbConnectionStringDefaults.Replication;
        private bool _userInstance = DbConnectionStringDefaults.UserInstance;
        private SqlAuthenticationMethod _authentication = DbConnectionStringDefaults.Authentication;
        private SqlConnectionColumnEncryptionSetting _columnEncryptionSetting = DbConnectionStringDefaults.ColumnEncryptionSetting;
        private string _enclaveAttestationUrl = DbConnectionStringDefaults.EnclaveAttestationUrl;
        private SqlConnectionAttestationProtocol _attestationProtocol = DbConnectionStringDefaults.AttestationProtocol;
        private SqlConnectionIPAddressPreference _ipAddressPreference = DbConnectionStringDefaults.IPAddressPreference;
        private string _serverSPN = DbConnectionStringDefaults.ServerSPN;
        private string _failoverPartnerSPN = DbConnectionStringDefaults.FailoverPartnerSPN;

#if NETFRAMEWORK
        private bool _connectionReset = DbConnectionStringDefaults.ConnectionReset;
        private bool _transparentNetworkIPResolution = DbConnectionStringDefaults.TransparentNetworkIPResolution;
        private string _networkLibrary = DbConnectionStringDefaults.NetworkLibrary;
#endif
        #endregion //Fields

        #region Private Methods
        private static string[] CreateValidKeywords()
        {
            string[] validKeywords = new string[KeywordsCount];
            validKeywords[(int)Keywords.ApplicationIntent] = DbConnectionStringKeywords.ApplicationIntent;
            validKeywords[(int)Keywords.ApplicationName] = DbConnectionStringKeywords.ApplicationName;
            validKeywords[(int)Keywords.AttachDBFilename] = DbConnectionStringKeywords.AttachDBFilename;
            validKeywords[(int)Keywords.PoolBlockingPeriod] = DbConnectionStringKeywords.PoolBlockingPeriod;
            validKeywords[(int)Keywords.CommandTimeout] = DbConnectionStringKeywords.CommandTimeout;
            validKeywords[(int)Keywords.ConnectTimeout] = DbConnectionStringKeywords.ConnectTimeout;
            validKeywords[(int)Keywords.CurrentLanguage] = DbConnectionStringKeywords.CurrentLanguage;
            validKeywords[(int)Keywords.DataSource] = DbConnectionStringKeywords.DataSource;
            validKeywords[(int)Keywords.Encrypt] = DbConnectionStringKeywords.Encrypt;
            validKeywords[(int)Keywords.HostNameInCertificate] = DbConnectionStringKeywords.HostNameInCertificate;
            validKeywords[(int)Keywords.ServerCertificate] = DbConnectionStringKeywords.ServerCertificate;
            validKeywords[(int)Keywords.Enlist] = DbConnectionStringKeywords.Enlist;
            validKeywords[(int)Keywords.FailoverPartner] = DbConnectionStringKeywords.FailoverPartner;
            validKeywords[(int)Keywords.InitialCatalog] = DbConnectionStringKeywords.InitialCatalog;
            validKeywords[(int)Keywords.IntegratedSecurity] = DbConnectionStringKeywords.IntegratedSecurity;
            validKeywords[(int)Keywords.LoadBalanceTimeout] = DbConnectionStringKeywords.LoadBalanceTimeout;
            validKeywords[(int)Keywords.MaxPoolSize] = DbConnectionStringKeywords.MaxPoolSize;
            validKeywords[(int)Keywords.MinPoolSize] = DbConnectionStringKeywords.MinPoolSize;
            validKeywords[(int)Keywords.MultipleActiveResultSets] = DbConnectionStringKeywords.MultipleActiveResultSets;
            validKeywords[(int)Keywords.MultiSubnetFailover] = DbConnectionStringKeywords.MultiSubnetFailover;
            validKeywords[(int)Keywords.PacketSize] = DbConnectionStringKeywords.PacketSize;
            validKeywords[(int)Keywords.Password] = DbConnectionStringKeywords.Password;
            validKeywords[(int)Keywords.PersistSecurityInfo] = DbConnectionStringKeywords.PersistSecurityInfo;
            validKeywords[(int)Keywords.Pooling] = DbConnectionStringKeywords.Pooling;
            validKeywords[(int)Keywords.Replication] = DbConnectionStringKeywords.Replication;
            validKeywords[(int)Keywords.TransactionBinding] = DbConnectionStringKeywords.TransactionBinding;
            validKeywords[(int)Keywords.TrustServerCertificate] = DbConnectionStringKeywords.TrustServerCertificate;
            validKeywords[(int)Keywords.TypeSystemVersion] = DbConnectionStringKeywords.TypeSystemVersion;
            validKeywords[(int)Keywords.UserID] = DbConnectionStringKeywords.UserID;
            validKeywords[(int)Keywords.UserInstance] = DbConnectionStringKeywords.UserInstance;
            validKeywords[(int)Keywords.WorkstationID] = DbConnectionStringKeywords.WorkstationID;
            validKeywords[(int)Keywords.ConnectRetryCount] = DbConnectionStringKeywords.ConnectRetryCount;
            validKeywords[(int)Keywords.ConnectRetryInterval] = DbConnectionStringKeywords.ConnectRetryInterval;
            validKeywords[(int)Keywords.Authentication] = DbConnectionStringKeywords.Authentication;
            validKeywords[(int)Keywords.ColumnEncryptionSetting] = DbConnectionStringKeywords.ColumnEncryptionSetting;
            validKeywords[(int)Keywords.EnclaveAttestationUrl] = DbConnectionStringKeywords.EnclaveAttestationUrl;
            validKeywords[(int)Keywords.AttestationProtocol] = DbConnectionStringKeywords.AttestationProtocol;
            validKeywords[(int)Keywords.IPAddressPreference] = DbConnectionStringKeywords.IPAddressPreference;
            validKeywords[(int)Keywords.ServerSPN] = DbConnectionStringKeywords.ServerSPN;
            validKeywords[(int)Keywords.FailoverPartnerSPN] = DbConnectionStringKeywords.FailoverPartnerSPN;
            validKeywords[(int)Keywords.ContextConnection] = DbConnectionStringKeywords.ContextConnection;
#if NETFRAMEWORK
            validKeywords[(int)Keywords.ConnectionReset] = DbConnectionStringKeywords.ConnectionReset;
            validKeywords[(int)Keywords.NetworkLibrary] = DbConnectionStringKeywords.NetworkLibrary;
            validKeywords[(int)Keywords.TransparentNetworkIPResolution] = DbConnectionStringKeywords.TransparentNetworkIPResolution;
#endif
            return validKeywords;
        }

        private static Dictionary<string, Keywords> CreateKeywordsDictionary()
        {
            Dictionary<string, Keywords> pairs = new(KeywordsCount + SqlConnectionString.SynonymCount, StringComparer.OrdinalIgnoreCase)
            {
                { DbConnectionStringKeywords.ApplicationIntent, Keywords.ApplicationIntent },
                { DbConnectionStringKeywords.ApplicationName, Keywords.ApplicationName },
                { DbConnectionStringKeywords.AttachDBFilename, Keywords.AttachDBFilename },
                { DbConnectionStringKeywords.PoolBlockingPeriod, Keywords.PoolBlockingPeriod },
                { DbConnectionStringKeywords.CommandTimeout, Keywords.CommandTimeout },
                { DbConnectionStringKeywords.ConnectTimeout, Keywords.ConnectTimeout },
                { DbConnectionStringKeywords.CurrentLanguage, Keywords.CurrentLanguage },
                { DbConnectionStringKeywords.DataSource, Keywords.DataSource },
                { DbConnectionStringKeywords.Encrypt, Keywords.Encrypt },
                { DbConnectionStringKeywords.Enlist, Keywords.Enlist },
                { DbConnectionStringKeywords.FailoverPartner, Keywords.FailoverPartner },
                { DbConnectionStringKeywords.HostNameInCertificate, Keywords.HostNameInCertificate },
                { DbConnectionStringKeywords.ServerCertificate, Keywords.ServerCertificate },
                { DbConnectionStringKeywords.InitialCatalog, Keywords.InitialCatalog },
                { DbConnectionStringKeywords.IntegratedSecurity, Keywords.IntegratedSecurity },
                { DbConnectionStringKeywords.LoadBalanceTimeout, Keywords.LoadBalanceTimeout },
                { DbConnectionStringKeywords.MultipleActiveResultSets, Keywords.MultipleActiveResultSets },
                { DbConnectionStringKeywords.MaxPoolSize, Keywords.MaxPoolSize },
                { DbConnectionStringKeywords.MinPoolSize, Keywords.MinPoolSize },
                { DbConnectionStringKeywords.MultiSubnetFailover, Keywords.MultiSubnetFailover },
                { DbConnectionStringKeywords.PacketSize, Keywords.PacketSize },
                { DbConnectionStringKeywords.Password, Keywords.Password },
                { DbConnectionStringKeywords.PersistSecurityInfo, Keywords.PersistSecurityInfo },
                { DbConnectionStringKeywords.Pooling, Keywords.Pooling },
                { DbConnectionStringKeywords.Replication, Keywords.Replication },
                { DbConnectionStringKeywords.TransactionBinding, Keywords.TransactionBinding },
                { DbConnectionStringKeywords.TrustServerCertificate, Keywords.TrustServerCertificate },
                { DbConnectionStringKeywords.TypeSystemVersion, Keywords.TypeSystemVersion },
                { DbConnectionStringKeywords.UserID, Keywords.UserID },
                { DbConnectionStringKeywords.UserInstance, Keywords.UserInstance },
                { DbConnectionStringKeywords.WorkstationID, Keywords.WorkstationID },
                { DbConnectionStringKeywords.ConnectRetryCount, Keywords.ConnectRetryCount },
                { DbConnectionStringKeywords.ConnectRetryInterval, Keywords.ConnectRetryInterval },
                { DbConnectionStringKeywords.Authentication, Keywords.Authentication },
                { DbConnectionStringKeywords.ColumnEncryptionSetting, Keywords.ColumnEncryptionSetting },
                { DbConnectionStringKeywords.EnclaveAttestationUrl, Keywords.EnclaveAttestationUrl },
                { DbConnectionStringKeywords.AttestationProtocol, Keywords.AttestationProtocol },
                { DbConnectionStringKeywords.IPAddressPreference, Keywords.IPAddressPreference },
                { DbConnectionStringKeywords.ServerSPN, Keywords.ServerSPN },
                { DbConnectionStringKeywords.FailoverPartnerSPN, Keywords.FailoverPartnerSPN },
                { DbConnectionStringKeywords.ContextConnection, Keywords.ContextConnection },
#if NETFRAMEWORK
                { DbConnectionStringKeywords.ConnectionReset, Keywords.ConnectionReset },
                { DbConnectionStringKeywords.TransparentNetworkIPResolution, Keywords.TransparentNetworkIPResolution },
                { DbConnectionStringKeywords.NetworkLibrary, Keywords.NetworkLibrary },
                { DbConnectionStringSynonyms.NET, Keywords.NetworkLibrary },
                { DbConnectionStringSynonyms.NETWORK, Keywords.NetworkLibrary },
                { DbConnectionStringSynonyms.TRANSPARENTNETWORKIPRESOLUTION, Keywords.TransparentNetworkIPResolution },
#endif
                { DbConnectionStringSynonyms.IPADDRESSPREFERENCE, Keywords.IPAddressPreference },
                { DbConnectionStringSynonyms.APP, Keywords.ApplicationName },
                { DbConnectionStringSynonyms.APPLICATIONINTENT, Keywords.ApplicationIntent },
                { DbConnectionStringSynonyms.EXTENDEDPROPERTIES, Keywords.AttachDBFilename },
                { DbConnectionStringSynonyms.HOSTNAMEINCERTIFICATE, Keywords.HostNameInCertificate },
                { DbConnectionStringSynonyms.SERVERCERTIFICATE, Keywords.ServerCertificate },
                { DbConnectionStringSynonyms.INITIALFILENAME, Keywords.AttachDBFilename },
                { DbConnectionStringSynonyms.CONNECTIONTIMEOUT, Keywords.ConnectTimeout },
                { DbConnectionStringSynonyms.CONNECTRETRYCOUNT, Keywords.ConnectRetryCount },
                { DbConnectionStringSynonyms.CONNECTRETRYINTERVAL, Keywords.ConnectRetryInterval },
                { DbConnectionStringSynonyms.TIMEOUT, Keywords.ConnectTimeout },
                { DbConnectionStringSynonyms.LANGUAGE, Keywords.CurrentLanguage },
                { DbConnectionStringSynonyms.ADDR, Keywords.DataSource },
                { DbConnectionStringSynonyms.ADDRESS, Keywords.DataSource },
                { DbConnectionStringSynonyms.MULTIPLEACTIVERESULTSETS, Keywords.MultipleActiveResultSets },
                { DbConnectionStringSynonyms.MULTISUBNETFAILOVER, Keywords.MultiSubnetFailover },
                { DbConnectionStringSynonyms.NETWORKADDRESS, Keywords.DataSource },
                { DbConnectionStringSynonyms.POOLBLOCKINGPERIOD, Keywords.PoolBlockingPeriod },
                { DbConnectionStringSynonyms.SERVER, Keywords.DataSource },
                { DbConnectionStringSynonyms.DATABASE, Keywords.InitialCatalog },
                { DbConnectionStringSynonyms.TRUSTEDCONNECTION, Keywords.IntegratedSecurity },
                { DbConnectionStringSynonyms.TRUSTSERVERCERTIFICATE, Keywords.TrustServerCertificate },
                { DbConnectionStringSynonyms.ConnectionLifetime, Keywords.LoadBalanceTimeout },
                { DbConnectionStringSynonyms.Pwd, Keywords.Password },
                { DbConnectionStringSynonyms.PERSISTSECURITYINFO, Keywords.PersistSecurityInfo },
                { DbConnectionStringSynonyms.UID, Keywords.UserID },
                { DbConnectionStringSynonyms.User, Keywords.UserID },
                { DbConnectionStringSynonyms.WSID, Keywords.WorkstationID },
                { DbConnectionStringSynonyms.ServerSPN, Keywords.ServerSPN },
                { DbConnectionStringSynonyms.FailoverPartnerSPN, Keywords.FailoverPartnerSPN },
            };
            Debug.Assert((KeywordsCount + SqlConnectionString.SynonymCount) == pairs.Count, "initial expected size is incorrect");
            return pairs;
        }
        
        // @TODO These methods are completely unnecessary.

        private static bool ConvertToBoolean(object value) => DbConnectionStringUtilities.ConvertToBoolean(value);

        private static int ConvertToInt32(object value) => DbConnectionStringUtilities.ConvertToInt32(value);

        private static bool ConvertToIntegratedSecurity(object value) => DbConnectionStringUtilities.ConvertToIntegratedSecurity(value);

        private static SqlAuthenticationMethod ConvertToAuthenticationType(string keyword, object value) => DbConnectionStringUtilities.ConvertToAuthenticationType(keyword, value);

        private static string ConvertToString(object value) => DbConnectionStringUtilities.ConvertToString(value);

        private static ApplicationIntent ConvertToApplicationIntent(string keyword, object value) => DbConnectionStringUtilities.ConvertToApplicationIntent(keyword, value);

        private static SqlConnectionColumnEncryptionSetting ConvertToColumnEncryptionSetting(string keyword, object value)
            => DbConnectionStringUtilities.ConvertToColumnEncryptionSetting(keyword, value);

        private static SqlConnectionAttestationProtocol ConvertToAttestationProtocol(string keyword, object value)
            => AttestationProtocolUtilities.ConvertToAttestationProtocol(keyword, value);

        private static SqlConnectionEncryptOption ConvertToSqlConnectionEncryptOption(string keyword, object value)
           => AttestationProtocolUtilities.ConvertToSqlConnectionEncryptOption(keyword, value);

        private static SqlConnectionIPAddressPreference ConvertToIPAddressPreference(string keyword, object value)
            => IpAddressPreferenceUtilities.ConvertToIPAddressPreference(keyword, value);

        private static PoolBlockingPeriod ConvertToPoolBlockingPeriod(string keyword, object value)
            => PoolBlockingUtilities.ConvertToPoolBlockingPeriod(keyword, value);

        private object GetAt(Keywords index)
        {
            switch (index)
            {
                case Keywords.ApplicationIntent:
                    return ApplicationIntent;
                case Keywords.ApplicationName:
                    return ApplicationName;
                case Keywords.AttachDBFilename:
                    return AttachDBFilename;
                case Keywords.PoolBlockingPeriod:
                    return PoolBlockingPeriod;
                case Keywords.CommandTimeout:
                    return CommandTimeout;
                case Keywords.ConnectTimeout:
                    return ConnectTimeout;
                case Keywords.CurrentLanguage:
                    return CurrentLanguage;
                case Keywords.DataSource:
                    return DataSource;
                case Keywords.Encrypt:
                    return Encrypt;
                case Keywords.HostNameInCertificate:
                    return HostNameInCertificate;
                case Keywords.ServerCertificate:
                    return ServerCertificate;
                case Keywords.Enlist:
                    return Enlist;
                case Keywords.FailoverPartner:
                    return FailoverPartner;
                case Keywords.InitialCatalog:
                    return InitialCatalog;
                case Keywords.IntegratedSecurity:
                    return IntegratedSecurity;
                case Keywords.LoadBalanceTimeout:
                    return LoadBalanceTimeout;
                case Keywords.MultipleActiveResultSets:
                    return MultipleActiveResultSets;
                case Keywords.MaxPoolSize:
                    return MaxPoolSize;
                case Keywords.MinPoolSize:
                    return MinPoolSize;
                case Keywords.MultiSubnetFailover:
                    return MultiSubnetFailover;
                case Keywords.PacketSize:
                    return PacketSize;
                case Keywords.Password:
                    return Password;
                case Keywords.PersistSecurityInfo:
                    return PersistSecurityInfo;
                case Keywords.Pooling:
                    return Pooling;
                case Keywords.Replication:
                    return Replication;
                case Keywords.TransactionBinding:
                    return TransactionBinding;
                case Keywords.TrustServerCertificate:
                    return TrustServerCertificate;
                case Keywords.TypeSystemVersion:
                    return TypeSystemVersion;
                case Keywords.UserID:
                    return UserID;
                case Keywords.UserInstance:
                    return UserInstance;
                case Keywords.WorkstationID:
                    return WorkstationID;
                case Keywords.ConnectRetryCount:
                    return ConnectRetryCount;
                case Keywords.ConnectRetryInterval:
                    return ConnectRetryInterval;
                case Keywords.Authentication:
                    return Authentication;
                case Keywords.ColumnEncryptionSetting:
                    return ColumnEncryptionSetting;
                case Keywords.EnclaveAttestationUrl:
                    return EnclaveAttestationUrl;
                case Keywords.AttestationProtocol:
                    return AttestationProtocol;
                case Keywords.IPAddressPreference:
                    return IPAddressPreference;
                case Keywords.ServerSPN:
                    return ServerSPN;
                case Keywords.FailoverPartnerSPN:
                    return FailoverPartnerSPN;
                case Keywords.ContextConnection:
                    return false;
#if NETFRAMEWORK
#pragma warning disable 618 // Obsolete properties
                case Keywords.ConnectionReset:
                    return ConnectionReset;
#pragma warning restore 618
                case Keywords.TransparentNetworkIPResolution:
                    return TransparentNetworkIPResolution;
                case Keywords.NetworkLibrary:
                    return NetworkLibrary;
#endif
                default:
                    Debug.Fail("unexpected keyword");
                    throw UnsupportedKeyword(s_validKeywords[(int)index]);
            }
        }

        private Keywords GetIndex(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            if (s_keywords.TryGetValue(keyword, out Keywords index))
            {
                return index;
            }

            throw UnsupportedKeyword(keyword);
        }

        private void Reset(Keywords index)
        {
            switch (index)
            {
                case Keywords.ApplicationIntent:
                    _applicationIntent = DbConnectionStringDefaults.ApplicationIntent;
                    break;
                case Keywords.ApplicationName:
                    _applicationName = DbConnectionStringDefaults.ApplicationName;
                    break;
                case Keywords.AttachDBFilename:
                    _attachDBFilename = DbConnectionStringDefaults.AttachDBFilename;
                    break;
                case Keywords.Authentication:
                    _authentication = DbConnectionStringDefaults.Authentication;
                    break;
                case Keywords.PoolBlockingPeriod:
                    _poolBlockingPeriod = DbConnectionStringDefaults.PoolBlockingPeriod;
                    break;
                case Keywords.CommandTimeout:
                    _commandTimeout = DbConnectionStringDefaults.CommandTimeout;
                    break;
                case Keywords.ConnectTimeout:
                    _connectTimeout = DbConnectionStringDefaults.ConnectTimeout;
                    break;
                case Keywords.CurrentLanguage:
                    _currentLanguage = DbConnectionStringDefaults.CurrentLanguage;
                    break;
                case Keywords.DataSource:
                    _dataSource = DbConnectionStringDefaults.DataSource;
                    break;
                case Keywords.Encrypt:
                    _encrypt = DbConnectionStringDefaults.Encrypt;
                    break;
                case Keywords.HostNameInCertificate:
                    _hostNameInCertificate = DbConnectionStringDefaults.HostNameInCertificate;
                    break;
                case Keywords.ServerCertificate:
                    _serverCertificate = DbConnectionStringDefaults.ServerCertificate;
                    break;
                case Keywords.Enlist:
                    _enlist = DbConnectionStringDefaults.Enlist;
                    break;
                case Keywords.FailoverPartner:
                    _failoverPartner = DbConnectionStringDefaults.FailoverPartner;
                    break;
                case Keywords.InitialCatalog:
                    _initialCatalog = DbConnectionStringDefaults.InitialCatalog;
                    break;
                case Keywords.IntegratedSecurity:
                    _integratedSecurity = DbConnectionStringDefaults.IntegratedSecurity;
                    break;
                case Keywords.LoadBalanceTimeout:
                    _loadBalanceTimeout = DbConnectionStringDefaults.LoadBalanceTimeout;
                    break;
                case Keywords.MultipleActiveResultSets:
                    _multipleActiveResultSets = DbConnectionStringDefaults.MultipleActiveResultSets;
                    break;
                case Keywords.MaxPoolSize:
                    _maxPoolSize = DbConnectionStringDefaults.MaxPoolSize;
                    break;
                case Keywords.MinPoolSize:
                    _minPoolSize = DbConnectionStringDefaults.MinPoolSize;
                    break;
                case Keywords.MultiSubnetFailover:
                    _multiSubnetFailover = DbConnectionStringDefaults.MultiSubnetFailover;
                    break;
                case Keywords.PacketSize:
                    _packetSize = DbConnectionStringDefaults.PacketSize;
                    break;
                case Keywords.Password:
                    _password = DbConnectionStringDefaults.Password;
                    break;
                case Keywords.PersistSecurityInfo:
                    _persistSecurityInfo = DbConnectionStringDefaults.PersistSecurityInfo;
                    break;
                case Keywords.Pooling:
                    _pooling = DbConnectionStringDefaults.Pooling;
                    break;
                case Keywords.ConnectRetryCount:
                    _connectRetryCount = DbConnectionStringDefaults.ConnectRetryCount;
                    break;
                case Keywords.ConnectRetryInterval:
                    _connectRetryInterval = DbConnectionStringDefaults.ConnectRetryInterval;
                    break;
                case Keywords.Replication:
                    _replication = DbConnectionStringDefaults.Replication;
                    break;
                case Keywords.TransactionBinding:
                    _transactionBinding = DbConnectionStringDefaults.TransactionBinding;
                    break;
                case Keywords.TrustServerCertificate:
                    _trustServerCertificate = DbConnectionStringDefaults.TrustServerCertificate;
                    break;
                case Keywords.TypeSystemVersion:
                    _typeSystemVersion = DbConnectionStringDefaults.TypeSystemVersion;
                    break;
                case Keywords.UserID:
                    _userID = DbConnectionStringDefaults.UserID;
                    break;
                case Keywords.UserInstance:
                    _userInstance = DbConnectionStringDefaults.UserInstance;
                    break;
                case Keywords.WorkstationID:
                    _workstationID = DbConnectionStringDefaults.WorkstationID;
                    break;
                case Keywords.ColumnEncryptionSetting:
                    _columnEncryptionSetting = DbConnectionStringDefaults.ColumnEncryptionSetting;
                    break;
                case Keywords.EnclaveAttestationUrl:
                    _enclaveAttestationUrl = DbConnectionStringDefaults.EnclaveAttestationUrl;
                    break;
                case Keywords.AttestationProtocol:
                    _attestationProtocol = DbConnectionStringDefaults.AttestationProtocol;
                    break;
                case Keywords.IPAddressPreference:
                    _ipAddressPreference = DbConnectionStringDefaults.IPAddressPreference;
                    break;
                case Keywords.ServerSPN:
                    _serverSPN = DbConnectionStringDefaults.ServerSPN;
                    break;
                case Keywords.FailoverPartnerSPN:
                    _failoverPartnerSPN = DbConnectionStringDefaults.FailoverPartnerSPN;
                    break;
                case Keywords.ContextConnection:
                    break;
#if NETFRAMEWORK
                case Keywords.ConnectionReset:
                    _connectionReset = DbConnectionStringDefaults.ConnectionReset;
                    break;
                case Keywords.TransparentNetworkIPResolution:
                    _transparentNetworkIPResolution = DbConnectionStringDefaults.TransparentNetworkIPResolution;
                    break;
                case Keywords.NetworkLibrary:
                    _networkLibrary = DbConnectionStringDefaults.NetworkLibrary;
                    break;
#endif
                default:
                    Debug.Fail("unexpected keyword");
                    throw UnsupportedKeyword(s_validKeywords[(int)index]);
            }
        }

        // @TODO: These methods can be inlined with the property setters.
        
        private void SetValue(string keyword, bool value) => base[keyword] = value.ToString();

        private void SetValue(string keyword, int value) => base[keyword] = value.ToString((System.IFormatProvider)null);

        private void SetValue(string keyword, string value)
        {
            ADP.CheckArgumentNull(value, keyword);
            base[keyword] = value;
        }

        private void SetApplicationIntentValue(ApplicationIntent value)
        {
            Debug.Assert(DbConnectionStringUtilities.IsValidApplicationIntentValue(value), "invalid value for ApplicationIntent");
            base[DbConnectionStringKeywords.ApplicationIntent] = DbConnectionStringUtilities.ApplicationIntentToString(value);
        }

        private void SetColumnEncryptionSettingValue(SqlConnectionColumnEncryptionSetting value)
        {
            Debug.Assert(DbConnectionStringUtilities.IsValidColumnEncryptionSetting(value), "Invalid value for SqlConnectionColumnEncryptionSetting");
            base[DbConnectionStringKeywords.ColumnEncryptionSetting] = DbConnectionStringUtilities.ColumnEncryptionSettingToString(value);
        }

        private void SetAttestationProtocolValue(SqlConnectionAttestationProtocol value)
        {
            Debug.Assert(AttestationProtocolUtilities.IsValidAttestationProtocol(value), "Invalid value for SqlConnectionAttestationProtocol");
            base[DbConnectionStringKeywords.AttestationProtocol] = AttestationProtocolUtilities.AttestationProtocolToString(value);
        }

        private void SetSqlConnectionEncryptionValue(SqlConnectionEncryptOption value)
        {
            base[DbConnectionStringKeywords.Encrypt] = value.ToString();
        }

        private void SetIPAddressPreferenceValue(SqlConnectionIPAddressPreference value)
        {
            Debug.Assert(IpAddressPreferenceUtilities.IsValidIPAddressPreference(value), "Invalid value for SqlConnectionIPAddressPreference");
            base[DbConnectionStringKeywords.IPAddressPreference] = IpAddressPreferenceUtilities.IPAddressPreferenceToString(value);
        }

        private void SetAuthenticationValue(SqlAuthenticationMethod value)
        {
            Debug.Assert(DbConnectionStringUtilities.IsValidAuthenticationTypeValue(value), "Invalid value for AuthenticationType");
            base[DbConnectionStringKeywords.Authentication] = DbConnectionStringUtilities.AuthenticationTypeToString(value);
        }

        private void SetPoolBlockingPeriodValue(PoolBlockingPeriod value)
        {
            Debug.Assert(PoolBlockingUtilities.IsValidPoolBlockingPeriodValue(value), "Invalid value for PoolBlockingPeriod");
            base[DbConnectionStringKeywords.PoolBlockingPeriod] = PoolBlockingUtilities.PoolBlockingPeriodToString(value);
        }

        private Exception UnsupportedKeyword(string keyword)
        {
#if NET
            for (int index = 0; index < s_notSupportedKeywords.Length; index++)
            {
                if (string.Equals(keyword, s_notSupportedKeywords[index], StringComparison.OrdinalIgnoreCase))
                {
                    return SQL.UnsupportedKeyword(keyword);
                }
            }

            for (int index = 0; index < s_notSupportedNetworkLibraryKeywords.Length; index++)
            {
                if (string.Equals(keyword, s_notSupportedNetworkLibraryKeywords[index], StringComparison.OrdinalIgnoreCase))
                {
                    return SQL.NetworkLibraryKeywordNotSupported();
                }
            }
#endif
            return ADP.KeywordNotSupported(keyword);
        }

        private sealed class SqlInitialCatalogConverter : StringConverter
        {
            // converter classes should have public ctor
            public SqlInitialCatalogConverter() { }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => GetStandardValuesSupportedInternal(context);

            private bool GetStandardValuesSupportedInternal(ITypeDescriptorContext context)
            {
                // Only say standard values are supported if the connection string has enough
                // information set to instantiate a connection and retrieve a list of databases
                bool flag = false;
                if (context is not null)
                {
                    SqlConnectionStringBuilder constr = (context.Instance as SqlConnectionStringBuilder);
                    if (constr is not null)
                    {
                        if ((0 < constr.DataSource.Length) && (constr.IntegratedSecurity || (0 < constr.UserID.Length)))
                        {
                            flag = true;
                        }
                    }
                }
                return flag;
            }

            // Although theoretically this could be true, some people may want to just type in a name
            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                // There can only be standard values if the connection string is in a state that might
                // be able to instantiate a connection
                if (GetStandardValuesSupportedInternal(context))
                {
                    // Create an array list to store the database names
                    List<string> values = new List<string>();

                    try
                    {
                        SqlConnectionStringBuilder constr = (SqlConnectionStringBuilder)context.Instance;

                        // Create a connection
                        using (SqlConnection connection = new SqlConnection())
                        {
                            // Create a basic connection string from current property values
                            connection.ConnectionString = constr.ConnectionString;

                            // Try to open the connection
                            connection.Open();

                            DataTable databaseTable = connection.GetSchema("DATABASES");

                            foreach (DataRow row in databaseTable.Rows)
                            {
                                string dbName = (string)row["database_name"];
                                values.Add(dbName);
                            }
                        }
                    }
                    catch (SqlException e)
                    {
                        ADP.TraceExceptionWithoutRethrow(e);
                        // silently fail
                    }

                    // Return values as a StandardValuesCollection
                    return new StandardValuesCollection(values);
                }
                return null;
            }
        }

        internal sealed class SqlConnectionStringBuilderConverter : ExpandableObjectConverter
        {
            // converter classes should have public ctor
            public SqlConnectionStringBuilderConverter() { }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                if (typeof(InstanceDescriptor) == destinationType)
                {
                    return true;
                }
                return base.CanConvertTo(context, destinationType);
            }

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType is null)
                {
                    throw ADP.ArgumentNull(nameof(destinationType));
                }
                if (typeof(InstanceDescriptor) == destinationType)
                {
                    SqlConnectionStringBuilder obj = (value as SqlConnectionStringBuilder);
                    if (obj is not null)
                    {
                        return ConvertToInstanceDescriptor(obj);
                    }
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }

            private InstanceDescriptor ConvertToInstanceDescriptor(SqlConnectionStringBuilder options)
            {
                Type[] ctorParams = new Type[] { typeof(string) };
                object[] ctorValues = new object[] { options.ConnectionString };
                ConstructorInfo ctor = typeof(SqlConnectionStringBuilder).GetConstructor(ctorParams);
                return new InstanceDescriptor(ctor, ctorValues);
            }
        }

        private sealed class SqlDataSourceConverter : StringConverter
        {
            private StandardValuesCollection _standardValues;

            // converter classes should have public ctor
            public SqlDataSourceConverter() { }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                StandardValuesCollection dataSourceNames = _standardValues;
                if (_standardValues is null)
                {
                    // Get the sources rowset for the SQLOLEDB enumerator
                    DataTable table = SqlClientFactory.Instance.CreateDataSourceEnumerator().GetDataSources();
                    DataColumn serverName = table.Columns[Microsoft.Data.Sql.SqlDataSourceEnumeratorUtil.ServerNameCol];
                    DataColumn instanceName = table.Columns[Microsoft.Data.Sql.SqlDataSourceEnumeratorUtil.InstanceNameCol];
                    DataRowCollection rows = table.Rows;

                    string[] serverNames = new string[rows.Count];
                    for (int i = 0; i < serverNames.Length; ++i)
                    {
                        string server = rows[i][serverName] as string;
                        string instance = rows[i][instanceName] as string;
                        if ((instance is null) || (0 == instance.Length) || ("MSSQLSERVER" == instance))
                        {
                            serverNames[i] = server;
                        }
                        else
                        {
                            serverNames[i] = server + @"\" + instance;
                        }
                    }
                    Array.Sort<string>(serverNames);

                    // Create the standard values collection that contains the sources
                    dataSourceNames = new StandardValuesCollection(serverNames);
                    _standardValues = dataSourceNames;
                }
                return dataSourceNames;
            }
        }

        private sealed class NetworkLibraryConverter : TypeConverter
        {
            // private const string AppleTalk     = "Apple Talk (DBMSADSN)";  Invalid protocals
            // private const string BanyanVines   = "Banyan VINES (DBMSVINN)";
            // private const string IPXSPX        = "NWLink IPX/SPX (DBMSSPXN)";
            // private const string Multiprotocol = "Multiprotocol (DBMSRPCN)";
            private const string NamedPipes = "Named Pipes (DBNMPNTW)";   // valid protocols
            private const string SharedMemory = "Shared Memory (DBMSLPCN)";
            private const string TCPIP = "TCP/IP (DBMSSOCN)";
            private const string VIA = "VIA (DBMSGNET)";

            // these are correctly non-static, property grid will cache an instance
            private StandardValuesCollection _standardValues;

            // converter classes should have public ctor
            public NetworkLibraryConverter() { }

            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
                // Only know how to convert from a string
                => (typeof(string) == sourceType) || base.CanConvertFrom(context, sourceType);

            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                string svalue = (value as string);
                if (svalue is not null)
                {
                    svalue = svalue.Trim();
                    if (StringComparer.OrdinalIgnoreCase.Equals(svalue, NamedPipes))
                    {
                        return SqlConnectionString.NETLIB.NamedPipes;
                    }
                    else if (StringComparer.OrdinalIgnoreCase.Equals(svalue, SharedMemory))
                    {
                        return SqlConnectionString.NETLIB.SharedMemory;
                    }
                    else if (StringComparer.OrdinalIgnoreCase.Equals(svalue, TCPIP))
                    {
                        return SqlConnectionString.NETLIB.TCPIP;
                    }
                    else if (StringComparer.OrdinalIgnoreCase.Equals(svalue, VIA))
                    {
                        return SqlConnectionString.NETLIB.VIA;
                    }
                    else
                    {
                        return svalue;
                    }
                }
                return base.ConvertFrom(context, culture, value);
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
                => (typeof(string) == destinationType) || base.CanConvertTo(context, destinationType);

            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if ((value is string svalue) && (destinationType == typeof(string)))
                {
                    return svalue.Trim().ToLower(CultureInfo.InvariantCulture) switch
                    {
                        SqlConnectionString.NETLIB.NamedPipes => NamedPipes,
                        SqlConnectionString.NETLIB.SharedMemory => SharedMemory,
                        SqlConnectionString.NETLIB.TCPIP => TCPIP,
                        SqlConnectionString.NETLIB.VIA => VIA,
                        _ => svalue,
                    };
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                StandardValuesCollection standardValues = _standardValues;
                if (standardValues is null)
                {
                    string[] names = new string[] {
                        NamedPipes,
                        SharedMemory,
                        TCPIP,
                        VIA,
                    };
                    standardValues = new StandardValuesCollection(names);
                    _standardValues = standardValues;
                }
                return standardValues;
            }
        }
#if NET
        private static readonly string[] s_notSupportedKeywords = {
            DbConnectionStringKeywords.ConnectionReset,
            DbConnectionStringKeywords.TransactionBinding,
            DbConnectionStringKeywords.TransparentNetworkIPResolution,
            DbConnectionStringSynonyms.TRANSPARENTNETWORKIPRESOLUTION,
        };

        private static readonly string[] s_notSupportedNetworkLibraryKeywords = {
            DbConnectionStringKeywords.NetworkLibrary,

            DbConnectionStringSynonyms.NET,
            DbConnectionStringSynonyms.NETWORK,
        };
#endif
        #endregion //Private Methods

        #region Public APIs
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ctor2/*' />
        public SqlConnectionStringBuilder() : this(null)
        {
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ctorConnectionString/*' />
        public SqlConnectionStringBuilder(string connectionString) : base()
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                ConnectionString = connectionString;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Item/*' />
        public override object this[string keyword]
        {
            get => GetAt(GetIndex(keyword));
            set
            {
                if (value is not null)
                {
                    Keywords index = GetIndex(keyword);
                    switch (index)
                    {
                        case Keywords.ApplicationIntent:
                            ApplicationIntent = ConvertToApplicationIntent(keyword, value);
                            break;
                        case Keywords.ApplicationName:
                            ApplicationName = ConvertToString(value);
                            break;
                        case Keywords.AttachDBFilename:
                            AttachDBFilename = ConvertToString(value);
                            break;
                        case Keywords.CurrentLanguage:
                            CurrentLanguage = ConvertToString(value);
                            break;
                        case Keywords.DataSource:
                            DataSource = ConvertToString(value);
                            break;
                        case Keywords.FailoverPartner:
                            FailoverPartner = ConvertToString(value);
                            break;
                        case Keywords.InitialCatalog:
                            InitialCatalog = ConvertToString(value);
                            break;
                        case Keywords.Password:
                            Password = ConvertToString(value);
                            break;
                        case Keywords.UserID:
                            UserID = ConvertToString(value);
                            break;
                        case Keywords.TransactionBinding:
                            TransactionBinding = ConvertToString(value);
                            break;
                        case Keywords.TypeSystemVersion:
                            TypeSystemVersion = ConvertToString(value);
                            break;
                        case Keywords.WorkstationID:
                            WorkstationID = ConvertToString(value);
                            break;

                        case Keywords.CommandTimeout:
                            CommandTimeout = ConvertToInt32(value);
                            break;
                        case Keywords.ConnectTimeout:
                            ConnectTimeout = ConvertToInt32(value);
                            break;
                        case Keywords.LoadBalanceTimeout:
                            LoadBalanceTimeout = ConvertToInt32(value);
                            break;
                        case Keywords.MaxPoolSize:
                            MaxPoolSize = ConvertToInt32(value);
                            break;
                        case Keywords.MinPoolSize:
                            MinPoolSize = ConvertToInt32(value);
                            break;
                        case Keywords.PacketSize:
                            PacketSize = ConvertToInt32(value);
                            break;

                        case Keywords.IntegratedSecurity:
                            IntegratedSecurity = ConvertToIntegratedSecurity(value);
                            break;
                        case Keywords.Authentication:
                            Authentication = ConvertToAuthenticationType(keyword, value);
                            break;
                        case Keywords.ColumnEncryptionSetting:
                            ColumnEncryptionSetting = ConvertToColumnEncryptionSetting(keyword, value);
                            break;
                        case Keywords.EnclaveAttestationUrl:
                            EnclaveAttestationUrl = ConvertToString(value);
                            break;
                        case Keywords.AttestationProtocol:
                            AttestationProtocol = ConvertToAttestationProtocol(keyword, value);
                            break;
                        case Keywords.IPAddressPreference:
                            IPAddressPreference = ConvertToIPAddressPreference(keyword, value);
                            break;
                        case Keywords.PoolBlockingPeriod:
                            PoolBlockingPeriod = ConvertToPoolBlockingPeriod(keyword, value);
                            break;
                        case Keywords.Encrypt:
                            Encrypt = ConvertToSqlConnectionEncryptOption(keyword, value);
                            break;
                        case Keywords.HostNameInCertificate:
                            HostNameInCertificate = ConvertToString(value);
                            break;
                        case Keywords.ServerCertificate:
                            ServerCertificate = ConvertToString(value);
                            break;
                        case Keywords.TrustServerCertificate:
                            TrustServerCertificate = ConvertToBoolean(value);
                            break;
                        case Keywords.Enlist:
                            Enlist = ConvertToBoolean(value);
                            break;
                        case Keywords.MultipleActiveResultSets:
                            MultipleActiveResultSets = ConvertToBoolean(value);
                            break;
                        case Keywords.MultiSubnetFailover:
                            MultiSubnetFailover = ConvertToBoolean(value);
                            break;
                        case Keywords.PersistSecurityInfo:
                            PersistSecurityInfo = ConvertToBoolean(value);
                            break;
                        case Keywords.Pooling:
                            Pooling = ConvertToBoolean(value);
                            break;
                        case Keywords.Replication:
                            Replication = ConvertToBoolean(value);
                            break;
                        case Keywords.UserInstance:
                            UserInstance = ConvertToBoolean(value);
                            break;
                        case Keywords.ConnectRetryCount:
                            ConnectRetryCount = ConvertToInt32(value);
                            break;
                        case Keywords.ConnectRetryInterval:
                            ConnectRetryInterval = ConvertToInt32(value);
                            break;
                        case Keywords.ServerSPN:
                            ServerSPN = ConvertToString(value);
                            break;
                        case Keywords.FailoverPartnerSPN:
                            FailoverPartnerSPN = ConvertToString(value);
                            break;
                        case Keywords.ContextConnection:
                            if (ConvertToBoolean(value))
                            {
                                throw SQL.ContextConnectionIsUnsupported();
                            }
                            break;
#if NETFRAMEWORK
#pragma warning disable 618 // Obsolete properties
                        case Keywords.ConnectionReset:
                            ConnectionReset = ConvertToBoolean(value);
                            break;
#pragma warning restore 618
                        case Keywords.NetworkLibrary:
                            NetworkLibrary = ConvertToString(value);
                            break;
                        case Keywords.TransparentNetworkIPResolution:
                            TransparentNetworkIPResolution = ConvertToBoolean(value);
                            break;
#endif
                        default:
                            Debug.Fail("unexpected keyword");
                            throw UnsupportedKeyword(keyword);
                    }
                }
                else
                {
                    Remove(keyword);
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ApplicationIntent/*' />
        [DisplayName(DbConnectionStringKeywords.ApplicationIntent)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ApplicationIntent)]
        [RefreshProperties(RefreshProperties.All)]
        public ApplicationIntent ApplicationIntent
        {
            get => _applicationIntent;
            set
            {
                if (!DbConnectionStringUtilities.IsValidApplicationIntentValue(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(ApplicationIntent), (int)value);
                }

                SetApplicationIntentValue(value);
                _applicationIntent = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ApplicationName/*' />
        [DisplayName(DbConnectionStringKeywords.ApplicationName)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Context)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ApplicationName)]
        [RefreshProperties(RefreshProperties.All)]
        public string ApplicationName
        {
            get => _applicationName;
            set
            {
                SetValue(DbConnectionStringKeywords.ApplicationName, value);
                _applicationName = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/AttachDBFilename/*' />
        [DisplayName(DbConnectionStringKeywords.AttachDBFilename)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_AttachDBFilename)]
        [Editor("System.Windows.Forms.Design.FileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        [RefreshProperties(RefreshProperties.All)]
        public string AttachDBFilename
        {
            get => _attachDBFilename;
            set
            {
                SetValue(DbConnectionStringKeywords.AttachDBFilename, value);
                _attachDBFilename = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/CommandTimeout/*' />
        [DisplayName(DbConnectionStringKeywords.CommandTimeout)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_CommandTimeout)]
        [RefreshProperties(RefreshProperties.All)]
        public int CommandTimeout
        {
            get => _commandTimeout;
            set
            {
                if (value < 0)
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.CommandTimeout);
                }
                SetValue(DbConnectionStringKeywords.CommandTimeout, value);
                _commandTimeout = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectTimeout/*' />
        [DisplayName(DbConnectionStringKeywords.ConnectTimeout)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ConnectTimeout)]
        [RefreshProperties(RefreshProperties.All)]
        public int ConnectTimeout
        {
            get => _connectTimeout;
            set
            {
                if (value < 0)
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ConnectTimeout);
                }
                SetValue(DbConnectionStringKeywords.ConnectTimeout, value);
                _connectTimeout = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/CurrentLanguage/*' />
        [DisplayName(DbConnectionStringKeywords.CurrentLanguage)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_CurrentLanguage)]
        [RefreshProperties(RefreshProperties.All)]
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                SetValue(DbConnectionStringKeywords.CurrentLanguage, value);
                _currentLanguage = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/DataSource/*' />
        [DisplayName(DbConnectionStringKeywords.DataSource)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_DataSource)]
        [RefreshProperties(RefreshProperties.All)]
        [TypeConverter("Microsoft.Data.SqlClient.SqlConnectionStringBuilder+SqlDataSourceConverter, Microsoft.Data.SqlClient")]
        public string DataSource
        {
            get => _dataSource;
            set
            {
                SetValue(DbConnectionStringKeywords.DataSource, value);
                _dataSource = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ServerSPN/*' />
        [DisplayName(DbConnectionStringKeywords.ServerSPN)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ServerSPN)]
        [RefreshProperties(RefreshProperties.All)]
        public string ServerSPN
        {
            get => _serverSPN;
            set
            {
                SetValue(DbConnectionStringKeywords.ServerSPN, value);
                _serverSPN = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Encrypt/*' />
        [DisplayName(DbConnectionStringKeywords.Encrypt)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_Encrypt)]
        [RefreshProperties(RefreshProperties.All)]
        public SqlConnectionEncryptOption Encrypt
        {
            get => _encrypt;
            set
            {
                SqlConnectionEncryptOption newValue = value ?? DbConnectionStringDefaults.Encrypt;
                SetSqlConnectionEncryptionValue(newValue);
                _encrypt = newValue;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/HostNameInCertificate/*' />
        [DisplayName(DbConnectionStringKeywords.HostNameInCertificate)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_HostNameInCertificate)]
        [RefreshProperties(RefreshProperties.All)]
        public string HostNameInCertificate
        {
            get => _hostNameInCertificate;
            set
            {
                SetValue(DbConnectionStringKeywords.HostNameInCertificate, value);
                _hostNameInCertificate = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ServerCertificate/*' />
        [DisplayName(DbConnectionStringKeywords.ServerCertificate)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ServerCertificate)]
        [RefreshProperties(RefreshProperties.All)]
        public string ServerCertificate
        {
            get => _serverCertificate;
            set
            {
                SetValue(DbConnectionStringKeywords.ServerCertificate, value);
                _serverCertificate = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ColumnEncryptionSetting/*' />
        [DisplayName(DbConnectionStringKeywords.ColumnEncryptionSetting)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.TCE_DbConnectionString_ColumnEncryptionSetting)]
        [RefreshProperties(RefreshProperties.All)]
        public SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting
        {
            get => _columnEncryptionSetting;
            set
            {
                if (!DbConnectionStringUtilities.IsValidColumnEncryptionSetting(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionColumnEncryptionSetting), (int)value);
                }

                SetColumnEncryptionSettingValue(value);
                _columnEncryptionSetting = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/EnclaveAttestationUrl/*' />
        [DisplayName(DbConnectionStringKeywords.EnclaveAttestationUrl)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.TCE_DbConnectionString_EnclaveAttestationUrl)]
        [RefreshProperties(RefreshProperties.All)]
        public string EnclaveAttestationUrl
        {
            get => _enclaveAttestationUrl;
            set
            {
                SetValue(DbConnectionStringKeywords.EnclaveAttestationUrl, value);
                _enclaveAttestationUrl = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/AttestationProtocol/*' />
        [DisplayName(DbConnectionStringKeywords.AttestationProtocol)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.TCE_DbConnectionString_AttestationProtocol)]
        [RefreshProperties(RefreshProperties.All)]
        public SqlConnectionAttestationProtocol AttestationProtocol
        {
            get => _attestationProtocol;
            set
            {
                if (!AttestationProtocolUtilities.IsValidAttestationProtocol(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionAttestationProtocol), (int)value);
                }

                SetAttestationProtocolValue(value);
                _attestationProtocol = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IPAddressPreference/*' />
        [DisplayName(DbConnectionStringKeywords.IPAddressPreference)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.TCE_DbConnectionString_IPAddressPreference)]
        [RefreshProperties(RefreshProperties.All)]
        public SqlConnectionIPAddressPreference IPAddressPreference
        {
            get => _ipAddressPreference;
            set
            {
                if (!IpAddressPreferenceUtilities.IsValidIPAddressPreference(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionIPAddressPreference), (int)value);
                }

                SetIPAddressPreferenceValue(value);
                _ipAddressPreference = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TrustServerCertificate/*' />
        [DisplayName(DbConnectionStringKeywords.TrustServerCertificate)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_TrustServerCertificate)]
        [RefreshProperties(RefreshProperties.All)]
        public bool TrustServerCertificate
        {
            get => _trustServerCertificate;
            set
            {
                SetValue(DbConnectionStringKeywords.TrustServerCertificate, value);
                _trustServerCertificate = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Enlist/*' />
        [DisplayName(DbConnectionStringKeywords.Enlist)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_Enlist)]
        [RefreshProperties(RefreshProperties.All)]
        public bool Enlist
        {
            get => _enlist;
            set
            {
                SetValue(DbConnectionStringKeywords.Enlist, value);
                _enlist = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/FailoverPartner/*' />
        [DisplayName(DbConnectionStringKeywords.FailoverPartner)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_FailoverPartner)]
        [RefreshProperties(RefreshProperties.All)]
        [TypeConverter("Microsoft.Data.SqlClient.SqlConnectionStringBuilder+SqlDataSourceConverter, Microsoft.Data.SqlClient")]
        public string FailoverPartner
        {
            get => _failoverPartner;
            set
            {
                SetValue(DbConnectionStringKeywords.FailoverPartner, value);
                _failoverPartner = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/FailoverPartnerSPN/*' />
        [DisplayName(DbConnectionStringKeywords.FailoverPartnerSPN)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_FailoverPartnerSPN)]
        [RefreshProperties(RefreshProperties.All)]
        public string FailoverPartnerSPN
        {
            get => _failoverPartnerSPN;
            set
            {
                SetValue(DbConnectionStringKeywords.FailoverPartnerSPN, value);
                _failoverPartnerSPN = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/InitialCatalog/*' />
        [DisplayName(DbConnectionStringKeywords.InitialCatalog)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_InitialCatalog)]
        [RefreshProperties(RefreshProperties.All)]
        [TypeConverter(typeof(SqlInitialCatalogConverter))]
        public string InitialCatalog
        {
            get => _initialCatalog;
            set
            {
                SetValue(DbConnectionStringKeywords.InitialCatalog, value);
                _initialCatalog = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IntegratedSecurity/*' />
        [DisplayName(DbConnectionStringKeywords.IntegratedSecurity)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_IntegratedSecurity)]
        [RefreshProperties(RefreshProperties.All)]
        public bool IntegratedSecurity
        {
            get => _integratedSecurity;
            set
            {
                SetValue(DbConnectionStringKeywords.IntegratedSecurity, value);
                _integratedSecurity = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Authentication/*' />
        [DisplayName(DbConnectionStringKeywords.Authentication)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_Authentication)]
        [RefreshProperties(RefreshProperties.All)]
        public SqlAuthenticationMethod Authentication
        {
            get => _authentication;
            set
            {
                if (!DbConnectionStringUtilities.IsValidAuthenticationTypeValue(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlAuthenticationMethod), (int)value);
                }

                SetAuthenticationValue(value);
                _authentication = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/LoadBalanceTimeout/*' />
        [DisplayName(DbConnectionStringKeywords.LoadBalanceTimeout)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_LoadBalanceTimeout)]
        [RefreshProperties(RefreshProperties.All)]
        public int LoadBalanceTimeout
        {
            get => _loadBalanceTimeout;
            set
            {
                if (value < 0)
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.LoadBalanceTimeout);
                }
                SetValue(DbConnectionStringKeywords.LoadBalanceTimeout, value);
                _loadBalanceTimeout = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MaxPoolSize/*' />
        [DisplayName(DbConnectionStringKeywords.MaxPoolSize)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_MaxPoolSize)]
        [RefreshProperties(RefreshProperties.All)]
        public int MaxPoolSize
        {
            get => _maxPoolSize;
            set
            {
                if (value < 1)
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.MaxPoolSize);
                }
                SetValue(DbConnectionStringKeywords.MaxPoolSize, value);
                _maxPoolSize = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectRetryCount/*' />
        [DisplayName(DbConnectionStringKeywords.ConnectRetryCount)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_ConnectionResiliency)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ConnectRetryCount)]
        [RefreshProperties(RefreshProperties.All)]
        public int ConnectRetryCount
        {
            get => _connectRetryCount;
            set
            {
                if ((value < 0) || (value > 255))
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ConnectRetryCount);
                }
                SetValue(DbConnectionStringKeywords.ConnectRetryCount, value);
                _connectRetryCount = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectRetryInterval/*' />
        [DisplayName(DbConnectionStringKeywords.ConnectRetryInterval)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_ConnectionResiliency)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ConnectRetryInterval)]
        [RefreshProperties(RefreshProperties.All)]
        public int ConnectRetryInterval
        {
            get => _connectRetryInterval;
            set
            {
                if ((value < 1) || (value > 60))
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.ConnectRetryInterval);
                }
                SetValue(DbConnectionStringKeywords.ConnectRetryInterval, value);
                _connectRetryInterval = value;
            }
        }


        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MinPoolSize/*' />
        [DisplayName(DbConnectionStringKeywords.MinPoolSize)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_MinPoolSize)]
        [RefreshProperties(RefreshProperties.All)]
        public int MinPoolSize
        {
            get => _minPoolSize;
            set
            {
                if (value < 0)
                {
                    throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.MinPoolSize);
                }
                SetValue(DbConnectionStringKeywords.MinPoolSize, value);
                _minPoolSize = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MultipleActiveResultSets/*' />
        [DisplayName(DbConnectionStringKeywords.MultipleActiveResultSets)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_MultipleActiveResultSets)]
        [RefreshProperties(RefreshProperties.All)]
        public bool MultipleActiveResultSets
        {
            get => _multipleActiveResultSets;
            set
            {
                SetValue(DbConnectionStringKeywords.MultipleActiveResultSets, value);
                _multipleActiveResultSets = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MultiSubnetFailover/*' />
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Justification = "Reviewed and Approved by UE")]
        [DisplayName(DbConnectionStringKeywords.MultiSubnetFailover)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_MultiSubnetFailover)]
        [RefreshProperties(RefreshProperties.All)]
        public bool MultiSubnetFailover
        {
            get => _multiSubnetFailover;
            set
            {
                SetValue(DbConnectionStringKeywords.MultiSubnetFailover, value);
                _multiSubnetFailover = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PacketSize/*' />
        [DisplayName(DbConnectionStringKeywords.PacketSize)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_PacketSize)]
        [RefreshProperties(RefreshProperties.All)]
        public int PacketSize
        {
            get => _packetSize;
            set
            {
                if ((value < TdsEnums.MIN_PACKET_SIZE) || (TdsEnums.MAX_PACKET_SIZE < value))
                {
                    throw SQL.InvalidPacketSizeValue();
                }
                SetValue(DbConnectionStringKeywords.PacketSize, value);
                _packetSize = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Password/*' />
        [DisplayName(DbConnectionStringKeywords.Password)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_Password)]
        [PasswordPropertyText(true)]
        [RefreshProperties(RefreshProperties.All)]
        public string Password
        {
            get => _password;
            set
            {
                SetValue(DbConnectionStringKeywords.Password, value);
                _password = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PersistSecurityInfo/*' />
        [DisplayName(DbConnectionStringKeywords.PersistSecurityInfo)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_PersistSecurityInfo)]
        [RefreshProperties(RefreshProperties.All)]
        public bool PersistSecurityInfo
        {
            get => _persistSecurityInfo;
            set
            {
                SetValue(DbConnectionStringKeywords.PersistSecurityInfo, value);
                _persistSecurityInfo = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PoolBlockingPeriod/*' />
        [DisplayName(DbConnectionStringKeywords.PoolBlockingPeriod)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_PoolBlockingPeriod)]
        [RefreshProperties(RefreshProperties.All)]
        public PoolBlockingPeriod PoolBlockingPeriod
        {
            get => _poolBlockingPeriod;
            set
            {
                if (!PoolBlockingUtilities.IsValidPoolBlockingPeriodValue(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(PoolBlockingPeriod), (int)value);
                }

                SetPoolBlockingPeriodValue(value);
                _poolBlockingPeriod = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Pooling/*' />
        [DisplayName(DbConnectionStringKeywords.Pooling)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_Pooling)]
        [RefreshProperties(RefreshProperties.All)]
        public bool Pooling
        {
            get => _pooling;
            set
            {
                SetValue(DbConnectionStringKeywords.Pooling, value);
                _pooling = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Replication/*' />
        [DisplayName(DbConnectionStringKeywords.Replication)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Replication)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_Replication)]
        [RefreshProperties(RefreshProperties.All)]
        public bool Replication
        {
            get => _replication;
            set
            {
                SetValue(DbConnectionStringKeywords.Replication, value);
                _replication = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TransactionBinding/*' />
        [DisplayName(DbConnectionStringKeywords.TransactionBinding)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_TransactionBinding)]
        [RefreshProperties(RefreshProperties.All)]
        public string TransactionBinding
        {
            get => _transactionBinding;
            set
            {
                SetValue(DbConnectionStringKeywords.TransactionBinding, value);
                _transactionBinding = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TypeSystemVersion/*' />
        [DisplayName(DbConnectionStringKeywords.TypeSystemVersion)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_TypeSystemVersion)]
        [RefreshProperties(RefreshProperties.All)]
        public string TypeSystemVersion
        {
            get => _typeSystemVersion;
            set
            {
                SetValue(DbConnectionStringKeywords.TypeSystemVersion, value);
                _typeSystemVersion = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/UserID/*' />
        [DisplayName(DbConnectionStringKeywords.UserID)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_UserID)]
        [RefreshProperties(RefreshProperties.All)]
        public string UserID
        {
            get => _userID;
            set
            {
                SetValue(DbConnectionStringKeywords.UserID, value);
                _userID = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/UserInstance/*' />
        [DisplayName(DbConnectionStringKeywords.UserInstance)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_UserInstance)]
        [RefreshProperties(RefreshProperties.All)]
        public bool UserInstance
        {
            get => _userInstance;
            set
            {
                SetValue(DbConnectionStringKeywords.UserInstance, value);
                _userInstance = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/WorkstationID/*' />
        [DisplayName(DbConnectionStringKeywords.WorkstationID)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Context)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_WorkstationID)]
        [RefreshProperties(RefreshProperties.All)]
        public string WorkstationID
        {
            get => _workstationID;
            set
            {
                SetValue(DbConnectionStringKeywords.WorkstationID, value);
                _workstationID = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IsFixedSize/*' />
        public override bool IsFixedSize => true;

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Keys/*' />
        public override ICollection Keys => new ReadOnlyCollection<string>(s_validKeywords);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Values/*' />
        public override ICollection Values
        {
            get
            {
                // written this way so if the ordering of Keywords & _validKeywords changes
                // this is one less place to maintain
                object[] values = new object[s_validKeywords.Length];
                for (int i = 0; i < values.Length; ++i)
                {
                    values[i] = GetAt((Keywords)i);
                }
                return new ReadOnlyCollection<object>(values);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Clear/*' />
        public override void Clear()
        {
            base.Clear();
            for (int i = 0; i < s_validKeywords.Length; ++i)
            {
                Reset((Keywords)i);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ContainsKey/*' />
        public override bool ContainsKey(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            return s_keywords.ContainsKey(keyword);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Remove/*' />
        public override bool Remove(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            if (s_keywords.TryGetValue(keyword, out Keywords index))
            {
                if (base.Remove(s_validKeywords[(int)index]))
                {
                    Reset(index);
                    return true;
                }
            }
            return false;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ShouldSerialize/*' />
        public override bool ShouldSerialize(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            return s_keywords.TryGetValue(keyword, out Keywords index) && base.ShouldSerialize(s_validKeywords[(int)index]);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TryGetValue/*' />
        public override bool TryGetValue(string keyword, out object value)
        {
            if (s_keywords.TryGetValue(keyword, out Keywords index))
            {
                value = GetAt(index);
                return true;
            }
            value = null;
            return false;
        }

#if NETFRAMEWORK
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectionReset/*' />
        [Browsable(false)]
        [DisplayName(DbConnectionStringKeywords.ConnectionReset)]
        [Obsolete("ConnectionReset has been deprecated. SqlConnection will ignore the 'connection reset' keyword and always reset the connection.")] // SQLPT 41700
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_ConnectionReset)]
        [RefreshProperties(RefreshProperties.All)]
        public bool ConnectionReset
        {
            get => _connectionReset;
            set
            {
                SetValue(DbConnectionStringKeywords.ConnectionReset, value);
                _connectionReset = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TransparentNetworkIPResolution/*' />
        [DisplayName(DbConnectionStringKeywords.TransparentNetworkIPResolution)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_TransparentNetworkIPResolution)]
        [RefreshProperties(RefreshProperties.All)]
        public bool TransparentNetworkIPResolution
        {
            get => _transparentNetworkIPResolution;
            set
            {
                SetValue(DbConnectionStringKeywords.TransparentNetworkIPResolution, value);
                _transparentNetworkIPResolution = value;
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/NetworkLibrary/*' />
        [DisplayName(DbConnectionStringKeywords.NetworkLibrary)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescription(StringsHelper.ResourceNames.DbConnectionString_NetworkLibrary)]
        [RefreshProperties(RefreshProperties.All)]
        [TypeConverter(typeof(NetworkLibraryConverter))]
        public string NetworkLibrary
        {
            get => _networkLibrary;
            set
            {
                if (value is not null)
                {
                    value = value.Trim().ToLower(CultureInfo.InvariantCulture) switch
                    {
                        SqlConnectionString.NETLIB.AppleTalk => SqlConnectionString.NETLIB.AppleTalk,
                        SqlConnectionString.NETLIB.BanyanVines => SqlConnectionString.NETLIB.BanyanVines,
                        SqlConnectionString.NETLIB.IPXSPX => SqlConnectionString.NETLIB.IPXSPX,
                        SqlConnectionString.NETLIB.Multiprotocol => SqlConnectionString.NETLIB.Multiprotocol,
                        SqlConnectionString.NETLIB.NamedPipes => SqlConnectionString.NETLIB.NamedPipes,
                        SqlConnectionString.NETLIB.SharedMemory => SqlConnectionString.NETLIB.SharedMemory,
                        SqlConnectionString.NETLIB.TCPIP => SqlConnectionString.NETLIB.TCPIP,
                        SqlConnectionString.NETLIB.VIA => SqlConnectionString.NETLIB.VIA,
                        _ => throw ADP.InvalidConnectionOptionValue(DbConnectionStringKeywords.NetworkLibrary),
                    };
                }
                SetValue(DbConnectionStringKeywords.NetworkLibrary, value);
                _networkLibrary = value;
            }
        }
#endif
        #endregion // Public APIs
    }
}
