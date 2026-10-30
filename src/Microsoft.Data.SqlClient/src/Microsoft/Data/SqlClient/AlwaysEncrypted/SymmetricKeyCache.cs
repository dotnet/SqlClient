// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly object _publicationLock = new();
        private readonly Dictionary<string, KeyLoadGate> _pendingKeyLoads = new();

        private sealed class KeyLoadGate
        {
            internal readonly SemaphoreSlim Semaphore = new(1, 1);
            // Count owners and waiters so removal cannot create a second gate for an active key.
            internal int Users;
        }

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
            string cacheLookupKey = CreateCacheLookupKey(keyInfo, serverName!);

            // Lookup the key in cache
            if (!_cache.TryGetValue(cacheLookupKey, out SymmetricKey? encryptionKey)
                // A null cryptographic key is never added to the cache, but this null check satisfies the nullability warning.
                || encryptionKey is null)
            {
                // Acquire the lock to ensure thread safety when modifying the cache, and to guarantee that only one thread calls
                // DecryptColumnEncryptionKey on a user-provided SqlColumnEncryptionKeyStoreProvider at a time.
                s_cacheLock.Wait();

                try
                {
                    // Perform a second check to see if the key was added to the cache while waiting for the lock, to avoid redundant work.
                    if (!_cache.TryGetValue(cacheLookupKey, out encryptionKey)
                        || encryptionKey is null)
                    {
                        Debug.Assert(SqlConnection.ColumnEncryptionTrustedMasterKeyPaths is not null, @"SqlConnection.ColumnEncryptionTrustedMasterKeyPaths should not be null");

                        // Verify key path trust before adding the key to the cache.
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
                            encryptionKey = PublishKey(cacheLookupKey, encryptionKey, SqlConnection.ColumnEncryptionKeyCacheTtl);
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

        /// <summary>
        /// Asynchronously retrieves a plaintext key, preserving cancellation and provider error wrapping.
        /// </summary>
        /// <remarks>
        /// Provider I/O runs outside the process-wide gate so synchronous callers are not blocked by it.
        /// Async misses for the same key wait for its pending load and recheck the cache.
        /// Different keys load independently; synchronous races retain first-published key identity.
        /// </remarks>
        internal async Task<SymmetricKey> GetKeyAsync(
            SqlEncryptionKeyInfo keyInfo,
            SqlConnection connection,
            SqlCommand? command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string serverName = connection.DataSource;
            Debug.Assert(serverName is not null, @"serverName should not be null.");
            string cacheLookupKey = CreateCacheLookupKey(keyInfo, serverName!);

            if (_cache.TryGetValue(cacheLookupKey, out SymmetricKey? cachedKey) && cachedKey is not null)
            {
                return cachedKey;
            }

            KeyLoadGate gate;
            lock (_publicationLock)
            {
                if (!_pendingKeyLoads.TryGetValue(cacheLookupKey, out gate!))
                {
                    gate = new KeyLoadGate();
                    _pendingKeyLoads.Add(cacheLookupKey, gate);
                }
                gate.Users++;
            }

            bool acquired = false;
            try
            {
                await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;
                cancellationToken.ThrowIfCancellationRequested();
                return await LoadKeyAsync(keyInfo, connection, command, cancellationToken, serverName!, cacheLookupKey)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (acquired)
                {
                    gate.Semaphore.Release();
                }

                lock (_publicationLock)
                {
                    if (--gate.Users == 0)
                    {
                        _pendingKeyLoads.Remove(cacheLookupKey);
                        gate.Semaphore.Dispose();
                    }
                }
            }
        }

        private async Task<SymmetricKey> LoadKeyAsync(
            SqlEncryptionKeyInfo keyInfo,
            SqlConnection connection,
            SqlCommand? command,
            CancellationToken cancellationToken,
            string serverName,
            string cacheLookupKey)
        {
            if (_cache.TryGetValue(cacheLookupKey, out SymmetricKey? cachedKey) && cachedKey is not null)
            {
                return cachedKey;
            }

            Debug.Assert(SqlConnection.ColumnEncryptionTrustedMasterKeyPaths is not null,
                @"SqlConnection.ColumnEncryptionTrustedMasterKeyPaths should not be null");
            SqlSecurityUtility.ThrowIfKeyPathIsNotTrustedForServer(serverName, keyInfo.keyPath);

            if (!SqlSecurityUtility.TryGetColumnEncryptionKeyStoreProvider(
                keyInfo.keyStoreName, out SqlColumnEncryptionKeyStoreProvider provider, connection, command))
            {
                throw SQL.UnrecognizedKeyStoreProviderName(keyInfo.keyStoreName,
                    SqlConnection.GetColumnEncryptionSystemKeyStoreProvidersNames(),
                    SqlSecurityUtility.GetListOfProviderNamesThatWereSearched(connection, command));
            }

            cancellationToken.ThrowIfCancellationRequested();

            byte[] plaintextKey;
            try
            {
                // Global providers must not keep a second CEK cache. Concurrent assignments are idempotent.
                provider.ColumnEncryptionKeyCacheTtl = TimeSpan.Zero;
                plaintextKey = await provider
                    .DecryptColumnEncryptionKeyAsync(keyInfo.keyPath, keyInfo.algorithmName, keyInfo.encryptedKey, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                string keyHex = SqlSecurityUtility.GetBytesAsString(keyInfo.encryptedKey, fLast: true, countOfBytes: 10);
                throw SQL.KeyDecryptionFailed(keyInfo.keyStoreName, keyHex, e);
            }

            SymmetricKey decryptedKey = new(plaintextKey);
            TimeSpan cacheTtl = SqlConnection.ColumnEncryptionKeyCacheTtl;
            if (cacheTtl == TimeSpan.Zero)
            {
                return decryptedKey;
            }

            // Publish completed decryption even if cancellation arrived after the provider returned.
            return PublishKey(cacheLookupKey, decryptedKey, cacheTtl);
        }

        /// <summary>
        /// Publishes one shared key instance under a short lock that never covers provider I/O.
        /// </summary>
        private SymmetricKey PublishKey(string cacheLookupKey, SymmetricKey decryptedKey, TimeSpan cacheTtl)
        {
            lock (_publicationLock)
            {
                if (_cache.TryGetValue(cacheLookupKey, out SymmetricKey? cachedKey) && cachedKey is not null)
                {
                    return cachedKey;
                }

                _cache.Set(cacheLookupKey, decryptedKey, absoluteExpirationRelativeToNow: cacheTtl);
                return decryptedKey;
            }
        }

        /// <summary>
        /// Builds the shared sync/async cache lookup key from the server and encrypted key material.
        /// </summary>
        private static string CreateCacheLookupKey(SqlEncryptionKeyInfo keyInfo, string serverName)
        {
            int capacity = serverName.Length + SqlSecurityUtility.GetBase64LengthFromByteLength(keyInfo.encryptedKey.Length) + keyInfo.keyStoreName.Length + 2 /* separators */;
            StringBuilder cacheLookupKeyBuilder = new(serverName, capacity);
            cacheLookupKeyBuilder.Append(':');
            cacheLookupKeyBuilder.Append(Convert.ToBase64String(keyInfo.encryptedKey));
            cacheLookupKeyBuilder.Append(':');
            cacheLookupKeyBuilder.Append(keyInfo.keyStoreName);

            string cacheLookupKey = cacheLookupKeyBuilder.ToString();
            Debug.Assert(cacheLookupKey.Length <= capacity, "We needed to allocate a larger array");
            return cacheLookupKey;
        }
    }
}
