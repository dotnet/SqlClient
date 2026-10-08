// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using static System.Math;
using Microsoft.Data.SqlClient.Internal;

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
        /// Gates concurrent asynchronous creation of the same cache entry, so that a burst of
        /// concurrent misses for one key results in a single invocation of the create delegate.
        /// </summary>
        private readonly KeyedAsyncLock<TKey> _entryCreationLock = new();

        /// <summary>
        /// Sets an absolute expiration time, relative to now.
        /// </summary>
        internal TimeSpan? TimeToLive { get; set; }

        /// <summary>
        /// Gets the count of the current entries for diagnostic purposes.
        /// </summary>
        internal int Count => _cache.Count;

        /// <summary>
        /// Gets the number of asynchronous creation gates currently retained.
        /// Used in unit tests to verify that gates do not accumulate.
        /// </summary>
        internal int EntryCreationGateCount => _entryCreationLock.GateCount;

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
                SqlClientEventSource.Log.TryTraceEvent("Key caching found disabled, fetching key information.");
                return createItem();
            }

            if (!_cache.TryGetValue(key, out TValue cacheEntry))
            {
                SqlClientEventSource.Log.TryTraceEvent("Cached entry not found, creating new entry.");
                if (_cache.Count >= _maxSize)
                {
                    _cache.Compact(Max(0.10, 1.0 / _maxSize));
                }

                cacheEntry = createItem();
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeToLive
                };

                _cache.Set(key, cacheEntry, cacheEntryOptions);
                SqlClientEventSource.Log.TryTraceEvent("Entry added to local cache.");
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("Cached entry found.");
            }

            return cacheEntry;
        }

        /// <summary>
        /// Asynchronous counterpart of <see cref="GetOrCreate(TKey, Func{TValue})"/>. Looks for the cache entry that maps to the
        /// <paramref name="key"/> value. If it exists (cache hit) it will simply be returned. Otherwise, the
        /// <paramref name="createItem"/> delegate function will be awaited to create the value. It will then get stored in the
        /// cache and set the time-to-live before getting returned.
        /// </summary>
        /// <param name="key">The key for the cache entry.</param>
        /// <param name="createItem">The delegate function that will asynchronously create the cache entry if it does not exist.</param>
        /// <param name="cancellationToken">Token used to request cancellation of the wait for another caller's creation of the entry.</param>
        /// <returns>The cache entry.</returns>
        /// <remarks>
        /// Concurrent misses for the same key are gated so that only one caller invokes <paramref name="createItem"/>; the others
        /// await that caller and then observe the cached value. The gate is per key, so misses for different keys proceed in
        /// parallel, and no lock is held by a blocked thread. Cancellation applies only to the caller requesting it: if the caller
        /// that owns the gate is cancelled, the next waiter creates the entry using its own cancellation token.
        /// <para>
        /// When caching is disabled the gate is bypassed, because there is no entry for a waiting caller to observe and gating
        /// would serialize callers without saving any work. Callers therefore reach the create delegate in parallel.
        /// </para>
        /// </remarks>
        internal async Task<TValue> GetOrCreateAsync(TKey key, Func<Task<TValue>> createItem, CancellationToken cancellationToken = default)
        {
            if (TimeToLive <= TimeSpan.Zero)
            {
                SqlClientEventSource.Log.TryTraceEvent("Key caching found disabled, fetching key information.");
                return await createItem().ConfigureAwait(false);
            }

            if (_cache.TryGetValue(key, out TValue cacheEntry))
            {
                SqlClientEventSource.Log.TryTraceEvent("Cached entry found.");
                return cacheEntry;
            }

            using (await _entryCreationLock.AcquireAsync(key, cancellationToken).ConfigureAwait(false))
            {
                // Another caller may have created the entry while this one waited for the gate.
                if (_cache.TryGetValue(key, out cacheEntry))
                {
                    SqlClientEventSource.Log.TryTraceEvent("Cached entry found.");
                    return cacheEntry;
                }

                SqlClientEventSource.Log.TryTraceEvent("Cached entry not found, creating new entry.");

                cacheEntry = await createItem().ConfigureAwait(false);

                if (_cache.Count >= _maxSize)
                {
                    _cache.Compact(Max(0.10, 1.0 / _maxSize));
                }

                MemoryCacheEntryOptions cacheEntryOptions = new()
                {
                    AbsoluteExpirationRelativeToNow = TimeToLive
                };

                _cache.Set(key, cacheEntry, cacheEntryOptions);
                SqlClientEventSource.Log.TryTraceEvent("Entry added to local cache.");

                return cacheEntry;
            }
        }

        /// <summary>
        /// Determines whether the <see cref="LocalCache{TKey, TValue}">LocalCache</see> contains the specified key.
        /// Used in unit tests to verify that the cache contains the expected entries.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal bool Contains(TKey key)
        {
            return _cache.TryGetValue(key, out _);
        }
    }
}
