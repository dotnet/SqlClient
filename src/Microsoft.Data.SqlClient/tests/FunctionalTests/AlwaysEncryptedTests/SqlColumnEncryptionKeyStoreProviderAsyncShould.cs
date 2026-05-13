// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class SqlColumnEncryptionKeyStoreProviderAsyncShould
    {
        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_DefaultFallback_ReturnsSameResultAsSyncMethod()
        {
            var provider = new TestKeyStoreProvider();
            byte[] expected = provider.DecryptColumnEncryptionKey("path", "algo", new byte[] { 1, 2, 3 });
            byte[] actual = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_DefaultFallback_ReturnsSameResultAsSyncMethod()
        {
            var provider = new TestKeyStoreProvider();
            byte[] expected = provider.EncryptColumnEncryptionKey("path", "algo", new byte[] { 4, 5, 6 });
            byte[] actual = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_WhenSyncThrows_ReturnsFaultedTask()
        {
            var provider = new ThrowingKeyStoreProvider();
            Task<byte[]> task = provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1 });

            // Must not throw synchronously — the Task itself should be faulted
            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_WhenSyncThrows_ReturnsFaultedTask()
        {
            var provider = new ThrowingKeyStoreProvider();
            Task<byte[]> task = provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1 });

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        [Fact]
        public async Task SignColumnMasterKeyMetadataAsync_DefaultImplementation_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<byte[]> task = provider.SignColumnMasterKeyMetadataAsync("path", true);

            // The base sync method throws NotImplementedException; the async version
            // must surface this as a faulted Task, not a synchronous throw.
            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        [Fact]
        public async Task VerifyColumnMasterKeyMetadataAsync_DefaultImplementation_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<bool> task = provider.VerifyColumnMasterKeyMetadataAsync("path", true, new byte[] { 1 });

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_AcceptsCancellationToken()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            byte[] result = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 }, cts.Token);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_AcceptsCancellationToken()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            byte[] result = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 }, cts.Token);
            Assert.NotNull(result);
        }

        /// <summary>
        /// A test provider that implements sync methods with real (non-throwing) behavior.
        /// </summary>
        private class TestKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                // Simple identity operation for testing
                return encryptedColumnEncryptionKey;
            }

            public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            {
                // Simple identity operation for testing
                return columnEncryptionKey;
            }
        }

        /// <summary>
        /// A test provider whose sync methods throw exceptions.
        /// </summary>
        private class ThrowingKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                throw new InvalidOperationException("Decrypt failed");
            }

            public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            {
                throw new InvalidOperationException("Encrypt failed");
            }
        }
    }
}
