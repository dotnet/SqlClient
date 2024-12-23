// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Identity;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public class AKVTest : IClassFixture<SQLSetupStrategyAzureKeyVault>
    {
        private readonly SQLSetupStrategyAzureKeyVault _fixture;
        private readonly string _akvTableName;

        public AKVTest(SQLSetupStrategyAzureKeyVault fixture)
        {
            _fixture = fixture;
            _akvTableName = fixture.AKVTestTable.Name;

            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TestEncryptDecryptWithAKV()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionStringHGSVBS)
            {
                ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled,
                AttestationProtocol = SqlConnectionAttestationProtocol.NotSpecified,
                EnclaveAttestationUrl = ""
            };
            using SqlConnection sqlConnection = new(builder.ConnectionString);

            sqlConnection.Open();
            Customer customer = new(45, "Microsoft", "Corporation");

            // Start a transaction and either commit or rollback based on the test variation.
            using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
            {
                DatabaseHelper.InsertCustomerData(sqlConnection, sqlTransaction, _akvTableName, customer);
                sqlTransaction.Commit();
            }

            // Test INPUT parameter on an encrypted parameter
            using SqlCommand sqlCommand = new($"SELECT CustomerId, FirstName, LastName FROM [{_akvTableName}] WHERE FirstName = @firstName",
                                                            sqlConnection);
            SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
            customerFirstParam.Direction = System.Data.ParameterDirection.Input;
            customerFirstParam.ForceColumnEncryption = true;

            using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
            DatabaseHelper.ValidateResultSet(sqlDataReader);
        }

        /*
         This unit test is going to assess an issue where a failed decryption leaves a connection in a bad state  
         when it is returned to the connection pool. If a subsequent connection is retried it will result in an "Internal connection fatal error",
         which causes that connection to be doomed, preventing it from being returned to the pool. 
         Consequently, retrying a third connection will encounter the same decryption error, leading to a repetitive failure cycle.

         The purpose of this unit test is to simulate a decryption error and verify that the connection remains usable when returned to the pool. 
         It aims to confirm that three consecutive connections will consistently fail with the "Failed to decrypt column" error.
        */
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void ForcedColumnDecryptErrorTestShouldFail()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionStringHGSVBS)
            {
                ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled,
                AttestationProtocol = SqlConnectionAttestationProtocol.NotSpecified,
                EnclaveAttestationUrl = ""
            };

            // Setup record to query
            using (SqlConnection sqlConnection = new(builder.ConnectionString))
            {
                sqlConnection.Open();
                Customer customer = new(88, "Microsoft2", "Corporation2");

                using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
                {
                    DatabaseHelper.InsertCustomerData(sqlConnection, sqlTransaction, _akvTableName, customer);
                    sqlTransaction.Commit();
                }
            }

            // Setup Empty key store provider
            Dictionary<String, SqlColumnEncryptionKeyStoreProvider> emptyKeyStoreProviders = new()
            {
                { "AZURE_KEY_VAULT", new EmptyKeyStoreProvider() }
            };

            // Three consecutive connections should fail with "Failed to decrypt column" error. This proves that an error in decryption
            // does not leave the connection in a bad state.
            // In each try, when a "Failed to decrypt error" is thrown, the connection's TDS Parser state object buffer is drained of any
            // pending data so it does not interfere with future operations. In addition, the TDS parser state object's reader.DataReady flag
            // is set to false so that the calling function that catches the exception will not continue to use the reader. Otherwise, it will 
            // timeout waiting to read data that doesn't exist. Also, the TDS Parser state object HasPendingData flag is also set to false
            // to indicate that the buffer has been cleared and to avoid it getting cleared again in SqlDataReader.TryCloseInternal function.
            // Finally, after successfully handling the decryption error, the connection is then returned back to the connection pool without 
            // an error. A proof that the connection's state object is clean is in the second connection being able to throw the same error.
            // The third connection is for making sure we test 3 times as the minimum number of connections to reproduce the issue previously.
            for (int i = 0; i < 3; i++)
            {
                using (SqlConnection sqlConnection = new SqlConnection(builder.ConnectionString))
                {
                    sqlConnection.Open();
                    // Setup connection using the empty key store provider thereby forcing a decryption error.
                    sqlConnection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(emptyKeyStoreProviders);

                    using SqlCommand sqlCommand = new($"SELECT FirstName FROM [{_akvTableName}] WHERE FirstName = @firstName", sqlConnection);
                    SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft2");
                    customerFirstParam.Direction = System.Data.ParameterDirection.Input;
                    customerFirstParam.ForceColumnEncryption = true;

                    using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
                    while (sqlDataReader.Read())
                    {
                        var error = Assert.Throws<SqlException>(() => DatabaseHelper.CompareResults(sqlDataReader, new string[] { @"string" }, 1));
                        Assert.Contains("Failed to decrypt column", error.Message);
                    }
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAKVSetupAvailable))]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestRoundTripWithAKVAndCertStoreProvider()
        {
            SqlColumnEncryptionCertificateStoreProvider certStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] plainTextColumnEncryptionKey = ColumnEncryptionKey.GenerateRandomBytes(ColumnEncryptionKey.KeySizeInBytes);
            byte[] encryptedColumnEncryptionKeyUsingAKV = _fixture.AkvStoreProvider.EncryptColumnEncryptionKey(_fixture.AkvKeyUrl, @"RSA_OAEP", plainTextColumnEncryptionKey);
            byte[] columnEncryptionKeyReturnedAKV2Cert = certStoreProvider.DecryptColumnEncryptionKey(_fixture.ColumnMasterKeyPath, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingAKV);
            Assert.True(plainTextColumnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedAKV2Cert), @"Roundtrip failed");

            // Try the opposite.
            byte[] encryptedColumnEncryptionKeyUsingCert = certStoreProvider.EncryptColumnEncryptionKey(_fixture.ColumnMasterKeyPath, @"RSA_OAEP", plainTextColumnEncryptionKey);
            byte[] columnEncryptionKeyReturnedCert2AKV = _fixture.AkvStoreProvider.DecryptColumnEncryptionKey(_fixture.AkvKeyUrl, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingCert);
            Assert.True(plainTextColumnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedCert2AKV), @"Roundtrip failed");
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE), nameof(DataTestUtility.IsAKVSetupAvailable))]
        public void TestLocalCekCacheIsScopedToProvider()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionStringHGSVBS)
            {
                ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled,
                AttestationProtocol = SqlConnectionAttestationProtocol.NotSpecified,
                EnclaveAttestationUrl = ""
            };

            using SqlConnection sqlConnection = new(builder.ConnectionString);

            sqlConnection.Open();

            // Test INPUT parameter on an encrypted parameter
            using SqlCommand sqlCommand = new($"SELECT CustomerId, FirstName, LastName FROM [{_akvTableName}] WHERE FirstName = @firstName",
                                                            sqlConnection);
            SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
            customerFirstParam.Direction = System.Data.ParameterDirection.Input;
            customerFirstParam.ForceColumnEncryption = true;

            SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
            sqlDataReader.Close();

            SqlColumnEncryptionAzureKeyVaultProvider sqlColumnEncryptionAzureKeyVaultProvider =
                new(new SqlClientCustomTokenCredential());

            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customProvider = new()
                    {
                        { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, sqlColumnEncryptionAzureKeyVaultProvider }
                    };

            // execute a query using provider from command-level cache. this will cache the cek in the local cek cache
            sqlCommand.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customProvider);
            SqlDataReader sqlDataReader2 = sqlCommand.ExecuteReader();
            sqlDataReader2.Close();

            // global cek cache and local cek cache are populated above
            // when using a new per-command provider, it will only use its local cek cache 
            // the following query should fail due to an empty cek cache and invalid credentials
            customProvider[SqlColumnEncryptionAzureKeyVaultProvider.ProviderName] =
                new SqlColumnEncryptionAzureKeyVaultProvider(new ClientSecretCredential("tenant", "client", "secret"));
            sqlCommand.RegisterColumnEncryptionKeyStoreProvidersOnCommand(customProvider);
            Exception ex = Assert.Throws<SqlException>(() => sqlCommand.ExecuteReader());
            Assert.StartsWith("The current credential is not configured to acquire tokens for tenant", ex.InnerException.Message);
        }

        private class EmptyKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                return new byte[32];
            }

            public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            {
                return new byte[32];
            }
        }
    }
}
