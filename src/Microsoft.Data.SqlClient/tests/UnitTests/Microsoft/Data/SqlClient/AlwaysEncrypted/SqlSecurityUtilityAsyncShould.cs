// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The Always Encrypted utility layer is internal and nullable-oblivious, and several of its members
// legitimately accept or produce nulls. Nullable analysis is disabled for this file so the tests can
// mirror those signatures exactly.
#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for the async counterparts of the Always Encrypted utility layer:
    /// <see cref="SqlSecurityUtility"/>.DecryptSymmetricKeyAsync / VerifyColumnMasterKeySignatureAsync
    /// and <see cref="SqlSymmetricKeyCache"/>.GetKeyAsync.
    /// </summary>
    public class SqlSecurityUtilityAsyncShould
    {
        private const string ProviderName = "TEST_ASYNC_PROVIDER";
        private const string SecondProviderName = "TEST_ASYNC_PROVIDER_2";
        private const string KeyPath = "test-key-path";
        private const string SecondKeyPath = "test-key-path-2";
        private const string EncryptionAlgorithm = "RSA_OAEP";

        // The AEAD_AES_256_CBC_HMAC_SHA256 algorithm requires a 256 bit root key.
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

            (SqlClientSymmetricKey key, SqlEncryptionKeyInfo keyInfoChosen) =
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
                encryptionType: (byte)SqlClientEncryptionType.Deterministic,
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

        #endregion

        #region VerifyColumnMasterKeySignatureAsync

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

        #endregion

        #region SqlSymmetricKeyCache.GetKeyAsync

        /// <summary>
        /// Verifies that a key cached by the sync path is reused by the async path without a second
        /// provider call, and vice versa.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_ReusesEntriesCachedBySyncPath_AndViceVersa()
        {
            SqlSymmetricKeyCache cache = SqlSymmetricKeyCache.GetInstance();

            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 11) };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Sync first, then async.
            SqlEncryptionKeyInfo syncFirst = NewKeyInfo();
            SqlClientSymmetricKey fromSync = cache.GetKey(syncFirst, connection, command: null);
            SqlClientSymmetricKey fromAsync = await cache.GetKeyAsync(syncFirst, connection, command: null, CancellationToken.None);

            Assert.Same(fromSync, fromAsync);
            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(0, provider.DecryptAsyncCallCount);

            // Async first, then sync.
            SqlEncryptionKeyInfo asyncFirst = NewKeyInfo();
            SqlClientSymmetricKey asyncKey = await cache.GetKeyAsync(asyncFirst, connection, command: null, CancellationToken.None);
            SqlClientSymmetricKey syncKey = cache.GetKey(asyncFirst, connection, command: null);

            Assert.Same(asyncKey, syncKey);
            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Verifies the documented consequence of check-release-fetch-relock (FR-012/FR-014): concurrent
        /// async cache misses for the same key may each decrypt it, but all callers end up observing the
        /// same cached key instance, and neither caller deadlocks behind the process-wide cache gate.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_WithConcurrentCacheMisses_FetchesTwiceButPublishesOneKey()
        {
            SqlSymmetricKeyCache cache = SqlSymmetricKeyCache.GetInstance();

            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int entered = 0;
            TaskCompletionSource<bool> bothEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = async _ =>
                {
                    if (Interlocked.Increment(ref entered) == 2)
                    {
                        bothEntered.TrySetResult(true);
                    }
                    await release.Task;
                    return NewPlaintextKey(seed: 21);
                }
            };

            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();

            Task<SqlClientSymmetricKey> first = cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None);
            Task<SqlClientSymmetricKey> second = cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None);

            // Both callers reach the provider concurrently, which proves the cache gate is not held
            // across the awaited decryption.
            await bothEntered.Task;
            release.TrySetResult(true);

            SqlClientSymmetricKey firstKey = await first;
            SqlClientSymmetricKey secondKey = await second;

            Assert.Equal(2, provider.DecryptAsyncCallCount);
            Assert.Same(firstKey, secondKey);

            // Subsequent callers, sync or async, see the published instance without another fetch.
            Assert.Same(firstKey, cache.GetKey(keyInfo, connection, command: null));
            Assert.Same(firstKey, await cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None));
            Assert.Equal(2, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// Verifies that cancellation of GetKeyAsync is observed before the provider call and while it
        /// is in flight, and that nothing is published to the cache in either case.
        /// </summary>
        [Fact]
        public async Task GetKeyAsync_WhenCancelled_ProducesCancelledTaskAndDoesNotCache()
        {
            SqlSymmetricKeyCache cache = SqlSymmetricKeyCache.GetInstance();

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
            SqlClientSymmetricKey key = await cache.GetKeyAsync(keyInfo, connection, command: null, CancellationToken.None);
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

            internal Func<CancellationToken, Task<bool>> VerifyAsyncCallback { get; set; }

            internal int DecryptCallCount => _decryptCallCount;

            internal int DecryptAsyncCallCount => _decryptAsyncCallCount;

            internal int VerifyAsyncCallCount => _verifyAsyncCallCount;

            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                Interlocked.Increment(ref _decryptCallCount);
                return PlaintextKey;
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
