// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Always Encrypted public CspProvider Manual tests.
    /// TODO: These tests are marked as Windows only for now but should be run for all platforms once the Master Key is accessible to this app from Azure Key Vault.
    /// </summary>
    [PlatformSpecific(TestPlatforms.Windows)]
    public class CspProviderExt
    {
        private string GenerateUniqueName(string baseName) => string.Concat("AE-", baseName, "-", Guid.NewGuid().ToString());

        // [Fact(Skip="Run this in non-parallel mode")] or [ConditionalFact()]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestKeysFromCertificatesCreatedWithMultipleCryptoProviders()
        {
            const string providersRegistryKeyPath = @"SOFTWARE\Microsoft\Cryptography\Defaults\Provider";
            Microsoft.Win32.RegistryKey defaultCryptoProvidersRegistryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(providersRegistryKeyPath);
        
            foreach (string subKeyName in defaultCryptoProvidersRegistryKey.GetSubKeyNames())
            {
                // NOTE: RSACryptoServiceProvider.SignData() fails for other providers when testing locally
                if (!subKeyName.Contains(@"RSA and AES"))
                {
                    Console.WriteLine(@"INFO: Skipping Certificate creation for {0}.", subKeyName);
                    continue;
                }

                using (Microsoft.Win32.RegistryKey providerKey = defaultCryptoProvidersRegistryKey.OpenSubKey(subKeyName))
                {
                    // Get Provider Name and its type
                    string providerName = providerKey.Name.Substring(providerKey.Name.LastIndexOf(@"\") + 1);
                    string providerType = providerKey.GetValue(@"Type").ToString();

                    // Create a certificate from that provider
                    string certificateName = string.Format(@"AETest - {0}", providerName);

                    CertificateUtilityWin.CreateCertificate(certificateName, StoreLocation.CurrentUser.ToString(), providerName, providerType);

                    if (false == CertificateUtilityWin.CertificateExists(certificateName, StoreLocation.CurrentUser))
                    {
                        Console.WriteLine(@"INFO: Certificate creation for provider {0} failed so skipping it.", providerName);
                        continue;
                    }

                    // Get CSP Path
                    X509Certificate2 cert = CertificateUtilityWin.GetCertificate(certificateName, StoreLocation.CurrentUser);
                    string cspPath = CertificateUtilityWin.GetCspPathFromCertificate(cert);
                    Console.WriteLine("CSP path is {0}", cspPath);

                    SQLSetupStrategyCspExt sqlSetupStrategyCsp = new SQLSetupStrategyCspExt(cspPath);
                    string tableName = sqlSetupStrategyCsp.CspProviderTable.Name;

                    try
                    {
                        using (SqlConnection sqlConn = new SqlConnection(DataTestUtility.TcpConnStr))
                        {
                            sqlConn.Open();

                            // insert 1 row data
                            Customer customer = new Customer(45, "Microsoft", "Corporation");

                            DatabaseHelper.InsertCustomerData(sqlConn, tableName, customer);

                            // Test INPUT parameter on an encrypted parameter
                            using (SqlCommand sqlCommand = new SqlCommand(string.Format(@"SELECT CustomerId, FirstName, LastName FROM [{0}] WHERE FirstName = @firstName", tableName),
                                                                        sqlConn, null, SqlCommandColumnEncryptionSetting.Enabled))
                            {
                                SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                                customerFirstParam.Direction = System.Data.ParameterDirection.Input;

                                using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                                {
                                    ValidateResultSet(sqlDataReader);
                                    Console.WriteLine(@"INFO: Successfully validated using a certificate using provider:{0}", providerName);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(@"INFO: Failed to validate using a certificate using provider:{0}", providerName);
                        Console.WriteLine(@"Exception: {0}", e.Message);
                    }
                    finally
                    {
                        CertificateUtilityWin.RemoveCertificate(certificateName, StoreLocation.CurrentUser);
                        sqlSetupStrategyCsp.DropTable();
                    }
                }
            }
        }

        // [Fact(Skip="Run this in non-parallel mode")] or [ConditionalFact()]
        [Fact(Skip = "Failing in TCE")]
        public void TestRoundTripWithCSPAndCertStoreProvider()
        {
            const string providerName = "Microsoft Enhanced RSA and AES Cryptographic Provider";
            string providerType = "24";

            string certificateName = string.Format(@"AETest - {0}", providerName);
            CertificateUtilityWin.CreateCertificate(certificateName, StoreLocation.CurrentUser.ToString(), providerName, providerType);

            X509Certificate2 cert = CertificateUtilityWin.GetCertificate(certificateName, StoreLocation.CurrentUser);
            string cspPath = CertificateUtilityWin.GetCspPathFromCertificate(cert);
            string certificatePath = String.Concat(@"CurrentUser/my/", cert.Thumbprint);

            SqlColumnEncryptionCertificateStoreProvider certProvider = new SqlColumnEncryptionCertificateStoreProvider();
            SqlColumnEncryptionCspProvider cspProvider = new SqlColumnEncryptionCspProvider();
            byte[] columnEncryptionKey = CertificateUtilityWin.GenerateRandomBytes(32);

            byte[] encryptedColumnEncryptionKeyUsingCert = certProvider.EncryptColumnEncryptionKey(certificatePath, @"RSA_OAEP", columnEncryptionKey);
            byte[] columnEncryptionKeyReturnedCert2CSP = cspProvider.DecryptColumnEncryptionKey(cspPath, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingCert);
            Assert.True(columnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedCert2CSP));

            byte[] encryptedColumnEncryptionKeyUsingCSP = cspProvider.EncryptColumnEncryptionKey(cspPath, @"RSA_OAEP", columnEncryptionKey);
            byte[] columnEncryptionKeyReturnedCSP2Cert = certProvider.DecryptColumnEncryptionKey(certificatePath, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingCSP);
            Assert.True(columnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedCSP2Cert));

            CertificateUtilityWin.RemoveCertificate(certificateName, StoreLocation.CurrentUser);
        }
        
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void TestEncryptDecryptWithCSP()
        {
            string providerName = @"Microsoft Enhanced RSA and AES Cryptographic Provider";
            string keyIdentifier = "BasicCMK";

            try
            {
                CertificateUtilityWin.RSAPersistKeyInCsp(providerName, keyIdentifier);
                string cspPath = String.Concat(providerName, @"/", keyIdentifier);

                SQLSetupStrategyCspExt sqlSetupStrategyCsp = new SQLSetupStrategyCspExt(cspPath);
                string tableName = sqlSetupStrategyCsp.CspProviderTable.Name;

                try
                {
                    using (SqlConnection sqlConn = new SqlConnection(DataTestUtility.TcpConnStr))
                    {
                        sqlConn.Open();

                        // insert 1 row data
                        Customer customer = new Customer(45, "Microsoft", "Corporation");

                        DatabaseHelper.InsertCustomerData(sqlConn, tableName, customer);

                        // Test INPUT parameter on an encrypted parameter
                        using (SqlCommand sqlCommand = new SqlCommand(string.Format(@"SELECT CustomerId, FirstName, LastName FROM [{0}] WHERE FirstName = @firstName", tableName),
                                                                    sqlConn, null, SqlCommandColumnEncryptionSetting.Enabled))
                        {
                            SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
                            customerFirstParam.Direction = System.Data.ParameterDirection.Input;

                            using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader())
                            {
                                ValidateResultSet(sqlDataReader);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(@"Exception: {0}", e.Message);
                }
                finally
                {
                    sqlSetupStrategyCsp.DropTable();
                }
            }
            finally
            {
                CertificateUtilityWin.RSADeleteKeyInCsp(providerName, keyIdentifier);
            }
        }

        /// <summary>
        /// Validates that the results are the ones expected.
        /// </summary>
        /// <param name="sqlDataReader"></param>
        private void ValidateResultSet(SqlDataReader sqlDataReader)
        {
            int rowsFound = 0;
            while (sqlDataReader.Read())
            {
                if (sqlDataReader.FieldCount == 3)
                {
                    Assert.Equal(45, sqlDataReader.GetInt32(0));
                    Assert.Equal(@"Microsoft", sqlDataReader.GetString(1));
                    Assert.Equal(@"Corporation", sqlDataReader.GetString(2));
                }
                rowsFound++;
            }

            Assert.Equal(1, rowsFound);
        }
    }
}