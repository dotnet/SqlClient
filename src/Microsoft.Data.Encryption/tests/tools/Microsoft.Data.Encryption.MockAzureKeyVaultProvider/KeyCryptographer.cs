using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using static Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm;
using static Microsoft.Data.Encryption.MockAzureKeyVaultProvider.AzureKeyVaultProviderTokenCredential;

namespace Microsoft.Data.Encryption.MockAzureKeyVaultProvider
{
    internal class KeyCryptographer
    {
        /// <summary>
        /// TokenCredential to be used with the KeyClient
        /// </summary>
        private TokenCredential TokenCredential { get; set; }

        /// <summary>
        /// AuthenticationCallback to be used with the KeyClient for legacy support.
        /// </summary>
        private AuthenticationCallback AuthenticationCallback { get; set; }

        /// <summary>
        /// A flag to determine whether to use AuthenticationCallback with the KeyClient for legacy support.
        /// </summary>
        private readonly bool isUsingLegacyAuthentication = false;

        /// <summary>
        /// A mapping of the KeyClient objects to the corresponding Azure Key Vault URI
        /// </summary>
        private readonly Dictionary<Uri, KeyClient> keyClientDictionary = new Dictionary<Uri, KeyClient>();

        /// <summary>
        /// Holds references to the fetch key tasks and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// These tasks will be used for returning the key in the event that the fetch task has not finished depositing the 
        /// key into the key dictionary.
        /// </summary>
        private readonly Dictionary<string, Task<Azure.Response<KeyVaultKey>>> keyFetchTaskDictionary = new Dictionary<string, Task<Azure.Response<KeyVaultKey>>>();

        /// <summary>
        /// Holds references to the Azure Key Vault keys and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// </summary>
        private readonly Dictionary<string, KeyVaultKey> keyDictionary = new Dictionary<string, KeyVaultKey>();

        /// <summary>
        /// Holds references to the Azure Key Vault CryptographyClient objects and maps them to their corresponding Azure Key Vault Key Identifier (URI).
        /// </summary>
        private readonly Dictionary<string, CryptographyClient> cryptoClientDictionary = new Dictionary<string, CryptographyClient>();

        /// <summary>
        /// Constructs a new KeyCryptographer
        /// </summary>
        /// <param name="tokenCredential"></param>
        /// <param name="trustedEndpoints"></param>
        public KeyCryptographer(TokenCredential tokenCredential)
        {
            TokenCredential = tokenCredential;
        }

        public KeyCryptographer(AuthenticationCallback authenticationCallback)
        {
            AuthenticationCallback = authenticationCallback;
            isUsingLegacyAuthentication = true;
        }

        /// <summary>
        /// Adds the key, specified by the Key Identifier URI, to the cache.
        /// </summary>
        /// <param name="keyIdentifierUri"></param>
        public void AddKey(string keyIdentifierUri)
        {
            if (TheKeyHasNotBeenCached(keyIdentifierUri))
            {
                if (isUsingLegacyAuthentication)
                {
                    TokenCredential = new AzureKeyVaultProviderTokenCredential(AuthenticationCallback, keyIdentifierUri);
                }

                ParseAKVPath(keyIdentifierUri, out Uri vaultUri, out string keyName);
                CreateKeyClient(vaultUri);
                FetchKey(vaultUri, keyName, keyIdentifierUri);
            }

            bool TheKeyHasNotBeenCached(string k) => !keyDictionary.ContainsKey(k) && !keyFetchTaskDictionary.ContainsKey(k);
        }

        /// <summary>
        /// Returns the key specified by the Key Identifier URI
        /// </summary>
        /// <param name="keyIdentifierUri"></param>
        /// <returns></returns>
        public KeyVaultKey GetKey(string keyIdentifierUri)
        {
            if (keyDictionary.ContainsKey(keyIdentifierUri))
            {
                return keyDictionary[keyIdentifierUri];
            }

            if (keyFetchTaskDictionary.ContainsKey(keyIdentifierUri))
            {
                return Task.Run(() => keyFetchTaskDictionary[keyIdentifierUri]).Result;
            }

            throw new KeyNotFoundException($"The key with identifier {keyIdentifierUri} was not found.");
        }

