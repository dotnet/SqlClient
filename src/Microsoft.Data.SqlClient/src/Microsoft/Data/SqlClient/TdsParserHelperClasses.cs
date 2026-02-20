// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Authentication;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.Common.ConnectionString;

namespace Microsoft.Data.SqlClient
{
    internal enum CallbackType
    {
        Read = 0,
        Write = 1
    }

    internal enum EncryptionOptions
    {
        OFF,
        ON,
        NOT_SUP,
        REQ,
        LOGIN,
        OPTIONS_MASK = 0x3f,
#if NETFRAMEWORK
        CTAIP = 0x40,
        CLIENT_CERT = 0x80,
#endif
    }

    internal enum PreLoginHandshakeStatus
    {
        Successful,
        InstanceFailure
    }

    internal enum PreLoginOptions
    {
        VERSION,
        ENCRYPT,
        INSTANCE,
        THREADID,
        MARS,
        TRACEID,
        FEDAUTHREQUIRED,
        NUMOPT,
        LASTOPT = 255
    }

    internal enum RunBehavior
    {
        UntilDone = 1, // 0001 binary
        ReturnImmediately = 2, // 0010 binary
        Clean = 5, // 0101 binary - Clean AND UntilDone
        Attention = 13  // 1101 binary - Clean AND UntilDone AND Attention
    }

    internal enum TdsParserState
    {
        Closed,
        OpenNotLoggedIn,
        OpenLoggedIn,
        Broken,
    }

    /// <summary>
    /// Class encapsulating the data to be sent to the server as part of Federated Authentication Feature Extension.
    /// </summary>
    internal class FederatedAuthenticationFeatureExtensionData
    {
        internal TdsEnums.FedAuthLibrary libraryType;
        internal bool fedAuthRequiredPreLoginResponse;
        internal SqlAuthenticationMethod authentication;
        internal byte[] accessToken;
    }

    internal class RoutingInfo
    {
        internal byte Protocol { get; private set; }
        internal ushort Port { get; private set; }
        internal string ServerName { get; private set; }

        /// <summary>
        /// The DatabaseName property is only used when routing via an EnhancedRouting ENVCHANGE token.
        /// It is not used when routing via the normal Routing ENVCHANGE token.
        /// </summary>
        internal string DatabaseName { get; private set; }

        internal RoutingInfo(byte protocol, ushort port, string serverName, string databaseName = null)
        {
            Protocol = protocol;
            Port = port;
            ServerName = serverName;
            DatabaseName = databaseName;
        }
    }

    internal sealed class SqlLogin
    {
        internal SqlAuthenticationMethod authentication = SqlAuthenticationMethod.NotSpecified;  // Authentication type
        internal int timeout;                                            // login timeout
        internal bool userInstance = false;                              // user instance
        internal string hostName = "";                                   // client machine name
        internal string userName = "";                                   // user id
        internal string password = "";                                   // password
        internal string applicationName = "";                            // application name
        internal string serverName = "";                                 // server name
        internal string language = "";                                   // initial language
        internal string database = "";                                   // initial database
        internal string attachDBFilename = "";                           // DB filename to be attached
        internal bool useReplication = false;                            // user login for replication
        internal string newPassword = "";                                // new password for reset password
        internal bool useSSPI = false;                                   // use integrated security
        internal int packetSize = DbConnectionStringDefaults.PacketSize; // packet size
        internal bool readOnlyIntent = false;                            // read-only intent
        internal SqlCredential credential;                               // user id and password in SecureString
        internal SecureString newSecurePassword;
    }

    internal sealed class SqlLoginAck
    {
        internal byte majorVersion;
        internal byte minorVersion;
        internal short buildNum;
        internal uint tdsVersion;
    }

    #nullable enable

    internal sealed class SqlFedAuthInfo
    {
        internal SqlFedAuthInfo(string spn, string stsurl)
        {
            Spn = spn;
            StsUrl = stsurl;
        }

