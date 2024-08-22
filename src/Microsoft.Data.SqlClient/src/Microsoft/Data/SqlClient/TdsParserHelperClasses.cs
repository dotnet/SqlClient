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
using System.Globalization;
using System.Security;
using System.Security.Authentication;
using System.Text;
using Microsoft.Data.Common;

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
#if NETFRAMEWORK
        OPTIONS_MASK = 0x3f,
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
    internal sealed class FederatedAuthenticationFeatureExtensionData
    {
        public TdsEnums.FedAuthLibrary LibraryType;
        public bool FedAuthRequiredPreLoginResponse;
        public SqlAuthenticationMethod Authentication;
        public byte[] AccessToken;
    }

    internal sealed class RoutingInfo
    {
        internal byte Protocol { get; private set; }
        internal ushort Port { get; private set; }
        internal string ServerName { get; private set; }

        internal RoutingInfo(byte protocol, ushort port, string servername)
        {
            Protocol = protocol;
            Port = port;
            ServerName = servername;
        }
    }

    internal sealed class SqlLogin
    {
        public SqlAuthenticationMethod Authentication = SqlAuthenticationMethod.NotSpecified;  // Authentication type
        public int Timeout;                                                       // login timeout
        public bool UserInstance = false;                                   // user instance
        public string HostName = "";                                      // client machine name
        public string UserName = "";                                      // user id
        public string Password = "";                                      // password
        public string ApplicationName = "";                                      // application name
        public string ServerName = "";                                      // server name
        public string Language = "";                                      // initial language
        public string Database = "";                                      // initial database
        public string AttachDbFilename = "";                                      // DB filename to be attached
        public bool UseReplication = false;                                   // user login for replication
        public string NewPassword = "";                                   // new password for reset password
        public bool UseSspi = false;                                   // use integrated security
        public int PacketSize = SqlConnectionString.DEFAULT.Packet_Size; // packet size
        public bool ReadOnlyIntent = false;                                   // read-only intent
        public SqlCredential Credential;                                      // user id and password in SecureString
        public SecureString NewSecurePassword;
    }

    internal sealed class SqlLoginAck
    {
        public byte MajorVersion;
        public byte MinorVersion;
        public short BuildNum;
        public uint TdsVersion;
#if NETFRAMEWORK
        public string ProgramName;

        public bool IsVersion8;
#endif
    }

    internal sealed class SqlFedAuthInfo
    {
        public string Spn;
        public string StsUrl;
        public override string ToString()
        {
            return $"STSURL: {StsUrl}, SPN: {Spn}";
        }
    }

    internal sealed class SqlFedAuthToken
    {
        public uint DataLen;
        public byte[] AccessToken;
        public long ExpirationFileTime;
    }

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

        public string Column;
        public string BaseColumn;
        public MultiPartTableName MultiPartTableName;
        public readonly int Ordinal;
        public byte TableNum;
        public byte Op;        // for altrow-columns only
        public ushort Operand; // for altrow-columns only
        private _SqlMetadataFlags _flags;

        public _SqlMetaData(int ordinal) : base()
        {
            Ordinal = ordinal;
        }

        private bool HasFlag(_SqlMetadataFlags flag)
        {
            return (_flags & flag) != 0;
        }

        public string ServerName
        {
            get
            {
                return MultiPartTableName.ServerName;
            }
        }
        public string CatalogName
        {
            get
            {
                return MultiPartTableName.CatalogName;
            }
        }
        public string SchemaName
        {
            get
            {
                return MultiPartTableName.SchemaName;
            }
        }
        public string TableName
        {
            get
            {
                return MultiPartTableName.TableName;
            }
        }

        public byte Updatability
        {
            get => (byte)(_flags & _SqlMetadataFlags.IsUpdatableMask);
            set => _flags = (_SqlMetadataFlags)((value & (byte)_SqlMetadataFlags.IsUpdatableMask) | ((int)_flags & ~(byte)_SqlMetadataFlags.IsUpdatableMask));
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
            _flags = value ? _flags | flag : _flags & ~flag;
        }

        public bool Is2008DateTimeType
        {
            get
            {
                return SqlDbType.Date == Type || SqlDbType.Time == Type || SqlDbType.DateTime2 == Type || SqlDbType.DateTimeOffset == Type;
            }
        }

        public bool IsLargeUdt
        {
            get
            {
                return Type == SqlDbType.Udt && Length == int.MaxValue;
            }
        }

        public object Clone()
        {
            _SqlMetaData result = new(Ordinal);
            result.CopyFrom(this);
            result.Column = Column;
            result.BaseColumn = BaseColumn;
            result.MultiPartTableName = MultiPartTableName;
            result.TableNum = TableNum;
            result._flags = _flags;
            result.Op = Op;
            result.Operand = Operand;
            return result;
        }
    }

    internal sealed partial class _SqlMetaDataSet
    {
        public ushort Id;             // for altrow-columns only

        public DataTable SchemaTable;
#if NET
        public ReadOnlyCollection<DbColumn> DbColumnSchema;
#endif

        private readonly _SqlMetaData[] _metaDataArray;

        private int _hiddenColumnCount;
        private int[] _visibleColumnMap;

        public _SqlMetaDataSet(int count)
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
            Id = original.Id;
            _hiddenColumnCount = original._hiddenColumnCount;
            _visibleColumnMap = original._visibleColumnMap;
#if NET
            DbColumnSchema = original.DbColumnSchema;
#else
            SchemaTable = original.SchemaTable;
#endif
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

        public int Length
        {
            get
            {
                return _metaDataArray.Length;
            }
        }

        public int VisibleColumnCount
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

        public _SqlMetaData this[int index]
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
        public _SqlMetaDataSet MetaDataSet;

        public _SqlMetaDataSetCollection()
        {
            _altMetaDataSetArray = new List<_SqlMetaDataSet>();
        }

        public void SetAltMetaData(_SqlMetaDataSet altMetaDataSet)
        {
            // If altmetadata with same id is found, override it rather than adding a new one
            int newId = altMetaDataSet.Id;
            for (int i = 0; i < _altMetaDataSetArray.Count; i++)
            {
                if (_altMetaDataSetArray[i].Id == newId)
                {
                    // override the existing metadata with the same id
                    _altMetaDataSetArray[i] = altMetaDataSet;
                    return;
                }
            }

            // if we did not find metadata to override, add as new
            _altMetaDataSetArray.Add(altMetaDataSet);
        }

        public _SqlMetaDataSet GetAltMetaData(int id)
        {
            foreach (_SqlMetaDataSet altMetaDataSet in _altMetaDataSetArray)
            {
                if (altMetaDataSet.Id == id)
                {
                    return altMetaDataSet;
                }
            }
            Debug.Fail("Can't match up altMetaDataSet with given id");
            return null;
        }

        public object Clone()
        {
            _SqlMetaDataSetCollection result = new() { MetaDataSet = MetaDataSet?.Clone() };
            
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

        public SqlDbType Type;    // SqlDbType enum value
        public byte TdsType; // underlying tds type
        public byte Precision = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
        public byte Scale = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
        private SqlMetaDataPrivFlags _flags;
        public int Length;
        public SqlCollation Collation;
        public int CodePage;
        public Encoding Encoding;

        public MetaType MetaType; // cached metaType
        public SqlMetaDataUdt Udt;
        public SqlMetaDataXmlSchemaCollection XmlSchemaCollection;

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
            return (_flags & flag) != 0;
        }

        private void Set(SqlMetaDataPrivFlags flag, bool value)
        {
            _flags = value ? _flags | flag : _flags & ~flag;
        }

        internal void CopyFrom(SqlMetaDataPriv original)
        {
            Type = original.Type;
            TdsType = original.TdsType;
            Precision = original.Precision;
            Scale = original.Scale;
            Length = original.Length;
            Collation = original.Collation;
            CodePage = original.CodePage;
            Encoding = original.Encoding;
            MetaType = original.MetaType;
            _flags = original._flags;

            if (original.Udt != null)
            {
                Udt = new SqlMetaDataUdt();
                Udt.CopyFrom(original.Udt);
            }

            if (original.XmlSchemaCollection != null)
            {
                XmlSchemaCollection = new SqlMetaDataXmlSchemaCollection();
                XmlSchemaCollection.CopyFrom(original.XmlSchemaCollection);
            }
        }
    }

    internal sealed class SqlMetaDataXmlSchemaCollection
    {
        public string Database;
        public string OwningSchema;
        public string Name;

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
#if NET6_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
        public Type Type;
        public string DatabaseName;
        public string SchemaName;
        public string TypeName;
        public string AssemblyQualifiedName;

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
        public string RpcName;
        public ushort ProcId;       // Used instead of name
        public ushort Options;

        public SqlParameter[] SystemParams;
        public byte[] SystemParamOptions;
        public int SystemParamCount;

        public SqlParameterCollection UserParams;
        public long[] UserParamMap;
        public int UserParamCount;

        public int? RecordsAffected;
        public int CumulativeRecordsAffected;

        public int ErrorsIndexStart;
        public int ErrorsIndexEnd;
        public SqlErrorCollection Errors;

        public int WarningsIndexStart;
        public int WarningsIndexEnd;
        public SqlErrorCollection Warnings;

        public bool NeedsFetchParameterEncryptionMetadata;

        public SqlBatchCommand BatchCommand;

        public string GetCommandTextOrRpcName()
        {
            if (TdsEnums.RPC_PROCID_EXECUTESQL == ProcId)
            {
                // Param 0 is the actual sql executing
                return (string)SystemParams[0].Value;
            }
            else
            {
                return RpcName;
            }
        }

        public SqlParameter GetParameterByIndex(int index, out byte options)
        {
            SqlParameter retval;

            if (index < SystemParamCount)
            {
                retval = SystemParams[index];
                options = SystemParamOptions[index];
            }
            else
            {
                long data = UserParamMap[index - SystemParamCount];
                int paramIndex = (int)(data & int.MaxValue);
                options = (byte)((data >> 32) & 0xFF);
                retval = UserParams[paramIndex];
            }
            return retval;
        }
    }

    internal sealed partial class SqlReturnValue : SqlMetaDataPriv
    {
        public string Parameter;
        public readonly SqlBuffer Value;
#if NETFRAMEWORK
        public ushort ParmIndex;      //2005 or later only
#endif

        public SqlReturnValue() : base()
        {
            Value = new SqlBuffer();
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

        public static readonly MultiPartTableName Null = new MultiPartTableName(new string[] { null, null, null, null });
    }

    internal static class SslProtocolsHelper
    {
#if NET
        private static string ToFriendlyName(this SslProtocols protocol)
        {
            string name;

            /* The SslProtocols.Tls13 is supported by netcoreapp3.1 and later
             * This driver does not support this version yet!
            if ((protocol & SslProtocols.Tls13) == SslProtocols.Tls13)
            {
                name = "TLS 1.3";
            }*/
            if ((protocol & SslProtocols.Tls12) == SslProtocols.Tls12)
            {
                name = "TLS 1.2";
            }
#if NET8_0_OR_GREATER
#pragma warning disable SYSLIB0039 // Type or member is obsolete: TLS 1.0 & 1.1 are deprecated
#endif
            else if ((protocol & SslProtocols.Tls11) == SslProtocols.Tls11)
            {
                name = "TLS 1.1";
            }
            else if ((protocol & SslProtocols.Tls) == SslProtocols.Tls)
            {
                name = "TLS 1.0";
            }
#if NET8_0_OR_GREATER
#pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
#endif
#pragma warning disable CS0618 // Type or member is obsolete: SSL is deprecated
            else if ((protocol & SslProtocols.Ssl3) == SslProtocols.Ssl3)
            {
                name = "SSL 3.0";
            }
            else if ((protocol & SslProtocols.Ssl2) == SslProtocols.Ssl2)
#pragma warning restore CS0618 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
            {
                name = "SSL 2.0";
            }
            else
            {
                name = protocol.ToString();
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
#pragma warning disable CS0618 // Type or member is obsolete : SSL is depricated
            if ((protocol & (SslProtocols.Ssl2 | SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11)) != SslProtocols.None)
#pragma warning restore CS0618 // Type or member is obsolete : SSL is depricated
#if NET8_0_OR_GREATER
#pragma warning restore SYSLIB0039 // Type or member is obsolete: SSL and TLS 1.0 & 1.1 is deprecated
#endif
            {
                message = StringsHelper.Format(Strings.SEC_ProtocolWarning, protocol.ToFriendlyName());
            }
            return message;
        }
#else
        // protocol versions from native sni
        [Flags]
        private enum NativeProtocols
        {
            SP_PROT_SSL2_SERVER = 0x00000004,
            SP_PROT_SSL2_CLIENT = 0x00000008,
            SP_PROT_SSL3_SERVER = 0x00000010,
            SP_PROT_SSL3_CLIENT = 0x00000020,
            SP_PROT_TLS1_0_SERVER = 0x00000040,
            SP_PROT_TLS1_0_CLIENT = 0x00000080,
            SP_PROT_TLS1_1_SERVER = 0x00000100,
            SP_PROT_TLS1_1_CLIENT = 0x00000200,
            SP_PROT_TLS1_2_SERVER = 0x00000400,
            SP_PROT_TLS1_2_CLIENT = 0x00000800,
            SP_PROT_TLS1_3_SERVER = 0x00001000,
            SP_PROT_TLS1_3_CLIENT = 0x00002000,
            SP_PROT_SSL2 = SP_PROT_SSL2_SERVER | SP_PROT_SSL2_CLIENT,
            SP_PROT_SSL3 = SP_PROT_SSL3_SERVER | SP_PROT_SSL3_CLIENT,
            SP_PROT_TLS1_0 = SP_PROT_TLS1_0_SERVER | SP_PROT_TLS1_0_CLIENT,
            SP_PROT_TLS1_1 = SP_PROT_TLS1_1_SERVER | SP_PROT_TLS1_1_CLIENT,
            SP_PROT_TLS1_2 = SP_PROT_TLS1_2_SERVER | SP_PROT_TLS1_2_CLIENT,
            SP_PROT_TLS1_3 = SP_PROT_TLS1_3_SERVER | SP_PROT_TLS1_3_CLIENT,
            SP_PROT_NONE = 0x0
        }

        private static string ToFriendlyName(this NativeProtocols protocol)
        {
            string name;

            if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_3_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_3_SERVER))
            {
                name = "TLS 1.3";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_2_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_2_SERVER))
            {
                name = "TLS 1.2";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_1_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_1_SERVER))
            {
                name = "TLS 1.1";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_0_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_TLS1_0_SERVER))
            {
                name = "TLS 1.0";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_SSL3_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_SSL3_SERVER))
            {
                name = "SSL 3.0";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_SSL2_CLIENT) || protocol.HasFlag(NativeProtocols.SP_PROT_SSL2_SERVER))
            {
                name = "SSL 2.0";
            }
            else if (protocol.HasFlag(NativeProtocols.SP_PROT_NONE))
            {
                name = "None";
            }
            else
            {
                throw new ArgumentException(StringsHelper.GetString(StringsHelper.net_invalid_enum, nameof(NativeProtocols)), nameof(NativeProtocols));
            }
            return name;
        }

        /// <summary>
        /// check the negotiated secure protocol if it's under TLS 1.2
        /// </summary>
        /// <param name="protocol"></param>
        /// <returns>Localized warning message</returns>
        public static string GetProtocolWarning(uint protocol)
        {
            var nativeProtocol = (NativeProtocols)protocol;
            string message = string.Empty;
            if ((nativeProtocol & (NativeProtocols.SP_PROT_SSL2 | NativeProtocols.SP_PROT_SSL3 | NativeProtocols.SP_PROT_TLS1_1)) != NativeProtocols.SP_PROT_NONE)
            {
                message = StringsHelper.GetString(Strings.SEC_ProtocolWarning, nativeProtocol.ToFriendlyName());
            }
            return message;
        }
#endif
    }
}
