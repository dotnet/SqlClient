// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Xunit;
using Azure.Security.KeyVault.Keys;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [Trait("Set", "AE")]
    public class AKVUnitTests : IClassFixture<AzureKeyVaultKeyFixture>
    {
        const string EncryptionAlgorithm = "RSA_OAEP";
        public static readonly byte[] s_columnEncryptionKey = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        private const string cekCacheName = "_columnEncryptionKeyCache";
        private const string signatureVerificationResultCacheName = "_columnMasterKeyMetadataSignatureVerificationCache";

        private readonly AzureKeyVaultKeyFixture _fixture;

        public AKVUnitTests(AzureKeyVaultKeyFixture fixture)
        {
            _fixture = fixture;
        }
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void LegacyAuthenticationCallbackTest()
        {
            // SqlClientCustomTokenCredential implements legacy authentication callback to request access token at client-side.
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TokenCredentialTest()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(DataTestUtility.GetTokenCredential());
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TokenCredentialRotationTest()
        {
            // SqlClientCustomTokenCredential implements a legacy authentication callback to request the access token from the client-side.
            SqlColumnEncryptionAzureKeyVaultProvider oldAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());

            SqlColumnEncryptionAzureKeyVaultProvider newAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(DataTestUtility.GetTokenCredential());

            byte[] encryptedCekWithNewProvider = newAkvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithOldProvider = oldAkvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCekWithNewProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithOldProvider);

            byte[] encryptedCekWithOldProvider = oldAkvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithNewProvider = newAkvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCekWithOldProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithNewProvider);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void ReturnSpecifiedVersionOfKeyWhenItIsNotTheMostRecentVersion()
        {
            Uri keyPathUri = new Uri(DataTestUtility.AKVOriginalUrl);
            Uri vaultUri = new Uri(keyPathUri.GetLeftPart(UriPartial.Authority));

            //If key version is not specified then we cannot test.
            if (KeyIsVersioned(keyPathUri))
            {
                string keyName = keyPathUri.Segments[2];
                string keyVersion = keyPathUri.Segments[3];
                KeyClient keyClient = new KeyClient(vaultUri, DataTestUtility.GetTokenCredential());
                KeyVaultKey currentVersionKey = keyClient.GetKey(keyName);
                KeyVaultKey specifiedVersionKey = keyClient.GetKey(keyName, keyVersion);

                //If specified versioned key is the most recent version of the key then we cannot test.
                if (!KeyIsLatestVersion(specifiedVersionKey, currentVersionKey))
                {
                    SqlColumnEncryptionAzureKeyVaultProvider azureKeyProvider = new SqlColumnEncryptionAzureKeyVaultProvider(DataTestUtility.GetTokenCredential());
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
            SqlColumnEncryptionAzureKeyVaultProvider azureKeyProvider = new(new SqlClientCustomTokenCredential());
            string invalidKeyPath = "https://my-key-vault.vault.azure.net/keys";
            Exception ex1 = Assert.Throws<ArgumentException>(() => azureKeyProvider.EncryptColumnEncryptionKey(invalidKeyPath, EncryptionAlgorithm, s_columnEncryptionKey));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex1.Message);
            Exception ex2 = Assert.Throws<ArgumentException>(() => azureKeyProvider.DecryptColumnEncryptionKey(invalidKeyPath, EncryptionAlgorithm, s_columnEncryptionKey));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex2.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void DecryptedCekIsCachedDuringDecryption()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] plaintextKey1 = { 1, 2, 3 };
            byte[] plaintextKey2 = { 1, 2, 3 };
            byte[] plaintextKey3 = { 0, 1, 2, 3 };
            byte[] encryptedKey1 = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", plaintextKey1);
            byte[] encryptedKey2 = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", plaintextKey2);
            byte[] encryptedKey3 = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", plaintextKey3);

            byte[] decryptedKey1 = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", encryptedKey1);
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));
            Assert.Equal(plaintextKey1, decryptedKey1);

            decryptedKey1 = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", encryptedKey1);
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));
            Assert.Equal(plaintextKey1, decryptedKey1);

            byte[] decryptedKey2 = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", encryptedKey2);
            Assert.Equal(2, GetCacheCount(cekCacheName, akvProvider));
            Assert.Equal(plaintextKey2, decryptedKey2);

            byte[] decryptedKey3 = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", encryptedKey3);
            Assert.Equal(3, GetCacheCount(cekCacheName, akvProvider));
            Assert.Equal(plaintextKey3, decryptedKey3);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void SignatureVerificationResultIsCachedDuringVerification()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] signature = akvProvider.SignColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, true);
            byte[] signature2 = akvProvider.SignColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, true);
            byte[] signatureWithoutEnclave = akvProvider.SignColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, false);

            Assert.True(akvProvider.VerifyColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, true, signature));
            Assert.Equal(1, GetCacheCount(signatureVerificationResultCacheName, akvProvider));

            Assert.True(akvProvider.VerifyColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, true, signature));
            Assert.Equal(1, GetCacheCount(signatureVerificationResultCacheName, akvProvider));

            Assert.True(akvProvider.VerifyColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, true, signature2));
            Assert.Equal(1, GetCacheCount(signatureVerificationResultCacheName, akvProvider));

            Assert.True(akvProvider.VerifyColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, false, signatureWithoutEnclave));
            Assert.Equal(2, GetCacheCount(signatureVerificationResultCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void CekCacheEntryIsEvictedAfterTtlExpires()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            akvProvider.ColumnEncryptionKeyCacheTtl = TimeSpan.FromSeconds(5);
            byte[] plaintextKey = { 1, 2, 3 };
            byte[] encryptedKey = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", plaintextKey);

            akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", encryptedKey);
            Assert.True(CekCacheContainsKey(encryptedKey, akvProvider));
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));

            Thread.Sleep(TimeSpan.FromSeconds(5));
            Assert.False(CekCacheContainsKey(encryptedKey, akvProvider));
            Assert.Equal(0, GetCacheCount(cekCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void CekCacheShouldBeDisabledWhenCustomProviderIsRegisteredGlobally()
        {
            if (SQLSetupStrategyAzureKeyVault.IsAKVProviderRegistered)
            {
                SqlConnection conn = new();
                FieldInfo globalCacheField = conn.GetType().GetField(
                    "s_globalCustomColumnEncryptionKeyStoreProviders", BindingFlags.Static | BindingFlags.NonPublic);
                IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> globalProviders =
                    globalCacheField.GetValue(conn) as IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider>;

                SqlColumnEncryptionAzureKeyVaultProvider akvProviderInGlobalCache =
                    globalProviders["AZURE_KEY_VAULT"] as SqlColumnEncryptionAzureKeyVaultProvider;
                byte[] plaintextKey = { 1, 2, 3 };
                byte[] encryptedKey = akvProviderInGlobalCache.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", plaintextKey);

                akvProviderInGlobalCache.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", encryptedKey);
                Assert.Equal(0, GetCacheCount(cekCacheName, akvProviderInGlobalCache));
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task EncryptAndDecryptColumnEncryptionKeyAsyncRoundTrips()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(DataTestUtility.GetTokenCredential());

            byte[] encryptedCek = await akvProvider.EncryptColumnEncryptionKeyAsync(
                _fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = await akvProvider.DecryptColumnEncryptionKeyAsync(
                _fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task AsyncAndSyncEncryptedKeysAreInterchangeable()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(DataTestUtility.GetTokenCredential());

            byte[] syncEncryptedCek = akvProvider.EncryptColumnEncryptionKey(
                _fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] asyncDecryptedCek = await akvProvider.DecryptColumnEncryptionKeyAsync(
                _fixture.GeneratedKeyUri, EncryptionAlgorithm, syncEncryptedCek);
            Assert.Equal(s_columnEncryptionKey, asyncDecryptedCek);

            byte[] asyncEncryptedCek = await akvProvider.EncryptColumnEncryptionKeyAsync(
                _fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] syncDecryptedCek = akvProvider.DecryptColumnEncryptionKey(
                _fixture.GeneratedKeyUri, EncryptionAlgorithm, asyncEncryptedCek);
            Assert.Equal(s_columnEncryptionKey, syncDecryptedCek);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task SignAndVerifyColumnMasterKeyMetadataAsyncRoundTrips()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(DataTestUtility.GetTokenCredential());

            byte[] signature = await akvProvider.SignColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, true);
            Assert.True(await akvProvider.VerifyColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, true, signature));

            // Signatures produced by the sync path must verify on the async path and vice versa.
            byte[] syncSignature = akvProvider.SignColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, false);
            Assert.True(await akvProvider.VerifyColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, false, syncSignature));
            Assert.True(akvProvider.VerifyColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, true, signature));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task DecryptedCekIsCachedDuringAsyncDecryption()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] plaintextKey1 = { 1, 2, 3 };
            byte[] plaintextKey2 = { 0, 1, 2, 3 };
            byte[] encryptedKey1 = await akvProvider.EncryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, plaintextKey1);
            byte[] encryptedKey2 = await akvProvider.EncryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, plaintextKey2);

            byte[] decryptedKey1 = await akvProvider.DecryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey1);
            Assert.Equal(plaintextKey1, decryptedKey1);
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));
            Assert.True(CekCacheContainsKey(encryptedKey1, akvProvider));

            // A repeated decryption of the same encrypted key must be served from the cache.
            decryptedKey1 = await akvProvider.DecryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey1);
            Assert.Equal(plaintextKey1, decryptedKey1);
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));

            byte[] decryptedKey2 = await akvProvider.DecryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey2);
            Assert.Equal(plaintextKey2, decryptedKey2);
            Assert.Equal(2, GetCacheCount(cekCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task CekCacheIsSharedBetweenSyncAndAsyncDecryption()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] plaintextKey = { 1, 2, 3 };
            byte[] encryptedKey = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, plaintextKey);

            akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey);
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));

            byte[] decryptedKey = await akvProvider.DecryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey);
            Assert.Equal(plaintextKey, decryptedKey);
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task CekCachingIsDisabledForAsyncDecryptionWhenTtlIsZero()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            akvProvider.ColumnEncryptionKeyCacheTtl = TimeSpan.Zero;
            byte[] plaintextKey = { 1, 2, 3 };
            byte[] encryptedKey = await akvProvider.EncryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, plaintextKey);

            byte[] decryptedKey = await akvProvider.DecryptColumnEncryptionKeyAsync(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey);

            Assert.Equal(plaintextKey, decryptedKey);
            Assert.Equal(0, GetCacheCount(cekCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task SignatureVerificationResultIsCachedDuringAsyncVerification()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] signature = await akvProvider.SignColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, true);
            byte[] signatureWithoutEnclave = await akvProvider.SignColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, false);

            Assert.True(await akvProvider.VerifyColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, true, signature));
            Assert.Equal(1, GetCacheCount(signatureVerificationResultCacheName, akvProvider));

            Assert.True(await akvProvider.VerifyColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, true, signature));
            Assert.Equal(1, GetCacheCount(signatureVerificationResultCacheName, akvProvider));

            Assert.True(await akvProvider.VerifyColumnMasterKeyMetadataAsync(_fixture.GeneratedKeyUri, false, signatureWithoutEnclave));
            Assert.Equal(2, GetCacheCount(signatureVerificationResultCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task CancelledAsyncDecryptionsDoNotAccumulateCreationGates()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] plaintextKey = { 1, 2, 3 };

            // A gate is only created once a caller reaches the cache, so the cancellation being
            // exercised here must happen while a caller waits on a gate another caller holds.
            // Each iteration uses a distinct cache key, so a gate that is abandoned rather than
            // removed would accumulate without bound.
            for (int i = 0; i < 20; i++)
            {
                byte[] encryptedKey = akvProvider.EncryptColumnEncryptionKey(
                    _fixture.GeneratedKeyUri, EncryptionAlgorithm, plaintextKey);

                // The first caller takes the gate and holds it for the duration of the key vault
                // round trip. It is not cancelled, so it always creates the entry.
                Task<byte[]> gateOwner = Task.Run(() => akvProvider.DecryptColumnEncryptionKeyAsync(
                    _fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey));

                // The remaining callers queue behind it and are cancelled while waiting.
                using CancellationTokenSource cts = new();
                Task<byte[]>[] waiters = new Task<byte[]>[8];
                for (int j = 0; j < waiters.Length; j++)
                {
                    waiters[j] = Task.Run(() => akvProvider.DecryptColumnEncryptionKeyAsync(
                        _fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey, cts.Token));
                }

                cts.Cancel();

                // A waiter either observed the cancellation or completed first; both are valid.
                foreach (Task<byte[]> waiter in waiters)
                {
                    try
                    {
                        Assert.Equal(plaintextKey, await waiter);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                Assert.Equal(plaintextKey, await gateOwner);
            }

            Assert.Equal(0, GetEntryCreationGateCount(cekCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task ConcurrentAsyncDecryptionOfSameKeyIsDeduplicated()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] plaintextKey = { 1, 2, 3 };
            byte[] encryptedKey = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, plaintextKey);

            Task<byte[]>[] decryptions = new Task<byte[]>[32];
            for (int i = 0; i < decryptions.Length; i++)
            {
                decryptions[i] = Task.Run(() => akvProvider.DecryptColumnEncryptionKeyAsync(
                    _fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey));
            }

            byte[][] decryptedKeys = await Task.WhenAll(decryptions);

            foreach (byte[] decryptedKey in decryptedKeys)
            {
                Assert.Equal(plaintextKey, decryptedKey);
            }

            // Concurrent misses for the same key must collapse into a single cache entry.
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task AsyncApisHonorCancellationToken()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            byte[] encryptedKey = akvProvider.EncryptColumnEncryptionKey(
                _fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] signature = akvProvider.SignColumnMasterKeyMetadata(_fixture.GeneratedKeyUri, true);

            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => akvProvider.EncryptColumnEncryptionKeyAsync(
                    _fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey, cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => akvProvider.DecryptColumnEncryptionKeyAsync(
                    _fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedKey, cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => akvProvider.SignColumnMasterKeyMetadataAsync(
                    _fixture.GeneratedKeyUri, true, cts.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => akvProvider.VerifyColumnMasterKeyMetadataAsync(
                    _fixture.GeneratedKeyUri, true, signature, cts.Token));

            // Cancellation is observed before argument validation, matching the base class.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => akvProvider.DecryptColumnEncryptionKeyAsync(
                    "https://my-key-vault.vault.azure.net/keys", EncryptionAlgorithm, encryptedKey, cts.Token));
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public async Task AsyncApisValidateMasterKeyPath()
        {
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            string invalidKeyPath = "https://my-key-vault.vault.azure.net/keys";

            ArgumentException ex1 = await Assert.ThrowsAsync<ArgumentException>(
                () => akvProvider.EncryptColumnEncryptionKeyAsync(invalidKeyPath, EncryptionAlgorithm, s_columnEncryptionKey));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex1.Message);

            ArgumentException ex2 = await Assert.ThrowsAsync<ArgumentException>(
                () => akvProvider.DecryptColumnEncryptionKeyAsync(invalidKeyPath, EncryptionAlgorithm, s_columnEncryptionKey));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex2.Message);

            ArgumentException ex3 = await Assert.ThrowsAsync<ArgumentException>(
                () => akvProvider.SignColumnMasterKeyMetadataAsync(invalidKeyPath, true));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex3.Message);

            ArgumentException ex4 = await Assert.ThrowsAsync<ArgumentException>(
                () => akvProvider.VerifyColumnMasterKeyMetadataAsync(invalidKeyPath, true, s_columnEncryptionKey));
            Assert.Contains($"Invalid url specified: '{invalidKeyPath}'", ex4.Message);
        }

        private static int GetCacheCount(string cacheName, SqlColumnEncryptionAzureKeyVaultProvider akvProvider)
        {
            var cacheInstance = GetCacheInstance(cacheName, akvProvider);
            Type cacheType = cacheInstance.GetType();
            PropertyInfo countProperty = cacheType.GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic);
            int countValue = (int)countProperty.GetValue(cacheInstance);
            return countValue;
        }

        private static int GetEntryCreationGateCount(string cacheName, SqlColumnEncryptionAzureKeyVaultProvider akvProvider)
        {
            var cacheInstance = GetCacheInstance(cacheName, akvProvider);
            Type cacheType = cacheInstance.GetType();
            PropertyInfo gateCountProperty = cacheType.GetProperty(
                "EntryCreationGateCount", BindingFlags.Instance | BindingFlags.NonPublic);
            return (int)gateCountProperty.GetValue(cacheInstance);
        }

        private static bool CekCacheContainsKey(byte[] encryptedCek, SqlColumnEncryptionAzureKeyVaultProvider akvProvider)
        {
            var cacheInstance = GetCacheInstance("_columnEncryptionKeyCache", akvProvider);
            Type cacheType = cacheInstance.GetType();
            MethodInfo containsMethod = cacheType.GetMethod("Contains", BindingFlags.Instance | BindingFlags.NonPublic);
            bool containsResult = (bool)containsMethod.Invoke(cacheInstance, new object[] { ToHexString(encryptedCek) });
            return containsResult;
        }

        private static object GetCacheInstance(string cacheName, SqlColumnEncryptionAzureKeyVaultProvider akvProvider)
        {
            Assembly akvProviderAssembly = typeof(SqlColumnEncryptionAzureKeyVaultProvider).Assembly;
            Type akvProviderType = akvProviderAssembly.GetType(
                "Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.SqlColumnEncryptionAzureKeyVaultProvider");
            FieldInfo cacheField = akvProviderType.GetField(cacheName, BindingFlags.Instance | BindingFlags.NonPublic);
            return cacheField.GetValue(akvProvider);
        }

        private static string ToHexString(byte[] source)
        {
            if (source is null)
            {
                return null;
            }

            return "0x" + BitConverter.ToString(source).Replace("-", "");
        }
    }
}
