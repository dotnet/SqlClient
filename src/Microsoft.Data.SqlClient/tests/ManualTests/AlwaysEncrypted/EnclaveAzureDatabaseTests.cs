// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.using System;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    // This test class is for internal use only
    public class EnclaveAzureDatabaseTests : IDisposable
    {
        private ColumnMasterKey akvColumnMasterKey;
        private ColumnEncryptionKey akvColumnEncryptionKey;
        private SqlColumnEncryptionAzureKeyVaultProvider sqlColumnEncryptionAzureKeyVaultProvider; 
        private List<DbObject> databaseObjects = new List<DbObject>();
        private List<string> connStrings = new List<string>();
             
        public EnclaveAzureDatabaseTests()
        {
            if (DataTestUtility.IsEnclaveAzureDatabaseSetup())
            {
                // Initialize AKV provider
                sqlColumnEncryptionAzureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(AADUtility.AzureActiveDirectoryAuthenticationCallback);

                if (!SQLSetupStrategyAzureKeyVault.isAKVProviderRegistered) 
                {                    
                    // Register AKV provider
                    SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders: new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
                    {
                        { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, sqlColumnEncryptionAzureKeyVaultProvider}
                    });

                    SQLSetupStrategyAzureKeyVault.isAKVProviderRegistered = true;
                }               

                akvColumnMasterKey = new AkvColumnMasterKey(DatabaseHelper.GenerateUniqueName("AKVCMK"), akvUrl: DataTestUtility.AKVUrl, sqlColumnEncryptionAzureKeyVaultProvider, DataTestUtility.EnclaveEnabled);
                databaseObjects.Add(akvColumnMasterKey);

                akvColumnEncryptionKey= new ColumnEncryptionKey(DatabaseHelper.GenerateUniqueName("AKVCEK"),
                                                              akvColumnMasterKey,
                                                              sqlColumnEncryptionAzureKeyVaultProvider);
                databaseObjects.Add(akvColumnEncryptionKey);

                SqlConnectionStringBuilder connString1 = new SqlConnectionStringBuilder(DataTestUtility.EnclaveAzureDatabaseConnString);
                connString1.InitialCatalog = "testdb001";

                SqlConnectionStringBuilder connString2 = new SqlConnectionStringBuilder(DataTestUtility.EnclaveAzureDatabaseConnString);
                connString2.InitialCatalog = "testdb002";

                connStrings.Add(connString1.ToString());
                connStrings.Add(connString2.ToString());

                foreach (string connString in connStrings)
                {
                    using (SqlConnection connection = new SqlConnection(connString))
                    {
                        connection.Open();
                        databaseObjects.ForEach(o => o.Create(connection));
                    }
                }
            }            
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsEnclaveAzureDatabaseSetup))]
        public void ConnectToAzureDatabaseWithEnclave()
        {
            string tableName = DatabaseHelper.GenerateUniqueName("AzureTable");

            foreach (string connString in connStrings)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connString))
                {
                    sqlConnection.Open();

                    Customer customer = new Customer(1, @"Microsoft", @"Corporation");

                    try
                    {
                        CreateTable(sqlConnection, akvColumnEncryptionKey.Name, tableName);
                        InsertData(sqlConnection, tableName, customer);
                        VerifyData(sqlConnection, tableName, customer);
                    }
                    finally
                    {
                        DropTableIfExists(sqlConnection, tableName);
                    }
                }
            }
        }

        private void CreateTable(SqlConnection sqlConnection, string cekName, string tableName)
        {
            string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA_256";
            string sql =
                    $@"CREATE TABLE [dbo].[{tableName}]
                (
                    [CustomerId] [int] ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{cekName}], ENCRYPTION_TYPE = RANDOMIZED, ALGORITHM = '{ColumnEncryptionAlgorithmName}'),
                    [FirstName] [nvarchar](50) COLLATE Latin1_General_BIN2 ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{cekName}], ENCRYPTION_TYPE = RANDOMIZED, ALGORITHM = '{ColumnEncryptionAlgorithmName}'),
                    [LastName] [nvarchar](50) COLLATE Latin1_General_BIN2 ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{cekName}], ENCRYPTION_TYPE = RANDOMIZED, ALGORITHM = '{ColumnEncryptionAlgorithmName}')
                )";
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private void InsertData(SqlConnection sqlConnection, string tableName, Customer newCustomer)
        {
            string insertSql = $"INSERT INTO [{tableName}] (CustomerId, FirstName, LastName) VALUES (@CustomerId, @FirstName, @LastName);";
            using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
            using (SqlCommand sqlCommand = new SqlCommand(insertSql,
            connection: sqlConnection, transaction: sqlTransaction,
            columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                sqlCommand.Parameters.AddWithValue(@"CustomerId", newCustomer.Id);
                sqlCommand.Parameters.AddWithValue(@"FirstName", newCustomer.FirstName);
                sqlCommand.Parameters.AddWithValue(@"LastName", newCustomer.LastName);
                sqlCommand.ExecuteNonQuery();
                sqlTransaction.Commit();
            }
        }

        private void VerifyData(SqlConnection sqlConnection, string tableName, Customer customer)
        {
            // Test INPUT parameter on an encrypted parameter
            using (SqlCommand sqlCommand = new SqlCommand($"SELECT CustomerId, FirstName, LastName FROM [{tableName}] WHERE FirstName = @firstName",
                                                            sqlConnection))
            {
                SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                customerFirstParam.Direction = System.Data.ParameterDirection.Input;
                customerFirstParam.ForceColumnEncryption = true;
                using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                {
                    ValidateResultSet(sqlDataReader);
                }
            }
        }

        private void ValidateResultSet(SqlDataReader sqlDataReader)
        {
            Assert.True(sqlDataReader.HasRows, "We didn't find any rows.");
            while (sqlDataReader.Read())
            {
                Assert.True(sqlDataReader.GetInt32(0) == 1, "Employee Id didn't match");
                Assert.True(sqlDataReader.GetString(1) == @"Microsoft", "Employee FirstName didn't match.");
                Assert.True(sqlDataReader.GetString(2) == @"Corporation", "Employee LastName didn't match.");
            }
        }

        private void DropTableIfExists(SqlConnection sqlConnection, string tableName)
        {
            string cmdText = $@"IF EXISTS (select * from sys.objects where name = '{tableName}') BEGIN DROP TABLE [{tableName}] END";
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = cmdText;
                command.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            if (DataTestUtility.IsEnclaveAzureDatabaseSetup())
            {
                databaseObjects.Reverse();
                foreach (string connStr in connStrings)
                {
                    using (SqlConnection sqlConnection = new SqlConnection(connStr))
                    {
                        sqlConnection.Open();
                        databaseObjects.ForEach(o => o.Drop(sqlConnection));
                    }
                }
            }
        }
    }    
}
