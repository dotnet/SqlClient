// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Azure.Identity;
using Xunit;
using Azure.Security.KeyVault.Keys;
using Azure.Core;
using System.Reflection;
using System;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public static class AKVUnitTests
    {
        const string EncryptionAlgorithm = "RSA_OAEP";
        public static readonly byte[] s_columnEncryptionKey = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void LegacyAuthenticationCallbackTest()
        {
            // SqlClientCustomTokenCredential implements legacy authentication callback to request access token at client-side.
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void TokenCredentialTest()
        {
            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(DataTestUtility.AKVTenantId, DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientSecretCredential);
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void TokenCredentialRotationTest()
        {
            // SqlClientCustomTokenCredential implements a legacy authentication callback to request the access token from the client-side.
            SqlColumnEncryptionAzureKeyVaultProvider oldAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());

            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(DataTestUtility.AKVTenantId, DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
            SqlColumnEncryptionAzureKeyVaultProvider newAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientSecretCredential);

            byte[] encryptedCekWithNewProvider = newAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithOldProvider = oldAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithNewProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithOldProvider);

            byte[] encryptedCekWithOldProvider = oldAkvProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithNewProvider = newAkvProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, EncryptionAlgorithm, encryptedCekWithOldProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithNewProvider);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void ReturnSpecifiedVersionOfKeyWhenItIsNotTheMostRecentVersion()
        {
            Uri keyPathUri = new Uri(DataTestUtility.AKVOriginalUrl);
            Uri vaultUri = new Uri(keyPathUri.GetLeftPart(UriPartial.Authority));

            //If key version is not specified then we cannot test.
            if (KeyIsVersioned(keyPathUri))
            {
                string keyName = keyPathUri.Segments[2];
                string keyVersion = keyPathUri.Segments[3];
                ClientSecretCredential clientSecretCredential = new ClientSecretCredential(DataTestUtility.AKVTenantId, DataTestUtility.AKVClientId, DataTestUtility.AKVClientSecret);
                KeyClient keyClient = new KeyClient(vaultUri, clientSecretCredential);
                KeyVaultKey currentVersionKey = keyClient.GetKey(keyName);
                KeyVaultKey specifiedVersionKey = keyClient.GetKey(keyName, keyVersion);

                //If specified versioned key is the most recent version of the key then we cannot test.
                if (!KeyIsLatestVersion(specifiedVersionKey, currentVersionKey))
                {
                    SqlColumnEncryptionAzureKeyVaultProvider azureKeyProvider = new SqlColumnEncryptionAzureKeyVaultProvider(clientSecretCredential);
                    // Perform an operation to initialize the internal caches
                    azureKeyProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVOriginalUrl, EncryptionAlgorithm, s_columnEncryptionKey);
                    
                    PropertyInfo keyCryptographerProperty = azureKeyProvider.GetType().GetProperty("KeyCryptographer", BindingFlags.NonPublic | BindingFlags.Instance);
                    var keyCryptographer = keyCryptographerProperty.GetValue(azureKeyProvider);
                    MethodInfo getKeyMethod = keyCryptographer.GetType().GetMethod("GetKey", BindingFlags.NonPublic | BindingFlags.Instance);
                    KeyVaultKey key = (KeyVaultKey)getKeyMethod.Invoke(keyCryptographer, new[] { DataTestUtility.AKVOriginalUrl });

                    Assert.Equal(keyVersion, key.Properties.Version);
                }
            }
        }

        static bool KeyIsVersioned(Uri keyPath) => keyPath.Segments.Length > 3;
        static bool KeyIsLatestVersion(KeyVaultKey specifiedVersionKey, KeyVaultKey currentVersionKey) => currentVersionKey.Properties.Version == specifiedVersionKey.Properties.Version;

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public static void ThrowWhenUrlHasLessThanThreeSegments()
        {
            SqlColumnEncryptionAzureKeyVaultProvider azureKeyProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());
            string invalidKeyPath = "https://my-key-vault.vault.azure.net/keys";
            Exception ex1 = Assert.Throws<ArgumentException>(() => azureKeyProvider.EncryptColumnEncryptionKey(invalidKeyPath, EncryptionAlgorithm, s_columnEncryptionKey));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex1.Message);
            Exception ex2 = Assert.Throws<ArgumentException>(() => azureKeyProvider.DecryptColumnEncryptionKey(invalidKeyPath, EncryptionAlgorithm, s_columnEncryptionKey));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex2.Message);
        }
    }
}