        internal string Spn { get; }
        internal string StsUrl { get; }

        public override string ToString()
        {
            return $"SPN: {Spn}, STSURL: {StsUrl}";
        }
    }

    internal sealed class SqlFedAuthToken
    {
        internal SqlFedAuthToken(
            byte[] accessToken,
            long expirationFileTime)
        {
            AccessToken = accessToken;
            DataLen = (uint)AccessToken.Length;
            ExpirationFileTime = expirationFileTime;
        }

        /// <summary>
        /// Convert from a SqlAuthenticationToken.
        /// </summary>
        internal SqlFedAuthToken(SqlAuthenticationToken token)
        {
            AccessToken = Encoding.Unicode.GetBytes(token.AccessToken);
            DataLen = (uint)AccessToken.Length;
            ExpirationFileTime = token.ExpiresOn.ToFileTime();
        }

        internal byte[] AccessToken { get; }
        internal uint DataLen { get; }
        internal long ExpirationFileTime { get; }
    }

    #nullable disable

    internal sealed class _SqlMetaData : SqlMetaDataPriv
    {
        [Flags]
        private enum _SqlMetadataFlags : int
        {
            None = 0,

            Updatable = 1 << 0,
            UpdateableUnknown = 1 << 1,
            IsDifferentName = 1 << 2,
            IsKey = 1 << 3,
            IsHidden = 1 << 4,
            IsExpression = 1 << 5,
            IsIdentity = 1 << 6,
            IsColumnSet = 1 << 7,

            IsUpdatableMask = (Updatable | UpdateableUnknown) // two bit field (0 is read only, 1 is updatable, 2 is updatability unknown)
        }

        internal string column;
        internal string baseColumn;
        internal MultiPartTableName multiPartTableName;
        internal readonly int ordinal;
        internal byte tableNum;
        internal byte op;        // for altrow-columns only
        internal ushort operand; // for altrow-columns only
        private _SqlMetadataFlags flags;

        internal _SqlMetaData(int ordinal) : base()
        {
            this.ordinal = ordinal;
        }

        private bool HasFlag(_SqlMetadataFlags flag)
        {
            return (flags & flag) != 0;
        }

        internal string serverName
        {
            get
            {
                return multiPartTableName.ServerName;
            }
        }
        internal string catalogName
        {
            get
            {
                return multiPartTableName.CatalogName;
            }
        }
        internal string schemaName
        {
            get
            {
                return multiPartTableName.SchemaName;
            }
        }
        internal string tableName
        {
            get
            {
                return multiPartTableName.TableName;
            }
        }

        public byte Updatability
        {
            get => (byte)(flags & _SqlMetadataFlags.IsUpdatableMask);
            set => flags = (_SqlMetadataFlags)((value & (byte)_SqlMetadataFlags.IsUpdatableMask) | ((int)flags & ~(byte)_SqlMetadataFlags.IsUpdatableMask));
        }

        public bool IsReadOnly
        {
            get => !HasFlag(_SqlMetadataFlags.IsUpdatableMask);
        }

        public bool IsDifferentName
        {
            get => HasFlag(_SqlMetadataFlags.IsDifferentName);
            set => Set(_SqlMetadataFlags.IsDifferentName, value);
        }

        public bool IsKey
        {
            get => HasFlag(_SqlMetadataFlags.IsKey);
            set => Set(_SqlMetadataFlags.IsKey, value);
        }

        public bool IsHidden
        {
            get => HasFlag(_SqlMetadataFlags.IsHidden);
            set => Set(_SqlMetadataFlags.IsHidden, value);
        }

        public bool IsExpression
        {
            get => HasFlag(_SqlMetadataFlags.IsExpression);
            set => Set(_SqlMetadataFlags.IsExpression, value);
        }

        public bool IsIdentity
        {
            get => HasFlag(_SqlMetadataFlags.IsIdentity);
            set => Set(_SqlMetadataFlags.IsIdentity, value);
        }

