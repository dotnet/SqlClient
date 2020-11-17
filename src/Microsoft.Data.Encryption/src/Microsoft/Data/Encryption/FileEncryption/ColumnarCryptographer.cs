using Microsoft.Data.Encryption.Cryptography;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;

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

            var splitBlock = new TransformManyBlock<IEnumerable<IColumn>, IColumn>(c => c);
            var transformBlock = new TransformBlock<IColumn, IColumn>(
                transform: column => TransformColumn(column, readerSettings[column.Index], writerSettings[column.Index]), 
                dataflowBlockOptions: new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 4 }
            );
            var collectorBlock = new BatchBlock<IColumn>(readerSettings.Count);
            var actionBlock = new ActionBlock<IEnumerable<IColumn>>(chunk => CryptoWriter.Write(chunk));

            splitBlock.LinkTo(transformBlock, new DataflowLinkOptions() { PropagateCompletion = true, });
            transformBlock.LinkTo(collectorBlock, new DataflowLinkOptions() { PropagateCompletion = true, });
            collectorBlock.LinkTo(actionBlock, new DataflowLinkOptions() { PropagateCompletion = true, });

            CryptoReader.Read().ForEach(chunk => splitBlock.Post(chunk));
            splitBlock.Complete();
            actionBlock.Completion.Wait();
        }

        private IColumn TransformColumn(IColumn column, FileEncryptionSettings readerSettings, FileEncryptionSettings writerSettings)
        {
            if (ShouldEncrypt(readerSettings, writerSettings))
            {
                return EncryptColumn(writerSettings, column);
            }
            else if (ShouldDecrypt(readerSettings, writerSettings))
            {
                return DecryptColumn(writerSettings, column);
            }
            else if (ShouldRotate(readerSettings, writerSettings))
            {
                return EncryptColumn(writerSettings, DecryptColumn(readerSettings, column));
            }
            else
            {
                return column;
            }
        }

        private static Column<byte[]> EncryptColumn(FileEncryptionSettings writerColumnSettings, IColumn column)
        {
            byte[][] encryptedData = column.Data
                .Encrypt(writerColumnSettings)
                .ToArray();

            return new Column<byte[]>(encryptedData) 
            {
                Name = column.Name,
                Index = column.Index
            };
        }

        private static IColumn DecryptColumn(FileEncryptionSettings writerColumnSettings, IColumn column)
        {
            IList encryptedData = column.Data
                .Decrypt(writerColumnSettings);

            Type type = encryptedData.GetType().GenericTypeArguments[0];
            IColumn encodedColumn = (IColumn)Activator.CreateInstance(typeof(Column<>).MakeGenericType(type), encryptedData);
            encodedColumn.Name = column.Name;
            encodedColumn.Index = column.Index;

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
