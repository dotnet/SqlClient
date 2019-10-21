using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [PlatformSpecific(TestPlatforms.Windows)]
    public class AKVTest : IClassFixture<SQLSetupStrategy>, IDisposable
    {
        private SQLSetupStrategy fixture;
        private readonly string tableName;

        public AKVTest(SQLSetupStrategy fixture)
        {
            this.fixture = fixture;
            tableName = fixture.AKVTestTable.Name;

            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TestEncryptDecryptWithAKV()
        {
            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(DataTestUtility.TcpConnStr, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                Customer customer = new Customer(45, "Microsoft", "Corporation");

                // Start a transaction and either commit or rollback based on the test variation.
                using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
                {
                    InsertCustomerRecord(sqlConnection, sqlTransaction, customer);
                    sqlTransaction.Commit();
                }

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
        }

        private void InsertCustomerRecord(SqlConnection sqlConnection, SqlTransaction sqlTransaction, Customer customer)
        {
            using (SqlCommand sqlCommand = new SqlCommand(
                $"INSERT INTO [{tableName}] (CustomerId, FirstName, LastName) VALUES (@CustomerId, @FirstName, @LastName);",
                connection: sqlConnection,
                transaction: sqlTransaction,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
                sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);
                sqlCommand.Parameters.AddWithValue(@"LastName", customer.LastName);

                sqlCommand.ExecuteNonQuery();
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TestRoundTripWithAKVAndCertStoreProvider()
        {
            byte[] plainTextColumnEncryptionKey = ColumnEncryptionKey.GenerateRandomBytes(ColumnEncryptionKey.KeySizeInBytes);
            byte[] encryptedColumnEncryptionKeyUsingAKV = fixture.akvStoreProvider.EncryptColumnEncryptionKey(DataTestUtility.AKVUrl, @"RSA_OAEP", plainTextColumnEncryptionKey);
            byte[] columnEncryptionKeyReturnedAKV2Cert = fixture.certStoreProvider.DecryptColumnEncryptionKey(fixture.cspColumnMasterKey.KeyPath, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingAKV);
            Assert.True(plainTextColumnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedAKV2Cert), @"Roundtrip failed");

            // Try the opposite.
            byte[] encryptedColumnEncryptionKeyUsingCert = fixture.certStoreProvider.EncryptColumnEncryptionKey(fixture.cspColumnMasterKey.KeyPath, @"RSA_OAEP", plainTextColumnEncryptionKey);
            byte[] columnEncryptionKeyReturnedCert2AKV = fixture.akvStoreProvider.DecryptColumnEncryptionKey(DataTestUtility.AKVUrl, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingCert);
            Assert.True(plainTextColumnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedCert2AKV), @"Roundtrip failed");

        }

        /// <summary>
        /// Validates that the results are the ones expected.
        /// </summary>
        /// <param name="sqlDataReader"></param>
        private void ValidateResultSet(SqlDataReader sqlDataReader)
        {
            // Validate the result set
            int rowsFound = 0;

            Assert.True(sqlDataReader.HasRows);
            while (sqlDataReader.Read())
            {
                if (sqlDataReader.FieldCount == 3)
                {
                    Assert.True(sqlDataReader.GetInt32(0) == 45, "Employee id didn't match.");
                    Assert.True(sqlDataReader.GetString(1) == @"Microsoft", "Employee FirstName didn't match.");
                    Assert.True(sqlDataReader.GetString(2) == @"Corporation", "Employee LastName didn't match.");
                }
                else if (sqlDataReader.FieldCount == 1)
                {
                    Assert.True(sqlDataReader.GetString(0) == @"Microsoft" || sqlDataReader.GetString(0) == @"Corporation", "Employee FirstName didn't match.");
                }

                rowsFound++;
            }

            Assert.True(rowsFound == 1, "Incorrect number of rows returned in first execution.");
        }

        public void Dispose()
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                sqlConnection.Open();
                Table.DeleteData(fixture.AKVTestTable.Name, sqlConnection);
            }
        }
    }
}
