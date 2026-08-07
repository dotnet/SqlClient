// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted
{
    /// <summary>
    /// Implements a cache of Symmetric Keys (once they are decrypted). Useful for rapidly decrypting multiple data values.
    /// </summary>
    internal sealed class SymmetricKeyCache
    {
        private static readonly SymmetricKeyCache s_singletonInstance = new();
        private static readonly SemaphoreSlim s_cacheLock = new(1, 1);

        private readonly MemoryCache _cache;

        private SymmetricKeyCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        public static SymmetricKeyCache Instance => s_singletonInstance;

        /// <summary>
        /// Retrieves Symmetric Key (in plaintext) given the encryption material.
        /// </summary>
        public SymmetricKey GetKey(SqlEncryptionKeyInfo keyInfo, SqlConnection connection, SqlCommand? command)
        {
            string serverName = connection.DataSource;
            Debug.Assert(serverName is not null, @"serverName should not be null.");
            int capacity = serverName!.Length + SqlSecurityUtility.GetBase64LengthFromByteLength(keyInfo.encryptedKey.Length) + keyInfo.keyStoreName.Length + 2 /* separators */;
            StringBuilder cacheLookupKeyBuilder = new(serverName, capacity);

            cacheLookupKeyBuilder.Append(':');
            cacheLookupKeyBuilder.Append(Convert.ToBase64String(keyInfo.encryptedKey));
            cacheLookupKeyBuilder.Append(':');
            cacheLookupKeyBuilder.Append(keyInfo.keyStoreName);

            string cacheLookupKey = cacheLookupKeyBuilder.ToString();

            Debug.Assert(cacheLookupKey.Length <= capacity, "We needed to allocate a larger array");

            // Lookup the key in cache
            if (!(_cache.TryGetValue(cacheLookupKey, out SymmetricKey? encryptionKey))
                // A null cryptographic key is never added to the cache, but this null check satisfies the nullability warning.
                || encryptionKey is null)
            {
                // Acquire the lock to ensure thread safety when modifying the cache, and to guarantee that only one thread calls
                // DecryptColumnEncryptionKey on a user-provided SqlColumnEncryptionKeyStoreProvider at a time.
                s_cacheLock.Wait();

                try
                {
                    // Perform a second check to see if the key was added to the cache while waiting for the lock, to avoid redundant work.
                    if (!(_cache.TryGetValue(cacheLookupKey, out encryptionKey))
                        || encryptionKey is null)
                    {
                        Debug.Assert(SqlConnection.ColumnEncryptionTrustedMasterKeyPaths is not null, @"SqlConnection.ColumnEncryptionTrustedMasterKeyPaths should not be null");

                        SqlSecurityUtility.ThrowIfKeyPathIsNotTrustedForServer(serverName, keyInfo.keyPath);

                        // Key Not found, attempt to look up the provider and decrypt CEK
                        if (!SqlSecurityUtility.TryGetColumnEncryptionKeyStoreProvider(keyInfo.keyStoreName, out SqlColumnEncryptionKeyStoreProvider provider, connection, command))
                        {
                            throw SQL.UnrecognizedKeyStoreProviderName(keyInfo.keyStoreName,
                                    SqlConnection.GetColumnEncryptionSystemKeyStoreProvidersNames(),
                                    SqlSecurityUtility.GetListOfProviderNamesThatWereSearched(connection, command));
                        }

                        // Decrypt the CEK
                        // We will simply bubble up the exception from the DecryptColumnEncryptionKey function.
                        byte[] plaintextKey;
                        try
                        {
                            // AKV provider registration supports multi-user scenarios, so it is not safe to cache the CEK in the global provider.
                            // The CEK cache is a global cache, and is shared across all connections.
                            // To prevent conflicts between CEK caches, global providers should not use their own CEK caches
                            provider.ColumnEncryptionKeyCacheTtl = TimeSpan.Zero;
                            plaintextKey = provider.DecryptColumnEncryptionKey(keyInfo.keyPath, keyInfo.algorithmName, keyInfo.encryptedKey);
                        }
                        catch (Exception e)
                        {
                            // Generate a new exception and throw.
                            string keyHex = SqlSecurityUtility.GetBytesAsString(keyInfo.encryptedKey, fLast: true, countOfBytes: 10);
                            throw SQL.KeyDecryptionFailed(keyInfo.keyStoreName, keyHex, e);
                        }

                        encryptionKey = new SymmetricKey(plaintextKey);

                        // If the cache TTL is zero, don't even bother inserting to the cache.
                        if (SqlConnection.ColumnEncryptionKeyCacheTtl != TimeSpan.Zero)
                        {
                            // In case multiple threads reach here at the same time, the first one wins.
                            // The allocated memory will be reclaimed by Garbage Collector.
                            _cache.Set(cacheLookupKey, encryptionKey, absoluteExpirationRelativeToNow: SqlConnection.ColumnEncryptionKeyCacheTtl);
                        }
                    }
                }
                finally
                {
                    // Release the lock to allow other threads to access the cache
                    s_cacheLock.Release();
                }
            }

            return encryptionKey;
        }
    }
}
