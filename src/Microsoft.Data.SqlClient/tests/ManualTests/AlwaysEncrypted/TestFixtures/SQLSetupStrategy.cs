// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategy : IDisposable
    {
        internal const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA256";

        private readonly X509Certificate2 certificate;
        public string keyPath { get; private set; }
        public Table ApiTestTable { get; private set; }
        public Table AKVTestTable { get; private set; }
        public Table BulkCopyAETestTable { get; private set; }
        public Table SqlParameterPropertiesTable { get; private set; }
        public Table End2EndSmokeTable { get; private set; }
        public Table TrustedMasterKeyPathsTestTable { get; private set; }

        public SqlColumnEncryptionAzureKeyVaultProvider akvStoreProvider;
        public SqlColumnEncryptionCertificateStoreProvider certStoreProvider;
        public CspColumnMasterKey cspColumnMasterKey;

        private static bool isAKVProviderRegistered = false;

        protected List<DbObject> databaseObjects = new List<DbObject>();

        public SQLSetupStrategy()
        {
            certificate = CertificateUtility.CreateCertificate();
            certStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();

            if (DataTestUtility.IsAKVSetupAvailable())
            {
                akvStoreProvider = new SqlColumnEncryptionAzureKeyVaultProvider(authenticationCallback: AADUtility.AzureActiveDirectoryAuthenticationCallback);

                if (!isAKVProviderRegistered)
                {
                    Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customAkvKeyStoreProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
                    {
                        {SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, akvStoreProvider}
                    };

                    SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders: customAkvKeyStoreProviders);
                    isAKVProviderRegistered = true;
                }
            }

            SetupDatabase();
        }

        protected SQLSetupStrategy(string customKeyPath) => keyPath = customKeyPath;

        internal virtual void SetupDatabase()
        {
            cspColumnMasterKey = new CspColumnMasterKey(GenerateUniqueName("CMK"), certificate.Thumbprint);
            databaseObjects.Add(cspColumnMasterKey);

            List<ColumnEncryptionKey> akvColumnEncryptionKeys = null;
            
            if (DataTestUtility.IsAKVSetupAvailable())
            {
                ColumnMasterKey akvColumnMasterKey = new AkvColumnMasterKey(GenerateUniqueName("AKVCMK"), akvUrl: DataTestUtility.AKVUrl);
                databaseObjects.Add(akvColumnMasterKey);

                akvColumnEncryptionKeys = CreateColumnEncryptionKeys(akvColumnMasterKey, 2, akvStoreProvider);
                databaseObjects.AddRange(akvColumnEncryptionKeys);
            }

            List<ColumnEncryptionKey> columnEncryptionKeys = CreateColumnEncryptionKeys(cspColumnMasterKey, 2, certStoreProvider);
            databaseObjects.AddRange(columnEncryptionKeys);

            List<Table> tables = CreateTables(columnEncryptionKeys, akvColumnEncryptionKeys);
            databaseObjects.AddRange(tables);

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Create(sqlConnection));
            }

            // Insert data for TrustedMasterKeyPaths tests.
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TcpConnStr);
            builder.ConnectTimeout = 10000;
            Customer customer = new Customer(45, "Microsoft", "Corporation");
            using (SqlConnection sqlConn = new SqlConnection(builder.ToString()))
            {
                sqlConn.Open();
                DatabaseHelper.InsertCustomerData(sqlConn, TrustedMasterKeyPathsTestTable.Name, customer);
            }
        }

        protected List<ColumnEncryptionKey> CreateColumnEncryptionKeys(ColumnMasterKey columnMasterKey, int count, SqlColumnEncryptionKeyStoreProvider columnEncryptionKeyStoreProvider)
        {
            List<ColumnEncryptionKey> columnEncryptionKeys = new List<ColumnEncryptionKey>();

            for (int i = 0; i < count; i++)
            {
                ColumnEncryptionKey columnEncryptionKey = new ColumnEncryptionKey(GenerateUniqueName("CEK"), columnMasterKey, columnEncryptionKeyStoreProvider);
                columnEncryptionKeys.Add(columnEncryptionKey);
            }

            return columnEncryptionKeys;
        }

        private List<Table> CreateTables(IList<ColumnEncryptionKey> columnEncryptionKeys, IList<ColumnEncryptionKey> akvColumnEncryptionKeys)
        {
            List<Table> tables = new List<Table>();

            ApiTestTable = new ApiTestTable(GenerateUniqueName("ApiTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(ApiTestTable);

            if (DataTestUtility.IsAKVSetupAvailable() && akvColumnEncryptionKeys != null)
            {
                AKVTestTable = new AKVTestTable(GenerateUniqueName("AKVTestTable"), akvColumnEncryptionKeys[0], akvColumnEncryptionKeys[1]);
                tables.Add(AKVTestTable);
            }

            BulkCopyAETestTable = new BulkCopyAETestTable(GenerateUniqueName("BulkCopyAETestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(BulkCopyAETestTable);

            SqlParameterPropertiesTable = new SqlParameterPropertiesTable(GenerateUniqueName("SqlParameterPropertiesTable"));
            tables.Add(SqlParameterPropertiesTable);

            End2EndSmokeTable = new ApiTestTable(GenerateUniqueName("End2EndSmokeTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(End2EndSmokeTable);

            TrustedMasterKeyPathsTestTable = new ApiTestTable(GenerateUniqueName("TrustedMasterKeyPathsTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(TrustedMasterKeyPathsTestTable);

            return tables;
        }

        protected string GenerateUniqueName(string baseName) => string.Concat("AE-", baseName, "-", Guid.NewGuid().ToString());

        public void Dispose()
        {
            databaseObjects.Reverse();
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Drop(sqlConnection));
            }
        }
    }
}
