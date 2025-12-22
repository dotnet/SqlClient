/**
 * TODO: This sample file should be deleted as the AKV Provider Ctor API is no longer supported with supported versions of AKV provider and MDS.
 * Depends on: Delete documentation and sample reference in MS Docs first: https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-key-vault-example?view=sql-server-ver17#legacy-callback-implementation-design-example-with-v20
 *

//< Snippet1>
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Data.SqlClient.Samples
{
    public class AzureKeyVaultProviderLegacyExample_2_0
    {
        const string s_algorithm = "RSA_OAEP";

        // ********* Provide details here ***********
        static readonly string s_akvUrl = "https://{KeyVaultName}.vault.azure.net/keys/{Key}/{KeyIdentifier}";
        static readonly string s_clientId = "{Application_Client_ID}";
        static readonly string s_clientSecret = "{Application_Client_Secret}";
        static readonly string s_connectionString = "Server={Server}; Database={database}; Integrated Security=true; Column Encryption Setting=Enabled;";
        // ******************************************

        public static void Main()
        {
            // Initialize AKV provider
            SqlColumnEncryptionAzureKeyVaultProvider akvProvider = new SqlColumnEncryptionAzureKeyVaultProvider(new LegacyAuthCallbackTokenCredential());

            // Register AKV provider
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders: new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>(capacity: 1, comparer: StringComparer.OrdinalIgnoreCase)
                {
                    { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, akvProvider}
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
                    createCEK(sqlConnection, cmkName, cekName, akvProvider);
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
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(plainTextColumnEncryptionKey);

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

        private class LegacyAuthCallbackTokenCredential : TokenCredential
        {
            string _authority = "";
            string _resource = "";
            string _akvUrl = "";

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
                AcquireTokenAsync().GetAwaiter().GetResult();

            public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
                await AcquireTokenAsync();

            private async Task<AccessToken> AcquireTokenAsync()
            {
                // Added to reduce HttpClient calls.
                // For multi-user support, a better design can be implemented as needed.
                if (_akvUrl != s_akvUrl)
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        HttpResponseMessage response = await httpClient.GetAsync(s_akvUrl);
                        string challenge = response?.Headers.WwwAuthenticate.FirstOrDefault()?.ToString();
                        string trimmedChallenge = ValidateChallenge(challenge);
                        string[] pairs = trimmedChallenge.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                        if (pairs != null && pairs.Length > 0)
                        {
                            for (int i = 0; i < pairs.Length; i++)
                            {
                                string[] pair = pairs[i]?.Split('=');

                                if (pair.Length == 2)
                                {
                                    string key = pair[0]?.Trim().Trim(new char[] { '\"' });
                                    string value = pair[1]?.Trim().Trim(new char[] { '\"' });

                                    if (!string.IsNullOrEmpty(key))
                                    {
                                        if (key.Equals("authorization", StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            _authority = value;
                                        }
                                        else if (key.Equals("resource", StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            _resource = value;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    _akvUrl = s_akvUrl;
                }

                string strAccessToken = await AzureActiveDirectoryAuthenticationCallback(_authority, _resource);
                DateTime expiryTime = InterceptAccessTokenForExpiry(strAccessToken);
                return new AccessToken(strAccessToken, new DateTimeOffset(expiryTime));
            }

            private DateTime InterceptAccessTokenForExpiry(string accessToken)
            {
                if (null == accessToken)
                {
                    throw new ArgumentNullException(accessToken);
                }

                var jwtHandler = new JwtSecurityTokenHandler();
                var jwtOutput = string.Empty;

                // Check Token Format
                if (!jwtHandler.CanReadToken(accessToken))
                    throw new FormatException(accessToken);

                JwtSecurityToken token = jwtHandler.ReadJwtToken(accessToken);

                // Re-serialize the Token Headers to just Key and Values
                var jwtHeader = JsonConvert.SerializeObject(token.Header.Select(h => new { h.Key, h.Value }));
                jwtOutput = $"{{\r\n\"Header\":\r\n{JToken.Parse(jwtHeader)},";

                // Re-serialize the Token Claims to just Type and Values
                var jwtPayload = JsonConvert.SerializeObject(token.Claims.Select(c => new { c.Type, c.Value }));
                jwtOutput += $"\r\n\"Payload\":\r\n{JToken.Parse(jwtPayload)}\r\n}}";

                // Output the whole thing to pretty JSON object formatted.
                string jToken = JToken.Parse(jwtOutput).ToString(Formatting.Indented);
                JToken payload = JObject.Parse(jToken).GetValue("Payload");

                return new DateTime(1970, 1, 1).AddSeconds((long)payload[4]["Value"]);
            }

            private static string ValidateChallenge(string challenge)
            {
                string Bearer = "Bearer ";
                if (string.IsNullOrEmpty(challenge))
                    throw new ArgumentNullException(nameof(challenge));

                string trimmedChallenge = challenge.Trim();

                if (!trimmedChallenge.StartsWith(Bearer))
                    throw new ArgumentException("Challenge is not Bearer", nameof(challenge));

                return trimmedChallenge.Substring(Bearer.Length);
            }

            /// <summary>
            /// Legacy implementation of Authentication Callback, used by Azure Key Vault provider 1.0.
            /// This can be leveraged to support multi-user authentication support in the same Azure Key Vault Provider.
            /// </summary>
            /// <param name="authority">Authorization URL</param>
            /// <param name="resource">Resource</param>
            /// <returns></returns>
            public static async Task<string> AzureActiveDirectoryAuthenticationCallback(string authority, string resource)
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
        }
    }
}
//</Snippet1>
*/
