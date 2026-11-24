// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities
{
    /// <summary>
    /// Exercises the base async implementation without overriding its synchronous fallback.
    /// </summary>
    internal sealed class SynchronousKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
    {
        internal Func<byte[]> DecryptCallback { get; set; } =
            () => throw new NotSupportedException();

        public override byte[] DecryptColumnEncryptionKey(
            string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            => DecryptCallback();

        public override byte[] EncryptColumnEncryptionKey(
            string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            => throw new NotSupportedException();

        public override byte[] SignColumnMasterKeyMetadata(
            string masterKeyPath, bool allowEnclaveComputations)
            => throw new NotSupportedException();

        public override bool VerifyColumnMasterKeyMetadata(
            string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Makes synchronous and asynchronous decryption independently observable in cache tests.
    /// </summary>
    internal sealed class TestKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
    {
        private int _decryptCallCount;
        private int _decryptAsyncCallCount;

        internal byte[] PlaintextKey { get; set; } = new byte[32];

        internal Func<CancellationToken, Task<byte[]>>? DecryptAsyncCallback { get; set; }

        internal Func<byte[]>? DecryptCallback { get; set; }

        internal int DecryptCallCount => _decryptCallCount;

        internal int DecryptAsyncCallCount => _decryptAsyncCallCount;

        public override byte[] DecryptColumnEncryptionKey(
            string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
        {
            Interlocked.Increment(ref _decryptCallCount);
            return DecryptCallback is not null ? DecryptCallback() : PlaintextKey;
        }

        public override Task<byte[]> DecryptColumnEncryptionKeyAsync(
            string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _decryptAsyncCallCount);
            Func<CancellationToken, Task<byte[]>>? callback = DecryptAsyncCallback;
            return callback is not null ? callback(cancellationToken) : Task.FromResult(PlaintextKey);
        }

        public override byte[] EncryptColumnEncryptionKey(
            string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            => throw new NotSupportedException();

        public override byte[] SignColumnMasterKeyMetadata(
            string masterKeyPath, bool allowEnclaveComputations)
            => throw new NotSupportedException();

        public override bool VerifyColumnMasterKeyMetadata(
            string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
            => throw new NotSupportedException();
    }
}
