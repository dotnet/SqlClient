// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{

    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SqlColumnEncryptionKeyStoreProvider/*'/>
    public abstract class SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/ColumnEncryptionKeyCacheTtl/*'/>
        public virtual TimeSpan? ColumnEncryptionKeyCacheTtl { get; set; } = new TimeSpan(0);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/DecryptColumnEncryptionKey/*'/>
        public abstract byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/DecryptColumnEncryptionKeyAsync/*'/>
        public virtual Task<byte[]> DecryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<byte[]>(cancellationToken);
            }

            try
            {
                return Task.FromResult(DecryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, encryptedColumnEncryptionKey));
            }
            catch (Exception e)
            {
                return Task.FromException<byte[]>(e);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/DecryptColumnEncryptionKeyAsyncNoCancellation/*'/>
        public Task<byte[]> DecryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
        {
            return DecryptColumnEncryptionKeyAsync(masterKeyPath, encryptionAlgorithm, encryptedColumnEncryptionKey, CancellationToken.None);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/EncryptColumnEncryptionKey/*'/>
        public abstract byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/EncryptColumnEncryptionKeyAsync/*'/>
        public virtual Task<byte[]> EncryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<byte[]>(cancellationToken);
            }

            try
            {
                return Task.FromResult(EncryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, columnEncryptionKey));
            }
            catch (Exception e)
            {
                return Task.FromException<byte[]>(e);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/EncryptColumnEncryptionKeyAsyncNoCancellation/*'/>
        public Task<byte[]> EncryptColumnEncryptionKeyAsync(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
        {
            return EncryptColumnEncryptionKeyAsync(masterKeyPath, encryptionAlgorithm, columnEncryptionKey, CancellationToken.None);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SignColumnMasterKeyMetadata/*'/>
        public virtual byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
        {
            throw new NotImplementedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SignColumnMasterKeyMetadataAsync/*'/>
        public virtual Task<byte[]> SignColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<byte[]>(cancellationToken);
            }

            try
            {
                return Task.FromResult(SignColumnMasterKeyMetadata(masterKeyPath, allowEnclaveComputations));
            }
            catch (Exception e)
            {
                return Task.FromException<byte[]>(e);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/SignColumnMasterKeyMetadataAsyncNoCancellation/*'/>
        public Task<byte[]> SignColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations)
        {
            return SignColumnMasterKeyMetadataAsync(masterKeyPath, allowEnclaveComputations, CancellationToken.None);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/VerifyColumnMasterKeyMetadata/*'/>
        public virtual bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            throw new NotImplementedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/VerifyColumnMasterKeyMetadataAsync/*'/>
        public virtual Task<bool> VerifyColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations, byte[] signature, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }

            try
            {
                return Task.FromResult(VerifyColumnMasterKeyMetadata(masterKeyPath, allowEnclaveComputations, signature));
            }
            catch (Exception e)
            {
                return Task.FromException<bool>(e);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionKeyStoreProvider.xml' path='docs/members[@name="SqlColumnEncryptionKeyStoreProvider"]/VerifyColumnMasterKeyMetadataAsyncNoCancellation/*'/>
        public Task<bool> VerifyColumnMasterKeyMetadataAsync(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            return VerifyColumnMasterKeyMetadataAsync(masterKeyPath, allowEnclaveComputations, signature, CancellationToken.None);
        }
    }
}
