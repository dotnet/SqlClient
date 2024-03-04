// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Text;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    internal enum CallbackType
    {
        Read = 0,
        Write = 1
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
        internal TdsEnums.FedAuthLibrary _libraryType;
        internal bool _fedAuthRequiredPreLoginResponse;
        internal SqlAuthenticationMethod _authentication;
        internal byte[] _accessToken;
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
        internal SqlAuthenticationMethod _authentication = SqlAuthenticationMethod.NotSpecified;  // Authentication type
        internal int _timeout;                                                       // login timeout
        internal bool _userInstance = false;                                   // user instance
        internal string _hostName = "";                                      // client machine name
        internal string _userName = "";                                      // user id
        internal string _password = "";                                      // password
        internal string _applicationName = "";                                      // application name
        internal string _serverName = "";                                      // server name
        internal string _language = "";                                      // initial language
        internal string _database = "";                                      // initial database
        internal string _attachDBFilename = "";                                      // DB filename to be attached
        internal bool _useReplication = false;                                   // user login for replication
        internal string _newPassword = "";                                   // new password for reset password
        internal bool _useSSPI = false;                                   // use integrated security
        internal int _packetSize = SqlConnectionString.DEFAULT.Packet_Size; // packet size
        internal bool _readOnlyIntent = false;                                   // read-only intent
        internal SqlCredential _credential;                                      // user id and password in SecureString
        internal SecureString _newSecurePassword;
    }

    internal sealed partial class SqlLoginAck
    {
        internal byte _majorVersion;
        internal byte _minorVersion;
        internal short _buildNum;
        internal uint _tdsVersion;
    }

    internal sealed class SqlFedAuthInfo
    {
        internal string _spn;
        internal string _stsurl;
        public override string ToString()
        {
            return $"STSURL: {_stsurl}, SPN: {_spn}";
        }
    }

    internal sealed class SqlFedAuthToken
    {
        internal uint _dataLen;
        internal byte[] _accessToken;
        internal long _expirationFileTime;
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

        internal string _column;
        internal string _baseColumn;
        internal MultiPartTableName _multiPartTableName;
        internal readonly int _ordinal;
        internal byte _tableNum;
        internal byte _op;        // for altrow-columns only
        internal ushort _operand; // for altrow-columns only
        private _SqlMetadataFlags _flags;

        internal _SqlMetaData(int ordinal) : base()
        {
            _ordinal = ordinal;
        }

        private bool HasFlag(_SqlMetadataFlags flag)
        {
            return (_flags & flag) != 0;
        }

        internal string serverName
        {
            get
            {
                return _multiPartTableName.ServerName;
            }
        }
        internal string catalogName
        {
            get
            {
                return _multiPartTableName.CatalogName;
            }
        }
        internal string schemaName
        {
            get
            {
                return _multiPartTableName.SchemaName;
            }
        }
        internal string tableName
        {
            get
            {
                return _multiPartTableName.TableName;
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

        internal bool Is2008DateTimeType
        {
            get
            {
                return SqlDbType.Date == _type || SqlDbType.Time == _type || SqlDbType.DateTime2 == _type || SqlDbType.DateTimeOffset == _type;
            }
        }

        internal bool IsLargeUdt
        {
            get
            {
                return _type == SqlDbType.Udt && _length == int.MaxValue;
            }
        }

        public object Clone()
        {
            _SqlMetaData result = new(_ordinal);
            result.CopyFrom(this);
            result._column = _column;
            result._baseColumn = _baseColumn;
            result._multiPartTableName = _multiPartTableName;
            result._tableNum = _tableNum;
            result._flags = _flags;
            result._op = _op;
            result._operand = _operand;
            return result;
        }
    }

    internal sealed partial class _SqlMetaDataSet
    {
        internal ushort _id;             // for altrow-columns only

        internal DataTable _schemaTable;
        private readonly _SqlMetaData[] _metaDataArray;

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
                Debug.Assert(null == value, "used only by SqlBulkCopy");
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
        internal _SqlMetaDataSet _metaDataSet;

        internal _SqlMetaDataSetCollection()
        {
            _altMetaDataSetArray = new List<_SqlMetaDataSet>();
        }

        internal void SetAltMetaData(_SqlMetaDataSet altMetaDataSet)
        {
            // If altmetadata with same id is found, override it rather than adding a new one
            int newId = altMetaDataSet._id;
            for (int i = 0; i < _altMetaDataSetArray.Count; i++)
            {
                if (_altMetaDataSetArray[i]._id == newId)
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
                if (altMetaDataSet._id == id)
                {
                    return altMetaDataSet;
                }
            }
            Debug.Fail("Can't match up altMetaDataSet with given id");
            return null;
        }

        public object Clone()
        {
            _SqlMetaDataSetCollection result = new() { _metaDataSet = _metaDataSet?.Clone() };
            
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

        internal SqlDbType _type;    // SqlDbType enum value
        internal byte _tdsType; // underlying tds type
        internal byte _precision = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
        internal byte _scale = TdsEnums.UNKNOWN_PRECISION_SCALE; // give default of unknown (-1)
        private SqlMetaDataPrivFlags _flags;
        internal int _length;
        internal SqlCollation _collation;
        internal int _codePage;
        internal Encoding _encoding;

        internal MetaType _metaType; // cached metaType
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
            return (_flags & flag) != 0;
        }

        private void Set(SqlMetaDataPrivFlags flag, bool value)
        {
            _flags = value ? _flags | flag : _flags & ~flag;
        }

        internal void CopyFrom(SqlMetaDataPriv original)
        {
            _type = original._type;
            _tdsType = original._tdsType;
            _precision = original._precision;
            _scale = original._scale;
            _length = original._length;
            _collation = original._collation;
            _codePage = original._codePage;
            _encoding = original._encoding;
            _metaType = original._metaType;
            _flags = original._flags;

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
        internal string _database;
        internal string _owningSchema;
        internal string _name;

        public void CopyFrom(SqlMetaDataXmlSchemaCollection original)
        {
            if (original != null)
            {
                _database = original._database;
                _owningSchema = original._owningSchema;
                _name = original._name;
            }
        }
    }

    internal sealed class SqlMetaDataUdt
    {
#if NET6_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
        internal Type _type;
        internal string _databaseName;
        internal string _schemaName;
        internal string _typeName;
        internal string _assemblyQualifiedName;

        public void CopyFrom(SqlMetaDataUdt original)
        {
            if (original != null)
            {
                _type = original._type;
                _databaseName = original._databaseName;
                _schemaName = original._schemaName;
                _typeName = original._typeName;
                _assemblyQualifiedName = original._assemblyQualifiedName;
            }
        }
    }

    internal sealed class _SqlRPC
    {
        internal string _rpcName;
        internal ushort _procID;       // Used instead of name
        internal ushort _options;

        internal SqlParameter[] _systemParams;
        internal byte[] _systemParamOptions;
        internal int _systemParamCount;

        internal SqlParameterCollection _userParams;
        internal long[] _userParamMap;
        internal int _userParamCount;

        internal int? _recordsAffected;
        internal int _cumulativeRecordsAffected;

        internal int _errorsIndexStart;
        internal int _errorsIndexEnd;
        internal SqlErrorCollection _errors;

        internal int _warningsIndexStart;
        internal int _warningsIndexEnd;
        internal SqlErrorCollection _warnings;

        internal bool _needsFetchParameterEncryptionMetadata;

        internal SqlBatchCommand _batchCommand;

        internal string GetCommandTextOrRpcName()
        {
            if (TdsEnums.RPC_PROCID_EXECUTESQL == _procID)
            {
                // Param 0 is the actual sql executing
                return (string)_systemParams[0].Value;
            }
            else
            {
                return _rpcName;
            }
        }

        internal SqlParameter GetParameterByIndex(int index, out byte options)
        {
            SqlParameter retval;

            if (index < _systemParamCount)
            {
                retval = _systemParams[index];
                options = _systemParamOptions[index];
            }
            else
            {
                long data = _userParamMap[index - _systemParamCount];
                int paramIndex = (int)(data & int.MaxValue);
                options = (byte)((data >> 32) & 0xFF);
                retval = _userParams[paramIndex];
            }
            return retval;
        }
    }

    internal sealed partial class SqlReturnValue : SqlMetaDataPriv
    {
        internal string _parameter;
        internal readonly SqlBuffer _value;

        internal SqlReturnValue() : base()
        {
            _value = new SqlBuffer();
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
            if (null != _multipartName)
            {
                string[] parts = MultipartIdentifier.ParseMultipartIdentifier(_multipartName, "[\"", "]\"", Strings.SQL_TDSParserTableName, false);
                _serverName = parts[0];
                _catalogName = parts[1];
                _schemaName = parts[2];
                _tableName = parts[3];
                _multipartName = null;
            }
        }
    }
}
