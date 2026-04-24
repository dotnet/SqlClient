// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    internal static class Constants
    {
        /// <summary>
        /// Azure Key Vault Domain Name
        /// </summary>
        internal static readonly string[] AzureKeyVaultPublicDomainNames = new string[] {
            @"vault.azure.net", // default
            @"vault.azure.cn", // Azure China
            @"vault.usgovcloudapi.net", // US Government
            @"vault.microsoftazure.de", // Azure Germany
            @"managedhsm.azure.net", // public HSM vault
            @"managedhsm.azure.cn", // Azure China HSM vault
            @"managedhsm.usgovcloudapi.net", // US Government HSM vault
            @"managedhsm.microsoftazure.de" // Azure Germany HSM vault
        };

        /// <summary>
        /// Always Encrypted Parameter names for exec handling
        /// </summary>
        internal const string AeParamColumnEncryptionKey = "columnEncryptionKey";
        internal const string AeParamEncryptionAlgorithm = "encryptionAlgorithm";
        internal const string AeParamMasterKeyPath = "masterKeyPath";
        internal const string AeParamEncryptedCek = "encryptedColumnEncryptionKey";
    }
}
