// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    [Collection("AlwaysEncryptedAKV")]
    public sealed class ColumnDecryptErrorTests : IDisposable
    {
        private SQLSetupStrategyAzureKeyVault fixture;

        private readonly string tableName;

        public ColumnDecryptErrorTests(SQLSetupStrategyAzureKeyVault context)
        {
            fixture = context;
            tableName = fixture.ColumnDecryptErrorTestTable.Name;
        }

        /*
         * This test ensures that column decryption errors and connection pooling play nicely together.
         * When a decryption error is encountered, we expect the connection to be drained of data and
         * properly reset before being returned to the pool. If this doesn't happen, then random bytes
         * may be left in the connection's state. These can interfere with the next operation that utilizes
         * the connection.
         * 
         * We test that state is properly reset by triggering the same error condition twice. Routing column key discovery
         * away from AKV toward a dummy key store achieves this. Each connection pulls from a pool of max 
         * size one to ensure we are using the same internal connection/socket both times. We expect to 
         * receive the "Failed to decrypt column" exception twice. If the state were not cleaned properly,
         * the second error would be different because the TDS stream would be unintelligible.
         * 
         * Finally, we assert that restoring the connection to AKV allows a successful query.
         */
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore), nameof(DataTestUtility.IsAKVSetupAvailable))]
        [ClassData(typeof(TestQueries))]
        public void TestCleanConnectionAfterDecryptFail(string connString, string selectQuery, int totalColumnsInSelect, string[] types)
        {
            // Arrange
            Assert.False(string.IsNullOrWhiteSpace(selectQuery), "FAILED: select query should not be null or empty.");
            Assert.True(totalColumnsInSelect <= 3, "FAILED: totalColumnsInSelect should <= 3.");

            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.Open();

                Table.DeleteData(tableName, sqlConnection);

                Customer customer = new Customer(
                    45,
                    "Microsoft",
                    "Corporation");

                DatabaseHelper.InsertCustomerData(sqlConnection, null, tableName, customer);
            }


            // Act - Trigger a column decrypt error on the connection
            Dictionary<String, SqlColumnEncryptionKeyStoreProvider> keyStoreProviders = new()
            {
                { "AZURE_KEY_VAULT", new DummyKeyStoreProvider() }
            };

            String poolEnabledConnString = new SqlConnectionStringBuilder(connString) { Pooling = true, MaxPoolSize = 1 }.ToString();

            using (SqlConnection sqlConnection = new SqlConnection(poolEnabledConnString))
            {
                sqlConnection.Open();
                sqlConnection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(keyStoreProviders);

                using SqlCommand sqlCommand = new SqlCommand(string.Format(selectQuery, tableName),
                                                            sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled);
                
                using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();

                Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");

                while (sqlDataReader.Read())
                {
                    var error = Assert.Throws<SqlException>(() => DatabaseHelper.CompareResults(sqlDataReader, types, totalColumnsInSelect));
                    Assert.Contains("Failed to decrypt column", error.Message);
                }
            }


            // Assert
            using (SqlConnection sqlConnection = new SqlConnection(poolEnabledConnString))
            {
                sqlConnection.Open();
                sqlConnection.RegisterColumnEncryptionKeyStoreProvidersOnConnection(keyStoreProviders);

                using SqlCommand sqlCommand = new SqlCommand(string.Format(selectQuery, tableName),
                                                            sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled);
                using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();

                Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");

                while (sqlDataReader.Read())
                {
                    var error = Assert.Throws<SqlException>(() => DatabaseHelper.CompareResults(sqlDataReader, types, totalColumnsInSelect));
                    Assert.Contains("Failed to decrypt column", error.Message);
                }
            }

            using (SqlConnection sqlConnection = new SqlConnection(poolEnabledConnString))
            {
                sqlConnection.Open();

                using SqlCommand sqlCommand = new SqlCommand(string.Format(selectQuery, tableName),
                                                            sqlConnection, null, SqlCommandColumnEncryptionSetting.Enabled);
                using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();

                Assert.True(sqlDataReader.HasRows, "FAILED: Select statement did not return any rows.");

                while (sqlDataReader.Read())
                {
                    DatabaseHelper.CompareResults(sqlDataReader, types, totalColumnsInSelect);
                }
            }
        }


        public void Dispose()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStringsSetup)
            {
                using (SqlConnection sqlConnection = new SqlConnection(connStrAE))
                {
                    sqlConnection.Open();
                    Table.DeleteData(fixture.ColumnDecryptErrorTestTable.Name, sqlConnection);
                }
            }
        }

        private sealed class DummyKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                // Must be 32 to match the key length expected for the 'AEAD_AES_256_CBC_HMAC_SHA256' algorithm
                return new byte[32];
            }

            public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
            {
                return new byte[32];
            }
        }
    }

    public class TestQueries : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (string connStrAE in DataTestUtility.AEConnStrings)
            {
                yield return new object[] { connStrAE, @"select CustomerId, FirstName, LastName from  [{0}] ", 3, new string[] { @"int", @"string", @"string" } };
                yield return new object[] { connStrAE, @"select CustomerId, FirstName from  [{0}] ", 2, new string[] { @"int", @"string" } };
                yield return new object[] { connStrAE, @"select LastName from  [{0}] ", 1, new string[] { @"string" } };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