        /// <summary>
        /// Gets the public Key size in bytes.
        /// </summary>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <returns></returns>
        public int GetKeySize(string keyIdentifierUri)
        {
            return GetKey(keyIdentifierUri).Key.N.Length;
        }

        /// <summary>
        /// Generates signature based on RSA PKCS#v1.5 scheme using a specified Azure Key Vault Key URL. 
        /// </summary>
        /// <param name="digest">The data to sign</param>
        /// <param name="keyIdentifierUri">The key vault key identifier URI</param>
        /// <returns></returns>
        public byte[] SignData(byte[] message, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            return cryptographyClient.SignData(RS256, message).Signature;
        }

        public bool VerifyData(byte[] message, byte[] signature, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            return cryptographyClient.VerifyData(RS256, message, signature).IsValid;
        }

        public byte[] UnwrapKey(KeyWrapAlgorithm keyWrapAlgorithm, byte[] encryptedKey, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            return cryptographyClient.UnwrapKey(keyWrapAlgorithm, encryptedKey).Key;
        }

        public byte[] WrapKey(KeyWrapAlgorithm keyWrapAlgorithm, byte[] key, string keyIdentifierUri)
        {
            CryptographyClient cryptographyClient = GetCryptographyClient(keyIdentifierUri);
            return cryptographyClient.WrapKey(keyWrapAlgorithm, key).EncryptedKey;
        }

        private CryptographyClient GetCryptographyClient(string keyIdentifierUri)
        {
            if (cryptoClientDictionary.ContainsKey(keyIdentifierUri))
            {
                return cryptoClientDictionary[keyIdentifierUri];
            }

            CryptographyClient cryptographyClient = new CryptographyClient(GetKey(keyIdentifierUri).Id, TokenCredential);
            cryptoClientDictionary[keyIdentifierUri] = cryptographyClient;

            return cryptographyClient;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        /// <param name="keyName">The name of the Azure Key Vault key</param>
        /// <param name="keyResourceUri">The Azure Key Vault key identifier</param>
        private void FetchKey(Uri vaultUri, string keyName, string keyResourceUri)
        {
            var fetchKeyTask = FetchKeyFromKeyVault(vaultUri, keyName);
            keyFetchTaskDictionary[keyResourceUri] = fetchKeyTask;

            fetchKeyTask
                .ContinueWith(k => ValidateRsaKey(k.Result))
                .ContinueWith(k => keyDictionary[keyResourceUri] = k.Result);

            Task.Run(() => fetchKeyTask);
        }

        /// <summary>
        /// Looks up the KeyClient object by it's URI and then fethces the key by name.
        /// </summary>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        /// <param name="keyName">Then name of the key</param>
        /// <returns></returns>
        private Task<Azure.Response<KeyVaultKey>> FetchKeyFromKeyVault(Uri vaultUri, string keyName) => keyClientDictionary[vaultUri].GetKeyAsync(keyName);

        /// <summary>
        /// Validates that a key is of type RSA
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private KeyVaultKey ValidateRsaKey(KeyVaultKey key)
        {            
            if (key.KeyType != KeyType.Rsa && key.KeyType != KeyType.RsaHsm)
            {
                throw new FormatException($"The key format should be RSA but found a key of type {key.KeyType}");
            }

            return key;
        }

        /// <summary>
        /// Instantiates and adds a KeyCleint to the KeyClient dictionary
        /// </summary>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        private void CreateKeyClient(Uri vaultUri)
        {
            if (!keyClientDictionary.ContainsKey(vaultUri))
            {
                keyClientDictionary[vaultUri] = new KeyClient(vaultUri, TokenCredential);
            }
        }

        /// <summary>
        /// Validates zand parses the Azure Key Vault URI and key name.
        /// </summary>
        /// <param name="masterKeyPath">The Azure Key Vault key identifier</param>
        /// <param name="vaultUri">The Azure Key Vault URI</param>
        /// <param name="masterKeyName">The name of the key</param>
        private void ParseAKVPath(string masterKeyPath, out Uri vaultUri, out string masterKeyName)
        {
            Uri masterKeyPathUri = new Uri(masterKeyPath);
            vaultUri = new Uri(masterKeyPathUri.GetLeftPart(UriPartial.Authority));
            masterKeyName = masterKeyPathUri.Segments[2];
        }
    }
}
