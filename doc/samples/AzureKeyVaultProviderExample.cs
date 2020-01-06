using System;
//<Snippet1>
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.Data.SqlClient.Samples
{
    public class AzureKeyVaultProviderExample
    {
        static readonly string s_algorithm = "RSA_OAEP";

        // ********* Provide details here ***********
        static readonly string s_akvUrl = "https://{KeyVaultName}.vault.azure.net/keys/{Key}/{KeyIdentifier}";
        static readonly string s_clientId = "{Application_Client_ID}";
        static readonly string s_clientSecret = "{Application_Client_Secret}";
        static readonly string s_connectionString = "Server={Server}; Database={database}; Integrated Security=true; Column Encryption Setting=Enabled;";
        // ******************************************

        public static void Main(string[] args)
        {
            // Initialize AKV provider
            SqlColumnEncryptionAzureKeyVaultProvider sqlColumnEncryptionAzureKeyVaultProvider = new SqlColumnEncryptionAzureKeyVaultProvider(AzureActiveDirectoryAuthenticationCallback);

            // Register AKV provider
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders: new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
                {
                    { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, sqlColumnEncryptionAzureKeyVaultProvider}
                });
            Console.WriteLine("AKV provider Registered");

            // Create connection to database
            using (SqlConnection sqlConnection = new SqlConnection(s_connectionString))
            {
                string cmkName = "CMK_WITH_AKV";
                string cekName = "CEK_WITH_AKV";
                string tblName = "AKV_TEST_TABLE";

                CustomerRecord customer = new CustomerRecord(1, @"Microsoft", @"Corporation");

                try
                {
                    sqlConnection.Open();

                    // Drop Objects if exists
                    dropObjects(sqlConnection, cmkName, cekName, tblName);

                    // Create Column Master Key with AKV Url
                    createCMK(sqlConnection, cmkName);
                    Console.WriteLine("Column Master Key created.");

                    // Create Column Encryption Key
                    createCEK(sqlConnection, cmkName, cekName, sqlColumnEncryptionAzureKeyVaultProvider);
                    Console.WriteLine("Column Encryption Key created.");

                    // Create Table with Encrypted Columns
                    createTbl(sqlConnection, cekName, tblName);
                    Console.WriteLine("Table created with Encrypted columns.");

                    // Insert Customer Record in table
                    insertData(sqlConnection, tblName, customer);
                    Console.WriteLine("Encryted data inserted.");

                    // Read data from table
                    verifyData(sqlConnection, tblName, customer);
                    Console.WriteLine("Data validated successfully.");
                }
                finally
                {
                    // Drop table and keys
                    dropObjects(sqlConnection, cmkName, cekName, tblName);
                    Console.WriteLine("Dropped Table, CEK and CMK");
                }

                Console.WriteLine("Completed AKV provider Sample.");
            }
        }

        public static async Task<string> AzureActiveDirectoryAuthenticationCallback(string authority, string resource, string scope)
        {
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(s_clientId, s_clientSecret);
            AuthenticationResult result = await authContext.AcquireTokenAsync(resource, clientCred);
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to retrieve an access token for {resource}");
            }

            return result.AccessToken;
        }

        private static void createCMK(SqlConnection sqlConnection, string cmkName)
        {
            string KeyStoreProviderName = SqlColumnEncryptionAzureKeyVaultProvider.ProviderName;

            string sql =
                $@"CREATE COLUMN MASTER KEY [{cmkName}]
                    WITH (
                        KEY_STORE_PROVIDER_NAME = N'{KeyStoreProviderName}',
                        KEY_PATH = N'{s_akvUrl}'
                    );";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static void createCEK(SqlConnection sqlConnection, string cmkName, string cekName, SqlColumnEncryptionAzureKeyVaultProvider sqlColumnEncryptionAzureKeyVaultProvider)
        {
            string sql =
                $@"CREATE COLUMN ENCRYPTION KEY [{cekName}] 
                    WITH VALUES (
                        COLUMN_MASTER_KEY = [{cmkName}],
                        ALGORITHM = '{s_algorithm}', 
                        ENCRYPTED_VALUE = {GetEncryptedValue(sqlColumnEncryptionAzureKeyVaultProvider)}
                    )";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static string GetEncryptedValue(SqlColumnEncryptionAzureKeyVaultProvider sqlColumnEncryptionAzureKeyVaultProvider)
        {
            byte[] plainTextColumnEncryptionKey = new byte[32];
            RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
            rngCsp.GetBytes(plainTextColumnEncryptionKey);

            byte[] encryptedColumnEncryptionKey = sqlColumnEncryptionAzureKeyVaultProvider.EncryptColumnEncryptionKey(s_akvUrl, s_algorithm, plainTextColumnEncryptionKey);
            string EncryptedValue = string.Concat("0x", BitConverter.ToString(encryptedColumnEncryptionKey).Replace("-", string.Empty));
            return EncryptedValue;
        }

        private static void createTbl(SqlConnection sqlConnection, string cekName, string tblName)
        {
            string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA_256";

            string sql =
                    $@"CREATE TABLE [dbo].[{tblName}]
                (
                    [CustomerId] [int] ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{cekName}], ENCRYPTION_TYPE = DETERMINISTIC, ALGORITHM = '{ColumnEncryptionAlgorithmName}'),
                    [FirstName] [nvarchar](50) COLLATE Latin1_General_BIN2 ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{cekName}], ENCRYPTION_TYPE = DETERMINISTIC, ALGORITHM = '{ColumnEncryptionAlgorithmName}'),
                    [LastName] [nvarchar](50) COLLATE Latin1_General_BIN2 ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{cekName}], ENCRYPTION_TYPE = DETERMINISTIC, ALGORITHM = '{ColumnEncryptionAlgorithmName}')
                )";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static void insertData(SqlConnection sqlConnection, string tblName, CustomerRecord customer)
        {
            string insertSql = $"INSERT INTO [{tblName}] (CustomerId, FirstName, LastName) VALUES (@CustomerId, @FirstName, @LastName);";

            using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
            using (SqlCommand sqlCommand = new SqlCommand(insertSql,
            connection: sqlConnection, transaction: sqlTransaction,
            columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled))
            {
                sqlCommand.Parameters.AddWithValue(@"CustomerId", customer.Id);
                sqlCommand.Parameters.AddWithValue(@"FirstName", customer.FirstName);
                sqlCommand.Parameters.AddWithValue(@"LastName", customer.LastName);

                sqlCommand.ExecuteNonQuery();
                sqlTransaction.Commit();
            }
        }

        private static void verifyData(SqlConnection sqlConnection, string tblName, CustomerRecord customer)
        {
            // Test INPUT parameter on an encrypted parameter
            using (SqlCommand sqlCommand = new SqlCommand($"SELECT CustomerId, FirstName, LastName FROM [{tblName}] WHERE FirstName = @firstName",
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

        private static void ValidateResultSet(SqlDataReader sqlDataReader)
        {
            Console.WriteLine(" * Row available: " + sqlDataReader.HasRows);

            while (sqlDataReader.Read())
            {
                if (sqlDataReader.GetInt32(0) == 1)
                {
                    Console.WriteLine(" * Employee Id received as sent: " + sqlDataReader.GetInt32(0));
                }
                else
                {
                    Console.WriteLine("Employee Id didn't match");
                }

                if (sqlDataReader.GetString(1) == @"Microsoft")
                {
                    Console.WriteLine(" * Employee Firstname received as sent: " + sqlDataReader.GetString(1));
                }
                else
                {
                    Console.WriteLine("Employee FirstName didn't match.");
                }

                if (sqlDataReader.GetString(2) == @"Corporation")
                {
                    Console.WriteLine(" * Employee LastName received as sent: " + sqlDataReader.GetString(2));
                }
                else
                {
                    Console.WriteLine("Employee LastName didn't match.");
                }
            }
        }

        private static void dropObjects(SqlConnection sqlConnection, string cmkName, string cekName, string tblName)
        {
            using (SqlCommand cmd = sqlConnection.CreateCommand())
            {
                cmd.CommandText = $@"IF EXISTS (select * from sys.objects where name = '{tblName}') BEGIN DROP TABLE [{tblName}] END";
                cmd.ExecuteNonQuery();
                cmd.CommandText = $@"IF EXISTS (select * from sys.column_encryption_keys where name = '{cekName}') BEGIN DROP COLUMN ENCRYPTION KEY [{cekName}] END";
                cmd.ExecuteNonQuery();
                cmd.CommandText = $@"IF EXISTS (select * from sys.column_master_keys where name = '{cmkName}') BEGIN DROP COLUMN MASTER KEY [{cmkName}] END";
                cmd.ExecuteNonQuery();
            }
        }

        private class CustomerRecord
        {
            internal int Id { get; set; }
            internal string FirstName { get; set; }
            internal string LastName { get; set; }

            public CustomerRecord(int id, string fName, string lName)
            {
                Id = id;
                FirstName = fName;
                LastName = lName;
            }
        }
    }
}
//</Snippet1>
