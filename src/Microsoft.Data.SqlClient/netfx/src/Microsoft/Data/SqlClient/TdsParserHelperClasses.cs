// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Server;
using Microsoft.Data.SqlTypes;

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
        CTAIP = 0x40,
        CLIENT_CERT = 0x80,
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

    /// <summary>
    /// <para> Represents a single encrypted value for a CEK. It contains the encrypted CEK,
    ///  the store type, name,the key path and encryption algorithm.</para>
    /// </summary>
    internal class SqlEncryptionKeyInfo
    {
        internal byte[] encryptedKey; // the encrypted "column encryption key"
        internal int databaseId;
        internal int cekId;
        internal int cekVersion;
        internal byte[] cekMdVersion;
        internal string keyPath;
        internal string keyStoreName;
        internal string algorithmName;
        internal byte normalizationRuleVersion;
    }

    /// <summary>
    /// <para> Encapsulates one entry in the CipherInfo table sent as part of Colmetadata.
    /// The same CEK is encrypted multiple times with different master keys (for master key
    /// rotation scenario) We need to keep all these around until we can resolve the CEK
    /// using the correct master key.</para>
    /// </summary>
    internal class SqlTceCipherInfoEntry
    {

        /// <summary>
        /// List of Column Encryption Key Information.
        /// </summary>
        private readonly List<SqlEncryptionKeyInfo> _columnEncryptionKeyValues;

        /// <summary>
        /// Key Ordinal.
        /// </summary>
        private readonly int _ordinal;

        /// <summary>
        /// Database ID
        /// </summary>
        private int _databaseId;

        /// <summary>
        /// Cek ID
        /// </summary>
        private int _cekId;

        /// <summary>
        /// Cek Version
        /// </summary>
        private int _cekVersion;

        /// <summary>
        /// Cek MD Version
        /// </summary>
        private byte[] _cekMdVersion;

        /// <summary>
        /// Return the ordinal.
        /// </summary>
        internal int Ordinal
        {
            get
            {
                return _ordinal;
            }
        }

        /// <summary>
        /// Return the DatabaseID.
        /// </summary>
        internal int DatabaseId
        {
            get
            {
                return _databaseId;
            }
        }

        /// <summary>
        /// Return the CEK ID.
        /// </summary>
        internal int CekId
        {
            get
            {
                return _cekId;
            }
        }

        /// <summary>
        /// Return the CEK Version.
        /// </summary>
        internal int CekVersion
        {
            get
            {
                return _cekVersion;
            }
        }

        /// <summary>
        /// Return the CEK MD Version.
        /// </summary>
        internal byte[] CekMdVersion
        {
            get
            {
                return _cekMdVersion;
            }
        }

        /// <summary>
        /// Return the list of Column Encryption Key Values.
        /// </summary>
        internal List<SqlEncryptionKeyInfo> ColumnEncryptionKeyValues
        {
            get
            {
                return _columnEncryptionKeyValues;
            }
        }

        /// <summary>
        /// Add an entry to the list of ColumnEncryptionKeyValues.
        /// </summary>
        /// <param name="encryptedKey"></param>
        /// <param name="databaseId"></param>
        /// <param name="cekId"></param>
        /// <param name="cekVersion"></param>
        /// <param name="cekMdVersion"></param>
        /// <param name="keyPath"></param>
        /// <param name="keyStoreName"></param>
        /// <param name="algorithmName"></param>
        internal void Add(byte[] encryptedKey, int databaseId, int cekId, int cekVersion, byte[] cekMdVersion, string keyPath, string keyStoreName, string algorithmName)
        {

            Debug.Assert(_columnEncryptionKeyValues != null, "_columnEncryptionKeyValues should already be initialized.");

            SqlEncryptionKeyInfo encryptionKey = new SqlEncryptionKeyInfo();
            encryptionKey.encryptedKey = encryptedKey;
            encryptionKey.databaseId = databaseId;
            encryptionKey.cekId = cekId;
            encryptionKey.cekVersion = cekVersion;
            encryptionKey.cekMdVersion = cekMdVersion;
            encryptionKey.keyPath = keyPath;
            encryptionKey.keyStoreName = keyStoreName;
            encryptionKey.algorithmName = algorithmName;
            _columnEncryptionKeyValues.Add(encryptionKey);

            if (0 == _databaseId)
            {
                _databaseId = databaseId;
                _cekId = cekId;
                _cekVersion = cekVersion;
                _cekMdVersion = cekMdVersion;
            }
            else
            {
                Debug.Assert(_databaseId == databaseId);
                Debug.Assert(_cekId == cekId);
                Debug.Assert(_cekVersion == cekVersion);
                Debug.Assert(_cekMdVersion != null && cekMdVersion != null && _cekMdVersion.Length == _cekMdVersion.Length);
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ordinal"></param>
        internal SqlTceCipherInfoEntry(int ordinal = 0)
        {
            _ordinal = ordinal;
            _databaseId = 0;
            _cekId = 0;
            _cekVersion = 0;
            _cekMdVersion = null;
            _columnEncryptionKeyValues = new List<SqlEncryptionKeyInfo>();
        }
    }

    /// <summary> 
    /// <para> Represents a table with various CEKs used in a resultset. Each entry corresponds to one (unique) CEK. The CEK
    /// may have been encrypted using multiple master keys (giving us multiple CEK values). All these values form one single
    /// entry in this table.</para>
    ///</summary>
    internal class SqlTceCipherInfoTable
    {
        private readonly SqlTceCipherInfoEntry[] keyList;

        internal SqlTceCipherInfoTable(int tabSize)
        {
            Debug.Assert(0 < tabSize, "Invalid Table Size");
            keyList = new SqlTceCipherInfoEntry[tabSize];
        }

        internal SqlTceCipherInfoEntry this[int index]
        {
            get
            {
                Debug.Assert(index < keyList.Length, "Invalid index specified.");
                return keyList[index];
            }
            set
            {
                Debug.Assert(index < keyList.Length, "Invalid index specified.");
                keyList[index] = value;
            }
        }

        internal int Size
        {
            get
            {
                return keyList.Length;
            }
        }
    }

    internal class RoutingInfo
    {
        internal byte Protocol { get; private set; }
        internal UInt16 Port { get; private set; }
        internal string ServerName { get; private set; }

        internal RoutingInfo(byte protocol, UInt16 port, string servername)
        {
            Protocol = protocol;
            Port = port;
            ServerName = servername;
        }
    }

    sealed internal class SqlLogin
    {
        internal SqlAuthenticationMethod authentication = SqlAuthenticationMethod.NotSpecified;               // Authentication type
        internal int timeout;                                                       // login timeout
        internal bool userInstance = false;                                   // user instance
        internal string hostName = "";                                      // client machine name
        internal string userName = "";                                      // user id
        internal string password = "";                                      // password
        internal string applicationName = "";                                      // application name
        internal string serverName = "";                                      // server name
        internal string language = "";                                      // initial language
        internal string database = "";                                      // initial database
        internal string attachDBFilename = "";                                      // DB filename to be attached
        internal string newPassword = "";                                      // new password for reset password
        internal bool useReplication = false;                                   // user login for replication
        internal bool useSSPI = false;                                   // use integrated security
        internal int packetSize = SqlConnectionString.DEFAULT.Packet_Size; // packet size
        internal bool readOnlyIntent = false;                                   // read-only intent
        internal SqlCredential credential;                                          // user id and password in SecureString
        internal SecureString newSecurePassword;                                    // new password in SecureString for resetting pasword
    }

    sealed internal class SqlLoginAck
    {
        internal string programName;
        internal byte majorVersion;
        internal byte minorVersion;
        internal short buildNum;
        internal bool isVersion8;
        internal UInt32 tdsVersion;
    }

    sealed internal class SqlFedAuthInfo
    {
        internal string spn;
        internal string stsurl;
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "STSURL: {0}, SPN: {1}", stsurl ?? String.Empty, spn ?? String.Empty);
        }
    }

    sealed internal class SqlFedAuthToken
    {
        internal UInt32 dataLen;
        internal byte[] accessToken;
        internal long expirationFileTime;
    }

    sealed internal class _SqlMetaData : SqlMetaDataPriv, ICloneable
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
        internal byte op;       // for altrow-columns only
        internal ushort operand;  // for altrow-columns only
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
                return type == SqlDbType.Udt && length == Int32.MaxValue;
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

    sealed internal class _SqlMetaDataSet 
    {
        internal ushort id;             // for altrow-columns only
        internal DataTable _schemaTable;
        internal readonly SqlTceCipherInfoTable cekTable; // table of "column encryption keys" used for this metadataset
        internal readonly _SqlMetaData[] _metaDataArray;
        private int _hiddenColumnCount;
        private int[] _visibleColumnMap;

        internal _SqlMetaDataSet(int count, SqlTceCipherInfoTable cipherTable)
        {
            _hiddenColumnCount = -1;
            cekTable = cipherTable;
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
            _schemaTable = original._schemaTable;
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

        internal int Length
        {
            get
            {
                return _metaDataArray.Length;
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


    sealed internal class _SqlMetaDataSetCollection : ICloneable
    {
        private readonly List<_SqlMetaDataSet> altMetaDataSetArray;
        internal _SqlMetaDataSet metaDataSet;

        internal _SqlMetaDataSetCollection()
        {
            altMetaDataSetArray = new List<_SqlMetaDataSet>();
        }

        internal void SetAltMetaData(_SqlMetaDataSet altMetaDataSet)
        {
            // VSTFDEVDIV 479675: if altmetadata with same id is found, override it rather than adding a new one
            int newId = altMetaDataSet.id;
            for (int i = 0; i < altMetaDataSetArray.Count; i++)
            {
                if (altMetaDataSetArray[i].id == newId)
                {
                    // override the existing metadata with the same id
                    altMetaDataSetArray[i] = altMetaDataSet;
                    return;
                }
            }

            // if we did not find metadata to override, add as new
            altMetaDataSetArray.Add(altMetaDataSet);
        }

        internal _SqlMetaDataSet GetAltMetaData(int id)
        {
            foreach (_SqlMetaDataSet altMetaDataSet in altMetaDataSetArray)
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
            _SqlMetaDataSetCollection result = new _SqlMetaDataSetCollection();
            result.metaDataSet = metaDataSet == null ? null : metaDataSet.Clone();
            foreach (_SqlMetaDataSet set in altMetaDataSetArray)
            {
                result.altMetaDataSetArray.Add(set.Clone());
            }
            return result;
        }
    }

    /// <summary>
    /// Represents Encryption related information of the cipher data.
    /// </summary>
    internal class SqlCipherMetadata
    {

        /// <summary>
        /// Cipher Info Entry.
        /// </summary>
        private SqlTceCipherInfoEntry _sqlTceCipherInfoEntry;

        /// <summary>
        /// Encryption Algorithm Id.
        /// </summary>
        private readonly byte _cipherAlgorithmId;

        /// <summary>
        /// Encryption Algorithm Name.
        /// </summary>
        private readonly string _cipherAlgorithmName;

        /// <summary>
        /// Encryption Type.
        /// </summary>
        private readonly byte _encryptionType;

        /// <summary>
        /// Normalization Rule Version.
        /// </summary>
        private readonly byte _normalizationRuleVersion;

        /// <summary>
        /// Encryption Algorithm Handle.
        /// </summary>
        private SqlClientEncryptionAlgorithm _sqlClientEncryptionAlgorithm;

        /// <summary>
        /// Sql Encryption Key Info.
        /// </summary>
        private SqlEncryptionKeyInfo _sqlEncryptionKeyInfo;

        /// <summary>
        /// Ordinal (into the Cek Table).
        /// </summary>
        private readonly ushort _ordinal;

        /// <summary>
        /// Return the Encryption Info Entry.
        /// </summary>
        internal SqlTceCipherInfoEntry EncryptionInfo
        {
            get
            {
                return _sqlTceCipherInfoEntry;
            }
            set
            {
                Debug.Assert(_sqlTceCipherInfoEntry == null, "We can only set the EncryptionInfo once.");
                _sqlTceCipherInfoEntry = value;
            }
        }

        /// <summary>
        /// Return the cipher's encryption algorithm id.
        /// </summary>
        internal byte CipherAlgorithmId
        {
            get
            {
                return _cipherAlgorithmId;
            }
        }

        /// <summary>
        /// Return the cipher's encryption algorithm name (could be null).
        /// </summary>
        internal string CipherAlgorithmName
        {
            get
            {
                return _cipherAlgorithmName;
            }
        }

        /// <summary>
        /// Return EncryptionType (Deterministic, Randomized, etc.)
        /// </summary>
        internal byte EncryptionType
        {
            get
            {
                return _encryptionType;
            }
        }

        /// <summary>
        /// Return normalization rule version.
        /// </summary>
        internal byte NormalizationRuleVersion
        {
            get
            {
                return _normalizationRuleVersion;
            }
        }

        /// <summary>
        /// Return the cipher encyrption algorithm handle.
        /// </summary>
        internal SqlClientEncryptionAlgorithm CipherAlgorithm
        {
            get
            {
                return _sqlClientEncryptionAlgorithm;
            }
            set
            {
                Debug.Assert(_sqlClientEncryptionAlgorithm == null, "_sqlClientEncryptionAlgorithm should not be set more than once.");
                _sqlClientEncryptionAlgorithm = value;
            }
        }

        /// <summary>
        /// Return Encryption Key Info.
        /// </summary>
        internal SqlEncryptionKeyInfo EncryptionKeyInfo
        {
            get
            {
                return _sqlEncryptionKeyInfo;
            }

            set
            {
                Debug.Assert(_sqlEncryptionKeyInfo == null, "_sqlEncryptionKeyInfo should not be set more than once.");
                _sqlEncryptionKeyInfo = value;
            }
        }

        /// <summary>
        /// Return Ordinal into Cek Table.
        /// </summary>
        internal ushort CekTableOrdinal
        {
            get
            {
                return _ordinal;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sqlTceCipherInfoEntry"></param>
        /// <param name="ordinal"></param>
        /// <param name="cipherAlgorithmId"></param>
        /// <param name="cipherAlgorithmName"></param>
        /// <param name="encryptionType"></param>
        /// <param name="normalizationRuleVersion"></param>
        internal SqlCipherMetadata(SqlTceCipherInfoEntry sqlTceCipherInfoEntry,
                                    ushort ordinal,
                                    byte cipherAlgorithmId,
                                    string cipherAlgorithmName,
                                    byte encryptionType,
                                    byte normalizationRuleVersion)
        {
            Debug.Assert(!sqlTceCipherInfoEntry.Equals(default(SqlTceCipherInfoEntry)), "sqlTceCipherInfoEntry should not be un-initialized.");

            _sqlTceCipherInfoEntry = sqlTceCipherInfoEntry;
            _ordinal = ordinal;
            _cipherAlgorithmId = cipherAlgorithmId;
            _cipherAlgorithmName = cipherAlgorithmName;
            _encryptionType = encryptionType;
            _normalizationRuleVersion = normalizationRuleVersion;
            _sqlEncryptionKeyInfo = null;
        }

        /// <summary>
        /// Do we have an handle to the cipher encryption algorithm already ?
        /// </summary>
        /// <returns></returns>
        internal bool IsAlgorithmInitialized()
        {
            return (null != _sqlClientEncryptionAlgorithm) ? true : false;
        }
    }

    internal class SqlMetaDataPriv
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

        internal bool isEncrypted; // TCE encrypted?
        internal SqlMetaDataPriv baseTI;   // for encrypted columns, represents the TYPE_INFO for plaintext value
        internal SqlCipherMetadata cipherMD; // Cipher related metadata for encrypted columns.

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

        /// <summary>
        /// Is the algorithm handle for the cipher encryption initialized ?
        /// </summary>
        /// <returns></returns>
        internal bool IsAlgorithmInitialized()
        {
            if (null != cipherMD)
            {
                return cipherMD.IsAlgorithmInitialized();
            }

            return false;
        }

        /// <summary>
        /// Returns the normalization rule version byte.
        /// </summary>
        /// <returns></returns>
        internal byte NormalizationRuleVersion
        {
            get
            {
                if (null != cipherMD)
                {
                    return cipherMD.NormalizationRuleVersion;
                }

                return 0x00;
            }
        }
    }

    sealed internal class SqlMetaDataXmlSchemaCollection
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

    sealed internal class SqlMetaDataUdt
    {
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

    /// <summary>
    /// Class encapsulating additional information when sending encrypted input parameters.
    /// </summary>
    sealed internal class SqlColumnEncryptionInputParameterInfo
    {
        /// <summary>
        /// Metadata of the parameter to write the TYPE_INFO of the unencrypted column data type.
        /// </summary>
        private readonly SmiParameterMetaData _smiParameterMetadata;

        /// <summary>
        /// Column encryption related metadata.
        /// </summary>
        private readonly SqlCipherMetadata _cipherMetadata;

        /// <summary>
        /// Serialized format for a subset of members.
        /// Does not include _smiParameterMetadata's serialization.
        /// </summary>
        private readonly byte[] _serializedWireFormat;

        /// <summary>
        /// Return the SMI Parameter Metadata.
        /// </summary>
        internal SmiParameterMetaData ParameterMetadata
        {
            get
            {
                return _smiParameterMetadata;
            }
        }

        /// <summary>
        /// Return the serialized format for some members.
        /// This is pre-calculated and cached since members are immutable.
        /// Does not include _smiParameterMetadata's serialization.
        /// </summary>
        internal byte[] SerializedWireFormat
        {
            get
            {
                return _serializedWireFormat;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="smiParameterMetadata"></param>
        /// <param name="cipherMetadata"></param>
        internal SqlColumnEncryptionInputParameterInfo(SmiParameterMetaData smiParameterMetadata, SqlCipherMetadata cipherMetadata)
        {
            Debug.Assert(smiParameterMetadata != null, "smiParameterMetadata should not be null.");
            Debug.Assert(cipherMetadata != null, "cipherMetadata should not be null");
            Debug.Assert(cipherMetadata.EncryptionKeyInfo != null, "cipherMetadata.EncryptionKeyInfo.HasValue should be true.");

            _smiParameterMetadata = smiParameterMetadata;
            _cipherMetadata = cipherMetadata;
            _serializedWireFormat = SerializeToWriteFormat();
        }

        /// <summary>
        /// Serializes some data members to wire format.
        /// </summary>
        private byte[] SerializeToWriteFormat()
        {
            int totalLength = 0;

            // CipherAlgorithmId.
            totalLength += sizeof(byte);

            // Encryption Type.
            totalLength += sizeof(byte);

            // Database id of the encryption key.
            totalLength += sizeof(int);

            // Id of the encryption key.
            totalLength += sizeof(int);

            // Version of the encryption key.
            totalLength += sizeof(int);

            // Metadata version of the encryption key.
            totalLength += _cipherMetadata.EncryptionKeyInfo.cekMdVersion.Length;

            // Normalization Rule Version.
            totalLength += sizeof(byte);

            byte[] serializedWireFormat = new byte[totalLength];

            // No:of bytes consumed till now. Running variable.
            int consumedBytes = 0;

            // 1 - Write Cipher Algorithm Id.
            serializedWireFormat[consumedBytes++] = _cipherMetadata.CipherAlgorithmId;

            // 2 - Write Encryption Type.
            serializedWireFormat[consumedBytes++] = _cipherMetadata.EncryptionType;

            // 3 - Write the database id of the encryption key.
            SerializeIntIntoBuffer(_cipherMetadata.EncryptionKeyInfo.databaseId, serializedWireFormat, ref consumedBytes);

            // 4 - Write the id of the encryption key.
            SerializeIntIntoBuffer(_cipherMetadata.EncryptionKeyInfo.cekId, serializedWireFormat, ref consumedBytes);

            // 5 - Write the version of the encryption key.
            SerializeIntIntoBuffer(_cipherMetadata.EncryptionKeyInfo.cekVersion, serializedWireFormat, ref consumedBytes);

            // 6 - Write the metadata version of the encryption key.
            Buffer.BlockCopy(_cipherMetadata.EncryptionKeyInfo.cekMdVersion, 0, serializedWireFormat, consumedBytes, _cipherMetadata.EncryptionKeyInfo.cekMdVersion.Length);
            consumedBytes += _cipherMetadata.EncryptionKeyInfo.cekMdVersion.Length;

            // 7 - Write Normalization Rule Version.
            serializedWireFormat[consumedBytes++] = _cipherMetadata.NormalizationRuleVersion;

            return serializedWireFormat;
        }

        /// <summary>
        /// Serializes an int into the provided buffer and offset.
        /// </summary>
        private void SerializeIntIntoBuffer(int value, byte[] buffer, ref int offset)
        {
            buffer[offset++] = (byte)(value & 0xff);
            buffer[offset++] = (byte)((value >> 8) & 0xff);
            buffer[offset++] = (byte)((value >> 16) & 0xff);
            buffer[offset++] = (byte)((value >> 24) & 0xff);
        }
    }

    sealed internal class _SqlRPC
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

    sealed internal class SqlReturnValue : SqlMetaDataPriv
    {

        internal ushort parmIndex;      //2005 or later only
        internal string parameter;
        internal readonly SqlBuffer value;

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

        internal static readonly MultiPartTableName Null = new MultiPartTableName(new string[] { null, null, null, null });
    }

    internal static class SslProtocolsHelper
    {
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
    }
}
