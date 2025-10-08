// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SqlColumnEncryptionCngProvider/*' />
    public class SqlColumnEncryptionCngProvider : SqlColumnEncryptionKeyStoreProvider
    {
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/ProviderName/*' />
        public const string ProviderName = @"MSSQL_CNG_STORE";

        /// <summary>
        /// This encryption keystore uses an asymmetric key as the column master key.
        /// </summary>
        internal const string MasterKeyType = @"asymmetric key";

        /// <summary>
        /// This encryption keystore uses the master key path to reference a CNG provider.
        /// </summary>
        internal const string KeyPathReference = @"Microsoft Cryptography API: Next Generation (CNG) provider";

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/DecryptColumnEncryptionKey/*' />
        public override byte[] DecryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? encryptedColumnEncryptionKey)
        {
            throw new PlatformNotSupportedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/EncryptColumnEncryptionKey/*' />
        public override byte[] EncryptColumnEncryptionKey(string? masterKeyPath, string? encryptionAlgorithm, byte[]? columnEncryptionKey)
        {
            throw new PlatformNotSupportedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/SignColumnMasterKeyMetadata/*' />
        public override byte[] SignColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations)
        {
            throw new PlatformNotSupportedException();
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlColumnEncryptionCngProvider.xml' path='docs/members[@name="SqlColumnEncryptionCngProvider"]/VerifyColumnMasterKeyMetadata/*' />
        public override bool VerifyColumnMasterKeyMetadata(string? masterKeyPath, bool allowEnclaveComputations, byte[]? signature)
        {
            throw new PlatformNotSupportedException();
        }
    }
}

#endif
