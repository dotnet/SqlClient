// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategy : IDisposable
    {
        internal const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA256";

        private readonly X509Certificate2 certificate;
        public string keyPath { get; private set; }
        public Table ApiTestTable { get; private set; }
        public Table BulkCopyAETestTable { get; private set; }
        public Table SqlParameterPropertiesTable { get; private set; }
        public Table End2EndSmokeTable { get; private set; }

        protected List<DbObject> databaseObjects = new List<DbObject>();

        public SQLSetupStrategy()
        {
            certificate = CertificateUtility.CreateCertificate();
            SetupDatabase();
        }

        protected SQLSetupStrategy(string customKeyPath) => keyPath = customKeyPath;

        internal virtual void SetupDatabase()
        {
            ColumnMasterKey columnMasterKey = new CspColumnMasterKey(GenerateUniqueName("CMK"), certificate.Thumbprint);
            databaseObjects.Add(columnMasterKey);

            SqlColumnEncryptionCertificateStoreProvider certStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();
            List<ColumnEncryptionKey> columnEncryptionKeys = CreateColumnEncryptionKeys(columnMasterKey, 2, certStoreProvider);
            databaseObjects.AddRange(columnEncryptionKeys);

            List<Table> tables = CreateTables(columnEncryptionKeys);
            databaseObjects.AddRange(tables);

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Create(sqlConnection));
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

        private List<Table> CreateTables(IList<ColumnEncryptionKey> columnEncryptionKeys)
        {
            List<Table> tables = new List<Table>();

            ApiTestTable = new ApiTestTable(GenerateUniqueName("ApiTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(ApiTestTable);
			
            BulkCopyAETestTable = new BulkCopyAETestTable(GenerateUniqueName("BulkCopyAETestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(BulkCopyAETestTable);

            SqlParameterPropertiesTable = new SqlParameterPropertiesTable(GenerateUniqueName("SqlParameterPropertiesTable"));
            tables.Add(SqlParameterPropertiesTable);

            End2EndSmokeTable = new ApiTestTable(GenerateUniqueName("End2EndSmokeTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(End2EndSmokeTable);

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
