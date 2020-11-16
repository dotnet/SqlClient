using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.Encryption.FileEncryption;
using System;
using System.IO;
using System.Linq;
using Xunit;

using static Microsoft.Data.Encryption.FileEncryption.FileEncryptionSettings;
using static Microsoft.Data.CommonTestUtilities.DataTestUtility;
using System.Collections.Generic;

namespace Microsoft.Data.Encryption.FileEncryptionTests
{
    public class ParquetFileReaderShould
    {
        [Fact]
        public void EncryptParquetFileCorrectly()
        {
            using Stream inputFile = File.OpenRead("ResourceFiles\\plaintext.parquet");
            using Stream outputFile = File.OpenWrite($"ResourceFiles\\{nameof(EncryptParquetFileCorrectly)}_out.parquet");
            using ParquetFileReader reader = new ParquetFileReader(inputFile);

            reader.RegisterKeyStoreProviders(
                new Dictionary<string, EncryptionKeyStoreProvider> { [azureKeyProvider.ProviderName] = azureKeyProvider }
            );

            var writerSettings = reader.FileEncryptionSettings
                .Select(s => (FileEncryptionSettings)s.Clone())
                .ToList();

            var sourceColumnTypes = reader.FileEncryptionSettings
                .Select(s => s.GetType().GetGenericArguments()[0])
                .ToList();

            writerSettings[0] = Create(sourceColumnTypes[0], dataEncryptionKey, GetSerializer(sourceColumnTypes[0]));
            writerSettings[3] = Create(sourceColumnTypes[3], dataEncryptionKey, new SqlVarcharSerializer(255, 65001));
            writerSettings[10] = Create(sourceColumnTypes[10], dataEncryptionKey, GetSerializer(sourceColumnTypes[10]));

            using ParquetFileWriter writer = new ParquetFileWriter(outputFile, writerSettings);

            ColumnarCryptographer cryptographer = new ColumnarCryptographer(reader, writer);
            cryptographer.Transform();
        }

        [Fact]
        public void DecryptParquetFileCorrectly()
        {
            using Stream inputFile = File.OpenRead("ResourceFiles\\ciphertext.parquet");
            using Stream outputFile = File.OpenWrite($"ResourceFiles\\{nameof(DecryptParquetFileCorrectly)}_out.parquet");
            using ParquetFileReader reader = new ParquetFileReader(inputFile);

            reader.RegisterKeyStoreProviders(
                new Dictionary<string, EncryptionKeyStoreProvider> { [azureKeyProvider.ProviderName] = azureKeyProvider }
            );

            var writerSettings = reader.FileEncryptionSettings
                .Select(s => (FileEncryptionSettings)s.Clone())
                .ToList();

            var targetColumnTypes = reader.FileEncryptionSettings
                .Select(s => s.GetSerializer().GetGenericType())
                .ToList();

            writerSettings[0] = Create(targetColumnTypes[0], dataEncryptionKey, EncryptionType.Plaintext, GetSerializer(targetColumnTypes[0]));
            writerSettings[3] = Create(targetColumnTypes[3], dataEncryptionKey, EncryptionType.Plaintext, GetSerializer(targetColumnTypes[3]));
            writerSettings[10] = Create(targetColumnTypes[10], dataEncryptionKey, EncryptionType.Plaintext, GetSerializer(targetColumnTypes[10]));

            using ParquetFileWriter writer = new ParquetFileWriter(outputFile, writerSettings);

            ColumnarCryptographer cryptographer = new ColumnarCryptographer(reader, writer);
            cryptographer.Transform();
        }

        private object GetSerializer(Type genericType)
        {
            return typeof(SqlSerializerFactory)
                        .GetMethod(nameof(SqlSerializerFactory.GetDefaultSerializer))
                        .MakeGenericMethod(genericType)
                        .Invoke(SqlSerializerFactory.Default, null);
        }
    }
}
