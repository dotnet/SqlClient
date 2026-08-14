// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using static Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    internal sealed class AzureSqlKeyCryptographer : IDisposable
    {
        /// <summary>
        /// TokenCredential to be used with the KeyClient
        /// </summary>
        private TokenCredential TokenCredential { get; set; }

        /// <summary>
        /// A mapping of the KeyClient objects to the corresponding Azure Key Vault URI
        /// </summary>
        private readonly ConcurrentDictionary<Uri, KeyClient> _keyClientDictionary = new();

        /// <summary>
        /// Holds references to the Azure Key Vault keys and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// </summary>
        private readonly ConcurrentDictionary<string, KeyVaultKey> _keyDictionary = new();

        /// <summary>
        /// SemaphoreSlim to ensure thread safety when accessing the key dictionary or making network calls to Azure Key Vault to fetch keys.
        /// </summary>
        private SemaphoreSlim _keyDictionarySemaphore = new(1, 1);

        /// <summary>
        /// Holds references to the Azure Key Vault CryptographyClient objects and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// </summary>
        private readonly ConcurrentDictionary<string, CryptographyClient> _cryptoClientDictionary = new();

        /// <summary>
        /// Constructs a new KeyCryptographer
        /// </summary>
        /// <param name="tokenCredential"></param>
        internal AzureSqlKeyCryptographer(TokenCredential tokenCredential)
        {
            TokenCredential = tokenCredential;
        }

        /// <summary>
        /// Disposes the SemaphoreSlim used for thread safety.
        /// </summary>
        public void Dispose()
        {
            _keyDictionarySemaphore.Dispose();
        }

        /// <summary>
        /// Adds the key, specified by the Key Identifier URI, to the cache.
        /// Validates the key type and fetches the key from Azure Key Vault if it is not already cached.
        /// </summary>
        /// <param name="keyIdentifierUri"></param>
        internal void AddKey(string keyIdentifierUri)
        {
            // Allow only one thread to proceed to ensure thread safety
            // as we will need to fetch key information from Azure Key Vault if the key is not found in cache.
            _keyDictionarySemaphore.Wait();

            try
            {
                if (!_keyDictionary.ContainsKey(keyIdentifierUri))
                {
                    ParseAKVPath(keyIdentifierUri, out Uri vaultUri, out string keyName, out string keyVersion);

                    // Fetch the KeyClient for the Key vault URI.
                    KeyClient keyClient = GetOrCreateKeyClient(vaultUri);

                    // Fetch the key from Azure Key Vault.
                    KeyVaultKey key = FetchKeyFromKeyVault(keyClient, keyName, keyVersion);

                    _keyDictionary.AddOrUpdate(keyIdentifierUri, key, (k, v) => key);
                }
            }
            finally
            {
                _keyDictionarySemaphore.Release();
            }
        }

        /// <summary>
        /// Asynchronously adds the key, specified by the Key Identifier URI, to the cache.
        /// Validates the key type and fetches the key from Azure Key Vault if it is not already cached.
        /// </summary>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <param name="cancellationToken">Token used to request cancellation of the operation</param>
        /// <remarks>
        /// Mirrors the deduplication of the synchronous path: only one caller fetches a given key from Azure Key Vault.
        /// The semaphore is awaited rather than blocked on, so no thread is held while the fetch is in flight.
        /// </remarks>
        internal async Task AddKeyAsync(string keyIdentifierUri, CancellationToken cancellationToken = default)
        {
            if (_keyDictionary.ContainsKey(keyIdentifierUri))
            {
                return;
            }

            // Allow only one caller to proceed to ensure thread safety
            // as we will need to fetch key information from Azure Key Vault if the key is not found in cache.
            await _keyDictionarySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (!_keyDictionary.ContainsKey(keyIdentifierUri))
                {
                    ParseAKVPath(keyIdentifierUri, out Uri vaultUri, out string keyName, out string keyVersion);

                    // Fetch the KeyClient for the Key vault URI.
                    KeyClient keyClient = GetOrCreateKeyClient(vaultUri);

                    // Fetch the key from Azure Key Vault.
                    KeyVaultKey key = await FetchKeyFromKeyVaultAsync(keyClient, keyName, keyVersion, cancellationToken).ConfigureAwait(false);

                    _keyDictionary.AddOrUpdate(keyIdentifierUri, key, (k, v) => key);
                }
            }
            finally
            {
                _keyDictionarySemaphore.Release();
            }
        }

        /// <summary>
        /// Returns the key specified by the Key Identifier URI
        /// </summary>
        /// <param name="keyIdentifierUri"></param>
        /// <returns></returns>
        internal KeyVaultKey GetKey(string keyIdentifierUri)
        {
            if (_keyDictionary.TryGetValue(keyIdentifierUri, out KeyVaultKey key))
            {
                SqlClientEventSource.Log.TryTraceEvent("Fetched key name={0} from cache", key.Name);
                return key;
            }

            // Not a public exception - not likely to occur.
            SqlClientEventSource.Log.TryTraceEvent("Key not found; URI={0}", keyIdentifierUri);
            throw ADP.MasterKeyNotFound(keyIdentifierUri);
        }

        /// <summary>
        /// Gets the public Key size in bytes.
        /// </summary>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <returns></returns>
        internal int GetKeySize(string keyIdentifierUri) =>  GetKey(keyIdentifierUri).Key.N.Length;

        /// <summary>
        /// Generates signature based on RSA PKCS#v1.5 scheme using a specified Azure Key Vault Key URL. 
        /// </summary>
        /// <param name="message">The data to sign</param>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <returns></returns>
        internal byte[] SignData(byte[] message, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            return cryptographyClient.SignData(RS256, message).Signature;
        }

        internal bool VerifyData(byte[] message, byte[] signature, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            SqlClientEventSource.Log.TryTraceEvent("Sending request to verify data");
            return cryptographyClient.VerifyData(RS256, message, signature).IsValid;
        }

        /// <summary>
        /// Asynchronously generates a signature based on the RSA PKCS#v1.5 scheme using a specified Azure Key Vault Key URL.
        /// </summary>
        /// <param name="message">The data to sign</param>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <param name="cancellationToken">Token used to request cancellation of the operation</param>
        internal async Task<byte[]> SignDataAsync(byte[] message, string keyIdentifierUri, CancellationToken cancellationToken = default)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            SignResult result = await cryptographyClient.SignDataAsync(RS256, message, cancellationToken).ConfigureAwait(false);
            return result.Signature;
        }

        /// <summary>
        /// Asynchronously verifies a signature based on the RSA PKCS#v1.5 scheme using a specified Azure Key Vault Key URL.
        /// </summary>
        /// <param name="message">The signed data</param>
        /// <param name="signature">The signature to verify</param>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <param name="cancellationToken">Token used to request cancellation of the operation</param>
        internal async Task<bool> VerifyDataAsync(byte[] message, byte[] signature, string keyIdentifierUri, CancellationToken cancellationToken = default)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            SqlClientEventSource.Log.TryTraceEvent("Sending request to verify data");
            VerifyResult result = await cryptographyClient.VerifyDataAsync(RS256, message, signature, cancellationToken).ConfigureAwait(false);
            return result.IsValid;
        }

        internal byte[] UnwrapKey(KeyWrapAlgorithm keyWrapAlgorithm, byte[] encryptedKey, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            SqlClientEventSource.Log.TryTraceEvent("Sending request to unwrap key.");
            return cryptographyClient.UnwrapKey(keyWrapAlgorithm, encryptedKey).Key;
        }

        internal byte[] WrapKey(KeyWrapAlgorithm keyWrapAlgorithm, byte[] key, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            SqlClientEventSource.Log.TryTraceEvent("Sending request to wrap key.");
            return cryptographyClient.WrapKey(keyWrapAlgorithm, key).EncryptedKey;
        }

        /// <summary>
        /// Asynchronously unwraps the given encrypted key using the specified Azure Key Vault key.
        /// </summary>
        /// <param name="keyWrapAlgorithm">The key wrap algorithm</param>
        /// <param name="encryptedKey">The encrypted key to unwrap</param>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <param name="cancellationToken">Token used to request cancellation of the operation</param>
        internal async Task<byte[]> UnwrapKeyAsync(KeyWrapAlgorithm keyWrapAlgorithm, byte[] encryptedKey, string keyIdentifierUri, CancellationToken cancellationToken = default)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            SqlClientEventSource.Log.TryTraceEvent("Sending request to unwrap key.");
            UnwrapResult result = await cryptographyClient.UnwrapKeyAsync(keyWrapAlgorithm, encryptedKey, cancellationToken).ConfigureAwait(false);
            return result.Key;
        }

        /// <summary>
        /// Asynchronously wraps the given key using the specified Azure Key Vault key.
        /// </summary>
        /// <param name="keyWrapAlgorithm">The key wrap algorithm</param>
        /// <param name="key">The key to wrap</param>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <param name="cancellationToken">Token used to request cancellation of the operation</param>
        internal async Task<byte[]> WrapKeyAsync(KeyWrapAlgorithm keyWrapAlgorithm, byte[] key, string keyIdentifierUri, CancellationToken cancellationToken = default)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            SqlClientEventSource.Log.TryTraceEvent("Sending request to wrap key.");
            WrapResult result = await cryptographyClient.WrapKeyAsync(keyWrapAlgorithm, key, cancellationToken).ConfigureAwait(false);
            return result.EncryptedKey;
        }

        private CryptographyClient GetCryptographyClient(string keyIdentifierUri)
        {
            if (_cryptoClientDictionary.TryGetValue(keyIdentifierUri, out CryptographyClient client))
            {
                return client;
            }

            CryptographyClient cryptographyClient = new(GetKey(keyIdentifierUri).Id, TokenCredential);
            _cryptoClientDictionary.TryAdd(keyIdentifierUri, cryptographyClient);
            return cryptographyClient;
        }

        /// <summary>
        /// Fetches the column encryption key from the Azure Key Vault.
        /// </summary>
        /// <param name="keyClient">The KeyClient instance</param>
        /// <param name="keyName">The name of the Azure Key Vault key</param>
        /// <param name="keyVersion">The version of the Azure Key Vault key</param>
        private KeyVaultKey FetchKeyFromKeyVault(KeyClient keyClient, string keyName, string keyVersion)
        {
            SqlClientEventSource.Log.TryTraceEvent("Fetching key name={0}", keyName);

            Azure.Response<KeyVaultKey> keyResponse = keyClient?.GetKey(keyName, keyVersion);

            return ValidateKeyResponse(keyResponse, keyName, keyVersion);
        }

        /// <summary>
        /// Asynchronously fetches the column master key from the Azure Key Vault.
        /// </summary>
        /// <param name="keyClient">The KeyClient instance</param>
        /// <param name="keyName">The name of the Azure Key Vault key</param>
        /// <param name="keyVersion">The version of the Azure Key Vault key</param>
        /// <param name="cancellationToken">Token used to request cancellation of the operation</param>
        private async Task<KeyVaultKey> FetchKeyFromKeyVaultAsync(KeyClient keyClient, string keyName, string keyVersion, CancellationToken cancellationToken)
        {
            SqlClientEventSource.Log.TryTraceEvent("Fetching key name={0}", keyName);

            Azure.Response<KeyVaultKey> keyResponse = keyClient is null
                ? null
                : await keyClient.GetKeyAsync(keyName, keyVersion, cancellationToken).ConfigureAwait(false);

            return ValidateKeyResponse(keyResponse, keyName, keyVersion);
        }

        /// <summary>
        /// Validates the response received from Azure Key Vault and ensures the returned key is an RSA key.
        /// </summary>
        /// <param name="keyResponse">The response returned by Azure Key Vault</param>
        /// <param name="keyName">The name of the Azure Key Vault key</param>
        /// <param name="keyVersion">The version of the Azure Key Vault key</param>
        private static KeyVaultKey ValidateKeyResponse(Azure.Response<KeyVaultKey> keyResponse, string keyName, string keyVersion)
        {
            // Handle the case where the key response is null or contains an error
            // This can happen if the key does not exist or if there is an issue with the KeyClient.
            // In such cases, we log the error and throw an exception.
            if (keyResponse == null || keyResponse.Value == null || keyResponse.GetRawResponse().IsError)
            {
                SqlClientEventSource.Log.TryTraceEvent("Get Key failed to fetch Key from Azure Key Vault for key {0}, version {1}", keyName, keyVersion);
                if (keyResponse?.GetRawResponse() is Azure.Response response)
                {
                    SqlClientEventSource.Log.TryTraceEvent("Response status {0} : {1}", response.Status, response.ReasonPhrase);
                }
                throw ADP.GetKeyFailed(keyName);
            }

            KeyVaultKey key = keyResponse.Value;

            // Validate that the key is of type RSA
            key = ValidateRsaKey(key);
            return key;
        }

        /// <summary>
        /// Gets or creates a KeyClient for the specified Azure Key Vault URI.
        /// </summary>
        /// <param name="vaultUri">Key Identifier URL</param>
        /// <returns></returns>
        private KeyClient GetOrCreateKeyClient(Uri vaultUri)
        {
            return _keyClientDictionary.GetOrAdd(
                vaultUri, (_) => new KeyClient(vaultUri, TokenCredential));
        }

        /// <summary>
        /// Validates that a key is of type RSA
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static KeyVaultKey ValidateRsaKey(KeyVaultKey key)
        {
            if (key.KeyType != KeyType.Rsa && key.KeyType != KeyType.RsaHsm)
            {
                SqlClientEventSource.Log.TryTraceEvent("Non-RSA KeyType received: {0}", key.KeyType);
                throw ADP.NonRsaKeyFormat(key.KeyType.ToString());
            }

            return key;
        }

        /// <summary>
        /// Validates and parses the Azure Key Vault URI and key name.
        /// </summary>
        /// <param name="masterKeyPath">The Azure Key Vault key identifier</param>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        /// <param name="masterKeyName">The name of the key</param>
        /// <param name="masterKeyVersion">The version of the key</param>
        private static void ParseAKVPath(string masterKeyPath, out Uri vaultUri, out string masterKeyName, out string masterKeyVersion)
        {
            Uri masterKeyPathUri = new(masterKeyPath);
            vaultUri = new Uri(masterKeyPathUri.GetLeftPart(UriPartial.Authority));
            masterKeyName = masterKeyPathUri.Segments[2];
            masterKeyVersion = masterKeyPathUri.Segments.Length > 3 ? masterKeyPathUri.Segments[3] : null;

            SqlClientEventSource.Log.TryTraceEvent("Received Key Name: {0}", masterKeyName);
            SqlClientEventSource.Log.TryTraceEvent("Received Key Version: {0}", masterKeyVersion);
        }
    }
}