        public bool IsColumnSet
        {
            get => HasFlag(_SqlMetadataFlags.IsColumnSet);
            set => Set(_SqlMetadataFlags.IsColumnSet, value);
        }

        private void Set(_SqlMetadataFlags flag, bool value)
        {
            flags = value ? flags | flag : flags & ~flag;
        }

        internal bool Is2008DateTimeType
        {
            get
            {
                return SqlDbType.Date == type || SqlDbType.Time == type || SqlDbType.DateTime2 == type || SqlDbType.DateTimeOffset == type;
            }
        }

        internal bool IsLargeUdt
        {
            get
            {
                return type == SqlDbType.Udt && length == int.MaxValue;
            }
        }

        public object Clone()
        {
            _SqlMetaData result = new _SqlMetaData(ordinal);
            result.CopyFrom(this);
            result.column = column;
            result.baseColumn = baseColumn;
            result.multiPartTableName = multiPartTableName;
            result.tableNum = tableNum;
            result.flags = flags;
            result.op = op;
            result.operand = operand;
            return result;
        }
    }

    internal sealed partial class _SqlMetaDataSet
    {
        internal ushort id;             // for altrow-columns only

        internal DataTable schemaTable;
        private readonly _SqlMetaData[] _metaDataArray;
        internal ReadOnlyCollection<DbColumn> dbColumnSchema;

        private int _hiddenColumnCount;
        private int[] _visibleColumnMap;

        internal _SqlMetaDataSet(int count)
        {
            _hiddenColumnCount = -1;
            _metaDataArray = new _SqlMetaData[count];
            for (int i = 0; i < _metaDataArray.Length; ++i)
            {
                _metaDataArray[i] = new _SqlMetaData(i);
            }
        }

        private _SqlMetaDataSet(_SqlMetaDataSet original)
        {
            id = original.id;
            _hiddenColumnCount = original._hiddenColumnCount;
            _visibleColumnMap = original._visibleColumnMap;
            dbColumnSchema = original.dbColumnSchema;
            schemaTable = original.schemaTable;

            if (original._metaDataArray == null)
            {
                _metaDataArray = null;
            }
            else
            {
                _metaDataArray = new _SqlMetaData[original._metaDataArray.Length];
                for (int idx = 0; idx < _metaDataArray.Length; idx++)
                {
                    _metaDataArray[idx] = (_SqlMetaData)original._metaDataArray[idx].Clone();
                }
            }
        }

        internal int Length
        {
            get
            {
                return _metaDataArray.Length;
            }
        }

        internal int VisibleColumnCount
        {
            get
            {
                if (_hiddenColumnCount == -1)
                {
                    SetupHiddenColumns();
                }
                return Length - _hiddenColumnCount;
            }
        }

        internal _SqlMetaData this[int index]
        {
            get
            {
                return _metaDataArray[index];
            }
            set
            {
                Debug.Assert(value == null, "used only by SqlBulkCopy");
                _metaDataArray[index] = value;
            }
        }

        public int GetVisibleColumnIndex(int index)
        {
            if (_hiddenColumnCount == -1)
            {
                SetupHiddenColumns();
            }
            if (_visibleColumnMap is null)
            {
                return index;
            }
            else
            {
                return _visibleColumnMap[index];
            }
        }

        public _SqlMetaDataSet Clone()
        {
            return new _SqlMetaDataSet(this);
        }

        private void SetupHiddenColumns()
        {
            int hiddenColumnCount = 0;
            for (int index = 0; index < Length; index++)
            {
                if (_metaDataArray[index].IsHidden)
                {
                    hiddenColumnCount += 1;
                }
            }

            if (hiddenColumnCount > 0)
            {
                int[] visibleColumnMap = new int[Length - hiddenColumnCount];
                int mapIndex = 0;
                for (int metaDataIndex = 0; metaDataIndex < Length; metaDataIndex++)
                {
                    if (!_metaDataArray[metaDataIndex].IsHidden)
                    {
                        visibleColumnMap[mapIndex] = metaDataIndex;
                        mapIndex += 1;
                    }
                }
                _visibleColumnMap = visibleColumnMap;
            }
            _hiddenColumnCount = hiddenColumnCount;
        }
    }

