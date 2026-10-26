// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The Always Encrypted utility layer is internal and nullable-oblivious, and several of its members
// legitimately accept or produce nulls. Nullable analysis is disabled for this file so the tests can
// mirror those signatures exactly.
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for the async counterparts of the Always Encrypted utility layer:
    /// <see cref="SqlSecurityUtility"/>.DecryptSymmetricKeyAsync / VerifyColumnMasterKeySignatureAsync
    /// and <see cref="SymmetricKeyCache"/>.GetKeyAsync.
    /// </summary>
    public class SqlSecurityUtilityAsyncShould
    {
        private const string ProviderName = "TEST_ASYNC_PROVIDER";
        private const string SecondProviderName = "TEST_ASYNC_PROVIDER_2";
        private const string KeyPath = "test-key-path";
        private const string SecondKeyPath = "test-key-path-2";
        private const string EncryptionAlgorithm = "RSA_OAEP";

        /// <summary>Creates a deterministic 256-bit root key for encryption tests.</summary>
        /// <param name="seed">Starting value for the key bytes.</param>
        /// <returns>Key material of the length required by AEAD_AES_256_CBC_HMAC_SHA256.</returns>
        private static byte[] NewPlaintextKey(byte seed)
        {
            byte[] key = new byte[32];
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(seed + i);
            }
            return key;
        }

        /// <summary>
        /// Produces key material whose cache lookup key cannot collide with any other test. The CEK
        /// cache is a process-wide singleton shared by every test in this assembly.
        /// </summary>
        /// <param name="providerName">Name of the provider that can decrypt this key.</param>
        /// <param name="keyPath">Master key path supplied to that provider.</param>
        /// <returns>Key information with unique encrypted bytes for cache isolation.</returns>
        private static SqlEncryptionKeyInfo NewKeyInfo(string providerName = ProviderName, string keyPath = KeyPath)
            => new SqlEncryptionKeyInfo
            {
                encryptedKey = Guid.NewGuid().ToByteArray(),
                databaseId = 1,
                cekId = 1,
                cekVersion = 1,
                cekMdVersion = 1,
                keyPath = keyPath,
                keyStoreName = providerName,
                algorithmName = EncryptionAlgorithm,
            };

        /// <summary>Registers test providers on an unopened connection without changing global registrations.</summary>
        /// <param name="providers">Provider names and instances used by this connection.</param>
        /// <returns>An unopened connection that the caller must dispose.</returns>
        private static SqlConnection NewConnection(params (string Name, SqlColumnEncryptionKeyStoreProvider Provider)[] providers)
        {
            SqlConnection connection = new SqlConnection("Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> map = new();
            foreach ((string name, SqlColumnEncryptionKeyStoreProvider provider) in providers)
            {
                map[name] = provider;
            }
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(map);
            return connection;
        }

        /// <summary>Combines candidate keys so decryption can exercise provider fallback in order.</summary>
        /// <param name="keyInfos">Ordered candidates for the same plaintext encryption key.</param>
        /// <returns>A cipher info entry containing all candidate keys.</returns>
        private static SqlTceCipherInfoEntry NewCipherInfoEntry(params SqlEncryptionKeyInfo[] keyInfos)
        {
            SqlTceCipherInfoEntry entry = new SqlTceCipherInfoEntry(ordinal: 0);
            foreach (SqlEncryptionKeyInfo keyInfo in keyInfos)
            {
                entry.Add(keyInfo.encryptedKey, keyInfo.databaseId, keyInfo.cekId, keyInfo.cekVersion,
                    keyInfo.cekMdVersion, keyInfo.keyPath, keyInfo.keyStoreName, keyInfo.algorithmName);
            }
            return entry;
        }

        #region DecryptSymmetricKeyAsync - multi key fallback

        /// <summary>
        /// Verifies that the per-key fallback loop of the sync overload is preserved: when the first
        /// candidate key fails, the next one is attempted and its key info is the one reported.
        /// </summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_WhenFirstKeyFails_FallsBackToSecondKey()
        {
            byte[] expectedKey = NewPlaintextKey(seed: 7);
            TestKeyStoreProvider failing = new TestKeyStoreProvider { DecryptAsyncCallback = _ => throw new InvalidOperationException("first key unavailable") };
            TestKeyStoreProvider succeeding = new TestKeyStoreProvider { PlaintextKey = expectedKey };

            using SqlConnection connection = NewConnection((ProviderName, failing), (SecondProviderName, succeeding));

            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(
                NewKeyInfo(ProviderName, KeyPath),
                NewKeyInfo(SecondProviderName, SecondKeyPath));

            (SymmetricKey key, SqlEncryptionKeyInfo keyInfoChosen) =
                await SqlSecurityUtility.DecryptSymmetricKeyAsync(entry, connection, command: null, CancellationToken.None);

            Assert.NotNull(key);
            Assert.Equal(expectedKey, key.RootKey);
            Assert.Equal(SecondProviderName, keyInfoChosen.keyStoreName);
            Assert.Equal(1, failing.DecryptAsyncCallCount);
            Assert.Equal(1, succeeding.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Verifies that when every candidate key fails, the last exception is rethrown, matching the
        /// sync overload.
        /// </summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_WhenAllKeysFail_ThrowsLastException()
        {
            TestKeyStoreProvider failing = new TestKeyStoreProvider { DecryptAsyncCallback = _ => throw new InvalidOperationException("boom") };
            using SqlConnection connection = NewConnection((ProviderName, failing));

            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo(), NewKeyInfo());

            // Failures from the provider are reported as SQL.KeyDecryptionFailed.
            SqlException exception = await Assert.ThrowsAsync<SqlException>(
                () => SqlSecurityUtility.DecryptSymmetricKeyAsync(entry, connection, command: null, CancellationToken.None));

            Assert.Contains("boom", exception.ToString());
            Assert.Equal(2, failing.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Verifies that the SqlCipherMetadata overload mutates the metadata exactly as the sync overload does.
        /// </summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_WithCipherMetadata_PopulatesAlgorithmAndKeyInfo()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 3) };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            SqlCipherMetadata metadata = new SqlCipherMetadata(
                NewCipherInfoEntry(keyInfo),
                ordinal: 0,
                cipherAlgorithmId: (byte)TdsEnums.AEAD_AES_256_CBC_HMAC_SHA256,
                cipherAlgorithmName: null,
                encryptionType: (byte)EncryptionType.Deterministic,
                normalizationRuleVersion: 0x01);

            await SqlSecurityUtility.DecryptSymmetricKeyAsync(metadata, connection, command: null, CancellationToken.None);

            Assert.True(metadata.IsAlgorithmInitialized());
            Assert.NotNull(metadata.EncryptionKeyInfo);
            Assert.Equal(keyInfo.keyStoreName, metadata.EncryptionKeyInfo.keyStoreName);
        }

        #endregion

        #region DecryptSymmetricKeyAsync - cancellation

        /// <summary>
        /// Verifies that a token cancelled before the call is observed without invoking the provider.
        /// </summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_WithCancelledToken_CancelsWithoutInvokingProvider()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 1) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo());

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => SqlSecurityUtility.DecryptSymmetricKeyAsync(entry, connection, command: null, cts.Token));

            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Verifies that a cancellation observed mid-flight is neither swallowed by the per-key fallback
        /// loop nor wrapped in SQL.KeyDecryptionFailed, and that the remaining candidate keys are not tried.
        /// </summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_WhenCancelledMidFlight_DoesNotAttemptRemainingKeys()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool> providerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = async token =>
                {
                    providerEntered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, token);
                    return NewPlaintextKey(seed: 2);
                }
            };

            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo(), NewKeyInfo());

            Task decryptTask = SqlSecurityUtility.DecryptSymmetricKeyAsync(entry, connection, command: null, cts.Token);
            await providerEntered.Task;
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decryptTask);
            Assert.True(decryptTask.IsCanceled);

            // The second key was never attempted: cancellation abandons the loop.
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Cancellation must win over fallback-provider errors when an earlier key fails normally.
        /// </summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_WhenCancelledAfterKeyFailure_DoesNotValidateNextProvider()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = _ =>
                {
                    cts.Cancel();
                    throw new InvalidOperationException("first key unavailable");
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(
                NewKeyInfo(ProviderName), NewKeyInfo(SecondProviderName));

            Task decryptTask = SqlSecurityUtility.DecryptSymmetricKeyAsync(entry, connection, command: null, cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decryptTask);
            Assert.True(decryptTask.IsCanceled);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Cancellation during the final provider failure wins even when no next candidate checks the token.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task DecryptSymmetricKeyAsync_WhenFinalCandidateFailsAfterCancellation_ProducesCancelledTask(
            bool completeAsynchronously, bool multipleCandidates)
        {
            using CancellationTokenSource cts = new();
            TaskCompletionSource<byte[]> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = _ =>
                {
                    if (multipleCandidates && ++calls == 1)
                    {
                        return Task.FromException<byte[]>(new InvalidOperationException("First candidate failed"));
                    }
                    if (completeAsynchronously)
                    {
                        return result.Task;
                    }
                    cts.Cancel();
                    return Task.FromException<byte[]>(new InvalidOperationException("Final candidate failed"));
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = multipleCandidates
                ? NewCipherInfoEntry(NewKeyInfo(), NewKeyInfo())
                : NewCipherInfoEntry(NewKeyInfo());
            Task decrypt = SqlSecurityUtility.DecryptSymmetricKeyAsync(entry, connection, null, cts.Token);
            if (completeAsynchronously)
            {
                cts.Cancel();
                result.TrySetException(new InvalidOperationException("Final candidate failed"));
            }

            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decrypt);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(decrypt.IsCanceled);
            Assert.Equal(multipleCandidates ? 2 : 1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// A pre-cancelled operation must not be replaced by validation errors from an unregistered provider.
        /// </summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_WithCancelledToken_DoesNotValidateProvider()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider();
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo(SecondProviderName));
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Task decryptTask = SqlSecurityUtility.DecryptSymmetricKeyAsync(entry, connection, command: null, cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decryptTask);
            Assert.True(decryptTask.IsCanceled);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        #endregion

        #region VerifyColumnMasterKeySignatureAsync

        /// <summary>
        /// A normal provider exception must not replace cancellation requested during CMK verification.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task VerifyColumnMasterKeySignatureAsync_WhenProviderFailsAfterCancellation_ProducesCancelledTask(
            bool completeAsynchronously)
        {
            using CancellationTokenSource cts = new();
            TaskCompletionSource<bool> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new()
            {
                VerifyAsyncCallback = _ =>
                {
                    if (completeAsynchronously)
                    {
                        return result.Task;
                    }
                    cts.Cancel();
                    return Task.FromException<bool>(new InvalidOperationException("Verification failed"));
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, true, new byte[] { 1, 2, 3 }, connection, null, cts.Token);
            if (completeAsynchronously)
            {
                cts.Cancel();
                result.TrySetException(new InvalidOperationException("Verification failed"));
            }

            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(verify.IsCanceled);
            Assert.Equal(1, provider.VerifyAsyncCallCount);
        }

        /// <summary>
        /// Verifies that a valid signature completes and an invalid one is reported the same way the
        /// sync path reports it.
        /// </summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_ReportsProviderResult()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { VerifyResult = true };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            await SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, isEnclaveEnabled: false, CMKSignature: new byte[] { 1, 2, 3 },
                connection, command: null, CancellationToken.None);

            Assert.Equal(1, provider.VerifyAsyncCallCount);

            provider.VerifyResult = false;
            await Assert.ThrowsAsync<InvalidOperationException>(() => SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, isEnclaveEnabled: false, CMKSignature: new byte[] { 1, 2, 3 },
                connection, command: null, CancellationToken.None));
        }

        /// <summary>
        /// Cancellation during verification wins over either provider result, including synchronous completion.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task VerifyColumnMasterKeySignatureAsync_WhenProviderReturnsAfterCancellation_ProducesCancelledTask(
            bool verificationResult, bool completeAsynchronously)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                VerifyResult = verificationResult,
                VerifyAsyncCallback = _ =>
                {
                    if (completeAsynchronously)
                    {
                        return completion.Task;
                    }

                    cts.Cancel();
                    return Task.FromResult(verificationResult);
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            Task verification = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, isEnclaveEnabled: false, CMKSignature: new byte[] { 1, 2, 3 },
                connection, command: null, cts.Token);

            if (completeAsynchronously)
            {
                cts.Cancel();
                completion.SetResult(verificationResult);
            }

            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verification);
            Assert.Equal(cts.Token, exception.CancellationToken);
            Assert.True(verification.IsCanceled);

            provider.VerifyAsyncCallback = null;
            Task uncancelledVerification = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, isEnclaveEnabled: false, CMKSignature: new byte[] { 1, 2, 3 },
                connection, command: null, CancellationToken.None);

            if (verificationResult)
            {
                await uncancelledVerification;
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => uncancelledVerification);
            }

            Assert.Equal(2, provider.VerifyAsyncCallCount);
        }

        /// <summary>
        /// Verifies that provider failures are still wrapped in SQL.UnableToVerifyColumnMasterKeySignature.
        /// </summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_WhenProviderThrows_WrapsException()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                VerifyAsyncCallback = _ => throw new InvalidOperationException("verification exploded")
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                    ProviderName, KeyPath, isEnclaveEnabled: false, CMKSignature: new byte[] { 1, 2, 3 },
                    connection, command: null, CancellationToken.None));

            Assert.Contains("verification exploded", exception.ToString());
        }

        /// <summary>
        /// Pre-cancellation takes precedence over missing-signature and unregistered-provider errors.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task VerifyColumnMasterKeySignatureAsync_WithCancelledToken_DoesNotValidateSignatureOrProvider(bool missingSignature)
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { VerifyResult = true };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            string providerName = missingSignature ? ProviderName : SecondProviderName;
            byte[] signature = missingSignature ? Array.Empty<byte>() : new byte[] { 1, 2, 3 };
            Task verifyTask = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                providerName, KeyPath, isEnclaveEnabled: false, CMKSignature: signature,
                connection, command: null, cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifyTask);
            Assert.True(verifyTask.IsCanceled);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                    providerName, KeyPath, isEnclaveEnabled: false, CMKSignature: signature,
                    connection, command: null, CancellationToken.None));
            Assert.Equal(0, provider.VerifyAsyncCallCount);
        }

        /// <summary>
        /// Verifies that cancellation surfaces as a cancelled Task rather than being wrapped in
        /// SQL.UnableToVerifyColumnMasterKeySignature, both before and during the provider call.
        /// </summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_WhenCancelled_ProducesCancelledTask()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { VerifyResult = true };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            using CancellationTokenSource preCancelled = new CancellationTokenSource();
            preCancelled.Cancel();

            Task preCancelledTask = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, isEnclaveEnabled: false, CMKSignature: new byte[] { 1, 2, 3 },
                connection, command: null, preCancelled.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preCancelledTask);
            Assert.True(preCancelledTask.IsCanceled);
            Assert.Equal(0, provider.VerifyAsyncCallCount);

            using CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool> providerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.VerifyAsyncCallback = async token =>
            {
                providerEntered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return true;
            };

            Task verifyTask = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, "unique-" + Guid.NewGuid(), isEnclaveEnabled: false, CMKSignature: new byte[] { 4, 5, 6 },
                connection, command: null, cts.Token);

            await providerEntered.Task;
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifyTask);
            Assert.True(verifyTask.IsCanceled);
        }

        /// <summary>
        /// Cached positive and negative signature results must not override caller cancellation.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task VerifyColumnMasterKeySignatureAsync_WithCachedResult_ObservesCancellation(bool cachedResult)
        {
            string providerName = SqlColumnEncryptionCertificateStoreProvider.ProviderName;
            string keyPath = "cached-signature-" + Guid.NewGuid();
            byte[] signature = Guid.NewGuid().ToByteArray();
            using SqlConnection connection = new SqlConnection("Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
            ColumnMasterKeyMetadataSignatureVerificationCache.Instance.AddSignatureVerificationResult(
                providerName, keyPath, allowEnclaveComputations: true, signature, cachedResult);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Task verification = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                providerName, keyPath, isEnclaveEnabled: true, signature, connection, command: null, cts.Token);

            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verification);
            Assert.Equal(cts.Token, exception.CancellationToken);
            Assert.True(verification.IsCanceled);

            if (cachedResult)
            {
                SqlSecurityUtility.VerifyColumnMasterKeySignature(
                    providerName, keyPath, isEnclaveEnabled: true, signature, connection, command: null);
                await SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                    providerName, keyPath, isEnclaveEnabled: true, signature, connection, command: null, CancellationToken.None);
            }
            else
            {
                InvalidOperationException syncException = Assert.Throws<InvalidOperationException>(() =>
                    SqlSecurityUtility.VerifyColumnMasterKeySignature(
                        providerName, keyPath, isEnclaveEnabled: true, signature, connection, command: null));
                InvalidOperationException asyncException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                        providerName, keyPath, isEnclaveEnabled: true, signature, connection, command: null, CancellationToken.None));
                Assert.Equal(syncException.Message, asyncException.Message);
            }
        }

        #endregion

        #region SymmetricKeyCache.GetKeyAsync

        /// <summary>
        /// Verifies that a key cached by the sync path is reused by the async path without a second
        /// provider call, and vice versa.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_ReusesEntriesCachedBySyncPath_AndViceVersa()
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 11) };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Sync first, then async.
            SqlEncryptionKeyInfo syncFirst = NewKeyInfo();
            SymmetricKey fromSync = cache.GetKey(syncFirst, connection, command: null);
            SymmetricKey fromAsync = await cache.GetKeyAsync(syncFirst, connection, command: null, CancellationToken.None);

            Assert.Same(fromSync, fromAsync);
            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(0, provider.DecryptAsyncCallCount);

            // Async first, then sync.
            SqlEncryptionKeyInfo asyncFirst = NewKeyInfo();
            SymmetricKey asyncKey = await cache.GetKeyAsync(asyncFirst, connection, command: null, CancellationToken.None);
            SymmetricKey syncKey = cache.GetKey(asyncFirst, connection, command: null);

            Assert.Same(asyncKey, syncKey);
            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// A warm cache must still honor cancellation, whether the entry was populated synchronously or asynchronously.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetKeyAsync_WithCachedKey_ObservesCancellation(bool populateAsync)
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 17) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            SymmetricKey cachedKey = populateAsync
                ? await cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None)
                : cache.GetKey(keyInfo, connection, command: null);
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Task<SymmetricKey> cancelled = cache.GetKeyAsync(keyInfo, connection, command: null, cts.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.True(cancelled.IsCanceled);
            Assert.Same(cachedKey, cache.GetKey(keyInfo, connection, command: null));
            Assert.Equal(populateAsync ? 0 : 1, provider.DecryptCallCount);
            Assert.Equal(populateAsync ? 1 : 0, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Concurrent cold-cache callers share one provider fetch and the published key instance.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_WithConcurrentCacheMisses_FetchesOnceAndPublishesOneKey()
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = async _ =>
                {
                    await release.Task;
                    return NewPlaintextKey(seed: 21);
                }
            };

            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            Task<SymmetricKey> first = cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None);
            Task<SymmetricKey>[] followers = Enumerable.Range(0, 8)
                .Select(_ => cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None)).ToArray();
            int callsBeforeRelease = provider.DecryptAsyncCallCount;
            release.TrySetResult(true);

            SymmetricKey firstKey = await first;
            SymmetricKey[] followerKeys = await Task.WhenAll(followers);

            Assert.Equal(1, callsBeforeRelease);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.All(followerKeys, key => Assert.Same(firstKey, key));

            // Subsequent callers, sync or async, see the published instance without another fetch.
            Assert.Same(firstKey, cache.GetKey(keyInfo, connection, command: null));
            Assert.Same(firstKey, await cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None));
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// Cancelling a waiting caller must not cancel the active fetch or prevent another key from loading.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_CancelledWaiter_DoesNotBlockOtherCallersOrKeys()
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new() { DecryptAsyncCallback = _ => result.Task };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            Task<SymmetricKey> first = cache.GetKeyAsync(keyInfo, connection, null, CancellationToken.None);
            using CancellationTokenSource cts = new();
            Task<SymmetricKey> cancelled = cache.GetKeyAsync(keyInfo, connection, null, cts.Token);
            Task<SymmetricKey> follower = cache.GetKeyAsync(keyInfo, connection, null, CancellationToken.None);
            cts.Cancel();

            try
            {
                Assert.Same(cancelled, await Task.WhenAny(cancelled, Task.Delay(TimeSpan.FromSeconds(5))));
                OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
                Assert.Equal(cts.Token, error.CancellationToken);
                Assert.False(first.IsCompleted);
                provider.DecryptAsyncCallback = null;
                Task<SymmetricKey> otherKey = cache.GetKeyAsync(NewKeyInfo(), connection, null, CancellationToken.None);
                Assert.Same(otherKey, await Task.WhenAny(otherKey, Task.Delay(TimeSpan.FromSeconds(5))));
                await otherKey;
                Assert.Equal(2, provider.DecryptAsyncCallCount);
            }
            finally
            {
                result.TrySetResult(NewPlaintextKey(seed: 22));
                await first;
                await follower;
                // Drain the task even when an assertion fails before the cancellation check.
                try { await cancelled; }
                catch (OperationCanceledException) { }
            }

            Assert.Same(await first, await follower);
            Assert.Equal(2, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Failed or cancelled owners release the per-key gate so a queued caller can retry independently.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetKeyAsync_UnsuccessfulOwner_AllowsWaitingCallerToRetry(bool cancelOwner)
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new() { DecryptAsyncCallback = _ => result.Task };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            using CancellationTokenSource cts = new();
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            Task<SymmetricKey> first = cache.GetKeyAsync(keyInfo, connection, null, cts.Token);
            Task<SymmetricKey> follower = cache.GetKeyAsync(keyInfo, connection, null, CancellationToken.None);
            int callsBeforeRelease = provider.DecryptAsyncCallCount;
            provider.DecryptAsyncCallback = null;
            if (cancelOwner)
            {
                cts.Cancel();
                result.TrySetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
                Assert.True(first.IsCanceled);
            }
            else
            {
                result.TrySetException(new InvalidOperationException("Provider failure"));
                await Assert.ThrowsAsync<SqlException>(() => first);
            }

            Assert.Same(follower, await Task.WhenAny(follower, Task.Delay(TimeSpan.FromSeconds(5))));
            SymmetricKey key = await follower;
            Assert.Equal(1, callsBeforeRelease);
            Assert.Equal(2, provider.DecryptAsyncCallCount);
            Assert.Same(key, await cache.GetKeyAsync(keyInfo, connection, null, CancellationToken.None));
        }

        /// <summary>
        /// Completed async decryption must not wait for unrelated synchronous provider I/O to publish its key.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetKeyAsync_PublishesWhileSynchronousProviderIsBlocked(bool sameKey)
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;
            TaskCompletionSource<byte[]> asyncResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> syncEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim releaseSync = new();
            byte[] expectedKey = NewPlaintextKey(seed: 43);
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = _ => asyncResult.Task,
                DecryptCallback = () =>
                {
                    syncEntered.TrySetResult(true);
                    releaseSync.Wait();
                    return NewPlaintextKey(seed: 44);
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo asyncKeyInfo = NewKeyInfo();
            SqlEncryptionKeyInfo syncKeyInfo = sameKey ? asyncKeyInfo : NewKeyInfo();
            Task<SymmetricKey> asyncKey = cache.GetKeyAsync(asyncKeyInfo, connection, command: null, CancellationToken.None);
            // Keep blocking provider I/O off the pool needed by the async continuations.
            Task<SymmetricKey> syncKey = Task.Factory.StartNew(
                () => cache.GetKey(syncKeyInfo, connection, command: null),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            try
            {
                Assert.Same(syncEntered.Task, await Task.WhenAny(syncEntered.Task, Task.Delay(TimeSpan.FromSeconds(5))));
                asyncResult.TrySetResult(expectedKey);
                Assert.Same(asyncKey, await Task.WhenAny(asyncKey, Task.Delay(TimeSpan.FromSeconds(5))));
                Assert.Equal(expectedKey, (await asyncKey).RootKey);
                Assert.False(syncKey.IsCompleted);
                Assert.Same(await asyncKey, cache.GetKey(asyncKeyInfo, connection, command: null));
            }
            finally
            {
                asyncResult.TrySetResult(expectedKey);
                releaseSync.Set();
                await syncKey;
                await asyncKey;
            }

            if (sameKey)
            {
                Assert.Same(await asyncKey, await syncKey);
            }
        }

        /// <summary>
        /// A decryption that has already succeeded must not be thrown away just because the caller
        /// cancelled while the result was being published. The remote work is done at that point, so
        /// discarding it would only force the next caller to repeat it.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_WhenCancelledAfterDecryptSucceeds_StillPublishesTheKey()
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            using CancellationTokenSource cts = new CancellationTokenSource();
            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = _ =>
                {
                    // Cancel between a successful provider call and the cache insertion.
                    cts.Cancel();
                    return Task.FromResult(NewPlaintextKey(seed: 57));
                }
            };

            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            SymmetricKey key = await cache.GetKeyAsync(keyInfo, connection, command: null, cts.Token);

            Assert.NotNull(key);
            Assert.Equal(1, provider.DecryptAsyncCallCount);

            // The key was published despite the cancellation, so no second decryption is needed.
            Assert.Same(key, cache.GetKey(keyInfo, connection, command: null));
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// Verifies that cancellation of GetKeyAsync is observed before the provider call and while it
        /// is in flight, and that nothing is published to the cache in either case.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_WhenCancelled_ProducesCancelledTaskAndDoesNotCache()
        {
            SymmetricKeyCache cache = SymmetricKeyCache.Instance;

            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 31) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            using CancellationTokenSource preCancelled = new CancellationTokenSource();
            preCancelled.Cancel();

            Task preCancelledTask = cache.GetKeyAsync(keyInfo, connection, command: null, preCancelled.Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preCancelledTask);
            Assert.True(preCancelledTask.IsCanceled);
            Assert.Equal(0, provider.DecryptAsyncCallCount);

            using CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool> providerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.DecryptAsyncCallback = async token =>
            {
                providerEntered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return NewPlaintextKey(seed: 32);
            };

            Task inFlight = cache.GetKeyAsync(keyInfo, connection, command: null, cts.Token);
            await providerEntered.Task;
            cts.Cancel();

            // Cancellation is not wrapped in SQL.KeyDecryptionFailed.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inFlight);
            Assert.True(inFlight.IsCanceled);

            // Nothing was cached, so a subsequent successful call still reaches the provider.
            provider.DecryptAsyncCallback = null;
            SymmetricKey key = await cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None);
            Assert.Equal(provider.PlaintextKey, key.RootKey);
        }

        #endregion

        /// <summary>
        /// A key store provider whose sync and async paths are separately observable, so that tests can
        /// assert which path was taken and how many times.
        /// </summary>
        private sealed class TestKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            private int _decryptCallCount;
            private int _decryptAsyncCallCount;
            private int _verifyAsyncCallCount;

            internal byte[] PlaintextKey { get; set; } = new byte[32];

            internal bool VerifyResult { get; set; }

            internal Func<CancellationToken, Task<byte[]>> DecryptAsyncCallback { get; set; }

            internal Func<byte[]> DecryptCallback { get; set; }

            internal Func<CancellationToken, Task<bool>> VerifyAsyncCallback { get; set; }

            internal int DecryptCallCount => _decryptCallCount;

            internal int DecryptAsyncCallCount => _decryptAsyncCallCount;

            internal int VerifyAsyncCallCount => _verifyAsyncCallCount;

            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                Interlocked.Increment(ref _decryptCallCount);
                return DecryptCallback is not null ? DecryptCallback() : PlaintextKey;
            }

            public override Task<byte[]> DecryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _decryptAsyncCallCount);
                Func<CancellationToken, Task<byte[]>> callback = DecryptAsyncCallback;
                return callback is not null ? callback(cancellationToken) : Task.FromResult(PlaintextKey);
            }

            public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
                => throw new NotSupportedException();

            public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
                => throw new NotSupportedException();

            public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
                => VerifyResult;

            public override Task<bool> VerifyColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations, byte[] signature, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _verifyAsyncCallCount);
                Func<CancellationToken, Task<bool>> callback = VerifyAsyncCallback;
                return callback is not null ? callback(cancellationToken) : Task.FromResult(VerifyResult);
            }
        }
    }
}
