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
            using SqlConnection sqlConnection = new (builder.ConnectionString);

            sqlConnection.Open();
            Customer customer = new(45, "Microsoft", "Corporation");

            // Start a transaction and either commit or rollback based on the test variation.
            using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction())
            {
                DatabaseHelper.InsertCustomerData(sqlConnection, sqlTransaction, _akvTableName, customer);
                sqlTransaction.Commit();
            }

            // Test INPUT parameter on an encrypted parameter
            using SqlCommand sqlCommand = new ($"SELECT CustomerId, FirstName, LastName FROM [{_akvTableName}] WHERE FirstName = @firstName",
                                                            sqlConnection);
            SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
            customerFirstParam.Direction = System.Data.ParameterDirection.Input;
            customerFirstParam.ForceColumnEncryption = true;

            using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
            DatabaseHelper.ValidateResultSet(sqlDataReader);
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
    }
}
