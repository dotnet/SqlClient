using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using Newtonsoft.Json;
using Parquet;
using Parquet.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Used to read data from a parquet file type.
    /// </summary>
    public class ParquetFileReader : IColumnarDataReader, IDisposable
    {
        private ParquetReader ParquetReader { get; set; }
        private Stream FileStream { get; set; }
        private IDictionary<string, EncryptionKeyStoreProvider> EncryptionKeyStoreProviders { get; set; }

        /// <inheritdoc/>
        public IList<FileEncryptionSettings> FileEncryptionSettings
        {
            get => GetEncryptionSettings();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParquetFileReader"/> class.
        /// </summary>
        /// <param name="file">The file from which to read.</param>
        public ParquetFileReader(Stream file)
        {
            file.ValidateNotNull(nameof(file));

            FileStream = file;
            ParquetReader = new ParquetReader(FileStream);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParquetFileReader"/> class.
        /// </summary>
        /// <param name="file">The file from which to read.</param>
        /// <param name="encryptionKeyStoreProviders">The <see cref="EncryptionKeyStoreProvider"/>s to register.</param>
        public ParquetFileReader(Stream file, IDictionary<string, EncryptionKeyStoreProvider> encryptionKeyStoreProviders) : this(file)
        {
            encryptionKeyStoreProviders.ValidateNotNull(nameof(encryptionKeyStoreProviders));
            encryptionKeyStoreProviders.Keys.ValidateNotEmpty(nameof(encryptionKeyStoreProviders.Keys));
            encryptionKeyStoreProviders.Keys.ValidateNotNullOrWhitespaceForEach(nameof(encryptionKeyStoreProviders.Keys));
            encryptionKeyStoreProviders.Values.ValidateNotNullForEach(nameof(encryptionKeyStoreProviders.Values));

            EncryptionKeyStoreProviders = encryptionKeyStoreProviders;
        }

        /// <inheritdoc/>
        public IEnumerable<IEnumerable<IColumn>> Read()
        {
            // enumerate through row groups in this file
            for (int i = 0; i < ParquetReader.RowGroupCount; i++)
            {
                List<IColumn> rowGroup = new List<IColumn>();
                Schema schema = ParquetReader.Schema;
                DataField[] data = schema.GetDataFields();

                // create row group reader
                using (ParquetRowGroupReader groupReader = ParquetReader.OpenRowGroupReader(i))
                {
                    // read all columns inside each row group (you have an option to read only
                    // required columns if you need to.
                    List<DataColumn> dataColumns = data.Select(groupReader.ReadColumn).ToList();

                    for (int j = 0; j < dataColumns.Count; j++)
                    {
                        DataColumn dataColumn = dataColumns[j];

                        Array columnData = dataColumn.Data;
                        Type dataType = columnData.GetType().GetElementType();

                        Type genericColumnType = typeof(Column<>).MakeGenericType(dataType);
                        IColumn column = (IColumn)Activator.CreateInstance(genericColumnType, columnData);
                        column.Name = dataColumn.Field.Name;

                        rowGroup.Add(column);
                    }
                }

                yield return rowGroup;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ParquetReader.Dispose();
        }

        private IList<FileEncryptionSettings> GetEncryptionSettings()
        {

            List<FileEncryptionSettings> encryptionSettings = new List<FileEncryptionSettings>();
            Schema schema = ParquetReader.Schema;
            DataField[] dataFields = schema.GetDataFields();

            Dictionary<int, FileEncryptionSettings> indexToEncryptionSettingsMap = GetindexToEncryptionSettingsMap();

            for (int i = 0; i < dataFields.Length; i++)
            {
                DataField dataField = dataFields[i];
                if (indexToEncryptionSettingsMap.ContainsKey(i))
                {
                    encryptionSettings.Add(indexToEncryptionSettingsMap[i]);
                }
                else
                {
                    Type genericType = GetFieldType(dataField);
                    object serializer = typeof(StandardSerializerFactory)
                        .GetMethod(nameof(StandardSerializerFactory.GetDefaultSerializer))
                        .MakeGenericMethod(genericType)
                        .Invoke(StandardSerializerFactory.Default, null);
                    Type settingsType = typeof(FileEncryptionSettings<>).MakeGenericType(genericType);
                    FileEncryptionSettings settings = (FileEncryptionSettings)Activator.CreateInstance(settingsType, null, serializer);
                    encryptionSettings.Add(settings);
                }
            }

            return encryptionSettings;
        }

        private Dictionary<int, FileEncryptionSettings> GetindexToEncryptionSettingsMap()
        {
            Dictionary<int, FileEncryptionSettings> indexToCryptoSettings = new Dictionary<int, FileEncryptionSettings>();
            CryptoMetadata metadata = GetCryptographyMetadata();

            if (!metadata.IsNull())
            {
                EncryptionKeyStoreProviders.ValidateNotNull(nameof(EncryptionKeyStoreProviders));

                foreach (ColumnEncryptionMetadata info in metadata.ColumnEncryptionInformation)
                {
                    string columnKeyName = info.DataEncryptionKeyName;
                    DataEncryptionKeyMetadata columnKeyInfo = metadata.DataEncryptionKeyInformation
                        .First(k => k.Name == columnKeyName);

                    string masterKeyName = columnKeyInfo.KeyEncryptionKeyName;
                    KeyEncryptionKeyMetadata masterKeyInfo = metadata.KeyEncryptionKeyInformation
                        .First(k => k.Name == masterKeyName);

                    ValidateContainsKey(EncryptionKeyStoreProviders, masterKeyInfo.KeyProvider);
                    EncryptionKeyStoreProvider provider = EncryptionKeyStoreProviders[masterKeyInfo.KeyProvider];

                    KeyEncryptionKey masterKey = new KeyEncryptionKey(masterKeyName, masterKeyInfo.KeyPath, provider);
                    DataEncryptionKey encryptionKey = ProtectedDataEncryptionKey.GetOrCreate(columnKeyName, masterKey, columnKeyInfo.EncryptedDataEncryptionKey.FromHexString());
                    ISerializer serializer = info.Serializer;
                    Type genericType = serializer.GetGenericType();
                    Type settingsType = typeof(FileEncryptionSettings<>).MakeGenericType(genericType);
                    FileEncryptionSettings settings = (FileEncryptionSettings)Activator.CreateInstance(settingsType, encryptionKey, info.EncryptionType, serializer);
                    indexToCryptoSettings[info.ColumnIndex] = settings;
                }
            }

            return indexToCryptoSettings;
        }

        /// <summary>
        /// Uses reflection to get the DataField's ClrType.
        /// </summary>
        /// <param name="dataField">The DataField on which to query.</param>
        /// <returns>The Type of data in the DataField.</returns>
        internal static Type GetFieldType(DataField dataField)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            PropertyInfo field = typeof(DataField).GetProperty("ClrNullableIfHasNullsType", bindFlags);
            return (Type)field.GetValue(dataField);
        }

        /// <summary>
        /// Parses the CryptoMetadata from the file metadata is it exists.
        /// </summary>
        /// <returns>The CryptoMetadata if it exists, null otherwise</returns>
        private CryptoMetadata GetCryptographyMetadata()
        {
            ParquetReader.CustomMetadata.TryGetValue(nameof(CryptoMetadata), out string json);

            if (json.IsNull())
            {
                return null;
            }

            SerializerConverter converter = new SerializerConverter();
            SqlSerializerFactory sqlFactory = new SqlSerializerFactory();

            StandardSerializerFactory standardFactory = new StandardSerializerFactory();
            converter = converter.build(sqlFactory).build(standardFactory);

            CryptoMetadata metadata = JsonConvert.DeserializeObject<CryptoMetadata>(json, new JsonSerializerSettings() { Converters = { converter } });
            return metadata;
        }

        private void ValidateContainsKey(IDictionary<string, EncryptionKeyStoreProvider> encryptionKeyStoreProviders, string providerName)
        {
            if (!encryptionKeyStoreProviders.ContainsKey(providerName))
            {
                throw new KeyNotFoundException($"This reader's registered encryption key store providers does not contain a provider named {providerName}");
            }
        }
    }
}
