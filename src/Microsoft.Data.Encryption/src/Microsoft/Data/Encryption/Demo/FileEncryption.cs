using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using Microsoft.Data.Encryption.FileEncryption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static Microsoft.Data.Encryption.Demo.Program;

namespace Microsoft.Data.Encryption.Demo
{
    public static class ParquetFileEncryption
    {
        public static void Demo()
        {
            // open input and output file streams
            Stream inputFile = File.OpenRead(".\\ResourceFiles\\userdata1.parquet");
            Stream outputFile = File.OpenWrite(".\\ResourceFiles\\out1.parquet");

            // Create reader
            using ParquetFileReader reader = new ParquetFileReader(inputFile);

            // Copy source settings as target settings
            List<FileEncryptionSettings> writerSettings = reader.FileEncryptionSettings
                .Select(s => Copy(s))
                .ToList();

            // Modify a few column settings
            writerSettings[0] = new FileEncryptionSettings<DateTimeOffset?>(encryptionKey, SqlSerializerFactory.Default.GetDefaultSerializer<DateTimeOffset?>());
            writerSettings[3] = new FileEncryptionSettings<string>(encryptionKey, EncryptionType.Deterministic, new SqlVarcharSerializer(size: 255));
            writerSettings[10] = new FileEncryptionSettings<double?>(encryptionKey, StandardSerializerFactory.Default.GetDefaultSerializer<double?>());

            // Create and pass the target settings to the writer
            using ParquetFileWriter writer = new ParquetFileWriter(outputFile, writerSettings);

            // Process the file
            ColumnarCryptographer cryptographer = new ColumnarCryptographer(reader, writer);
            cryptographer.Transform();

            Console.Clear();
        }

        public static FileEncryptionSettings Copy(FileEncryptionSettings encryptionSettings)
        {
            Type genericType = encryptionSettings.GetType().GenericTypeArguments[0];
            Type settingsType = typeof(FileEncryptionSettings<>).MakeGenericType(genericType);
            return (FileEncryptionSettings)Activator.CreateInstance(
                settingsType,
                new object[] {
                    encryptionSettings.DataEncryptionKey,
                    encryptionSettings.EncryptionType,
                    encryptionSettings.GetSerializer()
                }
            );
        }
    }
}
