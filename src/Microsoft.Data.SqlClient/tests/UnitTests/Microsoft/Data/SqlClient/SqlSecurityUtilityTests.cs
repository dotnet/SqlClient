// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Internal AE metadata signatures predate nullable annotations.
#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Verifies async key decryption, candidate fallback, and signature verification contracts.
    /// </summary>
    public class SqlSecurityUtilityTests
    {
        private const string ProviderName = "TEST_ASYNC_PROVIDER";
        private const string SecondProviderName = "TEST_ASYNC_PROVIDER_2";
        private const string KeyPath = "test-key-path";
        private const string SecondKeyPath = "test-key-path-2";
        private const string EncryptionAlgorithm = "RSA_OAEP";

        /// <summary>Creates deterministic 256-bit key material for the encryption algorithm.</summary>
        /// <param name="seed">Starting value for the key bytes.</param>
        /// <returns>A root key of the required length.</returns>
        private static byte[] NewPlaintextKey(byte seed)
        {
            byte[] key = new byte[32];
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(seed + i);
            }
            return key;
        }

        /// <summary>Creates unique encrypted bytes to isolate process-wide cache lookups.</summary>
        /// <param name="providerName">Provider registered for the candidate.</param>
        /// <param name="keyPath">Master key path supplied to that provider.</param>
        /// <returns>A candidate key with a unique cache identity.</returns>
        private static SqlEncryptionKeyInfo NewKeyInfo(
            string providerName = ProviderName, string keyPath = KeyPath)
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

        /// <summary>Registers providers without changing process-wide registrations.</summary>
        /// <param name="providers">Provider names and instances for this connection.</param>
        /// <returns>An unopened connection that the caller must dispose.</returns>
        private static SqlConnection NewConnection(
            params (string Name, SqlColumnEncryptionKeyStoreProvider Provider)[] providers)
        {
            SqlConnection connection = new(
                "Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> map = new();
            foreach ((string name, SqlColumnEncryptionKeyStoreProvider provider) in providers)
            {
                map[name] = provider;
            }
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(map);
            return connection;
        }

        /// <summary>Combines candidates to exercise ordered decryption fallback.</summary>
        /// <param name="keyInfos">Candidates for the same plaintext encryption key.</param>
        /// <returns>A cipher entry containing the ordered candidates.</returns>
        private static SqlTceCipherInfoEntry NewCipherInfoEntry(
            params SqlEncryptionKeyInfo[] keyInfos)
        {
            SqlTceCipherInfoEntry entry = new(ordinal: 0);
            foreach (SqlEncryptionKeyInfo keyInfo in keyInfos)
            {
                entry.Add(keyInfo.encryptedKey, keyInfo.databaseId, keyInfo.cekId, keyInfo.cekVersion,
                    keyInfo.cekMdVersion, keyInfo.keyPath, keyInfo.keyStoreName, keyInfo.algorithmName);
            }
            return entry;
        }

        /// <summary>A failed candidate does not prevent the next key from being selected.</summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_FirstKeyFails_FallsBackToSecondKey()
        {
            // Arrange
            byte[] expectedKey = NewPlaintextKey(seed: 7);
            TestKeyStoreProvider failing = new()
            {
                DecryptAsyncCallback = _ => throw new InvalidOperationException("first key unavailable")
            };
            TestKeyStoreProvider succeeding = new() { PlaintextKey = expectedKey };
            using SqlConnection connection = NewConnection(
                (ProviderName, failing), (SecondProviderName, succeeding));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(
                NewKeyInfo(ProviderName, KeyPath), NewKeyInfo(SecondProviderName, SecondKeyPath));

            // Act
            (SymmetricKey key, SqlEncryptionKeyInfo chosen) =
                await SqlSecurityUtility.DecryptSymmetricKeyAsync(
                    entry, connection, command: null, CancellationToken.None);

            // Assert
            Assert.Equal(expectedKey, key.RootKey);
            Assert.Equal(SecondProviderName, chosen.keyStoreName);
            Assert.Equal(1, failing.DecryptAsyncCallCount);
            Assert.Equal(1, succeeding.DecryptAsyncCallCount);
            Assert.Equal(0, succeeding.DecryptCallCount);
        }

        /// <summary>Exhausting candidate keys reports the final wrapped provider error.</summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_AllKeysFail_ThrowsLastException()
        {
            // Arrange
            int calls = 0;
            TestKeyStoreProvider failing = new()
            {
                DecryptAsyncCallback = _ => throw new InvalidOperationException(
                    ++calls == 1 ? "first failure" : "last failure")
            };
            using SqlConnection connection = NewConnection((ProviderName, failing));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo(), NewKeyInfo());

            // Act
            Func<Task> decrypt = () => SqlSecurityUtility.DecryptSymmetricKeyAsync(
                entry, connection, command: null, CancellationToken.None);

            // Assert
            SqlException exception = await Assert.ThrowsAsync<SqlException>(decrypt);
            Assert.Contains("last failure", exception.ToString());
            Assert.Equal(2, failing.DecryptAsyncCallCount);
        }

        /// <summary>Decryption initializes the metadata algorithm and selected key information.</summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_CipherMetadata_PopulatesAlgorithmAndKeyInfo()
        {
            // Arrange
            TestKeyStoreProvider provider = new() { PlaintextKey = NewPlaintextKey(seed: 3) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo keyInfo = NewKeyInfo();
            SqlCipherMetadata metadata = new(
                NewCipherInfoEntry(keyInfo), ordinal: 0,
                cipherAlgorithmId: (byte)TdsEnums.AEAD_AES_256_CBC_HMAC_SHA256,
                cipherAlgorithmName: null, encryptionType: (byte)EncryptionType.Deterministic,
                normalizationRuleVersion: 0x01);

            // Act
            await SqlSecurityUtility.DecryptSymmetricKeyAsync(
                metadata, connection, command: null, CancellationToken.None);

            // Assert
            Assert.True(metadata.IsAlgorithmInitialized());
            Assert.NotNull(metadata.EncryptionKeyInfo);
            Assert.Equal(keyInfo.keyStoreName, metadata.EncryptionKeyInfo.keyStoreName);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>Pre-cancellation avoids invoking an otherwise valid provider.</summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_CancelledToken_DoesNotInvokeProvider()
        {
            // Arrange
            TestKeyStoreProvider provider = new() { PlaintextKey = NewPlaintextKey(seed: 1) };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo());
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act
            Task decrypt = SqlSecurityUtility.DecryptSymmetricKeyAsync(
                entry, connection, command: null, cts.Token);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decrypt);
            Assert.True(decrypt.IsCanceled);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        /// <summary>In-flight cancellation abandons fallback and is not wrapped as a key error.</summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_CancelledMidFlight_DoesNotAttemptRemainingKeys()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TaskCompletionSource<bool> entered = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = async token =>
                {
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, token);
                    return NewPlaintextKey(seed: 2);
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo(), NewKeyInfo());

            // Act
            Task decrypt = SqlSecurityUtility.DecryptSymmetricKeyAsync(
                entry, connection, command: null, cts.Token);
            await entered.Task;
            cts.Cancel();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decrypt);
            Assert.True(decrypt.IsCanceled);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>Cancellation after a failed key wins over validation of the next provider.</summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_CancelledAfterFailure_DoesNotValidateNextProvider()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TestKeyStoreProvider provider = new()
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

            // Act
            Task decrypt = SqlSecurityUtility.DecryptSymmetricKeyAsync(
                entry, connection, command: null, cts.Token);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decrypt);
            Assert.True(decrypt.IsCanceled);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
        }

        /// <summary>A synchronously failed final candidate must not replace cancellation.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task DecryptSymmetricKeyAsync_FinalCandidateFailsInlineAfterCancellation_Cancels(
            int candidateCount)
        {
            // Arrange
            using CancellationTokenSource cts = new();
            int calls = 0;
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = _ =>
                {
                    if (++calls == candidateCount)
                    {
                        cts.Cancel();
                    }
                    return Task.FromException<byte[]>(new InvalidOperationException("Key failed"));
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo[] candidates = new SqlEncryptionKeyInfo[candidateCount];
            for (int i = 0; i < candidates.Length; i++)
            {
                candidates[i] = NewKeyInfo();
            }

            // Act
            Task decrypt = SqlSecurityUtility.DecryptSymmetricKeyAsync(
                NewCipherInfoEntry(candidates), connection, null, cts.Token);

            // Assert
            OperationCanceledException error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decrypt);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(decrypt.IsCanceled);
            Assert.Equal(candidateCount, provider.DecryptAsyncCallCount);
        }

        /// <summary>An asynchronously failed final candidate must not replace cancellation.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task DecryptSymmetricKeyAsync_FinalCandidateFailsLaterAfterCancellation_Cancels(
            int candidateCount)
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TaskCompletionSource<byte[]> result = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = _ => ++calls == candidateCount
                    ? result.Task
                    : Task.FromException<byte[]>(new InvalidOperationException("First key failed"))
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlEncryptionKeyInfo[] candidates = new SqlEncryptionKeyInfo[candidateCount];
            for (int i = 0; i < candidates.Length; i++)
            {
                candidates[i] = NewKeyInfo();
            }

            // Act
            Task decrypt = SqlSecurityUtility.DecryptSymmetricKeyAsync(
                NewCipherInfoEntry(candidates), connection, null, cts.Token);
            cts.Cancel();
            result.SetException(new InvalidOperationException("Final candidate failed"));

            // Assert
            OperationCanceledException error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decrypt);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(decrypt.IsCanceled);
            Assert.Equal(candidateCount, provider.DecryptAsyncCallCount);
        }

        /// <summary>Pre-cancellation wins over an unregistered provider error.</summary>
        [Fact]
        public async Task DecryptSymmetricKeyAsync_CancelledToken_DoesNotValidateProvider()
        {
            // Arrange
            TestKeyStoreProvider provider = new();
            using SqlConnection connection = NewConnection((ProviderName, provider));
            SqlTceCipherInfoEntry entry = NewCipherInfoEntry(NewKeyInfo(SecondProviderName));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act
            Task decrypt = SqlSecurityUtility.DecryptSymmetricKeyAsync(
                entry, connection, command: null, cts.Token);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decrypt);
            Assert.True(decrypt.IsCanceled);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        /// <summary>Cancellation wins over a verification failure completed inline.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_ProviderFailsInlineAfterCancellation_Cancels()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TestKeyStoreProvider provider = new()
            {
                VerifyAsyncCallback = _ =>
                {
                    cts.Cancel();
                    return Task.FromException<bool>(new InvalidOperationException("Verification failed"));
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, true, new byte[] { 1, 2, 3 }, connection, null, cts.Token);

            // Assert
            OperationCanceledException error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(verify.IsCanceled);
            Assert.Equal(1, provider.VerifyAsyncCallCount);
        }

        /// <summary>Cancellation wins over a verification failure completed after suspension.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_ProviderFailsLaterAfterCancellation_Cancels()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TaskCompletionSource<bool> result = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new() { VerifyAsyncCallback = _ => result.Task };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, true, new byte[] { 1, 2, 3 }, connection, null, cts.Token);
            cts.Cancel();
            result.SetException(new InvalidOperationException("Verification failed"));

            // Assert
            OperationCanceledException error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(verify.IsCanceled);
            Assert.Equal(1, provider.VerifyAsyncCallCount);
        }

        /// <summary>A valid provider signature completes using the async provider API.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_ValidSignature_Completes()
        {
            // Arrange
            TestKeyStoreProvider provider = new() { VerifyResult = true };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            await SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 },
                connection, null, CancellationToken.None);

            // Assert
            Assert.Equal(1, provider.VerifyAsyncCallCount);
        }

        /// <summary>An invalid provider signature reports the same error as synchronous verification.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_InvalidSignature_Throws()
        {
            // Arrange
            TestKeyStoreProvider provider = new() { VerifyResult = false };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            Func<Task> verify = () => SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 },
                connection, null, CancellationToken.None);

            // Assert
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(verify);
            InvalidOperationException syncError = Assert.Throws<InvalidOperationException>(() =>
                SqlSecurityUtility.VerifyColumnMasterKeySignature(
                    ProviderName, KeyPath, false, new byte[] { 1, 2, 3 }, connection, null));
            Assert.Equal(syncError.Message, error.Message);
            Assert.Equal(1, provider.VerifyAsyncCallCount);
        }

        /// <summary>Cancellation wins over either signature result completed inline.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task VerifyColumnMasterKeySignatureAsync_ProviderReturnsInlineAfterCancellation_Cancels(
            bool verificationResult)
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TestKeyStoreProvider provider = new()
            {
                VerifyResult = true,
                VerifyAsyncCallback = _ =>
                {
                    cts.Cancel();
                    return Task.FromResult(verificationResult);
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 }, connection, null, cts.Token);

            // Assert
            OperationCanceledException error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(verify.IsCanceled);

            // Verify that the cancelled result cannot bypass the instance provider on retry.
            provider.VerifyAsyncCallback = null;
            await SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 },
                connection, null, CancellationToken.None);
            Assert.Equal(2, provider.VerifyAsyncCallCount);
        }

        /// <summary>Cancellation wins over either signature result completed after suspension.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task VerifyColumnMasterKeySignatureAsync_ProviderReturnsLaterAfterCancellation_Cancels(
            bool verificationResult)
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TaskCompletionSource<bool> result = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new()
            {
                VerifyResult = true,
                VerifyAsyncCallback = _ => result.Task
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 }, connection, null, cts.Token);
            cts.Cancel();
            result.SetResult(verificationResult);

            // Assert
            OperationCanceledException error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(verify.IsCanceled);

            // Verify that the cancelled result cannot bypass the instance provider on retry.
            provider.VerifyAsyncCallback = null;
            await SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 },
                connection, null, CancellationToken.None);
            Assert.Equal(2, provider.VerifyAsyncCallCount);
        }

        /// <summary>Provider errors retain the signature-verification wrapper.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_ProviderThrows_WrapsException()
        {
            // Arrange
            TestKeyStoreProvider provider = new()
            {
                VerifyAsyncCallback = _ => throw new InvalidOperationException("verification exploded")
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            Func<Task> verify = () => SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 },
                connection, null, CancellationToken.None);

            // Assert
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(verify);
            Assert.IsType<InvalidOperationException>(error.InnerException);
            Assert.Contains("verification exploded", error.ToString());
        }

        /// <summary>Pre-cancellation takes precedence over empty signature validation.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_CancelledWithMissingSignature_Cancels()
        {
            // Arrange
            TestKeyStoreProvider provider = new() { VerifyResult = true };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, Array.Empty<byte>(), connection, null, cts.Token);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.True(verify.IsCanceled);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                    ProviderName, KeyPath, false, Array.Empty<byte>(),
                    connection, null, CancellationToken.None));
            Assert.Equal(0, provider.VerifyAsyncCallCount);
        }

        /// <summary>Pre-cancellation takes precedence over provider lookup errors.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_CancelledWithUnknownProvider_Cancels()
        {
            // Arrange
            TestKeyStoreProvider provider = new() { VerifyResult = true };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                SecondProviderName, KeyPath, false, new byte[] { 1, 2, 3 },
                connection, null, cts.Token);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.True(verify.IsCanceled);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                    SecondProviderName, KeyPath, false, new byte[] { 1, 2, 3 },
                    connection, null, CancellationToken.None));
            Assert.Equal(0, provider.VerifyAsyncCallCount);
        }

        /// <summary>Pre-cancellation does not invoke verification even with valid inputs.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_CancelledToken_DoesNotInvokeProvider()
        {
            // Arrange
            TestKeyStoreProvider provider = new() { VerifyResult = true };
            using SqlConnection connection = NewConnection((ProviderName, provider));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, KeyPath, false, new byte[] { 1, 2, 3 }, connection, null, cts.Token);

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.True(verify.IsCanceled);
            Assert.Equal(0, provider.VerifyAsyncCallCount);
        }

        /// <summary>In-flight cancellation is not wrapped as a signature error.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_CancelledMidFlight_ProducesCancelledTask()
        {
            // Arrange
            using CancellationTokenSource cts = new();
            TaskCompletionSource<bool> entered = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TestKeyStoreProvider provider = new()
            {
                VerifyAsyncCallback = async token =>
                {
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, token);
                    return true;
                }
            };
            using SqlConnection connection = NewConnection((ProviderName, provider));

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                ProviderName, "unique-" + Guid.NewGuid(), false, new byte[] { 4, 5, 6 },
                connection, null, cts.Token);
            await entered.Task;
            cts.Cancel();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.True(verify.IsCanceled);
        }

        /// <summary>Neither cached signature result may override caller cancellation.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task VerifyColumnMasterKeySignatureAsync_CachedResult_ObservesCancellation(
            bool cachedResult)
        {
            // Arrange
            string providerName = SqlColumnEncryptionCertificateStoreProvider.ProviderName;
            string keyPath = "cached-signature-" + Guid.NewGuid();
            byte[] signature = Guid.NewGuid().ToByteArray();
            using SqlConnection connection = NewConnection();
            ColumnMasterKeyMetadataSignatureVerificationCache.Instance
                .AddSignatureVerificationResult(providerName, keyPath, true, signature, cachedResult);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act
            Task verify = SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                providerName, keyPath, true, signature, connection, null, cts.Token);

            // Assert
            OperationCanceledException error =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verify);
            Assert.Equal(cts.Token, error.CancellationToken);
            Assert.True(verify.IsCanceled);
        }

        /// <summary>A positive signature cache entry is shared with the synchronous utility.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_CachedValidSignature_Completes()
        {
            // Arrange
            string providerName = SqlColumnEncryptionCertificateStoreProvider.ProviderName;
            string keyPath = "cached-signature-" + Guid.NewGuid();
            byte[] signature = Guid.NewGuid().ToByteArray();
            using SqlConnection connection = NewConnection();
            ColumnMasterKeyMetadataSignatureVerificationCache.Instance
                .AddSignatureVerificationResult(providerName, keyPath, true, signature, true);

            // Act
            await SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                providerName, keyPath, true, signature, connection, null, CancellationToken.None);

            // Assert
            SqlSecurityUtility.VerifyColumnMasterKeySignature(
                providerName, keyPath, true, signature, connection, null);
        }

        /// <summary>A negative signature cache entry reports the same sync and async error.</summary>
        [Fact]
        public async Task VerifyColumnMasterKeySignatureAsync_CachedInvalidSignature_ThrowsSameError()
        {
            // Arrange
            string providerName = SqlColumnEncryptionCertificateStoreProvider.ProviderName;
            string keyPath = "cached-signature-" + Guid.NewGuid();
            byte[] signature = Guid.NewGuid().ToByteArray();
            using SqlConnection connection = NewConnection();
            ColumnMasterKeyMetadataSignatureVerificationCache.Instance
                .AddSignatureVerificationResult(providerName, keyPath, true, signature, false);

            // Act
            Func<Task> verify = () => SqlSecurityUtility.VerifyColumnMasterKeySignatureAsync(
                providerName, keyPath, true, signature, connection, null, CancellationToken.None);

            // Assert
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(verify);
            InvalidOperationException syncError = Assert.Throws<InvalidOperationException>(() =>
                SqlSecurityUtility.VerifyColumnMasterKeySignature(
                    providerName, keyPath, true, signature, connection, null));
            Assert.Equal(syncError.Message, error.Message);
        }

        /// <summary>Separately observes synchronous decryption and asynchronous provider calls.</summary>
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

            public override byte[] DecryptColumnEncryptionKey(
                string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                Interlocked.Increment(ref _decryptCallCount);
                return PlaintextKey;
            }

            public override Task<byte[]> DecryptColumnEncryptionKeyAsync(
                string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _decryptAsyncCallCount);
                Func<CancellationToken, Task<byte[]>> callback = DecryptAsyncCallback;
                return callback is not null
                    ? callback(cancellationToken) : Task.FromResult(PlaintextKey);
            }

            public override byte[] EncryptColumnEncryptionKey(
                string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
                => throw new NotSupportedException();

            public override byte[] SignColumnMasterKeyMetadata(
                string masterKeyPath, bool allowEnclaveComputations)
                => throw new NotSupportedException();

            public override bool VerifyColumnMasterKeyMetadata(
                string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
                => VerifyResult;

            public override Task<bool> VerifyColumnMasterKeyMetadataAsync(
                string masterKeyPath, bool allowEnclaveComputations, byte[] signature,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _verifyAsyncCallCount);
                Func<CancellationToken, Task<bool>> callback = VerifyAsyncCallback;
                return callback is not null
                    ? callback(cancellationToken) : Task.FromResult(VerifyResult);
            }
        }
    }
}
