// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.AlwaysEncrypted;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// This is a factory class for AEAD_AES_256_CBC_HMAC_SHA256.
    /// </summary>
    internal class AeadAes256CbcHmac256Factory : EncryptionAlgorithmFactory
    {
        /// <summary>
        /// Factory classes cache the <see cref="SqlAeadAes256CbcHmac256EncryptionKey" /> objects to avoid recomputation of the derived keys.
        /// </summary>
        private readonly ConcurrentDictionary<string, SqlAeadAes256CbcHmac256Algorithm> _encryptionAlgorithms =
            new(concurrencyLevel: 4 * Environment.ProcessorCount /* default value in ConcurrentDictionary */, capacity: 2);

        public static AeadAes256CbcHmac256Factory Instance => field ??= new();

        /// <summary>
        /// Creates an instance of the <see cref="SqlAeadAes256CbcHmac256Algorithm" /> class with a given root key.
        /// </summary>
        /// <param name="encryptionKey">Root key.</param>
        /// <param name="encryptionType">Encryption type. Expected values are either Deterministic or Randomized.</param>
        /// <param name="encryptionAlgorithm">Cryptographic algorithm.</param>
        /// <returns>An implementation of the AEAD_AES_256_CBC_HMAC_SHA256 cryptographic algorithm.</returns>
        internal override SqlClientEncryptionAlgorithm Create(SqlClientSymmetricKey encryptionKey, SqlClientEncryptionType encryptionType, string encryptionAlgorithm)
        {
            // Callers should have validated the encryption algorithm and the encryption key
            Debug.Assert(string.Equals(encryptionAlgorithm, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName, StringComparison.OrdinalIgnoreCase));

            // Validate encryption type
            if (encryptionType is not SqlClientEncryptionType.Deterministic and not SqlClientEncryptionType.Randomized)
            {
                throw SQL.InvalidEncryptionType(SqlAeadAes256CbcHmac256Algorithm.AlgorithmName,
                                                encryptionType,
                                                SqlClientEncryptionType.Deterministic,
                                                SqlClientEncryptionType.Randomized);
            }

            // Get the cached cryptographic algorithm if one exists or create a new one, add it to cache and use it
            int capacity = SqlSecurityUtility.GetBase64LengthFromByteLength(encryptionKey.RootKey.Length) + 4 /* Separators, type and version */;
            StringBuilder algorithmKeyBuilder = new(Convert.ToBase64String(encryptionKey.RootKey), capacity);

            algorithmKeyBuilder.Append(':');
            algorithmKeyBuilder.Append((int)encryptionType);
            algorithmKeyBuilder.Append(':');
            algorithmKeyBuilder.Append(SqlAeadAes256CbcHmac256Algorithm.CurrentVersion);

            string algorithmKey = algorithmKeyBuilder.ToString();

            Debug.Assert(algorithmKey.Length <= capacity, "We needed to allocate a larger array");

            if (!_encryptionAlgorithms.TryGetValue(algorithmKey, out SqlAeadAes256CbcHmac256Algorithm? aesAlgorithm))
            {
                SqlAeadAes256CbcHmac256EncryptionKey encryptedKey = new(encryptionKey.RootKey, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);
                aesAlgorithm = new SqlAeadAes256CbcHmac256Algorithm(encryptedKey, encryptionType, SqlAeadAes256CbcHmac256Algorithm.CurrentVersion);

                // In case multiple threads reach here at the same time, the first one adds the value
                // the second one will be a no-op, the allocated memory will be claimed by Garbage Collector.
                _encryptionAlgorithms.TryAdd(algorithmKey, aesAlgorithm);
            }

            return aesAlgorithm;
        }
    }
}
