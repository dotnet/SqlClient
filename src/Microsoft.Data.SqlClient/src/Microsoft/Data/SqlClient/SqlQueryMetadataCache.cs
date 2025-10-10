// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.Data.SqlClient
{

    /// <summary>
    /// <para> Implements a cache of query parameter metadata that is used to avoid the extra roundtrip to the server for every execution of the same query.</para>
    /// </summary>
    sealed internal class SqlQueryMetadataCache
    {
        const int CacheSize = 2000; // Cache size in number of entries.
        const int CacheTrimThreshold = 300; // Threshold above the cache size when we start trimming.

        private readonly MemoryCache _cache;
        private static readonly SqlQueryMetadataCache s_singletonInstance = new();
        private int _inTrim = 0;
        private long _cacheHits = 0;
        private long _cacheMisses = 0;

#if DEBUG
        private bool _sleepOnTrim = false;
#endif

        private SqlQueryMetadataCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        // @TODO: Replace with Instance property.
        internal static SqlQueryMetadataCache GetInstance()
        {
            return s_singletonInstance;
        }

        /// <summary>
        /// <para> Retrieves the query metadata for a specific query from the cache.</para>
        /// </summary>
        internal bool GetQueryMetadataIfExists(SqlCommand sqlCommand)
        {
            // Return immediately if caching is disabled.
            if (!SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled)
            {
                return false;
            }

            // Check the cache to see if we have the MD for this query cached.
            (string cacheLookupKey, string enclaveLookupKey) = GetCacheLookupKeysFromSqlCommand(sqlCommand);
            if (cacheLookupKey is null)
            {
                IncrementCacheMisses();
                return false;
            }

            Dictionary<string, SqlCipherMetadata> cipherMetadataDictionary = _cache.Get<Dictionary<string, SqlCipherMetadata>>(cacheLookupKey);

            // If we had a cache miss just return false.
            if (cipherMetadataDictionary is null)
            {
                IncrementCacheMisses();
                return false;
            }

            // Iterate over all the parameters and try to get their cipher MD.
            foreach (SqlParameter param in sqlCommand.Parameters)
            {
                bool found = cipherMetadataDictionary.TryGetValue(param.GetPrefixedParameterName(), out SqlCipherMetadata paramCiperMetadata);

                // If we failed to identify the encryption for a specific parameter, clear up the cipher MD of all parameters and exit.
                if (!found)
                {
                    foreach (SqlParameter paramToCleanup in sqlCommand.Parameters)
                    {
                        paramToCleanup.CipherMetadata = null;
                    }

                    IncrementCacheMisses();
                    return false;
                }

                // Cached cipher MD should never have an initialized algorithm since this would contain the key.
                Debug.Assert(paramCiperMetadata is null || !paramCiperMetadata.IsAlgorithmInitialized());

                // We were able to identify the cipher MD for this parameter, so set it on the param.
                param.CipherMetadata = paramCiperMetadata;
            }

            // Create a copy of the cipherMD in order to load the key.
            // The key shouldn't be loaded in the cached version for security reasons.
            foreach (SqlParameter param in sqlCommand.Parameters)
            {
                SqlCipherMetadata cipherMdCopy = null;

                if (param.CipherMetadata is not null)
                {
                    cipherMdCopy = new SqlCipherMetadata(
                        param.CipherMetadata.EncryptionInfo,
                        0,
                        param.CipherMetadata.CipherAlgorithmId,
                        param.CipherMetadata.CipherAlgorithmName,
                        param.CipherMetadata.EncryptionType,
                        param.CipherMetadata.NormalizationRuleVersion);
                }

                param.CipherMetadata = cipherMdCopy;

                if (cipherMdCopy is not null)
                {
                    // Try to get the encryption key. If the key information is stale, this might fail.
                    // In this case, just fail the cache lookup.
                    try
                    {
                        SqlSecurityUtility.DecryptSymmetricKey(cipherMdCopy, sqlCommand.Connection, sqlCommand);
                    }
                    catch (Exception ex)
                    {
                        // Invalidate the cache entry.
                        InvalidateCacheEntry(sqlCommand);

                        // If we get one of the expected exceptions, just fail the cache lookup, otherwise throw.
                        if (ex is SqlException || ex is ArgumentException || ex is ArgumentNullException)
                        {
                            foreach (SqlParameter paramToCleanup in sqlCommand.Parameters)
                            {
                                paramToCleanup.CipherMetadata = null;
                            }

                            IncrementCacheMisses();
                            return false;
                        }

                        throw;
                    }
                }
            }

            ConcurrentDictionary<int, SqlTceCipherInfoEntry> enclaveKeys = 
                _cache.Get<ConcurrentDictionary<int, SqlTceCipherInfoEntry>>(enclaveLookupKey);
            if (enclaveKeys is not null)
            {
                sqlCommand.keysToBeSentToEnclave = CreateCopyOfEnclaveKeys(enclaveKeys);
            }

            IncrementCacheHits();
            return true;
        }

        /// <summary>
        /// <para> Add the metadata for a specific query to the cache.</para>
        /// </summary>
        internal void AddQueryMetadata(SqlCommand sqlCommand, bool ignoreQueriesWithReturnValueParams)
        {
            // Return immediately if caching is disabled.
            if (!SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled)
            {
                return;
            }

            // We don't want to cache parameter metadata for commands with ReturnValue because there is no way for the client to verify that the cached information is still valid.
            // ReturnStatus is fine because it is always plaintext, but we cannot distinguish between the two at RPC time (they are both ReturnValue parameters), but only when the TDS tokens with the result come back.
            // Therefore we want to postpone populating the cache for any queries that have a ReturnValue parameter until we get the return tokens from TDS.
            // Check if we have a ReturnValue parameter and simply exit unless the caller wants to include queries with return values.
            // Only stored procs can have a real ReturnValue so just check for these.
            if (sqlCommand.CommandType == CommandType.StoredProcedure)
            {
                foreach (SqlParameter param in sqlCommand.Parameters)
                {
                    // If we have a return value parameter don't cache the query MD.
                    // We will cache it after we have confirmed it is looking for ReturnStatus and not ReturnValue.
                    if (param.Direction == ParameterDirection.ReturnValue && ignoreQueriesWithReturnValueParams)
                    {
                        sqlCommand.CachingQueryMetadataPostponed = true;
                        return;
                    }
                }
            }

            // Construct the entry and put it in the cache.
            (string cacheLookupKey, string enclaveLookupKey) = GetCacheLookupKeysFromSqlCommand(sqlCommand);
            if (cacheLookupKey is null)
            {
                return;
            }

            Dictionary<string, SqlCipherMetadata> cipherMetadataDictionary = new(sqlCommand.Parameters.Count);

            // Create a copy of the cipherMD that doesn't have the algorithm and put it in the cache.
            foreach (SqlParameter param in sqlCommand.Parameters)
            {
                SqlCipherMetadata cipherMdCopy = null;
                if (param.CipherMetadata is not null)
                {
                    cipherMdCopy = new SqlCipherMetadata(
                        param.CipherMetadata.EncryptionInfo,
                        0,
                        param.CipherMetadata.CipherAlgorithmId,
                        param.CipherMetadata.CipherAlgorithmName,
                        param.CipherMetadata.EncryptionType,
                        param.CipherMetadata.NormalizationRuleVersion);
                }

                // Cached cipher MD should never have an initialized algorithm since this would contain the key.
                Debug.Assert(cipherMdCopy is null || !cipherMdCopy.IsAlgorithmInitialized());

                cipherMetadataDictionary.Add(param.GetPrefixedParameterName(), cipherMdCopy);
            }

            // If the size of the cache exceeds the threshold, set that we are in trimming and trim the cache accordingly.
            long currentCacheSize = _cache.Count;
            if ((currentCacheSize > CacheSize + CacheTrimThreshold) && (0 == Interlocked.CompareExchange(ref _inTrim, 1, 0)))
            {
                try
                {
#if DEBUG
                    if (_sleepOnTrim)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
#endif
                    _cache.Compact((int)(((double)(currentCacheSize - CacheSize) / (double)currentCacheSize) * 100));
                }
                finally
                {
                    Interlocked.CompareExchange(ref _inTrim, 0, 1);
                }
            }

            // By default evict after 10 hours.
            MemoryCacheEntryOptions options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10)
            };
            _cache.Set<Dictionary<string, SqlCipherMetadata>>(cacheLookupKey, cipherMetadataDictionary, options);
            if (sqlCommand.requiresEnclaveComputations)
            {
                ConcurrentDictionary<int, SqlTceCipherInfoEntry> keysToBeCached = CreateCopyOfEnclaveKeys(sqlCommand.keysToBeSentToEnclave);
                _cache.Set<ConcurrentDictionary<int, SqlTceCipherInfoEntry>>(enclaveLookupKey, keysToBeCached, options);
            }
        }

        /// <summary>
        /// <para> Remove the metadata for a specific query from the cache.</para>
        /// </summary>
        internal void InvalidateCacheEntry(SqlCommand sqlCommand)
        {
            (string cacheLookupKey, string enclaveLookupKey) = GetCacheLookupKeysFromSqlCommand(sqlCommand);
            if (cacheLookupKey is null)
            {
                return;
            }

            _cache.Remove(cacheLookupKey);
            _cache.Remove(enclaveLookupKey);
        }


        /// <summary>
        /// Increments the counter for the cache hits in the query metadata cache.
        /// </summary>
        private void IncrementCacheHits()
        {
            Interlocked.Increment(ref _cacheHits);
        }

        /// <summary>
        /// Increments the counter for the cache misses in the query metadata cache.
        /// </summary>
        private void IncrementCacheMisses()
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        private (string, string) GetCacheLookupKeysFromSqlCommand(SqlCommand sqlCommand)
        {
            const int SqlIdentifierLength = 128;

            SqlConnection connection = sqlCommand.Connection;

            // Return null if we have no connection.
            if (connection is null)
            {
                return (null, null);
            }

            StringBuilder cacheLookupKeyBuilder = new(connection.DataSource, capacity: connection.DataSource.Length + SqlIdentifierLength + sqlCommand.CommandText.Length + 6);
            cacheLookupKeyBuilder.Append(":::");
            // Pad database name to 128 characters to avoid any false cache matches because of weird DB names.
            cacheLookupKeyBuilder.Append(connection.Database.PadRight(SqlIdentifierLength));
            cacheLookupKeyBuilder.Append(":::");
            cacheLookupKeyBuilder.Append(sqlCommand.CommandText);

            string cacheLookupKey = cacheLookupKeyBuilder.ToString();
            string enclaveLookupKey = cacheLookupKeyBuilder.Append(":::enclaveKeys").ToString();
            return (cacheLookupKey, enclaveLookupKey);
        }

        private ConcurrentDictionary<int, SqlTceCipherInfoEntry> CreateCopyOfEnclaveKeys(ConcurrentDictionary<int, SqlTceCipherInfoEntry> keysToBeSentToEnclave)
        {
            ConcurrentDictionary<int, SqlTceCipherInfoEntry> enclaveKeys = new();
            foreach (KeyValuePair<int, SqlTceCipherInfoEntry> kvp in keysToBeSentToEnclave)
            {
                int ordinal = kvp.Key;
                SqlTceCipherInfoEntry original = kvp.Value;
                SqlTceCipherInfoEntry copy = new(ordinal);
                foreach (SqlEncryptionKeyInfo cekInfo in original.ColumnEncryptionKeyValues)
                {
                    copy.Add(cekInfo.encryptedKey, cekInfo.databaseId, cekInfo.cekId, cekInfo.cekVersion,
                            cekInfo.cekMdVersion, cekInfo.keyPath, cekInfo.keyStoreName, cekInfo.algorithmName);
                }
                enclaveKeys.TryAdd(ordinal, copy);
            }
            return enclaveKeys;
        }
    }
}