    internal sealed class _SqlMetaDataSetCollection
    {
        private readonly List<_SqlMetaDataSet> _altMetaDataSetArray;
        internal _SqlMetaDataSet metaDataSet;

        internal _SqlMetaDataSetCollection()
        {
            _altMetaDataSetArray = new List<_SqlMetaDataSet>();
        }

        internal void SetAltMetaData(_SqlMetaDataSet altMetaDataSet)
        {
            // If altmetadata with same id is found, override it rather than adding a new one
            int newId = altMetaDataSet.id;
            for (int i = 0; i < _altMetaDataSetArray.Count; i++)
            {
                if (_altMetaDataSetArray[i].id == newId)
                {
                    // override the existing metadata with the same id
                    _altMetaDataSetArray[i] = altMetaDataSet;
                    return;
                }
            }

            // if we did not find metadata to override, add as new
            _altMetaDataSetArray.Add(altMetaDataSet);
        }

        internal _SqlMetaDataSet GetAltMetaData(int id)
        {
            foreach (_SqlMetaDataSet altMetaDataSet in _altMetaDataSetArray)
            {
                if (altMetaDataSet.id == id)
                {
                    return altMetaDataSet;
                }
            }
            Debug.Fail("Can't match up altMetaDataSet with given id");
            return null;
        }

        public object Clone()
        {
            _SqlMetaDataSetCollection result = new _SqlMetaDataSetCollection() { metaDataSet = metaDataSet?.Clone() };

            foreach (_SqlMetaDataSet set in _altMetaDataSetArray)
            {
                result._altMetaDataSetArray.Add(set.Clone());
            }
            return result;
        }
    }

    internal partial class SqlMetaDataPriv
    {
        [Flags]
        private enum SqlMetaDataPrivFlags : byte
        {
            None = 0,
            IsNullable = 1 << 1,
            IsMultiValued = 1 << 2
        }

        internal SqlDbType type;    // SqlDbType enum value
        internal byte tdsType; // underlying tds type
        internal byte precision = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
        internal byte scale = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
        private SqlMetaDataPrivFlags flags;
        internal int length;
        internal SqlCollation collation;
        internal int codePage;
        internal Encoding encoding;

        internal MetaType metaType; // cached metaType
        public SqlMetaDataUdt udt;
        public SqlMetaDataXmlSchemaCollection xmlSchemaCollection;

        internal SqlMetaDataPriv()
        {
        }

        public bool IsNullable
        {
            get => HasFlag(SqlMetaDataPrivFlags.IsNullable);
            set => Set(SqlMetaDataPrivFlags.IsNullable, value);
        }

        public bool IsMultiValued
        {
            get => HasFlag(SqlMetaDataPrivFlags.IsMultiValued);
            set => Set(SqlMetaDataPrivFlags.IsMultiValued, value);
        }

        private bool HasFlag(SqlMetaDataPrivFlags flag)
        {
            return (flags & flag) != 0;
        }

        private void Set(SqlMetaDataPrivFlags flag, bool value)
        {
            flags = value ? flags | flag : flags & ~flag;
        }

        internal virtual void CopyFrom(SqlMetaDataPriv original)
        {
            this.type = original.type;
            this.tdsType = original.tdsType;
            this.precision = original.precision;
            this.scale = original.scale;
            this.length = original.length;
            this.collation = original.collation;
            this.codePage = original.codePage;
            this.encoding = original.encoding;
            this.metaType = original.metaType;
            this.flags = original.flags;

            if (original.udt != null)
            {
                udt = new SqlMetaDataUdt();
                udt.CopyFrom(original.udt);
            }

            if (original.xmlSchemaCollection != null)
            {
                xmlSchemaCollection = new SqlMetaDataXmlSchemaCollection();
                xmlSchemaCollection.CopyFrom(original.xmlSchemaCollection);
            }
        }
    }

