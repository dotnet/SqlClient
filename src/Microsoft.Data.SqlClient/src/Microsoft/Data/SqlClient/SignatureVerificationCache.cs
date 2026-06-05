// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Tri-state result returned by <see cref="ColumnMasterKeyMetadataSignatureVerificationCache.GetSignatureVerificationResult"/>.
    /// Distinguishes a cache miss from a cached negative result so callers cannot conflate the two.
    /// </summary>
    internal enum SignatureVerificationResult
    {
        /// <summary>
        /// No cached entry exists for the requested CMK metadata. 
        /// The caller must verify the signature with the key store provider.
        /// </summary>
        NotFound,

        /// <summary>
        /// A cached entry exists and indicates that signature verification previously failed.
        /// </summary>
        False,

        /// <summary>
        /// A cached entry exists and indicates that signature verification previously succeeded.
        /// </summary>
        True,
    }

    /// <summary>
    /// Cache for storing result of signature verification of CMK Metadata
    /// </summary>
    internal class ColumnMasterKeyMetadataSignatureVerificationCache
    {
        private const int CacheSize = 2000; // Cache size in number of entries.
        private const int CacheTrimThreshold = 300; // Threshold above the cache size when we start trimming.
        private const string _cacheLookupKeySeparator = ":";

        private static readonly TimeSpan s_verificationCacheTimeout = TimeSpan.FromDays(10);

        /// <summary>
        /// Gets the process-wide singleton instance of the signature verification cache.
        /// </summary>
        internal static ColumnMasterKeyMetadataSignatureVerificationCache Instance { get; } = new();

        private readonly MemoryCache _cache;
        private int _inTrim;

        private ColumnMasterKeyMetadataSignatureVerificationCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <summary>
        /// Get signature verification result for given CMK metadata 
        /// (KeystoreName, MasterKeyPath, allowEnclaveComputations) and a given signature
        /// </summary>
        /// <param name="keyStoreName">Key Store name for CMK</param>
        /// <param name="masterKeyPath">Key Path for CMK</param>
        /// <param name="allowEnclaveComputations">boolean indicating whether the key can be sent to enclave</param>
        /// <param name="signature">Signature for the CMK metadata</param>
        /// <returns>Tri-state result indicating whether signature verification succeeded, failed, or was not found in cache</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="masterKeyPath"/>, <paramref name="keyStoreName"/>, 
        /// or <paramref name="signature"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="masterKeyPath"/> or <paramref name="keyStoreName"/> 
        /// is empty or whitespace, or when <paramref name="signature"/> has length zero.
        /// </exception>
        internal SignatureVerificationResult GetSignatureVerificationResult(string keyStoreName, string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            ValidateStringArgumentNotNullOrEmpty(masterKeyPath, nameof(masterKeyPath), nameof(GetSignatureVerificationResult));
            ValidateStringArgumentNotNullOrEmpty(keyStoreName, nameof(keyStoreName), nameof(GetSignatureVerificationResult));
            ValidateSignatureNotNullOrEmpty(signature, nameof(GetSignatureVerificationResult));

            string cacheLookupKey = GetCacheLookupKey(masterKeyPath, allowEnclaveComputations, signature, keyStoreName);

            if (!_cache.TryGetValue(cacheLookupKey, out bool value))
            {
                return SignatureVerificationResult.NotFound;
            }

            return value ? SignatureVerificationResult.True : SignatureVerificationResult.False;
        }

        /// <summary>
        /// Add signature verification result for given CMK metadata (KeystoreName, 
        /// MasterKeyPath, allowEnclaveComputations) and a given signature in the cache
        /// </summary>
        /// <param name="keyStoreName">Key Store name for CMK</param>
        /// <param name="masterKeyPath">Key Path for CMK</param>
        /// <param name="allowEnclaveComputations">boolean indicating whether the key can be sent to enclave</param>
        /// <param name="signature">Signature for the CMK metadata</param>
        /// <param name="result">result indicating signature verification success/failure</param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="masterKeyPath"/>, <paramref name="keyStoreName"/>, 
        /// or <paramref name="signature"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="masterKeyPath"/> or <paramref name="keyStoreName"/> is empty or whitespace, 
        /// or when <paramref name="signature"/> has length zero.
        /// </exception>
        internal void AddSignatureVerificationResult(string keyStoreName, string masterKeyPath, bool allowEnclaveComputations, byte[] signature, bool result)
        {
            ValidateStringArgumentNotNullOrEmpty(masterKeyPath, nameof(masterKeyPath), nameof(AddSignatureVerificationResult));
            ValidateStringArgumentNotNullOrEmpty(keyStoreName, nameof(keyStoreName), nameof(AddSignatureVerificationResult));
            ValidateSignatureNotNullOrEmpty(signature, nameof(AddSignatureVerificationResult));

            string cacheLookupKey = GetCacheLookupKey(masterKeyPath, allowEnclaveComputations, signature, keyStoreName);

            TrimCacheIfNeeded();

            // By default evict after 10 days.
            _cache.Set(cacheLookupKey, result, absoluteExpirationRelativeToNow: s_verificationCacheTimeout);
        }

        private static void ValidateSignatureNotNullOrEmpty(byte[] signature, string methodName)
        {
            if (signature is null)
            {
                throw SQL.NullArgumentInternal(nameof(signature), nameof(ColumnMasterKeyMetadataSignatureVerificationCache), methodName);
            }
            if (signature.Length == 0)
            {
                throw SQL.EmptyArgumentInternal(nameof(signature), nameof(ColumnMasterKeyMetadataSignatureVerificationCache), methodName);
            }
        }

        private static void ValidateStringArgumentNotNullOrEmpty(string value, string argumentName, string methodName)
        {
            if (value is null)
            {
                throw SQL.NullArgumentInternal(argumentName, nameof(ColumnMasterKeyMetadataSignatureVerificationCache), methodName);
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                throw SQL.EmptyArgumentInternal(argumentName, nameof(ColumnMasterKeyMetadataSignatureVerificationCache), methodName);
            }
        }


        private void TrimCacheIfNeeded()
        {
            // If the size of the cache exceeds the threshold, set that we are in trimming and trim the cache accordingly.
            long currentCacheSize = _cache.Count;
            if (currentCacheSize <= CacheSize + CacheTrimThreshold || Interlocked.CompareExchange(ref _inTrim, 1, 0) != 0)
            {
                return;
            }

            try
            {
                // Example: 2301 - 2000 = 301; 301 / 2301 = 0.1308 * 100 = 13% compacting
                _cache.Compact((double)(currentCacheSize - CacheSize) / currentCacheSize * 100);
            }
            finally
            {
                Interlocked.Exchange(ref _inTrim, 0);
            }
        }

        /// <summary>
        /// Generates a cache key for the given CMK metadata and signature. The key is a
        /// concatenation of the key store name, master key path, allowEnclaveComputations value, and signature, separated by a delimiter.
        /// </summary>
        /// <param name="masterKeyPath">The master key path.</param>
        /// <param name="allowEnclaveComputations">Whether enclave computations are allowed.</param>
        /// <param name="signature">The signature.</param>
        /// <param name="keyStoreName">The key store name.</param>
        /// <returns>A string that can be used as a cache key.</returns>
        private static string GetCacheLookupKey(string masterKeyPath, bool allowEnclaveComputations, byte[] signature, string keyStoreName)
        {
            int cacheCapacity =
                keyStoreName.Length +
                masterKeyPath.Length +
                SqlSecurityUtility.GetBase64LengthFromByteLength(signature.Length) +
                4 * _cacheLookupKeySeparator.Length +
                10 /* boolean value + buffer */;

            return new StringBuilder(keyStoreName, capacity: cacheCapacity)
                .Append(_cacheLookupKeySeparator)
                .Append(masterKeyPath)
                .Append(_cacheLookupKeySeparator)
                .Append(allowEnclaveComputations)
                .Append(_cacheLookupKeySeparator)
                .Append(Convert.ToBase64String(signature))
                .Append(_cacheLookupKeySeparator)
                .ToString();
        }
    }
}
