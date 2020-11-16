using Microsoft.Data.Encryption.Cryptography;
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
        /// <inheritdoc/>
        public IList<FileEncryptionSettings> FileEncryptionSettings { get; private set; }

        private Stream FileStream { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParquetFileWriter"/> class.
        /// </summary>
        /// <param name="file">The file on which to write.</param>
        /// <param name="encryptionSettings">The <see cref="FileEncryption.FileEncryptionSettings"/> 
        /// that are used to determine which transformation to perform on which column of data.
        /// </param>
        public ParquetFileWriter(Stream file, IList<FileEncryptionSettings> encryptionSettings)
        {
            FileStream = file;
            FileEncryptionSettings = encryptionSettings;
        }

        /// <inheritdoc/>
        public void Write(IList<IColumn> columns)
        {
            List<DataColumn> parquetColumns = CreateParquetColumns(columns);
            List<DataField> parquetFields = parquetColumns.Select(p => p.Field).ToList();
            Schema schema = new Schema(parquetFields);

            using (var parquetWriter = new ParquetWriter(schema, FileStream))
            {
                // TODO - Write is called many times; one for each rowgroup in the file. We do not need to compile 
                // and write metadata many times. Refactor to write metadata only once.
                CryptoMetadata metadata = CompileMetadata(columns, FileEncryptionSettings);
                if (!metadata.IsEmpty())
                {
                    parquetWriter.CustomMetadata = new Dictionary<string, string>
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

                // create a new row group in the file
                using (ParquetRowGroupWriter groupWriter = parquetWriter.CreateRowGroup())
                {
                    parquetColumns.ForEach(groupWriter.WriteColumn);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            FileStream.Dispose();
        }

        private static List<DataColumn> CreateParquetColumns(IList<IColumn> columns)
        {
            var parquetColumns = new List<DataColumn>();

            foreach (var column in columns)
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
