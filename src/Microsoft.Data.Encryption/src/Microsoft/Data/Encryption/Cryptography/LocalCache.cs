using Microsoft.Extensions.Caching.Memory;
using System;

using static System.Math;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// LocalCache is to reuse heavy objects.
    /// When performing a heavy creation operation, we will save the result in our cache container.
    /// The next time that we need that result, we will pull it from the cache container, instead of performing the heavy operation again.
    /// </summary>
    internal class LocalCache<TKey, TValue>
    {
        /// <summary>
        /// A simple thread-safe implementation of an in-memory Cache.
        /// When the process dies, the cache dies with it.
        /// </summary>
        private readonly MemoryCache cache;

        private readonly int maxSize;

        /// <summary>
        /// Sets an absolute expiration time, relative to now.
        /// </summary>
        internal TimeSpan? TimeToLive { get; set; }

        /// <summary>
        /// Gets the count of the current entries for diagnostic purposes.
        /// </summary>
        internal int Count => cache.Count;

        /// <summary>
        /// Constructs a new <see cref="LocalCache{TKey, TValue}">LocalCache</see> object.
        /// </summary>
        internal LocalCache(int maxSizeLimit = int.MaxValue)
        {
            maxSizeLimit.ValidatePositive(nameof(maxSize));

            maxSize = maxSizeLimit;
            cache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Looks for the cache entry that maps to the <paramref name="key"/> value. If it exists (cache hit) it will simply be
        /// returned. Otherwise, the <paramref name="createItem"/> delegate function will be invoked to create the value. 
        /// It will then get stored it in the cache and set the time-to-live before getting returned.
        /// </summary>
        /// <param name="key">The encrypted key bytes.</param>
        /// <param name="createItem">The delegate function that will decrypt the encrypted column encryption key.</param>
        /// <returns>The decrypted column encryption key.</returns>
        internal TValue GetOrCreate(TKey key, Func<TValue> createItem)
        {
            if (!cache.TryGetValue(key, out TValue cacheEntry))
            {
                if (cache.Count == maxSize)
                {
                    cache.Compact(Max(0.10, 1.0 / maxSize));
                }

                cacheEntry = createItem();
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeToLive
                };
                cache.Set(key, cacheEntry, cacheEntryOptions);
            }

            return cacheEntry;
        }

        /// <summary>
        /// Determines whether the <see cref="LocalCache{TKey, TValue}">LocalCache</see> contains the specified key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal bool Contains(TKey key)
        {
            return cache.TryGetValue(key, out _);
        }
    }
}
