// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.SqlClient.Server;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// <para> Represents a single encrypted value for a CEK. It contains the encrypted CEK,
    ///  the store type, name,the key path and encryption algorithm.</para>
    /// </summary>
    internal struct SqlEncryptionKeyInfo
    {
        internal byte[] encryptedKey; // the encrypted "column encryption key"
        internal int databaseId;
        internal int cekId;
        internal int cekVersion;
        internal byte[] cekMdVersion;
        internal string keyPath;
        internal string keyStoreName;
        internal string algorithmName;
    }

    /// <summary>
    /// <para> Encapsulates one entry in the CipherInfo table sent as part of Colmetadata.
    /// The same CEK is encrypted multiple times with different master keys (for master key
    /// rotation scenario) We need to keep all these around until we can resolve the CEK
    /// using the correct master key.</para>
    /// </summary>
    internal struct SqlTceCipherInfoEntry
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

            SqlEncryptionKeyInfo encryptionKey = new SqlEncryptionKeyInfo
            {
                encryptedKey = encryptedKey,
                databaseId = databaseId,
                cekId = cekId,
                cekVersion = cekVersion,
                cekMdVersion = cekMdVersion,
                keyPath = keyPath,
                keyStoreName = keyStoreName,
                algorithmName = algorithmName
            };
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
        internal SqlTceCipherInfoEntry(int ordinal = 0) : this()
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
    internal struct SqlTceCipherInfoTable
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

    sealed internal partial class _SqlMetaDataSet
    {
        internal readonly SqlTceCipherInfoTable? cekTable; // table of "column encryption keys" used for this metadataset

        internal _SqlMetaDataSet(int count, SqlTceCipherInfoTable? cipherTable)
            : this(count)
        {
            cekTable = cipherTable;
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
        private SqlTceCipherInfoEntry? _sqlTceCipherInfoEntry;

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
        private SqlEncryptionKeyInfo? _sqlEncryptionKeyInfo;

        /// <summary>
        /// Ordinal (into the Cek Table).
        /// </summary>
        private readonly ushort _ordinal;

        /// <summary>
        /// Return the Encryption Info Entry.
        /// </summary>
        internal SqlTceCipherInfoEntry? EncryptionInfo
        {
            get
            {
                return _sqlTceCipherInfoEntry;
            }
            set
            {
                Debug.Assert(!_sqlTceCipherInfoEntry.HasValue, "We can only set the EncryptionInfo once.");
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
        /// Return the cipher encryption algorithm handle.
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
        internal SqlEncryptionKeyInfo? EncryptionKeyInfo
        {
            get
            {
                return _sqlEncryptionKeyInfo;
            }

            set
            {
                Debug.Assert(!_sqlEncryptionKeyInfo.HasValue, "_sqlEncryptionKeyInfo should not be set more than once.");
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
        internal SqlCipherMetadata(SqlTceCipherInfoEntry? sqlTceCipherInfoEntry,
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

    internal partial class SqlMetaDataPriv
    {
        internal bool isEncrypted; // TCE encrypted?
        internal SqlMetaDataPriv baseTI;   // for encrypted columns, represents the TYPE_INFO for plaintext value
        internal SqlCipherMetadata cipherMD; // Cipher related metadata for encrypted columns.

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
            Debug.Assert(cipherMetadata.EncryptionKeyInfo.HasValue, "cipherMetadata.EncryptionKeyInfo.HasValue should be true.");

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
            totalLength += _cipherMetadata.EncryptionKeyInfo.Value.cekMdVersion.Length;

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
            SerializeIntIntoBuffer(_cipherMetadata.EncryptionKeyInfo.Value.databaseId, serializedWireFormat, ref consumedBytes);

            // 4 - Write the id of the encryption key.
            SerializeIntIntoBuffer(_cipherMetadata.EncryptionKeyInfo.Value.cekId, serializedWireFormat, ref consumedBytes);

            // 5 - Write the version of the encryption key.
            SerializeIntIntoBuffer(_cipherMetadata.EncryptionKeyInfo.Value.cekVersion, serializedWireFormat, ref consumedBytes);

            // 6 - Write the metadata version of the encryption key.
            Buffer.BlockCopy(_cipherMetadata.EncryptionKeyInfo.Value.cekMdVersion, 0, serializedWireFormat, consumedBytes, _cipherMetadata.EncryptionKeyInfo.Value.cekMdVersion.Length);
            consumedBytes += _cipherMetadata.EncryptionKeyInfo.Value.cekMdVersion.Length;

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
}