    internal sealed class SqlMetaDataXmlSchemaCollection
    {
        internal string Database;
        internal string OwningSchema;
        internal string Name;

        public void CopyFrom(SqlMetaDataXmlSchemaCollection original)
        {
            if (original != null)
            {
                Database = original.Database;
                OwningSchema = original.OwningSchema;
                Name = original.Name;
            }
        }
    }

    internal sealed class SqlMetaDataUdt
    {
#if NET
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
        internal Type Type;
        internal string DatabaseName;
        internal string SchemaName;
        internal string TypeName;
        internal string AssemblyQualifiedName;

        public void CopyFrom(SqlMetaDataUdt original)
        {
            if (original != null)
            {
                Type = original.Type;
                DatabaseName = original.DatabaseName;
                SchemaName = original.SchemaName;
                TypeName = original.TypeName;
                AssemblyQualifiedName = original.AssemblyQualifiedName;
            }
        }
    }

    internal sealed class _SqlRPC
    {
        internal string rpcName;
        internal ushort ProcID;       // Used instead of name
        internal ushort options;

        internal SqlParameter[] systemParams;
        internal byte[] systemParamOptions;
        internal int systemParamCount;

        internal SqlParameterCollection userParams;
        internal long[] userParamMap;
        internal int userParamCount;

        internal int? recordsAffected;
        internal int cumulativeRecordsAffected;

        internal int errorsIndexStart;
        internal int errorsIndexEnd;
        internal SqlErrorCollection errors;

        internal int warningsIndexStart;
        internal int warningsIndexEnd;
        internal SqlErrorCollection warnings;

        internal bool needsFetchParameterEncryptionMetadata;

        internal SqlBatchCommand batchCommand;

        internal string GetCommandTextOrRpcName()
        {
            if (TdsEnums.RPC_PROCID_EXECUTESQL == ProcID)
            {
                // Param 0 is the actual sql executing
                return (string)systemParams[0].Value;
            }
            else
            {
                return rpcName;
            }
        }

        internal SqlParameter GetParameterByIndex(int index, out byte options)
        {
            SqlParameter retval;

            if (index < systemParamCount)
            {
                retval = systemParams[index];
                options = systemParamOptions[index];
            }
            else
            {
                long data = userParamMap[index - systemParamCount];
                int paramIndex = (int)(data & int.MaxValue);
                options = (byte)((data >> 32) & 0xFF);
                retval = userParams[paramIndex];
            }
            return retval;
        }
    }

    internal sealed class SqlReturnValue : SqlMetaDataPriv
    {
        // @TODO: Make auto properties (and rename to match)
        internal string parameter;
        internal readonly SqlBuffer value;
#if NETFRAMEWORK
        internal ushort parmIndex;      //2005 or later only
#endif

        internal SqlReturnValue() : base()
        {
            value = new SqlBuffer();
        }
    }

    internal struct MultiPartTableName
    {
        private string _multipartName;
        private string _serverName;
        private string _catalogName;
        private string _schemaName;
        private string _tableName;

        internal MultiPartTableName(string[] parts)
        {
            _multipartName = null;
            _serverName = parts[0];
            _catalogName = parts[1];
            _schemaName = parts[2];
            _tableName = parts[3];
        }

        internal MultiPartTableName(string multipartName)
        {
            _multipartName = multipartName;
            _serverName = null;
            _catalogName = null;
            _schemaName = null;
            _tableName = null;
        }

