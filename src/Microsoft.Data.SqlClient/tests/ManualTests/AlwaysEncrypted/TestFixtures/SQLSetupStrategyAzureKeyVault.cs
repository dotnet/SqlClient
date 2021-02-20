// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategyAzureKeyVault : SQLSetupStrategy
    {
        internal static bool isAKVProviderRegistered = false;

        public Table AKVTestTable { get; private set; }
        public SqlColumnEncryptionAzureKeyVaultProvider AkvStoreProvider;
        public DummyMasterKeyForAKVProvider DummyMasterKey;

        public SQLSetupStrategyAzureKeyVault() : base()
        {
            AkvStoreProvider = new SqlColumnEncryptionAzureKeyVaultProvider(authenticationCallback: AADUtility.AzureActiveDirectoryAuthenticationCallback);

            if (!isAKVProviderRegistered)
            {
                // this provider is required in ApiShould.TestCustomKeyStoreProviderRegistration()
                DummyKeyStoreProvider dummyProvider = new DummyKeyStoreProvider();

                Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customAkvKeyStoreProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
                {
                    {SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, AkvStoreProvider},
                    { "DummyProvider", dummyProvider}
                };

                SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders: customAkvKeyStoreProviders);
                isAKVProviderRegistered = true;
            }

            SetupDatabase();
        }

        internal override void SetupDatabase()
        {
            ColumnMasterKey akvColumnMasterKey = new AkvColumnMasterKey(GenerateUniqueName("AKVCMK"), akvUrl: DataTestUtility.AKVUrl, AkvStoreProvider, DataTestUtility.EnclaveEnabled);
            DummyMasterKey = new DummyMasterKeyForAKVProvider(GenerateUniqueName("DummyCMK"), DataTestUtility.AKVUrl, AkvStoreProvider, DataTestUtility.EnclaveEnabled);

            databaseObjects.Add(akvColumnMasterKey);
            databaseObjects.Add(DummyMasterKey);

            List<ColumnEncryptionKey> akvColumnEncryptionKeys = CreateColumnEncryptionKeys(akvColumnMasterKey, 2, AkvStoreProvider);
            List<ColumnEncryptionKey> dummyColumnEncryptionKeys = CreateColumnEncryptionKeys(DummyMasterKey, 1, AkvStoreProvider);
            akvColumnEncryptionKeys.AddRange(dummyColumnEncryptionKeys);
            databaseObjects.AddRange(akvColumnEncryptionKeys);

            List<Table> tables = CreateTables(akvColumnEncryptionKeys);
            AKVTestTable = new AKVTestTable(GenerateUniqueName("AKVTestTable"), akvColumnEncryptionKeys[0], akvColumnEncryptionKeys[1]);
            tables.Add(AKVTestTable);
            databaseObjects.AddRange(tables);

            base.SetupDatabase();
        }
    }
}
