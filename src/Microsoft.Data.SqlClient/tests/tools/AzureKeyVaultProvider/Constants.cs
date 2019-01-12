//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SqlServer.Management.AlwaysEncrypted.AzureKeyVaultProvider
{
    internal static class Constants
    {
        /// <summary>
        /// Hashing algoirthm used for signing
        /// </summary>
        internal const string HashingAlgorithm = @"RS256";

        /// <summary>
        /// Azure Key Vault Domain Name
        /// </summary>
        internal const string AzureKeyVaultPublicDomainName = @"vault.azure.net";

        /// <summary>
        /// Always Encrypted Param names for exec handling
        /// </summary>
        internal const string AeParamColumnEncryptionKey = "columnEncryptionKey";
        internal const string AeParamEncryptionAlgorithm = "encryptionAlgorithm";
        internal const string AeParamMasterKeyPath = "masterKeyPath";
        internal const string AeParamEncryptedCek = "encryptedColumnEncryptionKey";

    }
}
