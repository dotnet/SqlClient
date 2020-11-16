using Microsoft.Data.Encryption.Cryptography;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <summary>
    /// Provides cryptographic transformations on columnar data files.
    /// </summary>
    public class ColumnarCryptographer
    {
        private IColumnarDataReader CryptoReader { get;  set; }
        private IColumnarDataWriter CryptoWriter { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnarCryptographer"></see> class 
        /// with a specified reader and writer.
        /// </summary>
        /// <param name="reader">The reader to read the data to transform in.</param>
        /// <param name="writer">The writer to write the transformed data out.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="reader"/> or <paramref name="writer"/> is null.
        /// </exception>
        public ColumnarCryptographer(IColumnarDataReader reader, IColumnarDataWriter writer)
        {
            reader.ValidateNotNull(nameof(reader));
            writer.ValidateNotNull(nameof(writer));

            CryptoReader = reader;
            CryptoWriter = writer;
        }

        /// <summary>
        /// Applies the appropriate  cryptographic transformations on each column of the file. 
        /// </summary>
        /// <remarks>
        /// The cryptographic operation performed for each column is determined by evaluating the
        /// differences between the reader and writer file encryption settings for the column. If there is no 
        /// difference, then no transformation is performed.
        /// </remarks>
        public void Transform()
        {
            IList<FileEncryptionSettings> readerSettings = CryptoReader.FileEncryptionSettings;
            IList<FileEncryptionSettings> writerSettings = CryptoWriter.FileEncryptionSettings;

            CryptoReader.Read()
                .Select(chunk => TransformChunk(readerSettings, writerSettings, chunk))
                .ForEach(encodedChunk => CryptoWriter.Write(encodedChunk));
        }

        private IList<IColumn> TransformChunk(IList<FileEncryptionSettings> readerSettings, IList<FileEncryptionSettings> writerSettings, IList<IColumn> sourceRowGroup)
        {
            List<IColumn> transformedColumns = new List<IColumn>();

            for (int index = 0; index < sourceRowGroup.Count; index++)
            {
                FileEncryptionSettings readerColumnSettings = readerSettings[index];
                FileEncryptionSettings writerColumnSettings = writerSettings[index];
                IColumn column = sourceRowGroup[index];

                if (ShouldEncrypt(readerColumnSettings, writerColumnSettings))
                {
                    transformedColumns.Add(EncryptColumn(writerColumnSettings, column));
                }
                else if (ShouldDecrypt(readerColumnSettings, writerColumnSettings))
                {
                    transformedColumns.Add(DecryptColumn(writerColumnSettings, column));
                }
                else if (ShouldRotate(readerColumnSettings, writerColumnSettings))
                {
                    transformedColumns.Add(EncryptColumn(writerColumnSettings, DecryptColumn(readerColumnSettings, column)));
                }
                else
                {
                    transformedColumns.Add(sourceRowGroup[index]);
                }
            }

            return transformedColumns;
        }

        private static Column<byte[]> EncryptColumn(FileEncryptionSettings writerColumnSettings, IColumn column)
        {
            byte[][] encryptedData = column.Data
                .Encrypt(writerColumnSettings)
                .ToArray();

            return new Column<byte[]>(encryptedData) 
            {
                Name = column.Name
            };
        }

        private static IColumn DecryptColumn(FileEncryptionSettings writerColumnSettings, IColumn column)
        {
            IList encryptedData = column.Data
                .Decrypt(writerColumnSettings);

            Type type = encryptedData.GetType().GenericTypeArguments[0];
            IColumn encodedColumn = (IColumn)Activator.CreateInstance(typeof(Column<>).MakeGenericType(type), encryptedData);
            encodedColumn.Name = column.Name;

            return encodedColumn;
        }

        private bool ShouldEncrypt(FileEncryptionSettings readerEncryptionSettings, FileEncryptionSettings writerEncryptionSettings)
        {
            return (readerEncryptionSettings != writerEncryptionSettings)
                && readerEncryptionSettings.EncryptionType == EncryptionType.Plaintext
                && writerEncryptionSettings.EncryptionType != EncryptionType.Plaintext;
        }

        private bool ShouldDecrypt(FileEncryptionSettings readerEncryptionSettings, FileEncryptionSettings writerEncryptionSettings)
        {
            return (readerEncryptionSettings != writerEncryptionSettings)
                && readerEncryptionSettings.EncryptionType != EncryptionType.Plaintext
                && writerEncryptionSettings.EncryptionType == EncryptionType.Plaintext;
        }

        private bool ShouldRotate(FileEncryptionSettings readerEncryptionSettings, FileEncryptionSettings writerEncryptionSettings)
        {
            return (readerEncryptionSettings != writerEncryptionSettings)
                && readerEncryptionSettings.EncryptionType != EncryptionType.Plaintext
                && writerEncryptionSettings.EncryptionType != EncryptionType.Plaintext
                && (HasDifferentDataEncryptionRootKey() || HasDifferentEncryptionType());

            bool HasDifferentDataEncryptionRootKey() => readerEncryptionSettings.DataEncryptionKey != writerEncryptionSettings.DataEncryptionKey
                && !readerEncryptionSettings.DataEncryptionKey.RootKeyEquals(writerEncryptionSettings.DataEncryptionKey);

            bool HasDifferentEncryptionType() => readerEncryptionSettings.EncryptionType != writerEncryptionSettings.EncryptionType;
        }
    }
}
