// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System.Collections.Generic;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategyCertStoreProvider : SQLSetupStrategy
    {
        public SqlColumnEncryptionCertificateStoreProvider CertStoreProvider;
        public CspColumnMasterKey CspColumnMasterKey;

        public SQLSetupStrategyCertStoreProvider() : base()
        {
            CertStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();
            SetupDatabase();
        }

        protected SQLSetupStrategyCertStoreProvider(string customKeyPath) => keyPath = customKeyPath;

        internal override void SetupDatabase()
        {
            CspColumnMasterKey = new CspColumnMasterKey(GenerateUniqueName("CMK"), certificate.Thumbprint, CertStoreProvider, DataTestUtility.EnclaveEnabled);
            databaseObjects.Add(CspColumnMasterKey);

            List<ColumnEncryptionKey> columnEncryptionKeys = CreateColumnEncryptionKeys(CspColumnMasterKey, 2, CertStoreProvider);
            databaseObjects.AddRange(columnEncryptionKeys);

            List<Table> tables = CreateTables(columnEncryptionKeys);

            databaseObjects.AddRange(tables);

            base.SetupDatabase();
        }
    }
}
