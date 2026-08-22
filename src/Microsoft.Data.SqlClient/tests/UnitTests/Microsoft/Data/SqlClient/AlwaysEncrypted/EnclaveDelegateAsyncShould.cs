// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The Always Encrypted enclave layer is internal and nullable-oblivious, and several of its members
// legitimately accept or produce nulls. Nullable analysis is disabled for this file so the tests can
// mirror those signatures exactly.
#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for the asynchronous column encryption key resolution used when building an enclave package.
    /// </summary>
    /// <remarks>
    /// The enclave package itself cannot be produced without an attested enclave session, so these tests
    /// target the one step of that flow which performs key store I/O.
    /// </remarks>
    public class EnclaveDelegateAsyncShould
    {
        private const string ProviderName = "TEST_ENCLAVE_ASYNC_PROVIDER";
        private const string KeyPath = "test-enclave-key-path";
        private const string EncryptionAlgorithm = "RSA_OAEP";

        private const int DatabaseId = 42;
        private const int CekId = 7;
        private const ulong CekMdVersion = 3;

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

        private static SqlConnection NewConnection(SqlColumnEncryptionKeyStoreProvider provider)
        {
            SqlConnection connection = new SqlConnection("Data Source=async-ae-enclave-unit-test;Column Encryption Setting=Enabled");
            connection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(
                new Dictionary<string, SqlColumnEncryptionKeyStoreProvider> { [ProviderName] = provider });
            return connection;
        }

        /// <summary>
        /// Produces a cipher info entry whose cache lookup key cannot collide with any other test. The CEK
        /// cache is a process-wide singleton shared by every test in this assembly.
        /// </summary>
        private static SqlTceCipherInfoEntry NewCipherInfoEntry(int ordinal = 0)
        {
            SqlTceCipherInfoEntry entry = new SqlTceCipherInfoEntry(ordinal);
            entry.Add(
                encryptedKey: Guid.NewGuid().ToByteArray(),
                databaseId: DatabaseId,
                cekId: CekId,
                cekVersion: 1,
                cekMdVersion: CekMdVersion,
                keyPath: KeyPath,
                keyStoreName: ProviderName,
                algorithmName: EncryptionAlgorithm);
            return entry;
        }

        private static ConcurrentDictionary<int, SqlTceCipherInfoEntry> NewKeyTable(params SqlTceCipherInfoEntry[] entries)
        {
            ConcurrentDictionary<int, SqlTceCipherInfoEntry> table = new();
            for (int i = 0; i < entries.Length; i++)
            {
                table[i] = entries[i];
            }
            return table;
        }

        /// <summary>
        /// The whole point of the change: the enclave key resolution must go through the provider's async
        /// API so a key store performing network I/O never blocks the pooled thread.
        /// </summary>
        [Fact]
        public async Task GetDecryptedKeysToBeSentToEnclaveAsync_UsesAsyncProviderApi()
        {
            byte[] expectedKey = NewPlaintextKey(seed: 11);
            TestEnclaveKeyStoreProvider provider = new TestEnclaveKeyStoreProvider { PlaintextKey = expectedKey };
            using SqlConnection connection = NewConnection(provider);

            List<ColumnEncryptionKeyInfo> keys = await EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
                NewKeyTable(NewCipherInfoEntry()),
                serverName: "async-ae-enclave-unit-test",
                connection,
                command: null,
                CancellationToken.None);

            ColumnEncryptionKeyInfo key = Assert.Single(keys);
            Assert.Equal(expectedKey, key.DecryptedKeyBytes);
            Assert.Equal(DatabaseId, key.DatabaseId);
            Assert.Equal(CekId, key.KeyId);
            Assert.Equal(CekMdVersion, key.KeyMetadataVersion);

            Assert.Equal(1, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// Every requested key must be resolved, and each one must use the async provider API.
        /// </summary>
        [Fact]
        public async Task GetDecryptedKeysToBeSentToEnclaveAsync_ResolvesEveryRequestedKey()
        {
            TestEnclaveKeyStoreProvider provider = new TestEnclaveKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 23) };
            using SqlConnection connection = NewConnection(provider);

            List<ColumnEncryptionKeyInfo> keys = await EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
                NewKeyTable(NewCipherInfoEntry(ordinal: 0), NewCipherInfoEntry(ordinal: 1), NewCipherInfoEntry(ordinal: 2)),
                serverName: "async-ae-enclave-unit-test",
                connection,
                command: null,
                CancellationToken.None);

            Assert.Equal(3, keys.Count);
            Assert.Equal(3, provider.DecryptAsyncCallCount);
            Assert.Equal(0, provider.DecryptCallCount);
        }

        /// <summary>
        /// The synchronous path must keep using the synchronous provider API so that custom providers which
        /// only override the synchronous members continue to work unchanged.
        /// </summary>
        [Fact]
        public void GetDecryptedKeysToBeSentToEnclave_UsesSyncProviderApi()
        {
            TestEnclaveKeyStoreProvider provider = new TestEnclaveKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 37) };
            using SqlConnection connection = NewConnection(provider);

            List<ColumnEncryptionKeyInfo> keys = EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclave(
                NewKeyTable(NewCipherInfoEntry()),
                serverName: "async-ae-enclave-unit-test",
                connection,
                command: null);

            Assert.Single(keys);
            Assert.Equal(1, provider.DecryptCallCount);
            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// Cancellation must surface as a cancelled task rather than being wrapped in an
        /// Always Encrypted specific exception, and no provider call must be made.
        /// </summary>
        [Fact]
        public async Task GetDecryptedKeysToBeSentToEnclaveAsync_WhenCancelled_ProducesCancelledTask()
        {
            TestEnclaveKeyStoreProvider provider = new TestEnclaveKeyStoreProvider { PlaintextKey = NewPlaintextKey(seed: 53) };
            using SqlConnection connection = NewConnection(provider);

            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
                    NewKeyTable(NewCipherInfoEntry()),
                    serverName: "async-ae-enclave-unit-test",
                    connection,
                    command: null,
                    cts.Token));

            Assert.Equal(0, provider.DecryptAsyncCallCount);
        }

        /// <summary>
        /// A provider failure must propagate to the caller — wrapped exactly as the synchronous path wraps
        /// it — rather than producing a partially populated enclave key list.
        /// </summary>
        [Fact]
        public async Task GetDecryptedKeysToBeSentToEnclaveAsync_WhenProviderFails_Propagates()
        {
            TestEnclaveKeyStoreProvider provider = new TestEnclaveKeyStoreProvider
            {
                DecryptAsyncCallback = _ => throw new InvalidOperationException("key store unavailable"),
            };
            using SqlConnection connection = NewConnection(provider);

            SqlException exception = await Assert.ThrowsAsync<SqlException>(
                () => EnclaveDelegate.Instance.GetDecryptedKeysToBeSentToEnclaveAsync(
                    NewKeyTable(NewCipherInfoEntry()),
                    serverName: "async-ae-enclave-unit-test",
                    connection,
                    command: null,
                    CancellationToken.None));

            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Equal("key store unavailable", exception.InnerException.Message);
        }

        private sealed class TestEnclaveKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
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