        internal string ServerName
        {
            get
            {
                ParseMultipartName();
                return _serverName;
            }
            set { _serverName = value; }
        }
        internal string CatalogName
        {
            get
            {
                ParseMultipartName();
                return _catalogName;
            }
            set { _catalogName = value; }
        }
        internal string SchemaName
        {
            get
            {
                ParseMultipartName();
                return _schemaName;
            }
            set { _schemaName = value; }
        }
        internal string TableName
        {
            get
            {
                ParseMultipartName();
                return _tableName;
            }
            set { _tableName = value; }
        }

        private void ParseMultipartName()
        {
            if (_multipartName != null)
            {
                string[] parts = MultipartIdentifier.ParseMultipartIdentifier(_multipartName, "[\"", "]\"", Strings.SQL_TDSParserTableName, false);
                _serverName = parts[0];
                _catalogName = parts[1];
                _schemaName = parts[2];
                _tableName = parts[3];
                _multipartName = null;
            }
        }

        internal static readonly MultiPartTableName Null = new(new string[] { null, null, null, null });
    }

    internal static class SslProtocolsHelper
    {
        private static string ToFriendlyName(this SslProtocols protocol)
        {
            string name;

            /* The SslProtocols.Tls13 is supported by netcoreapp3.1 and later
             * This driver does not support this version yet!
            if ((protocol & SslProtocols.Tls13) == SslProtocols.Tls13)
            {
                name = "TLS 1.3";
            }*/
#pragma warning disable CA5397 // Do not use deprecated SslProtocols values
#pragma warning disable CA5398 // Avoid hardcoded SslProtocols values
            if ((protocol & SslProtocols.Tls12) != SslProtocols.None)
            {
                name = "TLS 1.2";
            }
#if NET8_0_OR_GREATER
#pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
            else if ((protocol & SslProtocols.Tls11) != SslProtocols.None)
            {
                name = "TLS 1.1";
            }
            else if ((protocol & SslProtocols.Tls) != SslProtocols.None)
            {
                name = "TLS 1.0";
            }
#if NET8_0_OR_GREATER
#pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
#endif
#pragma warning disable CS0618 // Type or member is obsolete: SSL is deprecated
            else if ((protocol & SslProtocols.Ssl3) != SslProtocols.None)
            {
                name = "SSL 3.0";
            }
            else if ((protocol & SslProtocols.Ssl2) != SslProtocols.None)
#pragma warning restore CS0618 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
            {
                name = "SSL 2.0";
            }
#pragma warning restore CA5397 // Do not use deprecated SslProtocols values
#pragma warning restore CA5398 // Avoid hardcoded SslProtocols values
            else
            {
#if !NETFRAMEWORK
                name = protocol.ToString();
#else
                throw new ArgumentException(StringsHelper.GetString(StringsHelper.net_invalid_enum, "NativeProtocols"), "NativeProtocols");
#endif
            }

            return name;
        }

        /// <summary>
        /// check the negotiated secure protocol if it's under TLS 1.2
        /// </summary>
        /// <param name="protocol"></param>
        /// <returns>Localized warning message</returns>
        public static string GetProtocolWarning(this SslProtocols protocol)
        {
            string message = string.Empty;
#if NET8_0_OR_GREATER
#pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
#pragma warning disable CS0618 // Type or member is obsolete : SSL is deprecated
#pragma warning disable CA5397 // Do not use deprecated SslProtocols values
#pragma warning disable CA5398 // Avoid hardcoded SslProtocols values
            if ((protocol & (SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11)) != SslProtocols.None)
#pragma warning restore CA5398 // Avoid hardcoded SslProtocols values
#pragma warning restore CA5397 // Do not use deprecated SslProtocols values
#pragma warning restore CS0618 // Type or member is obsolete : SSL is deprecated
#if NET8_0_OR_GREATER
#pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
#endif
            {
#if !NETFRAMEWORK
                message = StringsHelper.Format(Strings.SEC_ProtocolWarning, protocol.ToFriendlyName());
#else
                message = StringsHelper.GetString(Strings.SEC_ProtocolWarning, protocol.ToFriendlyName());
#endif
            }
            return message;
        }
    }
}
