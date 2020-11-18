using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Parquet;
using Parquet.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static Microsoft.Data.Encryption.FileEncryption.CryptoMetadata;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Used to write data to a parquet file type.
    /// </summary>
    public sealed class ParquetFileWriter : IColumnarDataWriter, IDisposable
    {
        private bool isMetadataWritten = false;
        private ParquetWriter ParquetWriter { get; set; }
        private Stream FileStream { get; set; }

        /// <inheritdoc/>
        public IList<FileEncryptionSettings> FileEncryptionSettings { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParquetFileWriter"/> class.
        /// </summary>
        /// <param name="file">The file on which to write.</param>
        /// <param name="encryptionSettings">The <see cref="FileEncryption.FileEncryptionSettings"/> 
        /// that are used to determine which transformation to perform on which column of data.
        /// </param>
        public ParquetFileWriter(Stream file, IList<FileEncryptionSettings> encryptionSettings)
        {
            file.ValidateNotNull(nameof(file));
            encryptionSettings.ValidateNotNull(nameof(encryptionSettings));
            encryptionSettings.ValidateNotEmpty(nameof(encryptionSettings));
            encryptionSettings.ValidateNotNullForEach(nameof(encryptionSettings));

            FileStream = file;
            FileEncryptionSettings = encryptionSettings;
        }

        /// <inheritdoc/>
        public void Write(IEnumerable<IColumn> columns)
        {
            List<DataColumn> parquetColumns = CreateParquetColumns(columns);

            if (ParquetWriter.IsNull())
            {
                List<DataField> parquetFields = parquetColumns.Select(p => p.Field).ToList();
                Schema schema = new Schema(parquetFields);
                ParquetWriter = new ParquetWriter(schema, FileStream);
            }

            if (!isMetadataWritten)
            {
                CryptoMetadata metadata = CompileMetadata(columns, FileEncryptionSettings);
                if (!metadata.IsEmpty())
                {
                    ParquetWriter.CustomMetadata = new Dictionary<string, string>
                    {
                        [nameof(CryptoMetadata)] = JsonConvert.SerializeObject(
                            value: metadata,
                            settings: new JsonSerializerSettings()
                            {
                                NullValueHandling = NullValueHandling.Ignore,
                                Converters = { new StringEnumConverter() },
                                Formatting = Formatting.Indented
                            })
                    };
                }

                isMetadataWritten = true;
            }

            using (ParquetRowGroupWriter groupWriter = ParquetWriter.CreateRowGroup())
            {
                parquetColumns.ForEach(groupWriter.WriteColumn);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ParquetWriter.Dispose();
        }

        private static List<DataColumn> CreateParquetColumns(IEnumerable<IColumn> columns)
        {
            var parquetColumns = new List<DataColumn>();

            foreach (IColumn column in columns)
            {
                parquetColumns.Add(new DataColumn(
                   new DataField(column.Name, column.DataType),
                   column.Data
                ));
            }

            return parquetColumns;
        }
    }
}
