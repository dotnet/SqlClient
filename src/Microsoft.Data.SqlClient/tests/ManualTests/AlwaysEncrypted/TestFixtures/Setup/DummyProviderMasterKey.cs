// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public class DummyMasterKeyForCertStoreProvider : ColumnMasterKey
    {
        public StoreLocation CertificateStoreLocation { get; set; } = StoreLocation.CurrentUser;
        public StoreName CertificateStoreName { get; set; } = StoreName.My;
        public string Thumbprint { get; }
        public override string KeyPath { get; }

        public DummyMasterKeyForCertStoreProvider(string name, string certificateThumbprint, SqlColumnEncryptionKeyStoreProvider certStoreProvider, bool allowEnclaveComputations) : base(name)
        {
            KeyStoreProviderName = DummyKeyStoreProvider.Name;
            Thumbprint = certificateThumbprint;
            KeyPath = string.Concat(CertificateStoreLocation.ToString(), "/", CertificateStoreName.ToString(), "/", Thumbprint);

            byte[] cmkSign = certStoreProvider.SignColumnMasterKeyMetadata(KeyPath, allowEnclaveComputations);
            CmkSignStr = string.Concat("0x", BitConverter.ToString(cmkSign).Replace("-", string.Empty));
        }
    }

    public class DummyMasterKeyForAKVProvider : ColumnMasterKey
    {
        public override string KeyPath { get; }

        public DummyMasterKeyForAKVProvider(string name, string akvUrl, SqlColumnEncryptionKeyStoreProvider akvProvider, bool allowEnclaveComputations) : base(name)
        {
            KeyStoreProviderName = DummyKeyStoreProvider.Name;
            KeyPath = akvUrl;

            byte[] cmkSign = akvProvider.SignColumnMasterKeyMetadata(KeyPath, allowEnclaveComputations);
            CmkSignStr = string.Concat("0x", BitConverter.ToString(cmkSign).Replace("-", string.Empty));
        }
    }
}
