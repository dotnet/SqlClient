// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities
{
    /// <summary>
    /// Holds asynchronous decryptions pending so tests can observe overlapping provider calls.
    /// </summary>
    internal sealed class PendingDecryptionKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
    {
        private int _callCount;

        internal int CallCount => Volatile.Read(ref _callCount);

        internal TaskCompletionSource<byte[]> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<byte[]> DecryptColumnEncryptionKeyAsync(
            string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return Completion.Task;
        }

        public override byte[] DecryptColumnEncryptionKey(
            string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            => throw new NotSupportedException();

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
