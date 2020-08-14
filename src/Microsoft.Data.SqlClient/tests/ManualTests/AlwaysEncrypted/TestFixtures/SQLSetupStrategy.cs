// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.TestFixtures.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategy : IDisposable
    {
        internal const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA256";

        protected internal readonly X509Certificate2 certificate;
        public string keyPath { get; internal set; }
        public Table ApiTestTable { get; private set; }
        public Table BulkCopyAEErrorMessageTestTable { get; private set; }
        public Table BulkCopyAETestTable { get; private set; }
        public Table SqlParameterPropertiesTable { get; private set; }
        public Table End2EndSmokeTable { get; private set; }
        public Table TrustedMasterKeyPathsTestTable { get; private set; }
        public Table SqlNullValuesTable { get; private set; }
        public Table TabIntSource { get; private set; }
        public Table TabTinyIntTarget { get; private set; }
        public Table TabIntSourceDirect { get; private set; }
        public Table TabIntTargetDirect { get; private set; }
        public Table TabDatetime2Source { get; private set; }
        public Table TabDatetime2Target { get; private set; }
        public Table TabDecimalSource { get; private set; }
        public Table TabDecimalTarget { get; private set; }
        public Table TabVarCharSmallSource { get; private set; }
        public Table TabNVarCharSmallSource { get; private set; }
        public Table TabVarCharTarget { get; private set; }
        public Table TabVarCharMaxSource { get; private set; }
        public Table TabSmallCharMaxTarget { get; private set; }
        public Table TabVarCharMaxTarget { get; private set; }
        public Table TabNVarCharSmallTarget { get; private set; }
        public Table TabNVarCharMaxSource { get; private set; }
        public Table TabNVarCharTarget { get; private set; }
        public Table TabVarBinaryMaxSource { get; private set; }
        public Table TabSmallBinaryMaxTarget { get; private set; }
        public Table TabVarBinaryTarget { get; private set; }
        public Table TabBinaryMaxSource { get; private set; }
        public Table TabBinaryTarget { get; private set; }
        public Table TabSmallBinarySource { get; private set; }
        public Table TabSmallBinaryTarget { get; private set; }
        public Table TabSmallCharSource { get; private set; }
        public Table TabSmallCharTarget { get; private set; }

        protected List<DbObject> databaseObjects = new List<DbObject>();
        public Dictionary<string, string> sqlBulkTruncationTableNames = new Dictionary<string, string>();

        public SQLSetupStrategy()
        {
            certificate = CertificateUtility.CreateCertificate();
        }

        protected SQLSetupStrategy(string customKeyPath) => keyPath = customKeyPath;

        internal virtual void SetupDatabase()
        {
            foreach (string value in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(value))
                {
                    sqlConnection.Open();
                    databaseObjects.ForEach(o => o.Create(sqlConnection));

                    // Enable rich computation when Enclave is enabled
                    if (DataTestUtility.EnclaveEnabled)
                    {
                        using (SqlCommand command = new SqlCommand(@"DBCC traceon(127,-1);", sqlConnection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }

                // Insert data for TrustedMasterKeyPaths tests.
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(value)
                {
                    ConnectTimeout = 10000
                };
                Customer customer = new Customer(45, "Microsoft", "Corporation");
                using (SqlConnection sqlConn = new SqlConnection(builder.ToString()))
                {
                    sqlConn.Open();
                    DatabaseHelper.InsertCustomerData(sqlConn, TrustedMasterKeyPathsTestTable.Name, customer);
                }
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

        protected List<Table> CreateTables(IList<ColumnEncryptionKey> columnEncryptionKeys)
        {
            List<Table> tables = new List<Table>();

            ApiTestTable = new ApiTestTable(GenerateUniqueName("ApiTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(ApiTestTable);

            BulkCopyAEErrorMessageTestTable = new BulkCopyAEErrorMessageTestTable(GenerateUniqueName("BulkCopyAEErrorMessageTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(BulkCopyAEErrorMessageTestTable);

            BulkCopyAETestTable = new BulkCopyAETestTable(GenerateUniqueName("BulkCopyAETestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(BulkCopyAETestTable);

            SqlParameterPropertiesTable = new SqlParameterPropertiesTable(GenerateUniqueName("SqlParameterPropertiesTable"));
            tables.Add(SqlParameterPropertiesTable);

            End2EndSmokeTable = new ApiTestTable(GenerateUniqueName("End2EndSmokeTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(End2EndSmokeTable);

            TrustedMasterKeyPathsTestTable = new ApiTestTable(GenerateUniqueName("TrustedMasterKeyPathsTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(TrustedMasterKeyPathsTestTable);

            SqlNullValuesTable = new SqlNullValuesTable(GenerateUniqueName("SqlNullValuesTable"), columnEncryptionKeys[0]);
            tables.Add(SqlNullValuesTable);

            TabNVarCharMaxSource = new BulkCopyTruncationTables(GenerateUniqueName("TabNVarCharMaxSource"), columnEncryptionKeys[0]);
            tables.Add(TabNVarCharMaxSource);
            sqlBulkTruncationTableNames.Add("TabNVarCharMaxSource", TabNVarCharMaxSource.Name);

            TabNVarCharSmallSource = new BulkCopyTruncationTables(GenerateUniqueName("TabNVarCharSmallSource"), columnEncryptionKeys[0]);
            tables.Add(TabNVarCharSmallSource);
            sqlBulkTruncationTableNames.Add("TabNVarCharSmallSource", TabNVarCharSmallSource.Name);

            TabNVarCharSmallTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabNVarCharSmallTarget"), columnEncryptionKeys[0]);
            tables.Add(TabNVarCharSmallTarget);
            sqlBulkTruncationTableNames.Add("TabNVarCharSmallTarget", TabNVarCharSmallTarget.Name);

            TabNVarCharTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabNVarCharTarget"), columnEncryptionKeys[0]);
            tables.Add(TabNVarCharTarget);
            sqlBulkTruncationTableNames.Add("TabNVarCharTarget", TabNVarCharTarget.Name);

            TabSmallBinaryMaxTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabSmallBinaryMaxTarget"), columnEncryptionKeys[0]);
            tables.Add(TabSmallBinaryMaxTarget);
            sqlBulkTruncationTableNames.Add("TabSmallBinaryMaxTarget", TabSmallBinaryMaxTarget.Name);

            TabSmallBinarySource = new BulkCopyTruncationTables(GenerateUniqueName("TabSmallBinarySource"), columnEncryptionKeys[0]);
            tables.Add(TabSmallBinarySource);
            sqlBulkTruncationTableNames.Add("TabSmallBinarySource", TabSmallBinarySource.Name);

            TabSmallBinaryTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabSmallBinaryTarget"), columnEncryptionKeys[0]);
            tables.Add(TabSmallBinaryTarget);
            sqlBulkTruncationTableNames.Add("TabSmallBinaryTarget", TabSmallBinaryTarget.Name);

            TabSmallCharMaxTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabSmallCharMaxTarget"), columnEncryptionKeys[0]);
            tables.Add(TabSmallCharMaxTarget);
            sqlBulkTruncationTableNames.Add("TabSmallCharMaxTarget", TabSmallCharMaxTarget.Name);

            TabSmallCharSource = new BulkCopyTruncationTables(GenerateUniqueName("TabSmallCharSource"), columnEncryptionKeys[0]);
            tables.Add(TabSmallCharSource);
            sqlBulkTruncationTableNames.Add("TabSmallCharSource", TabSmallCharSource.Name);

            TabSmallCharTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabSmallCharTarget"), columnEncryptionKeys[0]);
            tables.Add(TabSmallCharTarget);
            sqlBulkTruncationTableNames.Add("TabSmallCharTarget", TabSmallCharTarget.Name);

            TabTinyIntTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabTinyIntTarget"), columnEncryptionKeys[0]);
            tables.Add(TabTinyIntTarget);
            sqlBulkTruncationTableNames.Add("TabTinyIntTarget", TabTinyIntTarget.Name);

            TabVarBinaryMaxSource = new BulkCopyTruncationTables(GenerateUniqueName("TabVarBinaryMaxSource"), columnEncryptionKeys[0]);
            tables.Add(TabVarBinaryMaxSource);
            sqlBulkTruncationTableNames.Add("TabVarBinaryMaxSource", TabVarBinaryMaxSource.Name);

            TabVarBinaryTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabVarBinaryTarget"), columnEncryptionKeys[0]);
            tables.Add(TabVarBinaryTarget);
            sqlBulkTruncationTableNames.Add("TabVarBinaryTarget", TabVarBinaryTarget.Name);

            TabVarCharMaxSource = new BulkCopyTruncationTables(GenerateUniqueName("TabVarCharMaxSource"), columnEncryptionKeys[0]);
            tables.Add(TabVarCharMaxSource);
            sqlBulkTruncationTableNames.Add("TabVarCharMaxSource", TabVarCharMaxSource.Name);

            TabVarCharMaxTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabVarCharMaxTarget"), columnEncryptionKeys[0]);
            tables.Add(TabVarCharMaxTarget);
            sqlBulkTruncationTableNames.Add("TabVarCharMaxTarget", TabVarCharMaxTarget.Name);

            TabVarCharSmallSource = new BulkCopyTruncationTables(GenerateUniqueName("TabVarCharSmallSource"), columnEncryptionKeys[0]);
            tables.Add(TabVarCharSmallSource);
            sqlBulkTruncationTableNames.Add("TabVarCharSmallSource", TabVarCharSmallSource.Name);

            TabVarCharTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabVarCharTarget"), columnEncryptionKeys[0]);
            tables.Add(TabVarCharTarget);
            sqlBulkTruncationTableNames.Add("TabVarCharTarget", TabVarCharTarget.Name);

            TabBinaryMaxSource = new BulkCopyTruncationTables(GenerateUniqueName("TabBinaryMaxSource"), columnEncryptionKeys[0]);
            tables.Add(TabBinaryMaxSource);
            sqlBulkTruncationTableNames.Add("TabBinaryMaxSource", TabBinaryMaxSource.Name);

            TabBinaryTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabBinaryTarget"), columnEncryptionKeys[0]);
            tables.Add(TabBinaryTarget);
            sqlBulkTruncationTableNames.Add("TabBinaryTarget", TabBinaryTarget.Name);

            TabDatetime2Source = new BulkCopyTruncationTables(GenerateUniqueName("TabDatetime2Source"), columnEncryptionKeys[0]);
            tables.Add(TabDatetime2Source);
            sqlBulkTruncationTableNames.Add("TabDatetime2Source", TabDatetime2Source.Name);

            TabDatetime2Target = new BulkCopyTruncationTables(GenerateUniqueName("TabDatetime2Target"), columnEncryptionKeys[0]);
            tables.Add(TabDatetime2Target);
            sqlBulkTruncationTableNames.Add("TabDatetime2Target", TabDatetime2Target.Name);

            TabDecimalSource = new BulkCopyTruncationTables(GenerateUniqueName("TabDecimalSource"), columnEncryptionKeys[0]);
            tables.Add(TabDecimalSource);
            sqlBulkTruncationTableNames.Add("TabDecimalSource", TabDecimalSource.Name);

            TabDecimalTarget = new BulkCopyTruncationTables(GenerateUniqueName("TabDecimalTarget"), columnEncryptionKeys[0]);
            tables.Add(TabDecimalTarget);
            sqlBulkTruncationTableNames.Add("TabDecimalTarget", TabDecimalTarget.Name);

            TabIntSource = new BulkCopyTruncationTables(GenerateUniqueName("TabIntSource"), columnEncryptionKeys[0]);
            tables.Add(TabIntSource);
            sqlBulkTruncationTableNames.Add("TabIntSource", TabIntSource.Name);

            TabIntSourceDirect = new BulkCopyTruncationTables(GenerateUniqueName("TabIntSourceDirect"), columnEncryptionKeys[0]);
            tables.Add(TabIntSourceDirect);
            sqlBulkTruncationTableNames.Add("TabIntSourceDirect", TabIntSourceDirect.Name);

            TabIntTargetDirect = new BulkCopyTruncationTables(GenerateUniqueName("TabIntTargetDirect"), columnEncryptionKeys[0]);
            tables.Add(TabIntTargetDirect);
            sqlBulkTruncationTableNames.Add("TabIntTargetDirect", TabIntTargetDirect.Name);

            return tables;
        }

        protected string GenerateUniqueName(string baseName) => string.Concat("AE-", baseName, "-", Guid.NewGuid().ToString());

        public void Dispose()
        {
            databaseObjects.Reverse();
            foreach (string value in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(value))
                {
                    sqlConnection.Open();
                    databaseObjects.ForEach(o => o.Drop(sqlConnection));
                }
            }
        }
    }

    // Use this class as the fixture for AE tests to ensure only one platform-specific fixture
    // is created for each test class
    public class PlatformSpecificTestContext : IDisposable
    {
        private SQLSetupStrategy certStoreFixture = null;
        private SQLSetupStrategy akvFixture = null;
        public SQLSetupStrategy Fixture => certStoreFixture ?? akvFixture;

        public PlatformSpecificTestContext()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                certStoreFixture = new SQLSetupStrategyCertStoreProvider();
            }
            else
            {
                akvFixture = new SQLSetupStrategyAzureKeyVault();
            }
        }

        public void Dispose()
        {
            Fixture.Dispose();
        }
    }
}
