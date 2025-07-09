// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Extensions.Caching.Memory;
using static System.Math;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    /// <summary>
    /// LocalCache is to reuse heavy objects.
    /// When performing a heavy creation operation, we will save the result in our cache container.
    /// The next time that we need that result, we will pull it from the cache container, instead of performing the heavy operation again.
    /// It is used for decrypting CEKs and verifying CMK metadata. Encrypted CEKs and signatures are different every time, even 
    /// when done with the same key, and should not be cached.
    /// </summary>
    internal class LocalCache<TKey, TValue>
    {
        /// <summary>
        /// A simple thread-safe implementation of an in-memory Cache.
        /// When the process dies, the cache dies with it.
        /// </summary>
        private readonly MemoryCache _cache;

        private readonly int _maxSize;

        /// <summary>
        /// Sets an absolute expiration time, relative to now.
        /// </summary>
        internal TimeSpan? TimeToLive { get; set; }

        /// <summary>
        /// Gets the count of the current entries for diagnostic purposes.
        /// </summary>
        internal int Count => _cache.Count;

        /// <summary>
        /// Constructs a new <see cref="LocalCache{TKey, TValue}">LocalCache</see> object.
        /// </summary>
        internal LocalCache(int maxSizeLimit = int.MaxValue)
        {
            if (maxSizeLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSizeLimit));
            }

            _maxSize = maxSizeLimit;
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Looks for the cache entry that maps to the <paramref name="key"/> value. If it exists (cache hit) it will simply be
        /// returned. Otherwise, the <paramref name="createItem"/> delegate function will be invoked to create the value. 
        /// It will then get stored it in the cache and set the time-to-live before getting returned.
        /// </summary>
        /// <param name="key">The key for the cache entry.</param>
        /// <param name="createItem">The delegate function that will create the cache entry if it does not exist.</param>
        /// <returns>The cache entry.</returns>
        internal TValue GetOrCreate(TKey key, Func<TValue> createItem)
        {
            if (TimeToLive <= TimeSpan.Zero)
            {
                AKVEventSource.Log.TryTraceEvent("Key caching found disabled, fetching key information.");
                return createItem();
            }

            if (!_cache.TryGetValue(key, out TValue cacheEntry))
            {
                AKVEventSource.Log.TryTraceEvent("Cached entry not found, creating new entry.");
                if (_cache.Count == _maxSize)
                {
                    _cache.Compact(Max(0.10, 1.0 / _maxSize));
                }

                cacheEntry = createItem();
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeToLive
                };

                _cache.Set(key, cacheEntry, cacheEntryOptions);
                AKVEventSource.Log.TryTraceEvent("Entry added to local cache.");
            }
            else
            {
                AKVEventSource.Log.TryTraceEvent("Cached entry found.");
            }

            return cacheEntry;
        }
    }
}
