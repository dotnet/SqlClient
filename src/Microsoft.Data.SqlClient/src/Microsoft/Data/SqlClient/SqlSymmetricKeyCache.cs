// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// <para> Implements a cache of Symmetric Keys (once they are decrypted).Useful for rapidly decrypting multiple data values.</para>
    /// </summary>
    internal sealed class SqlSymmetricKeyCache
    {
        private readonly MemoryCache _cache;
        private static readonly SqlSymmetricKeyCache _singletonInstance = new();
        private static SemaphoreSlim _cacheLock = new(1, 1);

        private SqlSymmetricKeyCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        internal static SqlSymmetricKeyCache GetInstance()
        {
            return _singletonInstance;
        }

        /// <summary>
        /// <para> Retrieves Symmetric Key (in plaintext) given the encryption material.</para>
        /// </summary>
        internal SqlClientSymmetricKey GetKey(SqlEncryptionKeyInfo keyInfo, SqlConnection connection, SqlCommand command)
        {
            string serverName = connection.DataSource;
            Debug.Assert(serverName is not null, @"serverName should not be null.");
            string cacheLookupKey = CreateCacheLookupKey(keyInfo, serverName);

            // Acquire the lock to ensure thread safety when accessing the cache
            _cacheLock.Wait();

            try
            {
                // Lookup the key in cache
                if (!(_cache.TryGetValue(cacheLookupKey, out SqlClientSymmetricKey encryptionKey)))
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
                        provider.ColumnEncryptionKeyCacheTtl = new TimeSpan(0);
                        plaintextKey = provider.DecryptColumnEncryptionKey(keyInfo.keyPath, keyInfo.algorithmName, keyInfo.encryptedKey);
                    }
                    catch (Exception e)
                    {
                        // Generate a new exception and throw.
                        string keyHex = SqlSecurityUtility.GetBytesAsString(keyInfo.encryptedKey, fLast: true, countOfBytes: 10);
                        throw SQL.KeyDecryptionFailed(keyInfo.keyStoreName, keyHex, e);
                    }

                    encryptionKey = new SqlClientSymmetricKey(plaintextKey);

                    // If the cache TTL is zero, don't even bother inserting to the cache.
                    if (SqlConnection.ColumnEncryptionKeyCacheTtl != TimeSpan.Zero)
                    {
                        // In case multiple threads reach here at the same time, the first one wins.
                        // The allocated memory will be reclaimed by Garbage Collector.
                        _cache.Set(cacheLookupKey, encryptionKey, absoluteExpirationRelativeToNow: SqlConnection.ColumnEncryptionKeyCacheTtl);
                    }
                }

                return encryptionKey;
            }
            finally
            {
                // Release the lock to allow other threads to access the cache
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// <para> Asynchronously retrieves Symmetric Key (in plaintext) given the encryption material.</para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>_cacheLock</c> is a process-wide gate that the synchronous <see cref="GetKey"/> path blocks on
        /// with <see cref="SemaphoreSlim.Wait()"/>. Holding it across the awaited provider call would therefore
        /// stall every synchronous caller's thread for the duration of that (potentially remote) call. To avoid
        /// that, this method follows the check-release-fetch-relock pattern required by FR-012: the gate is held
        /// only for the cache lookup and for the cache insertion, never across I/O.
        /// </para>
        /// <para>
        /// The consequence, accepted by FR-014, is that concurrent async callers that miss the cache for the
        /// same key may each decrypt it. That is weaker than the synchronous path, which serializes the fetch.
        /// The results are equivalent, and the first caller to publish its result wins: subsequent callers
        /// return the already-cached instance so that all callers observe the same key object.
        /// </para>
        /// </remarks>
        internal async Task<SqlClientSymmetricKey> GetKeyAsync(SqlEncryptionKeyInfo keyInfo, SqlConnection connection, SqlCommand command, CancellationToken cancellationToken)
        {
            string serverName = connection.DataSource;
            Debug.Assert(serverName is not null, @"serverName should not be null.");
            string cacheLookupKey = CreateCacheLookupKey(keyInfo, serverName);

            // Acquire the lock to ensure thread safety when accessing the cache, and release it before
            // performing any I/O so that synchronous callers are never blocked behind a remote call.
            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue(cacheLookupKey, out SqlClientSymmetricKey cachedKey))
                {
                    return cachedKey;
                }
            }
            finally
            {
                _cacheLock.Release();
            }

            Debug.Assert(SqlConnection.ColumnEncryptionTrustedMasterKeyPaths is not null, @"SqlConnection.ColumnEncryptionTrustedMasterKeyPaths should not be null");

            SqlSecurityUtility.ThrowIfKeyPathIsNotTrustedForServer(serverName, keyInfo.keyPath);

            // Key Not found, attempt to look up the provider and decrypt CEK
            if (!SqlSecurityUtility.TryGetColumnEncryptionKeyStoreProvider(keyInfo.keyStoreName, out SqlColumnEncryptionKeyStoreProvider provider, connection, command))
            {
                throw SQL.UnrecognizedKeyStoreProviderName(keyInfo.keyStoreName,
                        SqlConnection.GetColumnEncryptionSystemKeyStoreProvidersNames(),
                        SqlSecurityUtility.GetListOfProviderNamesThatWereSearched(connection, command));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Decrypt the CEK
            // We will simply bubble up the exception from the DecryptColumnEncryptionKeyAsync function.
            byte[] plaintextKey;
            try
            {
                // AKV provider registration supports multi-user scenarios, so it is not safe to cache the CEK in the global provider.
                // The CEK cache is a global cache, and is shared across all connections.
                // To prevent conflicts between CEK caches, global providers should not use their own CEK caches
                //
                // Unlike the sync path this assignment happens outside the gate, so two async callers may write
                // it concurrently. The write is idempotent (always TimeSpan.Zero), so the outcome is the same.
                provider.ColumnEncryptionKeyCacheTtl = new TimeSpan(0);
                plaintextKey = await provider
                    .DecryptColumnEncryptionKeyAsync(keyInfo.keyPath, keyInfo.algorithmName, keyInfo.encryptedKey, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is propagated unwrapped so the returned Task is cancelled rather than faulted.
                throw;
            }
            catch (Exception e)
            {
                // Generate a new exception and throw.
                string keyHex = SqlSecurityUtility.GetBytesAsString(keyInfo.encryptedKey, fLast: true, countOfBytes: 10);
                throw SQL.KeyDecryptionFailed(keyInfo.keyStoreName, keyHex, e);
            }

            SqlClientSymmetricKey decryptedKey = new SqlClientSymmetricKey(plaintextKey);

            // If the cache TTL is zero, don't even bother inserting to the cache.
            TimeSpan cacheTtl = SqlConnection.ColumnEncryptionKeyCacheTtl;
            if (cacheTtl == TimeSpan.Zero)
            {
                return decryptedKey;
            }

            // Deliberately not cancellable. The expensive, remote part of this method is already done; the
            // section below is in-memory only. Honouring cancellation here would throw away a completed key
            // decryption and leave the next caller to repeat it.
            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Another caller may have populated the entry while this one was decrypting. In that case the
                // first one wins, exactly as in the sync path, so that all callers observe the same key object.
                if (_cache.TryGetValue(cacheLookupKey, out SqlClientSymmetricKey concurrentlyCachedKey))
                {
                    return concurrentlyCachedKey;
                }

                _cache.Set(cacheLookupKey, decryptedKey, absoluteExpirationRelativeToNow: cacheTtl);
            }
            finally
            {
                _cacheLock.Release();
            }

            return decryptedKey;
        }

        /// <summary>
        /// Builds the cache lookup key for the given encryption material. Pure function shared by the sync
        /// and async lookup paths.
        /// </summary>
        private static string CreateCacheLookupKey(SqlEncryptionKeyInfo keyInfo, string serverName)
        {
            StringBuilder cacheLookupKeyBuilder = new(serverName, capacity: serverName.Length + SqlSecurityUtility.GetBase64LengthFromByteLength(keyInfo.encryptedKey.Length) + keyInfo.keyStoreName.Length + 2/*separators*/);

#if DEBUG
            int capacity = cacheLookupKeyBuilder.Capacity;
#endif //DEBUG

            cacheLookupKeyBuilder.Append(":");
            cacheLookupKeyBuilder.Append(Convert.ToBase64String(keyInfo.encryptedKey));
            cacheLookupKeyBuilder.Append(":");
            cacheLookupKeyBuilder.Append(keyInfo.keyStoreName);

            string cacheLookupKey = cacheLookupKeyBuilder.ToString();

#if DEBUG
            Debug.Assert(cacheLookupKey.Length <= capacity, "We needed to allocate a larger array");
#endif //DEBUG

            return cacheLookupKey;
        }
    }
}
