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
    /// Cache for storing result of signature verification of CMK Metadata
    /// </summary>
    internal class ColumnMasterKeyMetadataSignatureVerificationCache
    {
        private const int CacheSize = 2000; // Cache size in number of entries.
        private const int CacheTrimThreshold = 300; // Threshold above the cache size when we start trimming.
        private const int VerificationCacheTimeOutInDays = 10;

        private const string _className = "ColumnMasterKeyMetadataSignatureVerificationCache";
        private const string _getSignatureVerificationResultMethodName = "GetSignatureVerificationResult";
        private const string _addSignatureVerificationResultMethodName = "AddSignatureVerificationResult";
        private const string _masterkeypathArgumentName = "masterKeyPath";
        private const string _keyStoreNameArgumentName = "keyStoreName";
        private const string _signatureName = "signature";
        private const string _cacheLookupKeySeparator = ":";

        private static readonly ColumnMasterKeyMetadataSignatureVerificationCache _signatureVerificationCache = new ColumnMasterKeyMetadataSignatureVerificationCache();

        //singleton instance
        internal static ColumnMasterKeyMetadataSignatureVerificationCache Instance { get { return _signatureVerificationCache; } }

        private readonly MemoryCache _cache;
        private int _inTrim = 0;

        private ColumnMasterKeyMetadataSignatureVerificationCache()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            _inTrim = 0;
        }

        /// <summary>
        /// Get signature verification result for given CMK metadata (KeystoreName, MasterKeyPath, allowEnclaveComputations) and a given signature
        /// </summary>
        /// <param name="keyStoreName">Key Store name for CMK</param>
        /// <param name="masterKeyPath">Key Path for CMK</param>
        /// <param name="allowEnclaveComputations">boolean indicating whether the key can be sent to enclave</param>
        /// <param name="signature">Signature for the CMK metadata</param>
        internal bool GetSignatureVerificationResult(string keyStoreName, string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            ValidateStringArgumentNotNullOrEmpty(masterKeyPath, _masterkeypathArgumentName, _getSignatureVerificationResultMethodName);
            ValidateStringArgumentNotNullOrEmpty(keyStoreName, _keyStoreNameArgumentName, _getSignatureVerificationResultMethodName);
            ValidateSignatureNotNullOrEmpty(signature, _getSignatureVerificationResultMethodName);

            string cacheLookupKey = GetCacheLookupKey(masterKeyPath, allowEnclaveComputations, signature, keyStoreName);

            return _cache.TryGetValue<bool>(cacheLookupKey, out bool value);
        }

        /// <summary>
        /// Add signature verification result for given CMK metadata (KeystoreName, MasterKeyPath, allowEnclaveComputations) and a given signature in the cache
        /// </summary>
        /// <param name="keyStoreName">Key Store name for CMK</param>
        /// <param name="masterKeyPath">Key Path for CMK</param>
        /// <param name="allowEnclaveComputations">boolean indicating whether the key can be sent to enclave</param>
        /// <param name="signature">Signature for the CMK metadata</param>
        /// <param name="result">result indicating signature verification success/failure</param>
        internal void AddSignatureVerificationResult(string keyStoreName, string masterKeyPath, bool allowEnclaveComputations, byte[] signature, bool result)
        {
            ValidateStringArgumentNotNullOrEmpty(masterKeyPath, _masterkeypathArgumentName, _addSignatureVerificationResultMethodName);
            ValidateStringArgumentNotNullOrEmpty(keyStoreName, _keyStoreNameArgumentName, _addSignatureVerificationResultMethodName);
            ValidateSignatureNotNullOrEmpty(signature, _addSignatureVerificationResultMethodName);

            string cacheLookupKey = GetCacheLookupKey(masterKeyPath, allowEnclaveComputations, signature, keyStoreName);

            TrimCacheIfNeeded();

            // By default evict after 10 days.
            _cache.Set<bool>(cacheLookupKey, result, absoluteExpirationRelativeToNow: TimeSpan.FromDays(VerificationCacheTimeOutInDays));
        }

        private void ValidateSignatureNotNullOrEmpty(byte[] signature, string methodName)
        {
            if (signature == null || signature.Length == 0)
            {
                if (signature == null)
                {
                    throw SQL.NullArgumentInternal(_signatureName, _className, methodName);
                }
                else
                {
                    throw SQL.EmptyArgumentInternal(_signatureName, _className, methodName);
                }
            }
        }

        private void ValidateStringArgumentNotNullOrEmpty(string stringArgValue, string stringArgName, string methodName)
        {
            if (string.IsNullOrWhiteSpace(stringArgValue))
            {
                if (stringArgValue == null)
                {
                    throw SQL.NullArgumentInternal(stringArgName, _className, methodName);
                }
                else
                {
                    throw SQL.EmptyArgumentInternal(stringArgName, _className, methodName);
                }
            }
        }

        private void TrimCacheIfNeeded()
        {
            // If the size of the cache exceeds the threshold, set that we are in trimming and trim the cache accordingly.
            long currentCacheSize = _cache.Count;
            if ((currentCacheSize > CacheSize + CacheTrimThreshold) && (0 == Interlocked.CompareExchange(ref _inTrim, 1, 0)))
            {
                try
                {
                    // Example: 2301 - 2000 = 301; 301 / 2301 = 0.1308 * 100 = 13% compacting
                    _cache.Compact((((double)(currentCacheSize - CacheSize) / (double)currentCacheSize) * 100));
                }
                finally
                {
                    // Reset _inTrim flag
                    Interlocked.CompareExchange(ref _inTrim, 0, 1);
                }
            }
        }

        private string GetCacheLookupKey(string masterKeyPath, bool allowEnclaveComputations, byte[] signature, string keyStoreName)
        {
            StringBuilder cacheLookupKeyBuilder = new StringBuilder(keyStoreName,
                capacity:
                    keyStoreName.Length +
                    masterKeyPath.Length +
                    SqlSecurityUtility.GetBase64LengthFromByteLength(signature.Length) +
                    3 /*separators*/ +
                    10 /*boolean value + somebuffer*/);

            cacheLookupKeyBuilder.Append(_cacheLookupKeySeparator);
            cacheLookupKeyBuilder.Append(masterKeyPath);
            cacheLookupKeyBuilder.Append(_cacheLookupKeySeparator);
            cacheLookupKeyBuilder.Append(allowEnclaveComputations);
            cacheLookupKeyBuilder.Append(_cacheLookupKeySeparator);
            cacheLookupKeyBuilder.Append(Convert.ToBase64String(signature));
            cacheLookupKeyBuilder.Append(_cacheLookupKeySeparator);
            string cacheLookupKey = cacheLookupKeyBuilder.ToString();
            return cacheLookupKey;
        }
    }
}
