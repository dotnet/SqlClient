using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Represents the metadata related to a file's encryption.
    /// </summary>
    public class CryptoMetadata
    {
        /// <summary>
        /// Represents the metadata related to a file's encrypted columns.
        /// </summary>
        public HashSet<ColumnEncryptionMetadata> ColumnEncryptionInformation { get; set; } = new HashSet<ColumnEncryptionMetadata>();

        /// <summary>
        /// Represents the metadata related to a file's data encryption keys.
        /// </summary>
        public HashSet<DataEncryptionKeyMetadata> DataEncryptionKeyInformation { get; set; } = new HashSet<DataEncryptionKeyMetadata>();

        /// <summary>
        /// Represents the metadata related to a file's key encryption keys.
        /// </summary>
        public HashSet<KeyEncryptionKeyMetadata> KeyEncryptionKeyInformation { get; set; } = new HashSet<KeyEncryptionKeyMetadata>();

        /// <summary>
        /// Determines whether the ColumnEncryptionInformation contains any elements.
        /// </summary>
        /// <returns></returns>
        public bool IsEmpty() => !ColumnEncryptionInformation.Any();

        /// <summary>
        /// Compiles a <see cref="CryptoMetadata"/> object from <see cref="List{T}"/>s of <see cref="IColumn"/>s and <see cref="FileEncryptionSettings"/>. 
        /// </summary>
        /// <param name="columns">The <see cref="IColumn"/>s on which to compile metadata.</param>
        /// <param name="encryptionSettings">The <see cref="FileEncryptionSettings"/> on which to compile metadata.</param>
        /// <returns></returns>
        public static CryptoMetadata CompileMetadata(IEnumerable<IColumn> columns, IList<FileEncryptionSettings> encryptionSettings)
        {
            if (columns.Count() != encryptionSettings.Count)
            {
                throw new ArgumentException($"{nameof(columns)}.Count does not equal {nameof(encryptionSettings)}.Count");
            }

            CryptoMetadata cryptoMetadata = new CryptoMetadata();
            int columnIndex = 0;

            foreach (IColumn column in columns)
            {
                FileEncryptionSettings settings = encryptionSettings[columnIndex];

                if (settings.EncryptionType != EncryptionType.Plaintext)
                {
                    ColumnEncryptionMetadata columnEncryptionInformation = new ColumnEncryptionMetadata()
                    {
                        ColumnName = column.Name,
                        DataEncryptionKeyName = settings.DataEncryptionKey.Name,
                        ColumnIndex = encryptionSettings.IndexOf(settings),
                        EncryptionAlgorithm = DataEncryptionKeyAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256,
                        EncryptionType = settings.EncryptionType,
                        Serializer = settings.GetSerializer()
                    };

                    DataEncryptionKeyMetadata columnKeyInformation = new DataEncryptionKeyMetadata()
                    {
                        KeyEncryptionKeyName = settings.DataEncryptionKey.KeyEncryptionKey.Name,
                        EncryptedDataEncryptionKey = settings.DataEncryptionKey.EncryptedValue.ToHexString(),
                        Name = settings.DataEncryptionKey.Name
                    };

                    KeyEncryptionKeyMetadata columnMasterKeyInformation = new KeyEncryptionKeyMetadata()
                    {
                        KeyPath = settings.DataEncryptionKey.KeyEncryptionKey.Path,
                        KeyProvider = settings.DataEncryptionKey.KeyEncryptionKey.KeyStoreProvider.ProviderName,
                        Name = settings.DataEncryptionKey.KeyEncryptionKey.Name
                    };

                    cryptoMetadata.ColumnEncryptionInformation.Add(columnEncryptionInformation);
                    cryptoMetadata.DataEncryptionKeyInformation.Add(columnKeyInformation);
                    cryptoMetadata.KeyEncryptionKeyInformation.Add(columnMasterKeyInformation);
                }

                columnIndex++;
            }

            return cryptoMetadata;
        }
    }

    /// <summary>
    /// Represents the metadata related to a file's column encryption.
    /// </summary>
    public class ColumnEncryptionMetadata
    {
        /// <summary>
        /// The column's name.
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// The column's index.
        /// </summary>
        public int ColumnIndex { get; set; }

        /// <summary>
        /// The name of the data encryption key protecting the column.
        /// </summary>
        public string DataEncryptionKeyName { get; set; }

        /// <summary>
        /// The encryption algorithm used to protect the column.
        /// </summary>
        public DataEncryptionKeyAlgorithm EncryptionAlgorithm { get; set; }

        /// <summary>
        /// The encryption type used to protect the column.
        /// </summary>
        public EncryptionType EncryptionType { get; set; }

        /// <summary>
        /// The Serializer used to protect the column
        /// </summary>
        public ISerializer Serializer { get; set; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            ColumnEncryptionMetadata c = obj as ColumnEncryptionMetadata;
            return !c.IsNull() && ColumnName == c.ColumnName && ColumnIndex == c.ColumnIndex;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (ColumnName.IsNull() ? 0 : ColumnName.GetHashCode())
                ^ (ColumnIndex.IsNull() ? 0 : ColumnIndex.GetHashCode());
        }
    }

    /// <summary>
    /// Represents the metadata related to a file's data encryption keys.
    /// </summary>
    public class DataEncryptionKeyMetadata
    {
        /// <summary>
        /// The name of the data encryption key.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The encrypted data encryption key.
        /// </summary>
        public string EncryptedDataEncryptionKey { get; set; }

        /// <summary>
        /// The key encryption key protecting this data encryption key
        /// </summary>
        public string KeyEncryptionKeyName { get; set; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            DataEncryptionKeyMetadata c = obj as DataEncryptionKeyMetadata;
            return !c.IsNull() && Name == c.Name;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Name.IsNull() ? 0 : Name.GetHashCode();
    }

    /// <summary>
    /// Represents the metadata related to a file's key encryption keys.
    /// </summary>
    public class KeyEncryptionKeyMetadata
    {
        /// <summary>
        /// The name of this key encryption key.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The key provider name.
        /// </summary>
        public string KeyProvider { get; set; }

        /// <summary>
        /// the path to the key encryption key.
        /// </summary>
        public string KeyPath { get; set; }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            KeyEncryptionKeyMetadata c = obj as KeyEncryptionKeyMetadata;
            return !c.IsNull() && Name == c.Name;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Name.IsNull() ? 0 : Name.GetHashCode();
    }
}
