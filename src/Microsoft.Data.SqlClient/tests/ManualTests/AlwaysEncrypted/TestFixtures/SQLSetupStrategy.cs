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
        private readonly X509Certificate2 certificate;
        public Table ApiTestTable { get; private set; }
        public Table SqlParameterPropertiesTable { get; private set; }

        List<DbObject> databaseObjects = new List<DbObject>();

        public SQLSetupStrategy()
        {
            certificate = CertificateUtility.CreateCertificate();
            SetupDatabase();
        }

        internal virtual void SetupDatabase()
        {
            ColumnMasterKey columnMasterKey = new CspColumnMasterKey(GenerateUniqueName("CMK"), certificate.Thumbprint);
            databaseObjects.Add(columnMasterKey);

            List<ColumnEncryptionKey> columnEncryptionKeys = CreateColumnEncryptionKeys(columnMasterKey, 2);
            databaseObjects.AddRange(columnEncryptionKeys);

            List<Table> tables = CreateTables(columnEncryptionKeys);
            databaseObjects.AddRange(tables);

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Create(sqlConnection));
            }
        }

        private List<ColumnEncryptionKey> CreateColumnEncryptionKeys(ColumnMasterKey columnMasterKey, int count)
        {
            List<ColumnEncryptionKey> columnEncryptionKeys = new List<ColumnEncryptionKey>();
            for (int i = 0; i < count; i++)
            {
                ColumnEncryptionKey columnEncryptionKey = new ColumnEncryptionKey(GenerateUniqueName("CEK"), columnMasterKey);
                columnEncryptionKeys.Add(columnEncryptionKey);
            }

            return columnEncryptionKeys;
        }

        private List<Table> CreateTables(IList<ColumnEncryptionKey> columnEncryptionKeys)
        {
            List<Table> tables = new List<Table>();

            ApiTestTable = new ApiTestTable(GenerateUniqueName("ApiTestTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);
            tables.Add(ApiTestTable);
            
            SqlParameterPropertiesTable = new SqlParameterPropertiesTable(GenerateUniqueName("SqlParameterPropertiesTable"));
            tables.Add(SqlParameterPropertiesTable);

            return tables;
        }

        private string GenerateUniqueName(string baseName) => string.Concat("AE-", baseName, "-", Guid.NewGuid().ToString());

        public void Dispose()
        {
            CertificateUtility.RemoveCertificate(certificate);
            databaseObjects.Reverse();
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Drop(sqlConnection));
            }
        }
    }
}
