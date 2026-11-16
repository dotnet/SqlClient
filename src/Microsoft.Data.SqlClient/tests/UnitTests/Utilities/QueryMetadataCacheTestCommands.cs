// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Data.SqlClient.Parser;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities
{
    /// <summary>
    /// Builds commands whose encrypted parameter metadata is primed in the process-wide query
    /// metadata cache without opening a connection.
    /// </summary>
    internal static class QueryMetadataCacheTestCommands
    {
        private const string ProviderName = "TEST_METADATA_CACHE_PROVIDER";

        /// <summary>Creates a deterministic 256-bit root key for encryption tests.</summary>
        /// <param name="seed">Starting value for the key bytes.</param>
        /// <returns>Key material of the length required by AEAD_AES_256_CBC_HMAC_SHA256.</returns>
        internal static byte[] NewPlaintextKey(byte seed)
        {
            byte[] key = new byte[32];
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(seed + i);
            }
            return key;
        }

        /// <summary>
        /// Builds a command whose single parameter carries cipher metadata backed by unique key
        /// material and query text, so neither process-wide cache can collide with another test.
        /// </summary>
        /// <param name="provider">Connection-scoped provider that will load the parameter's key.</param>
        /// <returns>A command with uncached cipher metadata and an unopened connection.</returns>
        internal static SqlCommand NewCommandWithCipherMetadata(
            SqlColumnEncryptionKeyStoreProvider provider)
        {
            SqlConnection connection = new(
                "Data Source=async-ae-metadata-cache-test;Initial Catalog=async-ae-db;" +
                "Column Encryption Setting=Enabled");
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>
                {
                    { ProviderName, provider },
                });

            SqlCommand command = new($"SELECT * FROM T WHERE C = @p -- {Guid.NewGuid()}", connection);
            SqlParameter parameter = command.Parameters.AddWithValue("@p", 1);

            SqlTceCipherInfoEntry entry = new(ordinal: 0);
            entry.Add(
                encryptedKey: Guid.NewGuid().ToByteArray(),
                databaseId: 1,
                cekId: 1,
                cekVersion: 1,
                cekMdVersion: 1,
                keyPath: "metadata-cache-key-path",
                keyStoreName: ProviderName,
                algorithmName: "RSA_OAEP");

            parameter.CipherMetadata = new SqlCipherMetadata(
                entry,
                ordinal: 0,
                cipherAlgorithmId: TdsEnums.AEAD_AES_256_CBC_HMAC_SHA256,
                cipherAlgorithmName: null,
                encryptionType: (byte)EncryptionType.Deterministic,
                normalizationRuleVersion: 1);

            return command;
        }

        /// <summary>
        /// Primes the query metadata cache without loading the parameter's encryption key.
        /// </summary>
        /// <param name="provider">Connection-scoped provider for the subsequent cache completion.</param>
        /// <returns>A command with cached metadata and an unopened connection.</returns>
        internal static SqlCommand NewCachedCommand(SqlColumnEncryptionKeyStoreProvider provider)
        {
            SqlCommand command = NewCommandWithCipherMetadata(provider);
            SqlQueryMetadataCache.GetInstance().AddQueryMetadata(
                command, ignoreQueriesWithReturnValueParams: true);
            return command;
        }
    }
}
