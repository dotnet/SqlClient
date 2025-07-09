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
using System.Diagnostics.Tracing;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
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

        private static void ValidateAKVTraces(List<EventWrittenEventArgs> eventData, Guid threadActivityId)
        {
            Assert.NotNull(eventData);
            Assert.NotEmpty(eventData);
            int currentScope = 0;

            // Validate event data captured.
            Assert.All(eventData, item =>
            {
                Assert.Equal(DataTestUtility.AKVEventSourceName, item.EventSource.Name);
                Assert.Equal(threadActivityId, item.ActivityId);
                Assert.Equal(EventLevel.Informational, item.Level);
                Assert.NotNull(item.Payload);
                Assert.Single(item.Payload);
                switch (item.EventId)
                {
                    case 1: // Trace
                        Assert.Equal("WriteTrace", item.EventName);
                        Assert.Matches(@"Caller: \w+, Message: (\w\s*)*", item.Payload[0].ToString());
                        break;
                    case 2: // Scope Enter
                        Assert.Equal("ScopeEnter", item.EventName);
                        Assert.Equal(EventOpcode.Start, item.Opcode);
                        Assert.Matches(@"Entered Scope: \w+, Caller: \w*", item.Payload[0].ToString());
                        string str = item.Payload[0].ToString();
                        int.TryParse(str.Substring(15, str.IndexOf(',') - 1), out currentScope);
                        break;
                    case 3: // Scope Exit
                        Assert.Equal("ScopeExit", item.EventName);
                        Assert.Equal(EventOpcode.Stop, item.Opcode);
                        if (currentScope != 0)
                        {
                            Assert.Equal(currentScope, (int)item.Payload[0]);
                        }
                        break;
                    default:
                        Assert.Fail("Unexpected event occurred: " + item.Message);
                        break;
                }
            });
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void LegacyAuthenticationCallbackTest()
        {
            Guid activityId = Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            using DataTestUtility.AKVEventListener AKVListener = new();

            // SqlClientCustomTokenCredential implements legacy authentication callback to request access token at client-side.
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
            ValidateAKVTraces(AKVListener.EventData, activityId);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TokenCredentialTest()
        {
            Guid activityId = Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            using DataTestUtility.AKVEventListener AKVListener = new();

            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(DataTestUtility.GetTokenCredential());
            byte[] encryptedCek = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCek = akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCek);

            Assert.Equal(s_columnEncryptionKey, decryptedCek);
            ValidateAKVTraces(AKVListener.EventData, activityId);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TokenCredentialRotationTest()
        {
            Guid activityId = Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            using DataTestUtility.AKVEventListener AKVListener = new();

            // SqlClientCustomTokenCredential implements a legacy authentication callback to request the access token from the client-side.
            SqlColumnEncryptionAzureKeyVaultProvider oldAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new SqlClientCustomTokenCredential());

            SqlColumnEncryptionAzureKeyVaultProvider newAkvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(DataTestUtility.GetTokenCredential());

            byte[] encryptedCekWithNewProvider = newAkvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithOldProvider = oldAkvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCekWithNewProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithOldProvider);

            byte[] encryptedCekWithOldProvider = oldAkvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, s_columnEncryptionKey);
            byte[] decryptedCekWithNewProvider = newAkvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, EncryptionAlgorithm, encryptedCekWithOldProvider);
            Assert.Equal(s_columnEncryptionKey, decryptedCekWithNewProvider);

            ValidateAKVTraces(AKVListener.EventData, activityId);
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
            Guid activityId = Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            using DataTestUtility.AKVEventListener AKVListener = new();

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

            ValidateAKVTraces(AKVListener.EventData, activityId);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void SignatureVerificationResultIsCachedDuringVerification()
        {
            Guid activityId = Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            using DataTestUtility.AKVEventListener AKVListener = new();

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

            ValidateAKVTraces(AKVListener.EventData, activityId);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void CekCacheEntryIsEvictedAfterTtlExpires()
        {
            Guid activityId = Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            using DataTestUtility.AKVEventListener AKVListener = new();

            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new(new SqlClientCustomTokenCredential());
            akvProvider.ColumnEncryptionKeyCacheTtl = TimeSpan.FromSeconds(5);
            byte[] plaintextKey = [1, 2, 3];
            byte[] encryptedKey = akvProvider.EncryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", plaintextKey);

            akvProvider.DecryptColumnEncryptionKey(_fixture.GeneratedKeyUri, "RSA_OAEP", encryptedKey);
            Assert.True(CekCacheContainsKey(encryptedKey, akvProvider));
            Assert.Equal(1, GetCacheCount(cekCacheName, akvProvider));

            Thread.Sleep(TimeSpan.FromSeconds(5));
            Assert.False(CekCacheContainsKey(encryptedKey, akvProvider));
            Assert.Equal(0, GetCacheCount(cekCacheName, akvProvider));

            ValidateAKVTraces(AKVListener.EventData, activityId);
        }

        private static int GetCacheCount(string cacheName, SqlColumnEncryptionAzureKeyVaultProvider akvProvider)
        {
            var cacheInstance = GetCacheInstance(cacheName, akvProvider);
            Type cacheType = cacheInstance.GetType();
            PropertyInfo countProperty = cacheType.GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic);
            int countValue = (int)countProperty.GetValue(cacheInstance);
            return countValue;
        }

        private static bool CekCacheContainsKey(byte[] encryptedCek, SqlColumnEncryptionAzureKeyVaultProvider akvProvider)
        {
            var cacheInstance = GetCacheInstance("_columnEncryptionKeyCache", akvProvider);
            Type cacheType = cacheInstance.GetType();
            MethodInfo containsMethod = cacheType.GetMethod("Contains", BindingFlags.Instance | BindingFlags.NonPublic);
            bool containsResult = (bool)containsMethod.Invoke(cacheInstance, [ToHexString(encryptedCek)]);
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
