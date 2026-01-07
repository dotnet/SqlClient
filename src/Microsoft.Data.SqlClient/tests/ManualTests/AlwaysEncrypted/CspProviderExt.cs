// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Xunit;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using Microsoft.Win32;

#if NET
using System.Runtime.Versioning;
#endif

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    /// <summary>
    /// Always Encrypted public CspProvider Manual tests.
    /// TODO: These tests are marked as Windows only for now but should be run for all platforms once the Master Key is accessible to this app from Azure Key Vault.
    /// </summary>
#if !NETFRAMEWORK
    [SupportedOSPlatform("windows")]
#endif
    [PlatformSpecific(TestPlatforms.Windows)]
    public class CspProviderExt
    {
        // [Fact(Skip="Run this in non-parallel mode")] or [ConditionalFact()]
        [Fact(Skip = "Failing in TCE")]
        public void TestRoundTripWithCspAndCertStoreProvider()
        {
            using CspCertificateFixture cspCertificateFixture = new CspCertificateFixture();

            X509Certificate2 cert = cspCertificateFixture.CspCertificate;
            string cspPath = cspCertificateFixture.CspKeyPath;
            string certificatePath = cspCertificateFixture.CspCertificatePath;

            SqlColumnEncryptionCertificateStoreProvider certProvider = new SqlColumnEncryptionCertificateStoreProvider();
            SqlColumnEncryptionCspProvider cspProvider = new SqlColumnEncryptionCspProvider();
            byte[] columnEncryptionKey = DatabaseHelper.GenerateRandomBytes(32);

            byte[] encryptedColumnEncryptionKeyUsingCert = certProvider.EncryptColumnEncryptionKey(certificatePath, @"RSA_OAEP", columnEncryptionKey);
            byte[] columnEncryptionKeyReturnedCert2CSP = cspProvider.DecryptColumnEncryptionKey(cspPath, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingCert);
            Assert.True(columnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedCert2CSP));

            byte[] encryptedColumnEncryptionKeyUsingCSP = cspProvider.EncryptColumnEncryptionKey(cspPath, @"RSA_OAEP", columnEncryptionKey);
            byte[] columnEncryptionKeyReturnedCSP2Cert = certProvider.DecryptColumnEncryptionKey(certificatePath, @"RSA_OAEP", encryptedColumnEncryptionKeyUsingCSP);
            Assert.True(columnEncryptionKey.SequenceEqual(columnEncryptionKeyReturnedCSP2Cert));
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringSetupForAE))]
        [MemberData(nameof(TestEncryptDecryptWithCsp_Data))]
        public void TestEncryptDecryptWithCsp(string connectionString, string providerName, int providerType)
        {
            string keyIdentifier = DataTestUtility.GetLongName("CSP");
            CspParameters namedCspParameters = new CspParameters(providerType, providerName, keyIdentifier);
            using SQLSetupStrategyCspProvider sqlSetupStrategyCsp = new SQLSetupStrategyCspProvider(namedCspParameters);

            using SqlConnection sqlConn = new(connectionString);
            sqlConn.Open();

            // Test INPUT parameter on an encrypted parameter
            string commandText = @$"SELECT CustomerId, FirstName, LastName " +
                                 @$"FROM [{sqlSetupStrategyCsp.ApiTestTable.Name}] " +
                                 @$"WHERE FirstName = @firstName";
            using SqlCommand sqlCommand = new(commandText, sqlConn, null, SqlCommandColumnEncryptionSetting.Enabled);
            
            SqlParameter customerFirstParam = sqlCommand.Parameters.AddWithValue(@"firstName", @"Microsoft");
            customerFirstParam.Direction = System.Data.ParameterDirection.Input;

            using SqlDataReader sqlDataReader = sqlCommand.ExecuteReader();
            ValidateResultSet(sqlDataReader);
        }

        public static IEnumerable<object[]> TestEncryptDecryptWithCsp_Data
        {
            get
            {
                const string providerRegistryKeyPath = @"SOFTWARE\Microsoft\Cryptography\Defaults\Provider";
                using RegistryKey defaultProviderRegistryKey = Registry.LocalMachine.OpenSubKey(providerRegistryKeyPath);
                if (defaultProviderRegistryKey is null)
                {
                    // No test cases can be generated if the registry key doesn't exist.
                    yield break;
                }
                
                foreach (string subKeyName in defaultProviderRegistryKey.GetSubKeyNames())
                {
                    // Skip inappropriate providers
                    if (!subKeyName.Contains(@"RSA and AES"))
                    {
                        continue;
                    }

                    // Open the provider
                    using RegistryKey providerKey = defaultProviderRegistryKey.OpenSubKey(subKeyName);
                    if (providerKey is null)
                    {
                        continue;
                    }
                    
                    // Read provider name
                    string providerName = Path.GetFileName(providerKey.Name);
                    
                    // Read provider type
                    object providerTypeValue = providerKey.GetValue(@"Type");
                    if (providerTypeValue is not int providerType)
                    {
                        continue;
                    }
                    
                    // Combine with AE connection strings
                    foreach (string aeConnectionString in DataTestUtility.AEConnStrings)
                    {
                        yield return new object[] { aeConnectionString, providerName, providerType };
                    }
                }
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
