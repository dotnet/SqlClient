// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The Always Encrypted utility layer is internal and nullable-oblivious, and several of its members
// legitimately accept or produce nulls. Nullable analysis is disabled for this file so the tests can
// mirror those signatures exactly.
#nullable disable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for the split of <see cref="SqlQueryMetadataCache"/> lookups into an in-memory probe
    /// (<c>TryGetCachedQueryMetadata</c>) and an awaitable column encryption key load
    /// (<c>CompleteCachedQueryMetadataAsync</c>), which is what lets asynchronous execution paths
    /// avoid blocking the caller's thread on key store I/O.
    /// </summary>
    public class SqlQueryMetadataCacheAsyncShould
    {
        private const string ProviderName = "TEST_METADATA_CACHE_PROVIDER";
        private const string KeyPath = "metadata-cache-key-path";
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
        /// Builds a command whose single parameter carries cipher metadata backed by unique key
        /// material, so that neither the process-wide query metadata cache nor the process-wide
        /// column encryption key cache can collide with another test.
        /// </summary>
        /// <param name="provider">Connection-scoped provider that will load the parameter's key.</param>
        /// <returns>A command with uncached cipher metadata and an unopened connection; dispose both.</returns>
        private static SqlCommand NewCommandWithCipherMetadata(SqlColumnEncryptionKeyStoreProvider provider)
        {
            SqlConnection connection = new SqlConnection(
                "Data Source=async-ae-metadata-cache-test;Initial Catalog=async-ae-db;Column Encryption Setting=Enabled");
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider> { { ProviderName, provider } });

            SqlCommand command = new SqlCommand($"SELECT * FROM T WHERE C = @p -- {Guid.NewGuid()}", connection);
            SqlParameter parameter = command.Parameters.AddWithValue("@p", 1);

            SqlTceCipherInfoEntry entry = new SqlTceCipherInfoEntry(ordinal: 0);
            entry.Add(
                encryptedKey: Guid.NewGuid().ToByteArray(),
                databaseId: 1,
                cekId: 1,
                cekVersion: 1,
                cekMdVersion: 1,
                keyPath: KeyPath,
                keyStoreName: ProviderName,
                algorithmName: EncryptionAlgorithm);

            parameter.CipherMetadata = new SqlCipherMetadata(
                entry,
                ordinal: 0,
                cipherAlgorithmId: TdsEnums.AEAD_AES_256_CBC_HMAC_SHA256,
                cipherAlgorithmName: null,
                encryptionType: (byte)EncryptionType.Deterministic,
                normalizationRuleVersion: 1);

            return command;
        }

        /// <summary>Primes the query metadata cache without loading the parameter's encryption key.</summary>
        /// <param name="provider">Connection-scoped provider for the subsequent cache completion.</param>
        /// <returns>A command with cached metadata and an unopened connection; dispose both.</returns>
        private static SqlCommand NewCachedCommand(SqlColumnEncryptionKeyStoreProvider provider)
        {
            SqlCommand command = NewCommandWithCipherMetadata(provider);
            SqlQueryMetadataCache.GetInstance().AddQueryMetadata(command, ignoreQueriesWithReturnValueParams: true);
            return command;
        }

        /// <summary>
        /// Synchronously completed key loads must not run cache completion or its stale-key describe
        /// fallback inline on the asynchronous command caller's thread.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PrepareCachedMetadataAsync_DispatchesSynchronouslyCompletedProvider(bool staleKey)
        {
            int callerThreadId = Environment.CurrentManagedThreadId;
            bool invokingPrepare = true;
            bool providerRanInline = false;
            TestKeyStoreProvider provider = new()
            {
                DecryptAsyncCallback = _ =>
                {
                    providerRanInline = invokingPrepare && Environment.CurrentManagedThreadId == callerThreadId;
                    return staleKey
                        ? Task.FromException<byte[]>(new ArgumentException("stale key"))
                        : Task.FromResult(NewPlaintextKey(seed: 53));
                },
            };
            using SqlCommand command = NewCachedCommand(provider);
            using SqlConnection connection = command.Connection;
            MethodInfo prepare = typeof(SqlCommand).GetMethod(
                "PrepareForTransparentEncryption", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Encryption preparation method not found.");
            object[] arguments = { true, 30, new TaskCompletionSource<object>(), null, true, false, false };
            try
            {
                prepare.Invoke(command, arguments);
            }
            finally
            {
                invokingPrepare = false;
            }

            Assert.Equal(true, arguments[5]);
            Task completion = Assert.IsAssignableFrom<Task>(arguments[3]);
            if (staleKey)
            {
                // The closed connection stops the describe fallback before any network I/O.
                await Assert.ThrowsAsync<InvalidOperationException>(() => completion);
                Assert.Null(command.Parameters[0].CipherMetadata);
                Assert.False(SqlQueryMetadataCache.GetInstance().TryGetCachedQueryMetadata(command, out _));
            }
            else
            {
                await completion;
                Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
            }

            Assert.False(providerRanInline);
            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// A cache hit must defer key-store I/O until the asynchronous completion step.
        /// </summary>
        [Fact]
        public async Task CompleteCachedQueryMetadataAsync_UsesAsyncKeyStoreProviderApi()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 41) };
            using SqlCommand command = NewCachedCommand(provider);
            using SqlConnection connection = command.Connection;

            Assert.True(SqlQueryMetadataCache.GetInstance()
                .TryGetCachedQueryMetadata(command, out SqlQueryMetadataCache.CachedQueryMetadata metadata));

            // The probe must not have touched the key store: that is the whole point of the split.
            Assert.Equal(0, provider.DecryptCallCount);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
            Assert.NotEmpty(metadata.KeysToLoad);

            Assert.True(await SqlQueryMetadataCache.GetInstance()
                .CompleteCachedQueryMetadataAsync(command, metadata, CancellationToken.None));

            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
            Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
        }

        /// <summary>
        /// Failed key loading must invalidate stale metadata so execution can describe parameters again.
        /// </summary>
        [Fact]
        public async Task CompleteCachedQueryMetadataAsync_WhenKeyIsStale_ReportsMissAndClearsMetadata()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider
            {
                DecryptAsyncCallback = _ => throw new ArgumentException("stale key"),
            };
            using SqlCommand command = NewCachedCommand(provider);
            using SqlConnection connection = command.Connection;

            Assert.True(SqlQueryMetadataCache.GetInstance()
                .TryGetCachedQueryMetadata(command, out SqlQueryMetadataCache.CachedQueryMetadata metadata));

            // A stale key must degrade to a cache miss rather than surface as an execution failure, so
            // that the caller falls back to a full describe parameter encryption round trip.
            Assert.False(await SqlQueryMetadataCache.GetInstance()
                .CompleteCachedQueryMetadataAsync(command, metadata, CancellationToken.None));

            Assert.Null(command.Parameters[0].CipherMetadata);

            // The entry must have been invalidated so the next lookup does not repeat the failure.
            Assert.False(SqlQueryMetadataCache.GetInstance().TryGetCachedQueryMetadata(command, out _));
        }

        /// <summary>
        /// The synchronous lookup is now expressed in terms of the same probe, so guard that it still
        /// uses the synchronous key store provider API and still reports a hit.
        /// </summary>
        [Fact]
        public void GetQueryMetadataIfExists_StillUsesSyncKeyStoreProviderApi()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 47) };
            using SqlCommand command = NewCachedCommand(provider);
            using SqlConnection connection = command.Connection;

            Assert.True(SqlQueryMetadataCache.GetInstance().GetQueryMetadataIfExists(command));

            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
            Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
        }

        /// <summary>
        /// Cancellation says nothing about whether the cached metadata is still valid, so a cancelled
        /// key load must not report a hit and must not evict the entry. Evicting it would make the next
        /// caller pay for a full describe parameter encryption round trip.
        /// </summary>
        [Fact]
        public async Task CompleteCachedQueryMetadataAsync_WhenCancelled_DoesNotReportHitOrEvictTheEntry()
        {
            TestKeyStoreProvider provider = new TestKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 43) };
            using SqlCommand command = NewCachedCommand(provider);
            using SqlConnection connection = command.Connection;

            Assert.True(SqlQueryMetadataCache.GetInstance()
                .TryGetCachedQueryMetadata(command, out SqlQueryMetadataCache.CachedQueryMetadata metadata));

            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => SqlQueryMetadataCache.GetInstance()
                    .CompleteCachedQueryMetadataAsync(command, metadata, cts.Token));

            // The entry must still be there, and a subsequent uncancelled load must succeed.
            Assert.True(SqlQueryMetadataCache.GetInstance()
                .TryGetCachedQueryMetadata(command, out SqlQueryMetadataCache.CachedQueryMetadata retryMetadata));

            Assert.True(await SqlQueryMetadataCache.GetInstance()
                .CompleteCachedQueryMetadataAsync(command, retryMetadata, CancellationToken.None));

            Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
        }

        /// <summary>
        /// A key store provider whose sync and async paths are separately observable, so that tests can
        /// assert which path was taken and how many times.
        /// </summary>
        private sealed class TestKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            private int _decryptCallCount;
            private int _decryptAsyncCallCount;

            internal byte[] PlaintextKey { get; set; } = new byte[32];

            internal Func<CancellationToken, Task<byte[]>> DecryptAsyncCallback { get; set; }

            internal int DecryptCallCount => _decryptCallCount;

            internal int DecryptAsyncCallCount => _decryptAsyncCallCount;

            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                Interlocked.Increment(ref _decryptCallCount);
                return PlaintextKey;
            }

            public override Task<byte[]> DecryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _decryptAsyncCallCount);
                cancellationToken.ThrowIfCancellationRequested();
                Func<CancellationToken, Task<byte[]>> callback = DecryptAsyncCallback;
                return callback is not null ? callback(cancellationToken) : Task.FromResult(PlaintextKey);
            }

            public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
                => throw new NotSupportedException();

            public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
                => throw new NotSupportedException();

            public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
                => throw new NotSupportedException();
        }
    }
}
