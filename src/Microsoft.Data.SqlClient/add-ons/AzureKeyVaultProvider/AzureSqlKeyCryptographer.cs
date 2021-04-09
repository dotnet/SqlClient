// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using static Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    internal class AzureSqlKeyCryptographer
    {
        /// <summary>
        /// TokenCredential to be used with the KeyClient
        /// </summary>
        private TokenCredential TokenCredential { get; set; }

        /// <summary>
        /// A mapping of the KeyClient objects to the corresponding Azure Key Vault URI
        /// </summary>
        private readonly ConcurrentDictionary<Uri, KeyClient> _keyClientDictionary = new ConcurrentDictionary<Uri, KeyClient>();

        /// <summary>
        /// Holds references to the fetch key tasks and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// These tasks will be used for returning the key in the event that the fetch task has not finished depositing the 
        /// key into the key dictionary.
        /// </summary>
        private readonly ConcurrentDictionary<string, Task<Azure.Response<KeyVaultKey>>> _keyFetchTaskDictionary = new ConcurrentDictionary<string, Task<Azure.Response<KeyVaultKey>>>();

        /// <summary>
        /// Holds references to the Azure Key Vault keys and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// </summary>
        private readonly ConcurrentDictionary<string, KeyVaultKey> _keyDictionary = new ConcurrentDictionary<string, KeyVaultKey>();

        /// <summary>
        /// Holds references to the Azure Key Vault CryptographyClient objects and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// </summary>
        private readonly ConcurrentDictionary<string, CryptographyClient> _cryptoClientDictionary = new ConcurrentDictionary<string, CryptographyClient>();

        /// <summary>
        /// Constructs a new KeyCryptographer
        /// </summary>
        /// <param name="tokenCredential"></param>
        internal AzureSqlKeyCryptographer(TokenCredential tokenCredential)
        {
            TokenCredential = tokenCredential;
        }

        /// <summary>
        /// Adds the key, specified by the Key Identifier URI, to the cache.
        /// </summary>
        /// <param name="keyIdentifierUri"></param>
        internal void AddKey(string keyIdentifierUri)
        {
            if (TheKeyHasNotBeenCached(keyIdentifierUri))
            {
                ParseAKVPath(keyIdentifierUri, out Uri vaultUri, out string keyName, out string keyVersion);
                CreateKeyClient(vaultUri);
                FetchKey(vaultUri, keyName, keyVersion, keyIdentifierUri);
            }

            bool TheKeyHasNotBeenCached(string k) => !_keyDictionary.ContainsKey(k) && !_keyFetchTaskDictionary.ContainsKey(k);
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
                return key;
            }

            if (_keyFetchTaskDictionary.TryGetValue(keyIdentifierUri, out Task<Azure.Response<KeyVaultKey>> task))
            {
                return Task.Run(() => task).GetAwaiter().GetResult();
            }

            // Not a public exception - not likely to occur.
            throw ADP.MasterKeyNotFound(keyIdentifierUri);
        }

        /// <summary>
        /// Gets the public Key size in bytes.
        /// </summary>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <returns></returns>
        internal int GetKeySize(string keyIdentifierUri)
        {
            return GetKey(keyIdentifierUri).Key.N.Length;
        }

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
            return cryptographyClient.VerifyData(RS256, message, signature).IsValid;
        }

        internal byte[] UnwrapKey(KeyWrapAlgorithm keyWrapAlgorithm, byte[] encryptedKey, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            return cryptographyClient.UnwrapKey(keyWrapAlgorithm, encryptedKey).Key;
        }

        internal byte[] WrapKey(KeyWrapAlgorithm keyWrapAlgorithm, byte[] key, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            return cryptographyClient.WrapKey(keyWrapAlgorithm, key).EncryptedKey;
        }

        private CryptographyClient GetCryptographyClient(string keyIdentifierUri)
        {
            if (_cryptoClientDictionary.TryGetValue(keyIdentifierUri, out CryptographyClient client))
            {
                return client;
            }

            CryptographyClient cryptographyClient = new CryptographyClient(GetKey(keyIdentifierUri).Id, TokenCredential);
            _cryptoClientDictionary.TryAdd(keyIdentifierUri, cryptographyClient);

            return cryptographyClient;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        /// <param name="keyName">The name of the Azure Key Vault key</param>
        /// <param name="keyVersion">The version of the Azure Key Vault key</param>
        /// <param name="keyResourceUri">The Azure Key Vault key identifier</param>
        private void FetchKey(Uri vaultUri, string keyName, string keyVersion, string keyResourceUri)
        {
            Task<Azure.Response<KeyVaultKey>> fetchKeyTask = FetchKeyFromKeyVault(vaultUri, keyName, keyVersion);
            _keyFetchTaskDictionary.AddOrUpdate(keyResourceUri, fetchKeyTask, (k, v) => fetchKeyTask);

            fetchKeyTask
                .ContinueWith(k => ValidateRsaKey(k.GetAwaiter().GetResult()))
                .ContinueWith(k => _keyDictionary.AddOrUpdate(keyResourceUri, k.GetAwaiter().GetResult(), (key, v) => k.GetAwaiter().GetResult()));

            Task.Run(() => fetchKeyTask);
        }

        /// <summary>
        /// Looks up the KeyClient object by it's URI and then fetches the key by name.
        /// </summary>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        /// <param name="keyName">Then name of the key</param>
        /// <param name="keyVersion">Then version of the key</param>
        /// <returns></returns>
        private Task<Azure.Response<KeyVaultKey>> FetchKeyFromKeyVault(Uri vaultUri, string keyName, string keyVersion)
        {
            _keyClientDictionary.TryGetValue(vaultUri, out KeyClient keyClient);
           return keyClient?.GetKeyAsync(keyName, keyVersion);
        }

        /// <summary>
        /// Validates that a key is of type RSA
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private KeyVaultKey ValidateRsaKey(KeyVaultKey key)
        {
            if (key.KeyType != KeyType.Rsa && key.KeyType != KeyType.RsaHsm)
            {
                throw ADP.NonRsaKeyFormat(key.KeyType.ToString());
            }

            return key;
        }

        /// <summary>
        /// Instantiates and adds a KeyClient to the KeyClient dictionary
        /// </summary>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        private void CreateKeyClient(Uri vaultUri)
        {
            if (!_keyClientDictionary.ContainsKey(vaultUri))
            {
                _keyClientDictionary.TryAdd(vaultUri, new KeyClient(vaultUri, TokenCredential));
            }
        }

        /// <summary>
        /// Validates and parses the Azure Key Vault URI and key name.
        /// </summary>
        /// <param name="masterKeyPath">The Azure Key Vault key identifier</param>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        /// <param name="masterKeyName">The name of the key</param>
        /// <param name="masterKeyVersion">The version of the key</param>
        private void ParseAKVPath(string masterKeyPath, out Uri vaultUri, out string masterKeyName, out string masterKeyVersion)
        {
            Uri masterKeyPathUri = new Uri(masterKeyPath);
            vaultUri = new Uri(masterKeyPathUri.GetLeftPart(UriPartial.Authority));
            masterKeyName = masterKeyPathUri.Segments[2];
            masterKeyVersion = masterKeyPathUri.Segments.Length > 3 ? masterKeyPathUri.Segments[3] : null;
        }
    }
}
