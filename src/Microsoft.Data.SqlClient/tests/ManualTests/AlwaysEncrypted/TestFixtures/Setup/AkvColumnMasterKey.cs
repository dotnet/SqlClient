// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public class AkvColumnMasterKey : ColumnMasterKey
    {
        public override string KeyPath { get; }

        public AkvColumnMasterKey(string name, string akvUrl, SqlColumnEncryptionKeyStoreProvider akvProvider, bool allowEnclaveComputations) : base(name)
        {
            KeyStoreProviderName = @"AZURE_KEY_VAULT";
            KeyPath = akvUrl;

            // For keys which allow enclave computation
            byte[] cmkSign = akvProvider.SignColumnMasterKeyMetadata(akvUrl, allowEnclaveComputations);
            CmkSignStr = string.Concat("0x", BitConverter.ToString(cmkSign).Replace("-", string.Empty));
        }
    }
}
