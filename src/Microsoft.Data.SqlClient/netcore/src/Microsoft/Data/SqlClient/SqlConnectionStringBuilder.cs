// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/SqlConnectionStringBuilder/*' />
    [DefaultPropertyAttribute(DbConnectionStringKeywords.DataSource)]
    public sealed partial class SqlConnectionStringBuilder : DbConnectionStringBuilder
    {
        private enum Keywords
        { // specific ordering for ConnectionString output construction
            // NamedConnection,
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

            // keep the count value last
            KeywordsCount
        }

        internal const int KeywordsCount = (int)Keywords.KeywordsCount;
        internal const int DeprecatedKeywordsCount = 3;

        private static readonly string[] s_validKeywords = CreateValidKeywords();
        private static readonly Dictionary<string, Keywords> s_keywords = CreateKeywordsDictionary();

        private ApplicationIntent _applicationIntent = DbConnectionStringDefaults.ApplicationIntent;
        private string _applicationName = DbConnectionStringDefaults.ApplicationName;
        private string _attachDBFilename = DbConnectionStringDefaults.AttachDBFilename;
        private string _currentLanguage = DbConnectionStringDefaults.CurrentLanguage;
        private string _dataSource = DbConnectionStringDefaults.DataSource;
        private string _failoverPartner = DbConnectionStringDefaults.FailoverPartner;
        private string _initialCatalog = DbConnectionStringDefaults.InitialCatalog;
        //      private string _namedConnection   = DbConnectionStringDefaults.NamedConnection;
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

        private bool _encrypt = DbConnectionStringDefaults.Encrypt;
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
            validKeywords[(int)Keywords.Enlist] = DbConnectionStringKeywords.Enlist;
            validKeywords[(int)Keywords.FailoverPartner] = DbConnectionStringKeywords.FailoverPartner;
            validKeywords[(int)Keywords.InitialCatalog] = DbConnectionStringKeywords.InitialCatalog;
            validKeywords[(int)Keywords.IntegratedSecurity] = DbConnectionStringKeywords.IntegratedSecurity;
            validKeywords[(int)Keywords.LoadBalanceTimeout] = DbConnectionStringKeywords.LoadBalanceTimeout;
            validKeywords[(int)Keywords.MaxPoolSize] = DbConnectionStringKeywords.MaxPoolSize;
            validKeywords[(int)Keywords.MinPoolSize] = DbConnectionStringKeywords.MinPoolSize;
            validKeywords[(int)Keywords.MultipleActiveResultSets] = DbConnectionStringKeywords.MultipleActiveResultSets;
            validKeywords[(int)Keywords.MultiSubnetFailover] = DbConnectionStringKeywords.MultiSubnetFailover;
            //          validKeywords[(int)Keywords.NamedConnection]          = DbConnectionStringKeywords.NamedConnection;
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
            return validKeywords;
        }

        private static Dictionary<string, Keywords> CreateKeywordsDictionary()
        {
            Dictionary<string, Keywords> hash = new Dictionary<string, Keywords>(KeywordsCount + SqlConnectionString.SynonymCount, StringComparer.OrdinalIgnoreCase);
            hash.Add(DbConnectionStringKeywords.ApplicationIntent, Keywords.ApplicationIntent);
            hash.Add(DbConnectionStringKeywords.ApplicationName, Keywords.ApplicationName);
            hash.Add(DbConnectionStringKeywords.AttachDBFilename, Keywords.AttachDBFilename);
            hash.Add(DbConnectionStringKeywords.PoolBlockingPeriod, Keywords.PoolBlockingPeriod);
            hash.Add(DbConnectionStringKeywords.CommandTimeout, Keywords.CommandTimeout);
            hash.Add(DbConnectionStringKeywords.ConnectTimeout, Keywords.ConnectTimeout);
            hash.Add(DbConnectionStringKeywords.CurrentLanguage, Keywords.CurrentLanguage);
            hash.Add(DbConnectionStringKeywords.DataSource, Keywords.DataSource);
            hash.Add(DbConnectionStringKeywords.Encrypt, Keywords.Encrypt);
            hash.Add(DbConnectionStringKeywords.Enlist, Keywords.Enlist);
            hash.Add(DbConnectionStringKeywords.FailoverPartner, Keywords.FailoverPartner);
            hash.Add(DbConnectionStringKeywords.InitialCatalog, Keywords.InitialCatalog);
            hash.Add(DbConnectionStringKeywords.IntegratedSecurity, Keywords.IntegratedSecurity);
            hash.Add(DbConnectionStringKeywords.LoadBalanceTimeout, Keywords.LoadBalanceTimeout);
            hash.Add(DbConnectionStringKeywords.MultipleActiveResultSets, Keywords.MultipleActiveResultSets);
            hash.Add(DbConnectionStringKeywords.MaxPoolSize, Keywords.MaxPoolSize);
            hash.Add(DbConnectionStringKeywords.MinPoolSize, Keywords.MinPoolSize);
            hash.Add(DbConnectionStringKeywords.MultiSubnetFailover, Keywords.MultiSubnetFailover);
            //          hash.Add(DbConnectionStringKeywords.NamedConnection,          Keywords.NamedConnection);
            hash.Add(DbConnectionStringKeywords.PacketSize, Keywords.PacketSize);
            hash.Add(DbConnectionStringKeywords.Password, Keywords.Password);
            hash.Add(DbConnectionStringKeywords.PersistSecurityInfo, Keywords.PersistSecurityInfo);
            hash.Add(DbConnectionStringKeywords.Pooling, Keywords.Pooling);
            hash.Add(DbConnectionStringKeywords.Replication, Keywords.Replication);
            hash.Add(DbConnectionStringKeywords.TransactionBinding, Keywords.TransactionBinding);
            hash.Add(DbConnectionStringKeywords.TrustServerCertificate, Keywords.TrustServerCertificate);
            hash.Add(DbConnectionStringKeywords.TypeSystemVersion, Keywords.TypeSystemVersion);
            hash.Add(DbConnectionStringKeywords.UserID, Keywords.UserID);
            hash.Add(DbConnectionStringKeywords.UserInstance, Keywords.UserInstance);
            hash.Add(DbConnectionStringKeywords.WorkstationID, Keywords.WorkstationID);
            hash.Add(DbConnectionStringKeywords.ConnectRetryCount, Keywords.ConnectRetryCount);
            hash.Add(DbConnectionStringKeywords.ConnectRetryInterval, Keywords.ConnectRetryInterval);
            hash.Add(DbConnectionStringKeywords.Authentication, Keywords.Authentication);
            hash.Add(DbConnectionStringKeywords.ColumnEncryptionSetting, Keywords.ColumnEncryptionSetting);
            hash.Add(DbConnectionStringKeywords.EnclaveAttestationUrl, Keywords.EnclaveAttestationUrl);
            hash.Add(DbConnectionStringKeywords.AttestationProtocol, Keywords.AttestationProtocol);
            hash.Add(DbConnectionStringKeywords.IPAddressPreference, Keywords.IPAddressPreference);

            hash.Add(DbConnectionStringSynonyms.IPADDRESSPREFERENCE, Keywords.IPAddressPreference);
            hash.Add(DbConnectionStringSynonyms.APP, Keywords.ApplicationName);
            hash.Add(DbConnectionStringSynonyms.APPLICATIONINTENT, Keywords.ApplicationIntent);
            hash.Add(DbConnectionStringSynonyms.EXTENDEDPROPERTIES, Keywords.AttachDBFilename);
            hash.Add(DbConnectionStringSynonyms.INITIALFILENAME, Keywords.AttachDBFilename);
            hash.Add(DbConnectionStringSynonyms.CONNECTIONTIMEOUT, Keywords.ConnectTimeout);
            hash.Add(DbConnectionStringSynonyms.CONNECTRETRYCOUNT, Keywords.ConnectRetryCount);
            hash.Add(DbConnectionStringSynonyms.CONNECTRETRYINTERVAL, Keywords.ConnectRetryInterval);
            hash.Add(DbConnectionStringSynonyms.TIMEOUT, Keywords.ConnectTimeout);
            hash.Add(DbConnectionStringSynonyms.LANGUAGE, Keywords.CurrentLanguage);
            hash.Add(DbConnectionStringSynonyms.ADDR, Keywords.DataSource);
            hash.Add(DbConnectionStringSynonyms.ADDRESS, Keywords.DataSource);
            hash.Add(DbConnectionStringSynonyms.MULTIPLEACTIVERESULTSETS, Keywords.MultipleActiveResultSets);
            hash.Add(DbConnectionStringSynonyms.MULTISUBNETFAILOVER, Keywords.MultiSubnetFailover);
            hash.Add(DbConnectionStringSynonyms.NETWORKADDRESS, Keywords.DataSource);
            hash.Add(DbConnectionStringSynonyms.POOLBLOCKINGPERIOD, Keywords.PoolBlockingPeriod);
            hash.Add(DbConnectionStringSynonyms.SERVER, Keywords.DataSource);
            hash.Add(DbConnectionStringSynonyms.DATABASE, Keywords.InitialCatalog);
            hash.Add(DbConnectionStringSynonyms.TRUSTEDCONNECTION, Keywords.IntegratedSecurity);
            hash.Add(DbConnectionStringSynonyms.TRUSTSERVERCERTIFICATE, Keywords.TrustServerCertificate);
            hash.Add(DbConnectionStringSynonyms.ConnectionLifetime, Keywords.LoadBalanceTimeout);
            hash.Add(DbConnectionStringSynonyms.Pwd, Keywords.Password);
            hash.Add(DbConnectionStringSynonyms.PERSISTSECURITYINFO, Keywords.PersistSecurityInfo);
            hash.Add(DbConnectionStringSynonyms.UID, Keywords.UserID);
            hash.Add(DbConnectionStringSynonyms.User, Keywords.UserID);
            hash.Add(DbConnectionStringSynonyms.WSID, Keywords.WorkstationID);
            Debug.Assert((KeywordsCount + SqlConnectionString.SynonymCount) == hash.Count, "initial expected size is incorrect");
            return hash;
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ctor2/*' />
        public SqlConnectionStringBuilder() : this((string)null)
        {
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ctorConnectionString/*' />
        public SqlConnectionStringBuilder(string connectionString) : base()
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                ConnectionString = connectionString;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Item/*' />
        public override object this[string keyword]
        {
            get
            {
                Keywords index = GetIndex(keyword);
                return GetAt(index);
            }
            set
            {
                if (null != value)
                {
                    Keywords index = GetIndex(keyword);
                    switch (index)
                    {
                        case Keywords.ApplicationIntent:
                            this.ApplicationIntent = ConvertToApplicationIntent(keyword, value);
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
                        //                  case Keywords.NamedConnection:          NamedConnection = ConvertToString(value); break;
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
                            Encrypt = ConvertToBoolean(value);
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ApplicationIntent/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.ApplicationIntent)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_ApplicationIntent)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public ApplicationIntent ApplicationIntent
        {
            get { return _applicationIntent; }
            set
            {
                if (!DbConnectionStringBuilderUtil.IsValidApplicationIntentValue(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(ApplicationIntent), (int)value);
                }

                SetApplicationIntentValue(value);
                _applicationIntent = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ApplicationName/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.ApplicationName)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Context)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_ApplicationName)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string ApplicationName
        {
            get { return _applicationName; }
            set
            {
                SetValue(DbConnectionStringKeywords.ApplicationName, value);
                _applicationName = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/AttachDBFilename/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.AttachDBFilename)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_AttachDBFilename)]
        [EditorAttribute("System.Windows.Forms.Design.FileNameEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string AttachDBFilename
        {
            get { return _attachDBFilename; }
            set
            {
                SetValue(DbConnectionStringKeywords.AttachDBFilename, value);
                _attachDBFilename = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/CommandTimeout/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.CommandTimeout)]
        [ResCategory(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescription(StringsHelper.ResourceNames.DbCommand_CommandTimeout)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int CommandTimeout
        {
            get { return _commandTimeout; }
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectTimeout/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.ConnectTimeout)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_ConnectTimeout)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int ConnectTimeout
        {
            get { return _connectTimeout; }
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/CurrentLanguage/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.CurrentLanguage)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Initialization)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_CurrentLanguage)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string CurrentLanguage
        {
            get { return _currentLanguage; }
            set
            {
                SetValue(DbConnectionStringKeywords.CurrentLanguage, value);
                _currentLanguage = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/DataSource/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.DataSource)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_DataSource)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string DataSource
        {
            get { return _dataSource; }
            set
            {
                SetValue(DbConnectionStringKeywords.DataSource, value);
                _dataSource = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Encrypt/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.Encrypt)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_Encrypt)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool Encrypt
        {
            get { return _encrypt; }
            set
            {
                SetValue(DbConnectionStringKeywords.Encrypt, value);
                _encrypt = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ColumnEncryptionSetting/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.ColumnEncryptionSetting)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.TCE_DbConnectionString_ColumnEncryptionSetting)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public SqlConnectionColumnEncryptionSetting ColumnEncryptionSetting
        {
            get { return _columnEncryptionSetting; }
            set
            {
                if (!DbConnectionStringBuilderUtil.IsValidColumnEncryptionSetting(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionColumnEncryptionSetting), (int)value);
                }

                SetColumnEncryptionSettingValue(value);
                _columnEncryptionSetting = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/EnclaveAttestationUrl/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.EnclaveAttestationUrl)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.TCE_DbConnectionString_EnclaveAttestationUrl)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string EnclaveAttestationUrl
        {
            get { return _enclaveAttestationUrl; }
            set
            {
                SetValue(DbConnectionStringKeywords.EnclaveAttestationUrl, value);
                _enclaveAttestationUrl = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/AttestationProtocol/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.AttestationProtocol)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.TCE_DbConnectionString_AttestationProtocol)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public SqlConnectionAttestationProtocol AttestationProtocol
        {
            get { return _attestationProtocol; }
            set
            {
                if (!DbConnectionStringBuilderUtil.IsValidAttestationProtocol(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionAttestationProtocol), (int)value);
                }

                SetAttestationProtocolValue(value);
                _attestationProtocol = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IPAddressPreference/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.IPAddressPreference)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.TCE_DbConnectionString_IPAddressPreference)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public SqlConnectionIPAddressPreference IPAddressPreference
        {
            get => _ipAddressPreference;
            set
            {
                if (!DbConnectionStringBuilderUtil.IsValidIPAddressPreference(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlConnectionIPAddressPreference), (int)value);
                }

                SetIPAddressPreferenceValue(value);
                _ipAddressPreference = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TrustServerCertificate/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.TrustServerCertificate)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_TrustServerCertificate)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool TrustServerCertificate
        {
            get { return _trustServerCertificate; }
            set
            {
                SetValue(DbConnectionStringKeywords.TrustServerCertificate, value);
                _trustServerCertificate = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Enlist/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.Enlist)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_Enlist)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool Enlist
        {
            get { return _enlist; }
            set
            {
                SetValue(DbConnectionStringKeywords.Enlist, value);
                _enlist = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/FailoverPartner/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.FailoverPartner)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_FailoverPartner)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string FailoverPartner
        {
            get { return _failoverPartner; }
            set
            {
                SetValue(DbConnectionStringKeywords.FailoverPartner, value);
                _failoverPartner = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/InitialCatalog/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.InitialCatalog)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_InitialCatalog)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        [TypeConverter(typeof(SqlInitialCatalogConverter))]
        public string InitialCatalog
        {
            get { return _initialCatalog; }
            set
            {
                SetValue(DbConnectionStringKeywords.InitialCatalog, value);
                _initialCatalog = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/IntegratedSecurity/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.IntegratedSecurity)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_IntegratedSecurity)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool IntegratedSecurity
        {
            get { return _integratedSecurity; }
            set
            {
                SetValue(DbConnectionStringKeywords.IntegratedSecurity, value);
                _integratedSecurity = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Authentication/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.Authentication)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_Authentication)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public SqlAuthenticationMethod Authentication
        {
            get { return _authentication; }
            set
            {
                if (!DbConnectionStringBuilderUtil.IsValidAuthenticationTypeValue(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(SqlAuthenticationMethod), (int)value);
                }

                SetAuthenticationValue(value);
                _authentication = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/LoadBalanceTimeout/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.LoadBalanceTimeout)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_LoadBalanceTimeout)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int LoadBalanceTimeout
        {
            get { return _loadBalanceTimeout; }
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MaxPoolSize/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.MaxPoolSize)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_MaxPoolSize)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int MaxPoolSize
        {
            get { return _maxPoolSize; }
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectRetryCount/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.ConnectRetryCount)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_ConnectionResilency)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_ConnectRetryCount)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int ConnectRetryCount
        {
            get { return _connectRetryCount; }
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ConnectRetryInterval/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.ConnectRetryInterval)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_ConnectionResilency)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_ConnectRetryInterval)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int ConnectRetryInterval
        {
            get { return _connectRetryInterval; }
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


        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MinPoolSize/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.MinPoolSize)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_MinPoolSize)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int MinPoolSize
        {
            get { return _minPoolSize; }
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MultipleActiveResultSets/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.MultipleActiveResultSets)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_MultipleActiveResultSets)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool MultipleActiveResultSets
        {
            get { return _multipleActiveResultSets; }
            set
            {
                SetValue(DbConnectionStringKeywords.MultipleActiveResultSets, value);
                _multipleActiveResultSets = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/MultiSubnetFailover/*' />
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Justification = "Reviewed and Approved by UE")]
        [DisplayNameAttribute(DbConnectionStringKeywords.MultiSubnetFailover)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_MultiSubnetFailover)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool MultiSubnetFailover
        {
            get { return _multiSubnetFailover; }
            set
            {
                SetValue(DbConnectionStringKeywords.MultiSubnetFailover, value);
                _multiSubnetFailover = value;
            }
        }
        /*
                [DisplayName(DbConnectionStringKeywords.NamedConnection)]
                [ResCategoryAttribute(Strings.DataCategory_NamedConnectionString)]
                [ResDescriptionAttribute(Strings.DbConnectionString_NamedConnection)]
                [RefreshPropertiesAttribute(RefreshProperties.All)]
                [TypeConverter(typeof(NamedConnectionStringConverter))]
                public string NamedConnection {
                    get { return _namedConnection; }
                    set {
                        SetValue(DbConnectionStringKeywords.NamedConnection, value);
                        _namedConnection = value;
                    }
                }
        */
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PacketSize/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.PacketSize)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_PacketSize)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public int PacketSize
        {
            get { return _packetSize; }
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

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Password/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.Password)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_Password)]
        [PasswordPropertyTextAttribute(true)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string Password
        {
            get { return _password; }
            set
            {
                SetValue(DbConnectionStringKeywords.Password, value);
                _password = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PersistSecurityInfo/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.PersistSecurityInfo)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_PersistSecurityInfo)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool PersistSecurityInfo
        {
            get { return _persistSecurityInfo; }
            set
            {
                SetValue(DbConnectionStringKeywords.PersistSecurityInfo, value);
                _persistSecurityInfo = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/PoolBlockingPeriod/*' />
        [DisplayName(DbConnectionStringKeywords.PoolBlockingPeriod)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_PoolBlockingPeriod)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public PoolBlockingPeriod PoolBlockingPeriod
        {
            get { return _poolBlockingPeriod; }
            set
            {
                if (!DbConnectionStringBuilderUtil.IsValidPoolBlockingPeriodValue(value))
                {
                    throw ADP.InvalidEnumerationValue(typeof(PoolBlockingPeriod), (int)value);
                }

                SetPoolBlockingPeriodValue(value);
                _poolBlockingPeriod = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Pooling/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.Pooling)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Pooling)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_Pooling)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool Pooling
        {
            get { return _pooling; }
            set
            {
                SetValue(DbConnectionStringKeywords.Pooling, value);
                _pooling = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Replication/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.Replication)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Replication)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_Replication)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool Replication
        {
            get { return _replication; }
            set
            {
                SetValue(DbConnectionStringKeywords.Replication, value);
                _replication = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TransactionBinding/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.TransactionBinding)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_TransactionBinding)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string TransactionBinding
        {
            get { return _transactionBinding; }
            set
            {
                SetValue(DbConnectionStringKeywords.TransactionBinding, value);
                _transactionBinding = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TypeSystemVersion/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.TypeSystemVersion)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Advanced)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_TypeSystemVersion)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string TypeSystemVersion
        {
            get { return _typeSystemVersion; }
            set
            {
                SetValue(DbConnectionStringKeywords.TypeSystemVersion, value);
                _typeSystemVersion = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/UserID/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.UserID)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Security)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_UserID)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string UserID
        {
            get { return _userID; }
            set
            {
                SetValue(DbConnectionStringKeywords.UserID, value);
                _userID = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/UserInstance/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.UserInstance)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Source)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_UserInstance)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public bool UserInstance
        {
            get { return _userInstance; }
            set
            {
                SetValue(DbConnectionStringKeywords.UserInstance, value);
                _userInstance = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/WorkstationID/*' />
        [DisplayNameAttribute(DbConnectionStringKeywords.WorkstationID)]
        [ResCategoryAttribute(StringsHelper.ResourceNames.DataCategory_Context)]
        [ResDescriptionAttribute(StringsHelper.ResourceNames.DbConnectionString_WorkstationID)]
        [RefreshPropertiesAttribute(RefreshProperties.All)]
        public string WorkstationID
        {
            get { return _workstationID; }
            set
            {
                SetValue(DbConnectionStringKeywords.WorkstationID, value);
                _workstationID = value;
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Keys/*' />
        public override ICollection Keys
        {
            get
            {
                return new System.Collections.ObjectModel.ReadOnlyCollection<string>(s_validKeywords);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Values/*' />
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
                return new System.Collections.ObjectModel.ReadOnlyCollection<object>(values);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Clear/*' />
        public override void Clear()
        {
            base.Clear();
            for (int i = 0; i < s_validKeywords.Length; ++i)
            {
                Reset((Keywords)i);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ContainsKey/*' />
        public override bool ContainsKey(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            return s_keywords.ContainsKey(keyword);
        }

        private static bool ConvertToBoolean(object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToBoolean(value);
        }
        private static int ConvertToInt32(object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToInt32(value);
        }
        private static bool ConvertToIntegratedSecurity(object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToIntegratedSecurity(value);
        }
        private static SqlAuthenticationMethod ConvertToAuthenticationType(string keyword, object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToAuthenticationType(keyword, value);
        }
        private static string ConvertToString(object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToString(value);
        }
        private static ApplicationIntent ConvertToApplicationIntent(string keyword, object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToApplicationIntent(keyword, value);
        }

        /// <summary>
        /// Convert to SqlConnectionColumnEncryptionSetting.
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="value"></param>
        private static SqlConnectionColumnEncryptionSetting ConvertToColumnEncryptionSetting(string keyword, object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToColumnEncryptionSetting(keyword, value);
        }

        /// <summary>
        /// Convert to SqlConnectionAttestationProtocol
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="value"></param>
        private static SqlConnectionAttestationProtocol ConvertToAttestationProtocol(string keyword, object value)
        {
            return DbConnectionStringBuilderUtil.ConvertToAttestationProtocol(keyword, value);
        }

        /// <summary>
        /// Convert to SqlConnectionIPAddressPreference
        /// </summary>
        /// <param name="keyword"></param>
        /// <param name="value"></param>
        private static SqlConnectionIPAddressPreference ConvertToIPAddressPreference(string keyword, object value)
            => DbConnectionStringBuilderUtil.ConvertToIPAddressPreference(keyword, value);

        private static PoolBlockingPeriod ConvertToPoolBlockingPeriod(string keyword, object value)
            => DbConnectionStringBuilderUtil.ConvertToPoolBlockingPeriod(keyword, value);

        private object GetAt(Keywords index)
        {
            switch (index)
            {
                case Keywords.ApplicationIntent:
                    return this.ApplicationIntent;
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
                //          case Keywords.NamedConnection:          return NamedConnection;
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
                default:
                    Debug.Fail("unexpected keyword");
                    throw UnsupportedKeyword(s_validKeywords[(int)index]);
            }
        }

        private Keywords GetIndex(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            Keywords index;
            if (s_keywords.TryGetValue(keyword, out index))
            {
                return index;
            }
            throw UnsupportedKeyword(keyword);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/Remove/*' />
        public override bool Remove(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            Keywords index;
            if (s_keywords.TryGetValue(keyword, out index))
            {
                if (base.Remove(s_validKeywords[(int)index]))
                {
                    Reset(index);
                    return true;
                }
            }
            return false;
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
                //          case Keywords.NamedConnection:
                //              _namedConnection = DbConnectionStringDefaults.NamedConnection;
                //              break;
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
                default:
                    Debug.Fail("unexpected keyword");
                    throw UnsupportedKeyword(s_validKeywords[(int)index]);
            }
        }

        private void SetValue(string keyword, bool value)
        {
            base[keyword] = value.ToString();
        }
        private void SetValue(string keyword, int value)
        {
            base[keyword] = value.ToString((System.IFormatProvider)null);
        }
        private void SetValue(string keyword, string value)
        {
            ADP.CheckArgumentNull(value, keyword);
            base[keyword] = value;
        }
        private void SetApplicationIntentValue(ApplicationIntent value)
        {
            Debug.Assert(DbConnectionStringBuilderUtil.IsValidApplicationIntentValue(value), "invalid value");
            base[DbConnectionStringKeywords.ApplicationIntent] = DbConnectionStringBuilderUtil.ApplicationIntentToString(value);
        }
        private void SetColumnEncryptionSettingValue(SqlConnectionColumnEncryptionSetting value)
        {
            Debug.Assert(DbConnectionStringBuilderUtil.IsValidColumnEncryptionSetting(value), "Invalid value for SqlConnectionColumnEncryptionSetting");
            base[DbConnectionStringKeywords.ColumnEncryptionSetting] = DbConnectionStringBuilderUtil.ColumnEncryptionSettingToString(value);
        }

        private void SetAttestationProtocolValue(SqlConnectionAttestationProtocol value)
        {
            Debug.Assert(DbConnectionStringBuilderUtil.IsValidAttestationProtocol(value), "Invalid value for SqlConnectionAttestationProtocol");
            base[DbConnectionStringKeywords.AttestationProtocol] = DbConnectionStringBuilderUtil.AttestationProtocolToString(value);
        }

        private void SetIPAddressPreferenceValue(SqlConnectionIPAddressPreference value)
        {
            Debug.Assert(DbConnectionStringBuilderUtil.IsValidIPAddressPreference(value), "Invalid value for SqlConnectionIPAddressPreference");
            base[DbConnectionStringKeywords.IPAddressPreference] = DbConnectionStringBuilderUtil.IPAddressPreferenceToString(value);
        }

        private void SetAuthenticationValue(SqlAuthenticationMethod value)
        {
            Debug.Assert(DbConnectionStringBuilderUtil.IsValidAuthenticationTypeValue(value), "Invalid value for AuthenticationType");
            base[DbConnectionStringKeywords.Authentication] = DbConnectionStringBuilderUtil.AuthenticationTypeToString(value);
        }

        private void SetPoolBlockingPeriodValue(PoolBlockingPeriod value)
        {
            Debug.Assert(DbConnectionStringBuilderUtil.IsValidPoolBlockingPeriodValue(value), "Invalid value for PoolBlockingPeriod");
            base[DbConnectionStringKeywords.PoolBlockingPeriod] = DbConnectionStringBuilderUtil.PoolBlockingPeriodToString(value);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/ShouldSerialize/*' />
        public override bool ShouldSerialize(string keyword)
        {
            ADP.CheckArgumentNull(keyword, nameof(keyword));
            Keywords index;
            return s_keywords.TryGetValue(keyword, out index) && base.ShouldSerialize(s_validKeywords[(int)index]);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionStringBuilder.xml' path='docs/members[@name="SqlConnectionStringBuilder"]/TryGetValue/*' />
        public override bool TryGetValue(string keyword, out object value)
        {
            Keywords index;
            if (s_keywords.TryGetValue(keyword, out index))
            {
                value = GetAt(index);
                return true;
            }
            value = null;
            return false;
        }

        private static readonly string[] s_notSupportedKeywords = new string[] {
            DbConnectionStringKeywords.ConnectionReset,
            DbConnectionStringKeywords.ContextConnection,
            DbConnectionStringKeywords.TransactionBinding,
        };

        private static readonly string[] s_notSupportedNetworkLibraryKeywords = new string[] {
            DbConnectionStringKeywords.NetworkLibrary,

            DbConnectionStringSynonyms.NET,
            DbConnectionStringSynonyms.NETWORK
        };

        private Exception UnsupportedKeyword(string keyword)
        {
            if (s_notSupportedKeywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
            {
                return SQL.UnsupportedKeyword(keyword);
            }
            else if (s_notSupportedNetworkLibraryKeywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
            {
                return SQL.NetworkLibraryKeywordNotSupported();
            }
            else
            {
                return ADP.KeywordNotSupported(keyword);
            }
        }

        private sealed class SqlInitialCatalogConverter : StringConverter
        {
            // converter classes should have public ctor
            public SqlInitialCatalogConverter()
            {
            }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return GetStandardValuesSupportedInternal(context);
            }

            private bool GetStandardValuesSupportedInternal(ITypeDescriptorContext context)
            {
                // Only say standard values are supported if the connection string has enough
                // information set to instantiate a connection and retrieve a list of databases
                bool flag = false;
                if (null != context)
                {
                    SqlConnectionStringBuilder constr = (context.Instance as SqlConnectionStringBuilder);
                    if (null != constr)
                    {
                        if ((0 < constr.DataSource.Length) && (constr.IntegratedSecurity || (0 < constr.UserID.Length)))
                        {
                            flag = true;
                        }
                    }
                }
                return flag;
            }

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
            {
                // Although theoretically this could be true, some people may want to just type in a name
                return false;
            }

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
    }
}
