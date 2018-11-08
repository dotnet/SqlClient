// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

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
        internal byte normalizationRuleVersion;
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
        /// <param name="sqlClientEncryptionAlgorithm"></param>
        /// <param name="cipherAlgorithmId"></param>
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
}
