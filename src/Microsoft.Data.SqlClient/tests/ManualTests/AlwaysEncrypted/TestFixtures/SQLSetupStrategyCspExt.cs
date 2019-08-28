// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class SQLSetupStrategyCspExt : SQLSetupStrategy
    {
        //public string keyPath {get; private set;}
        public Table CspProviderTable {get; private set;}
        public SqlColumnEncryptionCspProvider keyStoreProvider {get;}

        public SQLSetupStrategyCspExt(string cspKeyPath) : base(cspKeyPath)
        {
            keyStoreProvider = new SqlColumnEncryptionCspProvider();
            this.SetupDatabase();
        }

        internal override void SetupDatabase()
        {
            ColumnMasterKey columnMasterKey = new CspColumnMasterKey(GenerateUniqueName("CMK"), SqlColumnEncryptionCspProvider.ProviderName, keyPath);
            databaseObjects.Add(columnMasterKey);

            List<ColumnEncryptionKey> columnEncryptionKeys = CreateColumnEncryptionKeys(columnMasterKey, 2, keyStoreProvider);
            databaseObjects.AddRange(columnEncryptionKeys);

            Table table = CreateTable(columnEncryptionKeys);
            databaseObjects.Add(table);

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                databaseObjects.ForEach(o => o.Create(sqlConnection));
            }
        }

        private Table CreateTable(IList<ColumnEncryptionKey> columnEncryptionKeys)
        {
            CspProviderTable = new ApiTestTable(GenerateUniqueName("CspProviderTable"), columnEncryptionKeys[0], columnEncryptionKeys[1]);

            return CspProviderTable;
        }

        public void DropTable()
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                CspProviderTable.Drop(sqlConnection);
            }
        }
    }
}