// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for the async virtual methods on <see cref="SqlColumnEncryptionKeyStoreProvider"/>,
    /// verifying default fallback behavior, cancellation, faulted tasks, and convenience overloads.
    /// </summary>
    public class SqlColumnEncryptionKeyStoreProviderAsyncShould
    {
        /// <summary>
        /// Verifies DecryptColumnEncryptionKeyAsync default returns the same result as the sync method.
        /// </summary>
        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_DefaultFallback_ReturnsSameResultAsSyncMethod()
        {
            var provider = new TestKeyStoreProvider();
            byte[] expected = provider.DecryptColumnEncryptionKey("path", "algo", new byte[] { 1, 2, 3 });
            byte[] actual = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 }, CancellationToken.None);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_DefaultFallback_ReturnsSameResultAsSyncMethod()
        {
            var provider = new TestKeyStoreProvider();
            byte[] expected = provider.EncryptColumnEncryptionKey("path", "algo", new byte[] { 4, 5, 6 });
            byte[] actual = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 }, CancellationToken.None);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_WhenSyncThrows_ReturnsFaultedTask()
        {
            var provider = new ThrowingKeyStoreProvider();
            Task<byte[]> task = provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1 }, CancellationToken.None);

            // Must not throw synchronously — the Task itself should be faulted
            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_WhenSyncThrows_ReturnsFaultedTask()
        {
            var provider = new ThrowingKeyStoreProvider();
            Task<byte[]> task = provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1 }, CancellationToken.None);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        [Fact]
        public async Task SignColumnMasterKeyMetadataAsync_DefaultImplementation_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<byte[]> task = provider.SignColumnMasterKeyMetadataAsync("path", true, CancellationToken.None);

            // The base sync method throws NotImplementedException; the async version
            // must surface this as a faulted Task, not a synchronous throw.
            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        [Fact]
        public async Task VerifyColumnMasterKeyMetadataAsync_DefaultImplementation_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<bool> task = provider.VerifyColumnMasterKeyMetadataAsync("path", true, new byte[] { 1 }, CancellationToken.None);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        [Fact]
        public void DecryptColumnEncryptionKeyAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<byte[]> task = provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 }, cts.Token);

            Assert.True(task.IsCanceled);
        }

        [Fact]
        public void EncryptColumnEncryptionKeyAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<byte[]> task = provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 }, cts.Token);

            Assert.True(task.IsCanceled);
        }

        [Fact]
        public void SignColumnMasterKeyMetadataAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<byte[]> task = provider.SignColumnMasterKeyMetadataAsync("path", true, cts.Token);

            Assert.True(task.IsCanceled);
        }

        [Fact]
        public void VerifyColumnMasterKeyMetadataAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<bool> task = provider.VerifyColumnMasterKeyMetadataAsync("path", true, new byte[] { 1 }, cts.Token);

            Assert.True(task.IsCanceled);
        }

        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_NoCancellationOverload_Works()
        {
            var provider = new TestKeyStoreProvider();
            byte[] result = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 });
            Assert.NotNull(result);
            Assert.Equal(new byte[] { 1, 2, 3 }, result);
        }

        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_NoCancellationOverload_Works()
        {
            var provider = new TestKeyStoreProvider();
            byte[] result = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 });
            Assert.NotNull(result);
            Assert.Equal(new byte[] { 4, 5, 6 }, result);
        }

        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_LiveToken_CompletesSuccessfully()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();

            // A non-cancelled token should not interfere with the operation
            byte[] result = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 7, 8, 9 }, cts.Token);
            Assert.Equal(new byte[] { 7, 8, 9 }, result);
        }

        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_LiveToken_CompletesSuccessfully()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();

            byte[] result = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 7, 8, 9 }, cts.Token);
            Assert.Equal(new byte[] { 7, 8, 9 }, result);
        }

        /// <summary>
        /// A test provider that implements sync methods with real (non-throwing) behavior.
        /// </summary>
        private class TestKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                return encryptedColumnEncryptionKey;
            }

            public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            {
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
