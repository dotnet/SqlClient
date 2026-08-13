// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for the async virtual methods on <see cref="SqlColumnEncryptionKeyStoreProvider"/>,
    /// verifying default fallback behavior, cancellation, faulted tasks, and convenience overloads.
    /// </summary>
    public class SqlColumnEncryptionKeyStoreProviderAsyncShould
    {
        #region Default fallback behavior

        /// <summary>
        /// Verifies that the default async decrypt implementation wraps the sync result via Task.FromResult.
        /// </summary>
        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_DefaultFallback_ReturnsSameResultAsSyncMethod()
        {
            var provider = new TestKeyStoreProvider();
            byte[] expected = provider.DecryptColumnEncryptionKey("path", "algo", new byte[] { 1, 2, 3 });
            byte[] actual = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 }, CancellationToken.None);
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that the default async encrypt implementation wraps the sync result via Task.FromResult.
        /// </summary>
        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_DefaultFallback_ReturnsSameResultAsSyncMethod()
        {
            var provider = new TestKeyStoreProvider();
            byte[] expected = provider.EncryptColumnEncryptionKey("path", "algo", new byte[] { 4, 5, 6 });
            byte[] actual = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 }, CancellationToken.None);
            Assert.Equal(expected, actual);
        }

        #endregion

        #region Faulted task behavior

        /// <summary>
        /// Verifies that when the sync decrypt method throws, the async version returns a faulted Task
        /// rather than throwing synchronously.
        /// </summary>
        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_WhenSyncThrows_ReturnsFaultedTask()
        {
            var provider = new ThrowingKeyStoreProvider();
            Task<byte[]> task = provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1 }, CancellationToken.None);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        /// <summary>
        /// Verifies that when the sync encrypt method throws, the async version returns a faulted Task
        /// rather than throwing synchronously.
        /// </summary>
        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_WhenSyncThrows_ReturnsFaultedTask()
        {
            var provider = new ThrowingKeyStoreProvider();
            Task<byte[]> task = provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1 }, CancellationToken.None);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        /// <summary>
        /// Verifies that the base class SignColumnMasterKeyMetadata throws NotImplementedException,
        /// and the async version surfaces this as a faulted Task.
        /// </summary>
        [Fact]
        public async Task SignColumnMasterKeyMetadataAsync_DefaultImplementation_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<byte[]> task = provider.SignColumnMasterKeyMetadataAsync("path", true, CancellationToken.None);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        /// <summary>
        /// Verifies that the base class VerifyColumnMasterKeyMetadata throws NotImplementedException,
        /// and the async version surfaces this as a faulted Task.
        /// </summary>
        [Fact]
        public async Task VerifyColumnMasterKeyMetadataAsync_DefaultImplementation_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<bool> task = provider.VerifyColumnMasterKeyMetadataAsync("path", true, new byte[] { 1 }, CancellationToken.None);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        #endregion

        #region Cancellation behavior

        /// <summary>
        /// Verifies that passing an already-cancelled token returns a cancelled Task immediately.
        /// </summary>
        [Fact]
        public void DecryptColumnEncryptionKeyAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<byte[]> task = provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 }, cts.Token);

            Assert.True(task.IsCanceled);
        }

        /// <summary>
        /// Verifies that passing an already-cancelled token returns a cancelled Task immediately.
        /// </summary>
        [Fact]
        public void EncryptColumnEncryptionKeyAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<byte[]> task = provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 }, cts.Token);

            Assert.True(task.IsCanceled);
        }

        /// <summary>
        /// Verifies that passing an already-cancelled token returns a cancelled Task immediately.
        /// </summary>
        [Fact]
        public void SignColumnMasterKeyMetadataAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<byte[]> task = provider.SignColumnMasterKeyMetadataAsync("path", true, cts.Token);

            Assert.True(task.IsCanceled);
        }

        /// <summary>
        /// Verifies that passing an already-cancelled token returns a cancelled Task immediately.
        /// </summary>
        [Fact]
        public void VerifyColumnMasterKeyMetadataAsync_CancelledToken_ReturnsCancelledTask()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<bool> task = provider.VerifyColumnMasterKeyMetadataAsync("path", true, new byte[] { 1 }, cts.Token);

            Assert.True(task.IsCanceled);
        }

        /// <summary>
        /// Verifies that a non-cancelled (live) token does not interfere with successful decrypt.
        /// </summary>
        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_LiveToken_CompletesSuccessfully()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();

            byte[] result = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 7, 8, 9 }, cts.Token);
            Assert.Equal(new byte[] { 7, 8, 9 }, result);
        }

        /// <summary>
        /// Verifies that a non-cancelled (live) token does not interfere with successful encrypt.
        /// </summary>
        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_LiveToken_CompletesSuccessfully()
        {
            var provider = new TestKeyStoreProvider();
            using var cts = new CancellationTokenSource();

            byte[] result = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 7, 8, 9 }, cts.Token);
            Assert.Equal(new byte[] { 7, 8, 9 }, result);
        }

        #endregion

        #region Default CancellationToken parameter

        /// <summary>
        /// Verifies calling without CancellationToken (uses default) works for decrypt.
        /// </summary>
        [Fact]
        public async Task DecryptColumnEncryptionKeyAsync_DefaultCancellationToken_Works()
        {
            var provider = new TestKeyStoreProvider();
            byte[] result = await provider.DecryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 1, 2, 3 });
            Assert.NotNull(result);
            Assert.Equal(new byte[] { 1, 2, 3 }, result);
        }

        /// <summary>
        /// Verifies calling without CancellationToken (uses default) works for encrypt.
        /// </summary>
        [Fact]
        public async Task EncryptColumnEncryptionKeyAsync_DefaultCancellationToken_Works()
        {
            var provider = new TestKeyStoreProvider();
            byte[] result = await provider.EncryptColumnEncryptionKeyAsync("path", "algo", new byte[] { 4, 5, 6 });
            Assert.NotNull(result);
            Assert.Equal(new byte[] { 4, 5, 6 }, result);
        }

        /// <summary>
        /// Verifies calling sign without CancellationToken returns a faulted Task
        /// (base class throws NotImplementedException).
        /// </summary>
        [Fact]
        public async Task SignColumnMasterKeyMetadataAsync_DefaultCancellationToken_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<byte[]> task = provider.SignColumnMasterKeyMetadataAsync("path", true);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        /// <summary>
        /// Verifies calling verify without CancellationToken returns a faulted Task
        /// (base class throws NotImplementedException).
        /// </summary>
        [Fact]
        public async Task VerifyColumnMasterKeyMetadataAsync_DefaultCancellationToken_ReturnsFaultedTask()
        {
            var provider = new TestKeyStoreProvider();
            Task<bool> task = provider.VerifyColumnMasterKeyMetadataAsync("path", true, new byte[] { 1 });

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<NotImplementedException>(() => task);
        }

        #endregion

        #region Test helpers

        /// <summary>
        /// A test provider that implements sync methods with pass-through behavior (returns input as-is).
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
        /// A test provider whose sync methods always throw <see cref="InvalidOperationException"/>.
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

        #endregion
    }
}
