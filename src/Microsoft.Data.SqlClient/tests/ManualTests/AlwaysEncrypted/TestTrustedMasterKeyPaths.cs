using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class TestTrustedMasterKeyPaths : IClassFixture<SQLSetupStrategyCertStoreProvider>
    {
        private SQLSetupStrategyCertStoreProvider fixture;
        private readonly string tableName;
        private readonly string columnMasterKeyPath;

        public TestTrustedMasterKeyPaths(SQLSetupStrategyCertStoreProvider fixture)
        {
            columnMasterKeyPath = string.Format(@"{0}/{1}/{2}", StoreLocation.CurrentUser.ToString(), @"my", CertificateUtility.CreateCertificate().Thumbprint);
            this.fixture = fixture;
            tableName = fixture.TrustedMasterKeyPathsTestTable.Name;
        }

        /// <summary>
        /// Validates that the results are the ones expected.
        /// </summary>
        /// <param name="sqlDataReader"></param>
        private void ValidateResultSet(SqlDataReader sqlDataReader)
        {
            // Validate the result set
            int rowsFound = 0;
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

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestTrustedColumnEncryptionMasterKeyPathsWithNullDictionary(string connection)
        {
            SqlConnectionStringBuilder connBuilder = new SqlConnectionStringBuilder(connection);
            connBuilder.ConnectTimeout = 10000;
            string connStringNow = connBuilder.ToString();

            // 1. Default should succeed.
            if (SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Count != 0)
            {
                SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
            }

            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(connStringNow, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                // Test INPUT parameter on an encrypted parameter
                using (SqlCommand sqlCommand = new SqlCommand(
                       $@"SELECT CustomerId, FirstName, LastName 
                          FROM [{tableName}] 
                          WHERE FirstName = @firstName",
                       sqlConnection))
                {
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;

                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        ValidateResultSet(sqlDataReader);
                    }
                }
            }
            // Clear out trusted key paths
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
        }

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestTrustedColumnEncryptionMasterKeyPathsWithOneServer(string connection)
        {
            SqlConnectionStringBuilder connBuilder = new SqlConnectionStringBuilder(connection);
            connBuilder.ConnectTimeout = 10000;
            string connStringNow = connBuilder.ToString();

            // 2.. Test with valid key path
            //
            // Clear existing dictionary.
            if (SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Count != 0)
            {
                SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
            }

            // Add the keypath to the trusted keypaths
            List<string> trustedKeyPaths = new List<string>();
            trustedKeyPaths.Add(columnMasterKeyPath);
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Add(connBuilder.DataSource, trustedKeyPaths);

            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(connStringNow, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                // Test INPUT parameter on an encrypted parameter
                using (SqlCommand sqlCommand = new SqlCommand(
                       $@"SELECT CustomerId, FirstName, LastName 
                          FROM [{tableName}] 
                          WHERE FirstName = @firstName",
                       sqlConnection))
                {
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;

                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        ValidateResultSet(sqlDataReader);
                    }
                }
            }
            // Clear out trusted key paths
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
        }

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestTrustedColumnEncryptionMasterKeyPathsWithMultipleServers(string connection)
        {
            SqlConnectionStringBuilder connBuilder = new SqlConnectionStringBuilder(connection);
            connBuilder.ConnectTimeout = 10000;
            string connStringNow = connBuilder.ToString();

            // 3. Test with multiple servers with multiple key paths
            //
            // Clear existing dictionary.
            if (SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Count != 0)
            {
                SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
            }

            // Add entries for one server
            List<string> server1TrustedKeyPaths = new List<string>();

            // Add some random key paths
            foreach (char c in new char[] { 'A', 'B' })
            {
                string tempThumbprint = new string('F', CertificateUtility.CreateCertificate().Thumbprint.Length);
                string invalidKeyPath = string.Format(@"{0}/my/{1}", StoreLocation.CurrentUser.ToString(), tempThumbprint);
                server1TrustedKeyPaths.Add(invalidKeyPath);
            }

            // Add the key path used by the test
            server1TrustedKeyPaths.Add(columnMasterKeyPath);

            // Add it to the dictionary
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Add(connBuilder.DataSource, server1TrustedKeyPaths);

            // Add entries for another server
            List<string> server2TrustedKeyPaths = new List<string>();
            server2TrustedKeyPaths.Add(@"https://balneetestkeyvault.vault.azure.net/keys/CryptoTest4/f4eb1dbbe6a9446599efe3c952614e70");
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Add(@"randomeserver", server2TrustedKeyPaths);

            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(connStringNow, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                // Test INPUT parameter on an encrypted parameter
                using (SqlCommand sqlCommand = new SqlCommand(
                       $@"SELECT CustomerId, FirstName, LastName 
                          FROM [{tableName}] 
                          WHERE FirstName = @firstName",
                       sqlConnection))
                {
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;

                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                    {
                        ValidateResultSet(sqlDataReader);
                    }
                }
            }
            // Clear out trusted key paths
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
        }

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public void TestTrustedColumnEncryptionMasterKeyPathsWithInvalidInputs(string connection)
        {
            SqlConnectionStringBuilder connBuilder = new SqlConnectionStringBuilder(connection);
            connBuilder.ConnectTimeout = 10000;
            string connStringNow = connBuilder.ToString();

            // 1. Test with null List
            //
            // Clear existing dictionary.
            if (SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Count != 0)
            {
                SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
            }

            // Clear any cache
            CertificateUtility.CleanSqlClientCache();

            // Prepare a dictionary with null list.
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Add(connBuilder.DataSource, (List<string>)null);

            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(connStringNow, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                // Test INPUT parameter on an encrypted parameter
                using (SqlCommand sqlCommand = new SqlCommand(
                       $@"SELECT CustomerId, FirstName, LastName 
                          FROM [{tableName}] 
                          WHERE FirstName = @firstName",
                       sqlConnection))
                {
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;

                    string expectedErrorMessage = "not a trusted key path";
                    ArgumentException e = Assert.Throws<ArgumentException>(() => sqlCommand.ExecuteReader());
                    Assert.Contains(expectedErrorMessage, e.Message);
                }
            }

            // 2. Test with empty List
            //
            // Clear existing dictionary.
            if (SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Count != 0)
            {
                SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
            }

            // Prepare dictionary with an empty list
            List<string> emptyKeyPathList = new List<string>();
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Add(connBuilder.DataSource, emptyKeyPathList);

            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(connStringNow, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                // Test INPUT parameter on an encrypted parameter
                using (SqlCommand sqlCommand = new SqlCommand(
                       $@"SELECT CustomerId, FirstName, LastName 
                          FROM [{tableName}] 
                          WHERE FirstName = @firstName",
                       sqlConnection))
                {
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;

                    string expectedErrorMessage = "not a trusted key path";
                    ArgumentException e = Assert.Throws<ArgumentException>(() => sqlCommand.ExecuteReader());
                    Assert.Contains(expectedErrorMessage, e.Message);
                }
            }

            // 3. Test with invalid key paths
            //
            // Clear existing dictionary.
            if (SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Count != 0)
            {
                SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
            }

            // Prepare dictionary with invalid key path
            List<string> invalidKeyPathList = new List<string>();
            string tempThumbprint = new string('F', CertificateUtility.CreateCertificate().Thumbprint.Length);
            string invalidKeyPath = string.Format(@"{0}/my/{1}", StoreLocation.CurrentUser.ToString(), tempThumbprint);
            invalidKeyPathList.Add(invalidKeyPath);
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Add(connBuilder.DataSource, invalidKeyPathList);

            using (SqlConnection sqlConnection = new SqlConnection(string.Concat(connStringNow, @";Column Encryption Setting = Enabled;")))
            {
                sqlConnection.Open();

                // Test INPUT parameter on an encrypted parameter
                using (SqlCommand sqlCommand = new SqlCommand(
                       $@"SELECT CustomerId, FirstName, LastName 
                          FROM [{tableName}] 
                          WHERE FirstName = @firstName",
                       sqlConnection))
                {
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;
                    string expectedErrorMessage = "not a trusted key path";
                    ArgumentException e = Assert.Throws<ArgumentException>(() => sqlCommand.ExecuteReader());
                    Assert.Contains(expectedErrorMessage, e.Message);
                }
            }

            // Clear out trusted key paths
            SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.Clear();
        }
    }
}
