// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

        private readonly MemoryCache _cache;

        // One load gate per cache key, shared by sync and async callers. Concurrent misses for a
        // key wait for its owner and then reuse the published key, while different keys load in
        // parallel. User counts keep an entry alive until its owner and all waiters leave, so a
        // second gate is never created for an active key. Lock this dictionary to access it.
        private readonly Dictionary<string, KeyLoadGate> _keyLoadGates = new();

        private sealed class KeyLoadGate
        {
            internal readonly SemaphoreSlim Semaphore = new(1, 1);
            internal int Users;
        }

        private SymmetricKeyCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        public static SymmetricKeyCache Instance => s_singletonInstance;

        /// <summary>
        /// Retrieves a plaintext symmetric key for sync and async callers. When
        /// <paramref name="isAsync"/> is false, the gate and provider are called synchronously,
        /// the result is already complete, and callers use <c>.GetAwaiter().GetResult()</c>.
        /// </summary>
        /// <remarks>
        /// When caching is enabled, misses for the same key wait for its load and reuse the
        /// result, so sync and async callers share one decryption. Different keys and uncached
        /// loads do not wait for each other. Async gate waits observe
        /// <paramref name="cancellationToken"/>. Cache hits complete without allocating.
        /// </remarks>
        internal async ValueTask<SymmetricKey> GetKey(
            SqlEncryptionKeyInfo keyInfo,
            SqlConnection connection,
            SqlCommand? command,
            bool isAsync,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.Assert(connection.DataSource is not null, @"serverName should not be null.");
            string serverName = connection.DataSource!;
            string cacheLookupKey = CreateCacheLookupKey(keyInfo, serverName);

            if (TryGetCachedKey(cacheLookupKey, out SymmetricKey? cachedKey))
            {
                return cachedKey;
            }

            TimeSpan cacheTtl = SqlConnection.ColumnEncryptionKeyCacheTtl;
            if (cacheTtl == TimeSpan.Zero)
            {
                return await LoadKey(
                    keyInfo, connection, command, serverName, cacheLookupKey, cacheTtl,
                    isAsync, cancellationToken)
                    .ConfigureAwait(false);
            }

            KeyLoadGate gate = AddGateUser(cacheLookupKey);
            bool acquired = false;
            try
            {
                if (isAsync)
                {
                    await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    gate.Semaphore.Wait();
                }
                acquired = true;
                return await LoadKey(
                    keyInfo, connection, command, serverName, cacheLookupKey, cacheTtl,
                    isAsync, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                RemoveGateUser(cacheLookupKey, gate, acquired);
            }
        }

        /// <summary>
        /// Loads and caches a key. When <paramref name="isAsync"/> is false, the provider is
        /// called synchronously and the returned task is already complete.
        /// </summary>
        private async ValueTask<SymmetricKey> LoadKey(
            SqlEncryptionKeyInfo keyInfo,
            SqlConnection connection,
            SqlCommand? command,
            string serverName,
            string cacheLookupKey,
            TimeSpan cacheTtl,
            bool isAsync,
            CancellationToken cancellationToken)
        {
            // Another caller may have published this key while we waited for the gate.
            if (TryGetCachedKey(cacheLookupKey, out SymmetricKey? cachedKey))
            {
                return cachedKey;
            }

            SqlColumnEncryptionKeyStoreProvider provider =
                GetTrustedProvider(keyInfo, connection, command, serverName);
            cancellationToken.ThrowIfCancellationRequested();

            byte[] plaintextKey;
            try
            {
                // AKV provider registration supports multi-user scenarios, so it is not safe to
                // cache the CEK in the global provider. The CEK cache is global and shared across
                // all connections. To prevent conflicts, global providers must not use their own
                // CEK caches.
                provider.ColumnEncryptionKeyCacheTtl = TimeSpan.Zero;
                plaintextKey = isAsync
                    ? await provider.DecryptColumnEncryptionKeyAsync(
                        keyInfo.keyPath, keyInfo.algorithmName, keyInfo.encryptedKey,
                        cancellationToken)
                        .ConfigureAwait(false)
                    : provider.DecryptColumnEncryptionKey(
                        keyInfo.keyPath, keyInfo.algorithmName, keyInfo.encryptedKey);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw KeyDecryptionFailed(keyInfo, e);
            }

            // Cache a completed decryption even if cancellation arrived after the provider
            // returned.
            return CacheKey(cacheLookupKey, new SymmetricKey(plaintextKey), cacheTtl);
        }

        /// <summary>
        /// Verifies key path trust and resolves the provider.
        /// </summary>
        private static SqlColumnEncryptionKeyStoreProvider GetTrustedProvider(
            SqlEncryptionKeyInfo keyInfo,
            SqlConnection connection,
            SqlCommand? command,
            string serverName)
        {
            Debug.Assert(SqlConnection.ColumnEncryptionTrustedMasterKeyPaths is not null,
                @"SqlConnection.ColumnEncryptionTrustedMasterKeyPaths should not be null");

            // Verify key path trust before adding the key to the cache.
            SqlSecurityUtility.ThrowIfKeyPathIsNotTrustedForServer(serverName, keyInfo.keyPath);

            if (!SqlSecurityUtility.TryGetColumnEncryptionKeyStoreProvider(
                keyInfo.keyStoreName,
                out SqlColumnEncryptionKeyStoreProvider provider,
                connection,
                command))
            {
                throw SQL.UnrecognizedKeyStoreProviderName(keyInfo.keyStoreName,
                    SqlConnection.GetColumnEncryptionSystemKeyStoreProvidersNames(),
                    SqlSecurityUtility.GetListOfProviderNamesThatWereSearched(connection, command));
            }

            return provider;
        }

        private static Exception KeyDecryptionFailed(SqlEncryptionKeyInfo keyInfo, Exception inner)
        {
            string keyHex = SqlSecurityUtility.GetBytesAsString(
                keyInfo.encryptedKey, fLast: true, countOfBytes: 10);
            return SQL.KeyDecryptionFailed(keyInfo.keyStoreName, keyHex, inner);
        }

        private bool TryGetCachedKey(
            string cacheLookupKey, [NotNullWhen(true)] out SymmetricKey? key)
        {
            // A null key is never cached; the null check satisfies nullability analysis.
            return _cache.TryGetValue(cacheLookupKey, out key) && key is not null;
        }

        /// <summary>
        /// Caches a key loaded under its gate. Uncached loads (zero TTL) are returned as-is.
        /// </summary>
        private SymmetricKey CacheKey(string cacheLookupKey, SymmetricKey key, TimeSpan cacheTtl)
        {
            if (cacheTtl != TimeSpan.Zero)
            {
                _cache.Set(cacheLookupKey, key, absoluteExpirationRelativeToNow: cacheTtl);
            }
            return key;
        }

        private KeyLoadGate AddGateUser(string cacheLookupKey)
        {
            lock (_keyLoadGates)
            {
                if (!_keyLoadGates.TryGetValue(cacheLookupKey, out KeyLoadGate? gate))
                {
                    gate = new KeyLoadGate();
                    _keyLoadGates.Add(cacheLookupKey, gate);
                }
                gate.Users++;
                return gate;
            }
        }

        private void RemoveGateUser(string cacheLookupKey, KeyLoadGate gate, bool acquired)
        {
            if (acquired)
            {
                gate.Semaphore.Release();
            }

            lock (_keyLoadGates)
            {
                if (--gate.Users == 0)
                {
                    _keyLoadGates.Remove(cacheLookupKey);
                    gate.Semaphore.Dispose();
                }
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
