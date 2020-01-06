// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Xunit;
using Xunit.Sdk;
using static Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests.TestFixtures;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class SqlColumnEncryptionCertificateStoreProviderWindowsShould : IClassFixture<CertificateFixture>
    {
        private const string MASTER_KEY_PATH = "CurrentUser/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946";
        private const string ENCRYPTION_ALGORITHM = "RSA_OAEP";

        /// <summary>
        /// Current User path prefix.
        /// </summary>
        private const string CurrentUserPathPrefix = @"CurrentUser/";

        /// <summary>
        /// Local Machine path prefix.
        /// </summary>
        private const string LocalMachinePathPrefix = @"LocalMachine/";

        /// <summary>
        /// My Path Prefix.
        /// </summary>
        private const string MyPathPrefix = @"my/";

        /// <summary>
        /// LocalMachine/my/ path prefix.
        /// </summary>
        private const string LocalMachineMyPathPrefix = LocalMachinePathPrefix + MyPathPrefix;

        /// <summary>
        /// CurrentUser/my/ path prefix.
        /// </summary>
        private const string CurrentUserMyPathPrefix = CurrentUserPathPrefix + MyPathPrefix;

        private const int RootKeyLengthInBytes = 32;

        /// <summary>
        /// Const to refer to a string.
        /// </summary>
        private const string StringStr = "string";

        /// <summary>
        /// Const to refer to an int.
        /// </summary>
        private const string IntStr = "int";

        /// <summary>
        /// The index inside the encrypted cell value where the version byte resides.
        /// </summary>
        private const int VersionStartIndex = 0;

        /// <summary>
        /// The length of version byte inside the encrypted cell value.
        /// </summary>
        private const int VersionByteLengthInBytes = 1;

        /// <summary>
        /// The index inside the encrypted cell value where the Authentication Tag starts.
        /// </summary>
        private const int AuthenticationTagStartIndex = VersionStartIndex + VersionByteLengthInBytes;

        /// <summary>
        /// The length of Authentication Tag inside the encrypted cell value.
        /// </summary>
        private const int AuthenticationTagLengthInBytes = 32;

        /// <summary>
        /// The index inside the encrypted cell value where the IV starts.
        /// </summary>
        private const int IVStartIndex = AuthenticationTagStartIndex + AuthenticationTagLengthInBytes;

        /// <summary>
        /// The length of IV inside the encrypted cell value.
        /// </summary>
        private const int IVLengthInBytes = 16;

        /// <summary>
        /// The index inside the encrypted cell value where the ciphertext starts.
        /// </summary>
        private const int CipherTextStartIndex = IVStartIndex + IVLengthInBytes;

        /// <summary>
        /// SetCustomColumnEncryptionKeyStoreProvider can be called only once in a process. To workaround that, we use this flag.
        /// </summary>
        private static bool s_testCustomEncryptioKeyStoreProviderExecutedOnce = false;

        [Theory]
        [InvalidDecryptionParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowExceptionWithInvalidParameterWhileDecryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.DecryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, bytes));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Theory]
        [InvalidEncryptionParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowExceptionWithInvalidParameterWhileEncryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.EncryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, bytes));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Theory]
        [InvalidSigningParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowExceptionWithInvalidParameterWhileSigningColumnMasterKeyMetadata(string errorMsg, Type exceptionType, string masterKeyPath)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.SignColumnMasterKeyMetadata(masterKeyPath, true));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Theory]
        [InlineData("CurrentUser/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946")]
        [InlineData("CURRENTUSER/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946")]
        [InlineData("currentuser/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946")]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SetStoreLocationApproperiatelyFromMasterKeyPathRegardlessOfCase(string masterKeyPath)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(masterKeyPath, ENCRYPTION_ALGORITHM, new byte[] { 1, 2, 3, 4, 5 });
            Assert.NotNull(ciphertext);
        }

        [Theory]
        [InlineData("CurrentUser/my/C74D53B816A971E3FF9714FE1DD2E57E1710D946")]
        [InlineData("CurrentUser/MY/C74D53B816A971E3FF9714FE1DD2E57E1710D946")]
        [InlineData("CurrentUser/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946")]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SetStoreNameApproperiatelyFromMasterKeyPathRegardlessOfCase(string masterKeyPath)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(masterKeyPath, ENCRYPTION_ALGORITHM, new byte[] { 1, 2, 3, 4, 5 });
            Assert.NotNull(ciphertext);
        }

        [Theory]
        [InlineData("RSA_OAEP")]
        [InlineData("rsa_oaep")]
        [InlineData("RsA_oAeP")]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void AcceptEncryptionAlgorithmRegardlessOfCase(string algorithm)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(MASTER_KEY_PATH, algorithm, new byte[] { 1, 2, 3, 4, 5 });
            Assert.NotNull(ciphertext);
        }

        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        [InlineData(128)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void EncryptKeyAndThenDecryptItSuccessfully(int dataSize)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            var columnEncryptionKey = new byte[dataSize];
            var randomNumberGenerator = new Random();
            randomNumberGenerator.NextBytes(columnEncryptionKey);

            byte[] encryptedData = provider.EncryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, columnEncryptionKey);
            byte[] decryptedData = provider.DecryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, encryptedData);
            Assert.Equal(columnEncryptionKey, decryptedData);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void SignAndVerifyColumnMasterKeyMetadataSuccessfully(bool allowEnclaveComputations)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] signature = provider.SignColumnMasterKeyMetadata(MASTER_KEY_PATH, allowEnclaveComputations);
            Assert.NotNull(signature);
            Assert.True(provider.VerifyColumnMasterKeyMetadata(MASTER_KEY_PATH, allowEnclaveComputations, signature));
            Assert.False(provider.VerifyColumnMasterKeyMetadata(MASTER_KEY_PATH, !allowEnclaveComputations, signature));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void FailToVerifyColumnMasterKeyMetadataWithWrongCertificate(bool allowEnclaveComputations)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();

            byte[] signature = provider.SignColumnMasterKeyMetadata(MASTER_KEY_PATH, allowEnclaveComputations);
            Assert.NotNull(signature);
            Assert.False(
                provider.VerifyColumnMasterKeyMetadata("CurrentUser/My/4281446463C6F7F5B8EDFFA4BD6E345E46857CAD", allowEnclaveComputations, signature));
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void EncryptAndDecryptDataSuccessfully()
        {
            var input = new byte[] { 1, 2, 3, 4, 5 };
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM,
                new byte[] { 1, 2, 3, 4, 5 });
            byte[] output =
                provider.DecryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, ciphertext);
            Assert.Equal(input, output);
        }

        [Theory]
        [CEKEncryptionReversalParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestCEKEncryptionReversal(StoreLocation certificateStoreLocation, String certificateStoreNameAndLocation)
        {
            Assert.True(!string.IsNullOrWhiteSpace(certificateStoreNameAndLocation));
            string certificateName = @"TestCertificate12";

            // Fetch the newly created cert.
            X509Certificate2 masterKeyCertificate = Utility.GetCertificate(certificateName,
                                                                            certificateStoreLocation);

            Assert.True(masterKeyCertificate != null);

            string masterKeyThumbprint = masterKeyCertificate.Thumbprint;
            Assert.True(!string.IsNullOrWhiteSpace(masterKeyThumbprint));

            string masterKeyPath = String.Concat(certificateStoreNameAndLocation, masterKeyThumbprint);

            // Generate a root key.
            byte[] rootKey = Utility.GenerateRandomBytes(RootKeyLengthInBytes);
            Assert.True(rootKey != null);

            SqlColumnEncryptionCertificateStoreProvider sqlColumnCertStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();

            // Encrypt the CEK.
            byte[] cipherText1 = sqlColumnCertStoreProvider.EncryptColumnEncryptionKey(masterKeyPath, ENCRYPTION_ALGORITHM, rootKey);
            Assert.True(cipherText1 != null);

            // Convoluted derivation of the encrypted CEK.
            byte[] cipherText2 = Utility.StringToByteArray(Utility.GetHexString(cipherText1));
            Assert.True(cipherText2 != null);

            Assert.True(cipherText2.SequenceEqual(cipherText1), "cipherText1 and its convoluted hex string reversal don't match.");

            // Decrypt Column Encryption Key using cipherText1
            byte[] rootKeyReversal1 = sqlColumnCertStoreProvider.DecryptColumnEncryptionKey(masterKeyPath, ENCRYPTION_ALGORITHM, cipherText1);
            Assert.True(rootKeyReversal1 != null);

            // Decrypt Column Encryption Key using cipherText2
            byte[] rootKeyReversal2 = sqlColumnCertStoreProvider.DecryptColumnEncryptionKey(masterKeyPath, ENCRYPTION_ALGORITHM, cipherText2);
            Assert.True(rootKeyReversal2 != null);

            Assert.True(rootKeyReversal1.SequenceEqual(rootKeyReversal2), "rootKeyReversal1 and rootKeyReversal2 don't match.");
            Assert.True(rootKeyReversal1.SequenceEqual(rootKey), "rootKeyReversal1 and rootKey don't match.");
        }

        /// <summary>
        /// Helper function to test reversal of encryption using Aead.
        /// </summary>
        /// <param name="plainTextInBytes"></param>
        /// <param name="rootKey"></param>
        /// <param name="encryptionType"></param>
        private void TestEncryptionReversalUsingAead(byte[] plainTextInBytes, byte[] rootKey, Utility.CColumnEncryptionType encryptionType)
        {
            // Encrypt.
            byte[] encryptedCellBlob = Utility.EncryptDataUsingAED(plainTextInBytes, rootKey, encryptionType);
            Assert.True(encryptedCellBlob != null && encryptedCellBlob.Length > 0);

            // Decrypt.
            byte[] decryptedCellData = Utility.DecryptDataUsingAED(encryptedCellBlob, rootKey, encryptionType);
            Assert.True(decryptedCellData != null);

            // Compare decrypted value against plain text.
            Assert.True(decryptedCellData.SequenceEqual(plainTextInBytes), "Reversal was not successful.");
        }

        [Theory]
        [PlatformSpecific(TestPlatforms.Windows)]
        [AeadEncryptionParameters]
        public void TestAeadEncryptionReversal(string dataType, object data, Utility.CColumnEncryptionType encType)
        {
            Assert.True(!string.IsNullOrWhiteSpace(dataType));

            byte[] plainText = null;

            // Convert the data to bytes.
            switch (dataType)
            {
                case StringStr:
                    plainText = Encoding.UTF8.GetBytes((string)data);
                    break;

                case IntStr:
                    plainText = BitConverter.GetBytes((int)data);
                    break;

                default:
                    Assert.True(false, "unexpected data type.");
                    break;
            }

            Assert.True(plainText != null);

            // Generate a rootkey.
            byte[] rootKey = Utility.GenerateRandomBytes(RootKeyLengthInBytes);
            Assert.True(rootKey != null && rootKey.Length > 0);

            // Test EncryptDecrypt using Aead.
            TestEncryptionReversalUsingAead(plainText, rootKey, encType);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestCustomKeyProviderListSetter()
        {
            // SqlConnection.RegisterColumnEncryptionKeyStoreProviders can be called only once in a process.
            // This is a workaround to ensure re-runnability of the test.
            if (s_testCustomEncryptioKeyStoreProviderExecutedOnce)
            {
                return;
            }

            string expectedMessage1 = "Column encryption key store provider dictionary cannot be null. Expecting a non-null value.";
            // Verify that we are able to set it to null.
            ArgumentException e1 = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(null));
            Assert.Contains(expectedMessage1, e1.Message);

            // A dictionary holding custom providers.
            IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders = new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>();
            customProviders.Add(new KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider>(@"DummyProvider", new DummyKeyStoreProvider()));

            // Verify that setting a provider in the list with null value throws an exception.
            customProviders.Add(new KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider>(@"CustomProvider", null));
            string expectedMessage2 = "Null reference specified for key store provider 'CustomProvider'. Expecting a non-null value.";
            ArgumentNullException e2 = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage2, e2.Message);
            customProviders.Remove(@"CustomProvider");

            // Verify that setting a provider in the list with an empty provider name throws an exception.
            customProviders.Add(new KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider>(@"", new DummyKeyStoreProvider()));
            string expectedMessage3 = "Invalid key store provider name specified. Key store provider names cannot be null or empty";
            ArgumentNullException e3 = Assert.Throws<ArgumentNullException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage3, e3.Message);

            customProviders.Remove(@"");

            // Verify that setting a provider in the list with name that starts with 'MSSQL_' throws an exception.
            customProviders.Add(new KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider>(@"MSSQL_MyStore", new SqlColumnEncryptionCertificateStoreProvider()));
            string expectedMessage4 = "Invalid key store provider name 'MSSQL_MyStore'. 'MSSQL_' prefix is reserved for system key store providers.";
            ArgumentException e4 = Assert.Throws<ArgumentException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage4, e4.Message);

            customProviders.Remove(@"MSSQL_MyStore");

            // Verify that setting a provider in the list with name that starts with 'MSSQL_' but different case throws an exception.
            customProviders.Add(new KeyValuePair<string, SqlColumnEncryptionKeyStoreProvider>(@"MsSqL_MyStore", new SqlColumnEncryptionCertificateStoreProvider()));
            string expectedMessage5 = "Invalid key store provider name 'MsSqL_MyStore'. 'MSSQL_' prefix is reserved for system key store providers.";
            ArgumentException e5 = Assert.Throws<ArgumentException>(() => SqlConnection.RegisterColumnEncryptionKeyStoreProviders(customProviders));
            Assert.Contains(expectedMessage5, e5.Message);

            customProviders.Remove(@"MsSqL_MyStore");

            // Clear any providers set by other tests.
            Utility.ClearSqlConnectionProviders();
        }

        [Theory]
        [PlatformSpecific(TestPlatforms.Windows)]
        [ValidCertificatePathsParameters]
        public void TestValidCertificatePaths(string certificateStoreNameAndLocation, object location)
        {
            StoreLocation certificateStoreLocation;

            // Certificate Store Location and Name.
            Assert.True(certificateStoreNameAndLocation != null);

            if (null != location)
            {
                // Certificate Store Location.
                certificateStoreLocation = (StoreLocation)location;
            }
            else
            {
                certificateStoreLocation = StoreLocation.CurrentUser;
            }

            string certificateName = @"TestCertificate12";

            // Fetch the newly created cert.
            X509Certificate2 masterKeyCertificate = Utility.GetCertificate(certificateName,
                                                                            certificateStoreLocation);

            Assert.True(masterKeyCertificate != null);

            string masterKeyThumbprint = masterKeyCertificate.Thumbprint;
            Assert.True(!string.IsNullOrWhiteSpace(masterKeyThumbprint));

            string masterKeyPath = String.Concat(certificateStoreNameAndLocation, masterKeyThumbprint);

            // Generate a root key.
            byte[] rootKey = Utility.GenerateRandomBytes(RootKeyLengthInBytes);
            Assert.True(rootKey != null);

            SqlColumnEncryptionCertificateStoreProvider sqlColumnCertStoreProvider = new SqlColumnEncryptionCertificateStoreProvider();

            // Encrypt the CEK.
            byte[] cipherText1 = sqlColumnCertStoreProvider.EncryptColumnEncryptionKey(masterKeyPath, ENCRYPTION_ALGORITHM, rootKey);
            Assert.True(cipherText1 != null);
        }

        [Theory]
        [PlatformSpecific(TestPlatforms.Windows)]
        [InlineData(new object[3] { @"iv", Utility.CColumnEncryptionType.Randomized, @"Specified ciphertext has an invalid authentication tag.\s+\(?Parameter (name: )?'?cipherText('\))?" })]
        [InlineData(new object[3] { @"tag", Utility.CColumnEncryptionType.Randomized, @"Specified ciphertext has an invalid authentication tag.\s+\(?Parameter (name: )?'?cipherText('\))?" })]
        [InlineData(new object[3] { @"cipher", Utility.CColumnEncryptionType.Randomized, @"Specified ciphertext has an invalid authentication tag.\s+\(?Parameter (name: )?'?cipherText('\))?" })]
        [InlineData(new object[3] { @"version", Utility.CColumnEncryptionType.Randomized, @"The specified ciphertext's encryption algorithm version '02' does not match the expected encryption algorithm version '01'.\s+\(?Parameter (name: )?'?cipherText('\))?" })]
        public void TestEncryptedCellValueTampering(string parameterToTamper, Utility.CColumnEncryptionType encryptionType, string expectedErrorMessage)
        {
            Assert.True(!string.IsNullOrWhiteSpace(parameterToTamper));
            Assert.True(!string.IsNullOrWhiteSpace(expectedErrorMessage));

            byte[] rootKey = Utility.GenerateRandomBytes(RootKeyLengthInBytes);
            Assert.True(rootKey != null);

            byte[] encryptedCellValue = Utility.EncryptDataUsingAED(plainTextData: new byte[3] { 0x01, 0x02, 0x03 }, key: rootKey, encryptionType: encryptionType);
            Assert.True(encryptedCellValue != null && encryptedCellValue.Length > 0);

            switch (parameterToTamper)
            {
                case "iv":
                    encryptedCellValue[IVStartIndex] += 0x01;
                    break;

                case @"tag":
                    encryptedCellValue[AuthenticationTagStartIndex] += 0x01;
                    break;

                case "cipher":
                    encryptedCellValue[CipherTextStartIndex] += 0x01;
                    break;

                case "version":
                    Assert.True(encryptedCellValue[VersionStartIndex] == 0x01);
                    encryptedCellValue[VersionStartIndex] += 0x01;
                    break;

                default:
                    Assert.True(false);
                    break;
            }

            // try decrypting the tampered cell value.
            TargetInvocationException e = Assert.Throws<TargetInvocationException>(() => Utility.DecryptDataUsingAED(encryptedCellValue, rootKey, encryptionType));
            Assert.Matches(expectedErrorMessage, e.InnerException.Message);
        }

        public class AeadEncryptionParameters : DataAttribute
        {
            /// <summary>
            /// Const to refer to a string.
            /// </summary>
            private const string StringStr = "string";

            /// <summary>
            /// Const to refer to an int.
            /// </summary>
            private const string IntStr = "int";

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[3] { StringStr, @"", Utility.CColumnEncryptionType.Deterministic };
                yield return new object[3] { StringStr, @"Transparent Column Encryption", Utility.CColumnEncryptionType.Deterministic };
                yield return new object[3] { StringStr, @"ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1\""\", Utility.CColumnEncryptionType.Deterministic };
                yield return new object[3] { IntStr, 0, Utility.CColumnEncryptionType.Deterministic };
                yield return new object[3] { IntStr, 1234, Utility.CColumnEncryptionType.Deterministic };
                yield return new object[3] { IntStr, int.MaxValue, Utility.CColumnEncryptionType.Deterministic };
                yield return new object[3] { IntStr, int.MinValue, Utility.CColumnEncryptionType.Deterministic };
                yield return new object[3] { StringStr, @"", Utility.CColumnEncryptionType.Randomized };
                yield return new object[3] { StringStr, @"Transparent Column Encryption", Utility.CColumnEncryptionType.Randomized };
                yield return new object[3] { StringStr, @"ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1ABafd908`12`l2-0lkd as-3912-312-0 skfsla; i-09=-0 41]321`aksdlkf0ws--02iasfidl;sakfd 90-920-391`-0391- AW_!@*)_A:SKD:""sdfasdf90`-11`-=19=`1\""\", Utility.CColumnEncryptionType.Randomized };
                yield return new object[3] { IntStr, 0, Utility.CColumnEncryptionType.Randomized };
                yield return new object[3] { IntStr, 1234, Utility.CColumnEncryptionType.Randomized };
                yield return new object[3] { IntStr, int.MaxValue, Utility.CColumnEncryptionType.Randomized };
                yield return new object[3] { IntStr, int.MinValue, Utility.CColumnEncryptionType.Randomized };
            }
        }

        public class CEKEncryptionReversalParameters : DataAttribute
        {
            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[2] { StoreLocation.CurrentUser , CurrentUserMyPathPrefix };
                // use localmachine cert path only when current user is Admin.
                if (CertificateFixture.IsAdmin)
                {
                    yield return new object[2] { StoreLocation.LocalMachine, LocalMachineMyPathPrefix };
                }
            }
        }

        public class ValidCertificatePathsParameters : DataAttribute
        {
            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new object[2] { CurrentUserMyPathPrefix, StoreLocation.CurrentUser };
                yield return new object[2] { MyPathPrefix, null };
                yield return new object[2] { @"", null };
                // use localmachine cert path only when current user is Admin.
                if (CertificateFixture.IsAdmin)
                {
                    yield return new object[2] { LocalMachineMyPathPrefix, StoreLocation.LocalMachine };
                }
            }
        }

        public class InvalidDecryptionParameters : DataAttribute
        {
            private const string TCE_NullCertificatePath = @"Internal error. Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath = @"Internal error. Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Internal error. Encrypted column encryption key cannot be null.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Internal error. Empty encrypted column encryption key specified.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Internal error. Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Internal error. Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_LargeCertificatePathLength = @"Internal error. Specified certificate path has 32768 bytes, which exceeds maximum length of 32767 bytes.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificatePath = @"Internal error. Invalid certificate path: 'CurrentUser/My/Thumbprint/extra'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateLocation = @"Internal error. Invalid certificate location 'Invalid' in certificate path 'Invalid/My/Thumbprint'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateStore = @"Internal error. Invalid certificate store 'Invalid' specified in certificate path 'CurrentUser/Invalid/Thumbprint'. Expected value: 'My'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateSignature = @"Internal error. Empty certificate thumbprint specified in certificate path 'CurrentUser/My/'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidAlgorithmVersion = @"Specified encrypted column encryption key contains an invalid encryption algorithm version '02'. Expected version is '01'.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidCiphertextLengthInEncryptedCEK = @"The specified encrypted column encryption key's ciphertext length: 128 does not match the ciphertext length: 256 when using column master key \(certificate\) in 'CurrentUser/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946'. The encrypted column encryption key may be corrupt, or the specified certificate path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignatureInEncryptedCEK = @"The specified encrypted column encryption key's signature length: 128 does not match the signature length: 256 when using column master key \(certificate\) in 'CurrentUser/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946'. The encrypted column encryption key may be corrupt, or the specified certificate path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignature = @"The specified encrypted column encryption key signature does not match the signature computed with the column master key \(certificate\) in 'CurrentUser/My/C74D53B816A971E3FF9714FE1DD2E57E1710D946'. The encrypted column encryption key may be corrupt, or the specified path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";


            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCertificatePath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCertificatePath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), MASTER_KEY_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), MASTER_KEY_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_LargeCertificatePathLength, typeof(ArgumentException), GenerateString(Int16.MaxValue + 1), ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificatePath, typeof(ArgumentException), "CurrentUser/My/Thumbprint/extra", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateLocation, typeof(ArgumentException), "Invalid/My/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateStore, typeof(ArgumentException), "CurrentUser/Invalid/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateSignature, typeof(ArgumentException), "CurrentUser/My/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidAlgorithmVersion, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(2, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCiphertextLengthInEncryptedCEK, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 128, 256) };
                yield return new Object[] { TCE_InvalidSignatureInEncryptedCEK, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 128) };
                yield return new Object[] { TCE_InvalidSignature, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }

        public class InvalidEncryptionParameters : DataAttribute
        {
            private const string TCE_NullCertificatePath = @"Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath = @"Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Column encryption key cannot be null.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Empty column encryption key specified.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_LargeCertificatePathLength = @"Specified certificate path has 32768 bytes, which exceeds maximum length of 32767 bytes.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificatePath = @"Invalid certificate path: 'CurrentUser/My/Thumbprint/extra'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateLocation = @"Invalid certificate location 'Invalid' in certificate path 'Invalid/My/Thumbprint'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateStore = @"Invalid certificate store 'Invalid' specified in certificate path 'CurrentUser/Invalid/Thumbprint'. Expected value: 'My'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateSignature = @"Empty certificate thumbprint specified in certificate path 'CurrentUser/My/'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCertificatePath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCertificatePath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), MASTER_KEY_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), MASTER_KEY_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_LargeCertificatePathLength, typeof(ArgumentException), GenerateString(Int16.MaxValue + 1), ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificatePath, typeof(ArgumentException), "CurrentUser/My/Thumbprint/extra", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateLocation, typeof(ArgumentException), "Invalid/My/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateStore, typeof(ArgumentException), "CurrentUser/Invalid/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateSignature, typeof(ArgumentException), "CurrentUser/My/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }

        public class InvalidSigningParameters : DataAttribute
        {
            private const string TCE_NullCertificatePath = @"Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath = @"Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_LargeCertificatePathLength = @"Specified certificate path has 32768 bytes, which exceeds maximum length of 32767 bytes.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCertificatePath, typeof(ArgumentNullException), null };
                yield return new Object[] { TCE_EmptyCertificatePath, typeof(ArgumentException), "" };
                yield return new Object[] { TCE_LargeCertificatePathLength, typeof(ArgumentException), GenerateString(Int16.MaxValue + 1) };
            }
        }

        public static string GenerateString(int length)
        {
            StringBuilder s = new StringBuilder();
            Random random = new Random();
            char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
            for (int i = 0; i < length; i++)
            {
                s.Append(chars[random.Next(chars.Length)]);
            }

            return s.ToString();
        }
    }

    public class SqlColumnEncryptionCertificateStoreProviderUnixShould
    {
        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public void ThrowPlatformNotSupportedExceptionInUnix()
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            Assert.Throws<PlatformNotSupportedException>(() => provider.EncryptColumnEncryptionKey("", "", new byte[] { }));
            Assert.Throws<PlatformNotSupportedException>(() => provider.DecryptColumnEncryptionKey("", "", new byte[] { }));
            Assert.Throws<PlatformNotSupportedException>(() => provider.SignColumnMasterKeyMetadata("", false));
            Assert.Throws<PlatformNotSupportedException>(() => provider.VerifyColumnMasterKeyMetadata("", false, new byte[] { }));
        }
    }

    public class CertificateFixture : IDisposable
    {
        public static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        public string certificateThumbPrint1 = "C74D53B816A971E3FF9714FE1DD2E57E1710D946";
        public static byte[] certificateRawBytes1 = new byte[] { 48, 130, 10, 36, 2, 1, 3, 48, 130, 9, 224, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 9, 209, 4, 130, 9, 205, 48, 130, 9, 201, 48, 130, 5, 250, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 5, 235, 4, 130, 5, 231, 48, 130, 5, 227, 48, 130, 5, 223, 6, 11, 42, 134, 72, 134, 247, 13, 1, 12, 10, 1, 2, 160, 130, 4, 254, 48, 130, 4, 250, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 146, 126, 191, 6, 130, 18, 111, 71, 2, 2, 7, 208, 4, 130, 4, 216, 55, 138, 10, 135, 82, 84, 240, 82, 107, 75, 21, 156, 54, 53, 188, 62, 36, 248, 59, 17, 18, 41, 206, 171, 226, 168, 175, 59, 48, 50, 36, 26, 58, 39, 118, 231, 200, 107, 86, 144, 200, 20, 135, 22, 105, 159, 229, 116, 123, 122, 194, 69, 172, 171, 128, 251, 129, 222, 113, 27, 253, 48, 164, 116, 72, 194, 123, 12, 247, 186, 162, 40, 39, 114, 22, 118, 91, 192, 73, 122, 235, 247, 40, 89, 3, 222, 64, 214, 184, 67, 204, 188, 197, 188, 107, 126, 225, 194, 161, 110, 156, 45, 70, 26, 86, 69, 63, 120, 153, 164, 136, 15, 220, 153, 104, 50, 121, 87, 10, 180, 149, 98, 220, 73, 175, 50, 146, 231, 112, 230, 204, 132, 76, 43, 142, 7, 104, 142, 146, 92, 21, 52, 38, 59, 154, 108, 159, 192, 93, 174, 39, 134, 96, 189, 150, 77, 90, 160, 43, 127, 173, 199, 189, 4, 69, 44, 104, 148, 225, 44, 149, 167, 149, 121, 220, 232, 98, 131, 212, 130, 35, 79, 10, 173, 177, 150, 161, 91, 26, 12, 221, 136, 230, 124, 73, 96, 126, 12, 241, 99, 60, 140, 126, 140, 0, 166, 47, 16, 87, 102, 138, 45, 97, 21, 31, 224, 126, 231, 102, 99, 35, 207, 75, 22, 249, 115, 51, 106, 79, 208, 21, 108, 124, 143, 108, 130, 6, 61, 215, 227, 7, 224, 174, 193, 97, 211, 241, 224, 90, 37, 101, 147, 149, 173, 239, 113, 214, 1, 41, 69, 158, 203, 3, 63, 101, 196, 134, 7, 127, 58, 113, 243, 228, 162, 99, 75, 207, 153, 19, 193, 187, 52, 124, 85, 234, 7, 249, 75, 65, 230, 107, 247, 145, 64, 94, 106, 50, 117, 83, 138, 49, 10, 22, 211, 115, 183, 20, 119, 18, 117, 166, 153, 30, 210, 248, 118, 200, 21, 180, 118, 208, 53, 90, 243, 74, 76, 109, 106, 46, 103, 112, 197, 89, 92, 178, 83, 48, 97, 162, 73, 78, 105, 145, 213, 230, 17, 211, 121, 200, 101, 179, 158, 85, 99, 211, 68, 122, 234, 176, 4, 33, 225, 120, 139, 163, 110, 35, 199, 23, 45, 126, 199, 80, 145, 14, 74, 217, 200, 172, 216, 159, 237, 241, 157, 85, 210, 141, 180, 150, 187, 82, 48, 245, 154, 125, 60, 223, 244, 21, 20, 39, 88, 8, 153, 185, 227, 76, 78, 137, 99, 98, 81, 141, 27, 197, 41, 39, 251, 80, 27, 85, 78, 65, 15, 216, 106, 106, 113, 33, 253, 210, 46, 214, 47, 49, 89, 170, 215, 207, 62, 182, 88, 25, 186, 166, 214, 172, 63, 94, 17, 123, 235, 226, 72, 73, 204, 18, 173, 134, 92, 66, 2, 213, 151, 251, 95, 175, 38, 56, 156, 138, 96, 123, 190, 107, 59, 230, 24, 210, 224, 206, 169, 159, 95, 180, 237, 34, 194, 62, 4, 213, 228, 85, 216, 138, 157, 50, 20, 101, 160, 195, 138, 207, 18, 17, 232, 6, 73, 82, 247, 173, 50, 180, 53, 58, 156, 97, 230, 112, 211, 251, 204, 120, 188, 34, 41, 67, 83, 197, 131, 251, 176, 20, 70, 169, 116, 237, 43, 117, 45, 31, 66, 74, 152, 216, 3, 108, 102, 99, 5, 127, 76, 129, 57, 180, 90, 218, 157, 108, 85, 4, 240, 101, 149, 154, 221, 208, 70, 152, 34, 128, 57, 135, 38, 17, 139, 142, 167, 109, 73, 129, 181, 105, 45, 151, 106, 171, 166, 0, 113, 147, 141, 19, 228, 196, 88, 175, 219, 18, 213, 54, 105, 179, 8, 249, 250, 164, 86, 28, 185, 19, 60, 50, 140, 73, 237, 148, 201, 33, 204, 189, 43, 83, 163, 138, 1, 10, 13, 240, 196, 211, 221, 169, 207, 100, 167, 203, 146, 115, 70, 118, 230, 4, 224, 192, 209, 242, 144, 150, 72, 170, 149, 255, 196, 7, 91, 55, 251, 57, 127, 103, 98, 113, 83, 224, 97, 118, 132, 81, 119, 8, 105, 250, 155, 107, 149, 28, 127, 66, 127, 224, 79, 96, 9, 168, 73, 84, 228, 123, 161, 222, 179, 115, 73, 184, 62, 24, 228, 44, 156, 42, 124, 209, 29, 81, 19, 169, 24, 212, 6, 238, 239, 221, 68, 220, 106, 0, 45, 201, 129, 3, 50, 150, 244, 32, 220, 237, 20, 39, 175, 249, 80, 189, 166, 68, 251, 102, 60, 137, 93, 209, 86, 194, 55, 164, 100, 76, 220, 249, 30, 233, 101, 177, 150, 71, 28, 227, 180, 44, 115, 83, 201, 129, 44, 128, 247, 68, 175, 97, 36, 170, 76, 236, 57, 119, 240, 0, 129, 185, 35, 160, 231, 183, 56, 162, 197, 237, 186, 109, 118, 232, 84, 108, 125, 93, 92, 101, 193, 180, 210, 192, 244, 47, 55, 56, 217, 178, 200, 168, 232, 80, 223, 209, 255, 234, 146, 46, 215, 170, 197, 94, 84, 213, 233, 140, 247, 69, 185, 103, 183, 91, 23, 232, 32, 246, 244, 30, 41, 156, 28, 72, 109, 90, 127, 135, 132, 19, 136, 233, 168, 29, 98, 17, 111, 5, 185, 234, 86, 234, 114, 47, 227, 81, 77, 108, 179, 184, 91, 31, 74, 23, 29, 248, 41, 207, 8, 23, 181, 33, 99, 217, 48, 145, 97, 126, 139, 133, 11, 100, 69, 151, 146, 38, 79, 231, 155, 92, 134, 139, 189, 237, 132, 196, 95, 45, 141, 15, 26, 37, 58, 219, 10, 0, 36, 221, 240, 82, 117, 163, 121, 141, 206, 21, 180, 195, 58, 109, 56, 123, 152, 206, 116, 161, 221, 125, 248, 23, 31, 240, 227, 186, 52, 171, 147, 51, 39, 203, 92, 205, 182, 146, 149, 111, 27, 59, 219, 234, 216, 52, 89, 22, 224, 76, 62, 94, 76, 131, 48, 162, 134, 161, 177, 44, 205, 101, 253, 13, 237, 40, 29, 72, 224, 121, 74, 189, 57, 81, 58, 169, 178, 173, 157, 182, 143, 205, 64, 225, 137, 188, 235, 43, 195, 3, 187, 105, 113, 72, 82, 153, 58, 97, 38, 251, 212, 149, 191, 11, 153, 157, 106, 16, 236, 237, 209, 210, 208, 19, 68, 92, 176, 65, 24, 115, 181, 94, 24, 126, 2, 216, 63, 200, 136, 178, 92, 248, 11, 128, 68, 122, 14, 46, 234, 48, 142, 219, 92, 29, 136, 70, 200, 52, 78, 70, 160, 215, 113, 102, 190, 66, 16, 69, 120, 25, 201, 23, 209, 41, 79, 25, 151, 38, 38, 82, 244, 143, 121, 216, 111, 91, 167, 232, 32, 234, 243, 195, 168, 240, 135, 188, 1, 92, 145, 77, 240, 107, 20, 82, 147, 168, 132, 78, 115, 206, 95, 47, 8, 80, 91, 255, 28, 38, 161, 52, 168, 211, 236, 143, 238, 146, 172, 104, 2, 254, 240, 229, 210, 225, 47, 41, 76, 134, 5, 20, 203, 188, 48, 195, 120, 103, 234, 94, 217, 142, 238, 254, 131, 146, 214, 106, 212, 229, 201, 79, 151, 198, 100, 132, 99, 228, 82, 182, 94, 216, 226, 163, 42, 113, 110, 201, 70, 221, 127, 242, 7, 176, 60, 121, 158, 37, 56, 6, 156, 191, 75, 94, 222, 10, 155, 39, 64, 172, 216, 106, 210, 202, 246, 66, 83, 107, 250, 17, 134, 222, 212, 71, 200, 215, 103, 35, 82, 225, 106, 17, 106, 74, 18, 130, 236, 175, 45, 145, 155, 169, 88, 72, 244, 3, 38, 245, 208, 49, 129, 205, 48, 19, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 21, 49, 6, 4, 4, 1, 0, 0, 0, 48, 87, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 20, 49, 74, 30, 72, 0, 100, 0, 99, 0, 99, 0, 52, 0, 51, 0, 48, 0, 56, 0, 56, 0, 45, 0, 50, 0, 57, 0, 54, 0, 53, 0, 45, 0, 52, 0, 57, 0, 97, 0, 48, 0, 45, 0, 56, 0, 51, 0, 54, 0, 53, 0, 45, 0, 50, 0, 52, 0, 101, 0, 52, 0, 97, 0, 52, 0, 49, 0, 100, 0, 55, 0, 50, 0, 52, 0, 48, 48, 93, 6, 9, 43, 6, 1, 4, 1, 130, 55, 17, 1, 49, 80, 30, 78, 0, 77, 0, 105, 0, 99, 0, 114, 0, 111, 0, 115, 0, 111, 0, 102, 0, 116, 0, 32, 0, 83, 0, 116, 0, 114, 0, 111, 0, 110, 0, 103, 0, 32, 0, 67, 0, 114, 0, 121, 0, 112, 0, 116, 0, 111, 0, 103, 0, 114, 0, 97, 0, 112, 0, 104, 0, 105, 0, 99, 0, 32, 0, 80, 0, 114, 0, 111, 0, 118, 0, 105, 0, 100, 0, 101, 0, 114, 48, 130, 3, 199, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 6, 160, 130, 3, 184, 48, 130, 3, 180, 2, 1, 0, 48, 130, 3, 173, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 206, 244, 28, 93, 203, 68, 165, 233, 2, 2, 7, 208, 128, 130, 3, 128, 74, 136, 80, 43, 195, 182, 181, 122, 132, 229, 10, 181, 229, 1, 78, 122, 145, 95, 16, 236, 242, 107, 9, 141, 186, 205, 32, 139, 154, 132, 184, 180, 80, 26, 3, 85, 196, 10, 33, 216, 101, 105, 172, 196, 77, 222, 232, 229, 37, 199, 6, 189, 152, 8, 203, 15, 231, 164, 140, 163, 120, 23, 137, 34, 16, 241, 186, 64, 11, 241, 210, 160, 186, 90, 55, 39, 21, 210, 145, 74, 151, 40, 122, 221, 240, 191, 185, 115, 85, 208, 125, 136, 51, 210, 137, 124, 155, 65, 135, 50, 35, 233, 223, 157, 131, 108, 11, 142, 152, 217, 162, 163, 218, 47, 89, 255, 229, 21, 224, 139, 187, 4, 175, 251, 248, 8, 18, 16, 112, 134, 75, 17, 90, 246, 62, 150, 31, 207, 95, 172, 5, 220, 135, 201, 179, 247, 193, 177, 23, 5, 170, 207, 66, 219, 145, 117, 99, 167, 238, 100, 158, 169, 44, 22, 199, 132, 38, 67, 203, 66, 187, 53, 216, 98, 113, 76, 142, 153, 36, 238, 110, 152, 251, 68, 6, 154, 255, 51, 65, 75, 91, 9, 121, 86, 116, 35, 224, 47, 220, 194, 17, 136, 175, 76, 165, 210, 153, 89, 104, 197, 133, 200, 49, 173, 1, 167, 5, 88, 183, 58, 193, 146, 30, 60, 129, 195, 3, 16, 78, 87, 167, 135, 182, 182, 150, 68, 116, 161, 116, 125, 180, 155, 103, 63, 0, 98, 27, 179, 142, 64, 73, 31, 35, 63, 138, 137, 30, 169, 149, 221, 104, 21, 182, 23, 67, 246, 2, 162, 217, 165, 238, 124, 229, 149, 84, 5, 203, 174, 149, 79, 153, 25, 153, 233, 213, 86, 250, 10, 42, 6, 226, 113, 123, 90, 76, 153, 39, 203, 237, 124, 36, 191, 232, 132, 127, 82, 163, 109, 100, 121, 54, 254, 116, 155, 26, 255, 50, 150, 140, 172, 240, 208, 245, 65, 72, 49, 183, 149, 220, 244, 120, 193, 37, 222, 144, 137, 82, 168, 233, 13, 179, 2, 217, 29, 177, 4, 136, 69, 192, 133, 249, 180, 9, 62, 162, 216, 251, 164, 188, 173, 143, 149, 32, 204, 255, 246, 249, 33, 216, 75, 23, 127, 215, 134, 69, 79, 112, 213, 198, 89, 44, 51, 19, 226, 16, 210, 125, 212, 232, 18, 252, 178, 93, 245, 33, 62, 81, 207, 78, 167, 144, 238, 251, 27, 194, 21, 53, 44, 63, 58, 26, 176, 75, 79, 164, 67, 59, 80, 17, 54, 209, 58, 184, 2, 36, 202, 135, 91, 35, 78, 55, 203, 134, 238, 79, 178, 84, 242, 46, 223, 131, 227, 87, 255, 182, 244, 117, 162, 60, 134, 161, 49, 59, 95, 64, 190, 30, 195, 100, 106, 7, 120, 181, 202, 122, 174, 234, 30, 11, 88, 65, 238, 53, 64, 243, 233, 185, 168, 34, 8, 58, 233, 171, 210, 104, 105, 93, 49, 206, 11, 40, 172, 248, 204, 80, 128, 53, 143, 54, 95, 92, 70, 152, 209, 193, 116, 252, 138, 19, 50, 249, 43, 14, 225, 167, 8, 205, 112, 103, 79, 223, 14, 141, 147, 70, 197, 91, 11, 117, 202, 19, 180, 240, 21, 118, 108, 25, 63, 54, 94, 156, 112, 109, 16, 216, 113, 192, 246, 207, 156, 203, 65, 75, 143, 157, 125, 158, 151, 167, 207, 96, 6, 162, 97, 66, 114, 95, 227, 52, 44, 98, 121, 139, 181, 240, 89, 27, 59, 156, 189, 93, 28, 48, 165, 11, 245, 102, 198, 29, 5, 6, 180, 147, 58, 130, 65, 201, 10, 164, 193, 93, 168, 96, 156, 89, 225, 139, 70, 245, 74, 128, 3, 141, 133, 137, 21, 163, 77, 3, 19, 226, 35, 248, 156, 56, 56, 37, 221, 69, 67, 214, 3, 152, 149, 224, 92, 72, 173, 39, 196, 229, 153, 67, 151, 190, 115, 20, 70, 126, 210, 140, 109, 186, 46, 82, 88, 185, 96, 1, 254, 161, 217, 130, 226, 133, 18, 103, 175, 132, 249, 102, 51, 229, 192, 94, 44, 10, 25, 197, 237, 77, 196, 1, 253, 153, 78, 237, 151, 136, 89, 203, 113, 244, 217, 235, 252, 31, 116, 139, 233, 40, 197, 22, 176, 157, 130, 109, 149, 215, 11, 20, 3, 156, 239, 29, 250, 95, 188, 241, 184, 117, 108, 216, 74, 91, 169, 186, 122, 175, 214, 36, 62, 240, 142, 107, 172, 7, 250, 31, 101, 75, 83, 255, 56, 8, 231, 200, 194, 154, 105, 202, 170, 207, 252, 128, 10, 249, 53, 41, 168, 94, 225, 163, 10, 251, 149, 64, 10, 144, 252, 44, 136, 149, 119, 183, 7, 230, 87, 160, 46, 62, 185, 82, 218, 213, 125, 62, 70, 43, 27, 5, 181, 50, 193, 11, 30, 0, 8, 81, 94, 169, 171, 143, 113, 235, 171, 38, 129, 116, 11, 191, 75, 235, 185, 184, 178, 36, 193, 174, 177, 51, 87, 163, 142, 52, 62, 161, 237, 139, 50, 51, 227, 188, 164, 106, 233, 209, 8, 237, 241, 92, 145, 51, 6, 36, 197, 24, 255, 143, 5, 144, 43, 87, 242, 208, 251, 79, 171, 90, 103, 219, 73, 242, 95, 36, 48, 95, 127, 40, 128, 201, 80, 79, 74, 226, 25, 43, 50, 56, 180, 59, 84, 148, 110, 151, 9, 45, 4, 212, 172, 31, 189, 44, 115, 59, 169, 48, 59, 48, 31, 48, 7, 6, 5, 43, 14, 3, 2, 26, 4, 20, 238, 91, 24, 104, 64, 45, 237, 63, 114, 36, 111, 106, 82, 43, 251, 110, 60, 159, 42, 178, 4, 20, 20, 49, 70, 55, 115, 247, 221, 156, 47, 189, 197, 19, 116, 77, 161, 163, 216, 77, 166, 144, 2, 2, 7, 208 };
        public string certificateThumbPrint2 = "4281446463C6F7F5B8EDFFA4BD6E345E46857CAD";
        public static byte[] certificateRawBytes2 = new byte[] { 48, 130, 10, 36, 2, 1, 3, 48, 130, 9, 224, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 9, 209, 4, 130, 9, 205, 48, 130, 9, 201, 48, 130, 5, 250, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 5, 235, 4, 130, 5, 231, 48, 130, 5, 227, 48, 130, 5, 223, 6, 11, 42, 134, 72, 134, 247, 13, 1, 12, 10, 1, 2, 160, 130, 4, 254, 48, 130, 4, 250, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 95, 228, 98, 55, 21, 153, 43, 16, 2, 2, 7, 208, 4, 130, 4, 216, 210, 4, 83, 193, 62, 47, 171, 147, 165, 139, 74, 78, 118, 172, 54, 56, 118, 81, 203, 190, 58, 5, 220, 181, 64, 1, 213, 5, 156, 164, 86, 59, 63, 230, 250, 57, 26, 236, 10, 195, 200, 80, 217, 38, 214, 116, 250, 224, 236, 54, 59, 208, 206, 128, 167, 122, 89, 0, 195, 145, 41, 63, 122, 160, 157, 21, 226, 205, 179, 166, 243, 92, 94, 71, 61, 208, 183, 153, 105, 24, 232, 255, 139, 188, 32, 109, 120, 41, 18, 218, 212, 71, 219, 139, 182, 59, 78, 46, 97, 176, 67, 125, 239, 234, 107, 47, 202, 71, 81, 100, 243, 136, 162, 39, 197, 207, 4, 224, 52, 62, 104, 88, 44, 42, 234, 18, 70, 55, 248, 251, 121, 215, 162, 77, 109, 189, 135, 86, 219, 69, 208, 92, 147, 163, 21, 50, 0, 87, 166, 8, 39, 21, 72, 107, 75, 214, 165, 238, 50, 145, 2, 65, 138, 179, 21, 87, 149, 218, 163, 51, 214, 17, 93, 252, 224, 6, 245, 242, 132, 63, 100, 223, 136, 166, 89, 253, 171, 204, 215, 191, 170, 25, 40, 44, 14, 32, 25, 22, 149, 161, 62, 145, 61, 162, 32, 116, 7, 201, 33, 159, 128, 248, 172, 42, 33, 51, 248, 187, 135, 58, 107, 23, 7, 39, 21, 114, 186, 222, 132, 117, 193, 132, 94, 57, 213, 80, 9, 86, 186, 62, 201, 40, 12, 196, 207, 23, 182, 127, 245, 139, 18, 62, 107, 82, 58, 156, 156, 17, 29, 173, 60, 227, 180, 73, 245, 165, 16, 186, 246, 64, 178, 40, 66, 2, 119, 61, 78, 246, 214, 226, 89, 225, 85, 183, 163, 108, 82, 36, 109, 216, 195, 141, 174, 123, 162, 91, 128, 169, 37, 68, 121, 170, 80, 236, 27, 109, 173, 223, 6, 37, 37, 249, 166, 219, 226, 116, 236, 184, 212, 47, 70, 249, 89, 195, 216, 157, 227, 137, 247, 179, 148, 16, 40, 217, 220, 247, 97, 42, 120, 63, 3, 14, 104, 79, 111, 160, 245, 210, 33, 50, 6, 36, 90, 225, 206, 104, 70, 4, 191, 79, 16, 237, 200, 125, 92, 215, 175, 196, 143, 134, 95, 112, 251, 58, 68, 165, 157, 80, 82, 78, 203, 34, 131, 163, 136, 200, 4, 117, 174, 197, 159, 175, 14, 5, 110, 99, 70, 235, 91, 214, 136, 218, 14, 203, 28, 153, 48, 34, 60, 10, 202, 129, 212, 146, 83, 104, 236, 228, 248, 125, 120, 98, 174, 173, 216, 146, 254, 128, 0, 206, 64, 119, 56, 71, 153, 81, 155, 40, 195, 114, 41, 73, 108, 77, 229, 98, 15, 216, 164, 13, 139, 81, 9, 143, 137, 164, 122, 35, 192, 93, 87, 85, 88, 230, 168, 148, 233, 5, 76, 244, 116, 121, 157, 27, 174, 231, 58, 84, 159, 102, 149, 163, 142, 128, 195, 10, 214, 11, 164, 197, 182, 189, 33, 177, 232, 39, 44, 225, 74, 237, 197, 52, 82, 76, 105, 9, 221, 110, 251, 62, 255, 194, 24, 93, 184, 19, 220, 119, 127, 76, 198, 181, 14, 136, 106, 49, 241, 164, 18, 62, 80, 18, 88, 114, 167, 138, 183, 72, 160, 64, 27, 140, 160, 74, 113, 40, 212, 223, 128, 23, 113, 192, 162, 184, 234, 141, 207, 3, 246, 40, 2, 89, 184, 191, 67, 121, 16, 187, 117, 141, 163, 187, 170, 124, 169, 67, 148, 226, 202, 132, 61, 95, 7, 242, 116, 252, 228, 17, 102, 201, 178, 77, 215, 164, 204, 210, 31, 100, 243, 242, 190, 151, 96, 173, 74, 195, 214, 233, 78, 187, 73, 124, 183, 38, 124, 33, 108, 226, 113, 120, 25, 87, 201, 49, 134, 106, 127, 206, 234, 40, 37, 199, 56, 112, 0, 172, 136, 68, 8, 145, 225, 78, 186, 170, 121, 218, 37, 186, 80, 207, 29, 180, 129, 159, 178, 162, 152, 107, 39, 229, 192, 237, 226, 172, 88, 117, 144, 229, 124, 67, 74, 156, 81, 211, 118, 93, 188, 93, 209, 170, 240, 136, 37, 18, 181, 20, 48, 70, 79, 37, 169, 184, 240, 101, 153, 230, 10, 212, 36, 29, 201, 27, 39, 107, 221, 179, 226, 19, 199, 108, 158, 78, 217, 49, 255, 131, 36, 194, 37, 133, 47, 36, 207, 13, 16, 115, 179, 220, 57, 248, 194, 101, 181, 222, 170, 240, 120, 37, 50, 87, 198, 14, 251, 138, 115, 33, 231, 29, 240, 172, 130, 199, 77, 53, 245, 43, 178, 61, 103, 28, 33, 175, 247, 67, 232, 3, 139, 198, 115, 93, 146, 71, 154, 206, 118, 163, 99, 213, 241, 174, 20, 247, 181, 12, 112, 165, 116, 179, 220, 52, 200, 206, 162, 105, 12, 30, 212, 199, 179, 243, 176, 156, 113, 51, 142, 138, 70, 179, 130, 28, 118, 98, 7, 46, 26, 100, 200, 215, 16, 80, 138, 113, 160, 107, 209, 18, 85, 2, 69, 235, 2, 217, 80, 238, 212, 108, 18, 68, 63, 24, 174, 60, 253, 127, 94, 255, 249, 181, 98, 243, 240, 172, 109, 242, 155, 42, 70, 155, 38, 214, 231, 206, 60, 205, 46, 68, 77, 128, 192, 141, 4, 73, 54, 137, 32, 71, 20, 68, 11, 204, 124, 97, 205, 246, 80, 209, 175, 165, 121, 60, 195, 104, 104, 60, 2, 99, 142, 82, 121, 136, 118, 226, 178, 158, 80, 0, 159, 131, 208, 232, 46, 150, 196, 154, 196, 50, 183, 85, 170, 242, 218, 232, 236, 225, 52, 46, 109, 237, 127, 68, 251, 25, 105, 239, 32, 59, 205, 174, 131, 95, 75, 93, 218, 168, 173, 6, 152, 111, 251, 201, 146, 19, 230, 2, 32, 73, 32, 64, 101, 124, 96, 155, 101, 211, 232, 249, 143, 177, 147, 17, 187, 246, 46, 202, 155, 113, 236, 181, 70, 118, 220, 87, 20, 27, 17, 255, 223, 65, 217, 126, 5, 246, 161, 95, 186, 194, 77, 46, 26, 128, 253, 108, 178, 246, 121, 133, 172, 172, 75, 59, 12, 118, 7, 146, 154, 51, 94, 243, 112, 232, 103, 239, 159, 64, 183, 187, 79, 22, 43, 7, 250, 205, 183, 201, 178, 235, 80, 154, 233, 232, 125, 78, 62, 203, 132, 33, 4, 185, 234, 59, 190, 116, 133, 240, 41, 172, 207, 36, 177, 206, 49, 18, 40, 23, 177, 250, 36, 243, 59, 160, 24, 245, 218, 53, 234, 79, 17, 68, 19, 165, 156, 16, 250, 18, 111, 60, 179, 142, 168, 4, 67, 212, 11, 230, 113, 46, 105, 234, 98, 60, 36, 218, 202, 93, 54, 53, 160, 247, 144, 244, 225, 219, 101, 143, 239, 183, 75, 6, 144, 68, 10, 96, 98, 191, 251, 230, 243, 154, 236, 113, 164, 139, 155, 250, 133, 104, 45, 21, 106, 221, 20, 139, 191, 33, 38, 103, 135, 95, 5, 237, 173, 185, 173, 2, 40, 196, 114, 3, 174, 13, 201, 219, 150, 100, 30, 212, 38, 60, 106, 112, 244, 229, 34, 155, 169, 51, 62, 96, 154, 90, 225, 221, 103, 8, 201, 235, 189, 170, 42, 44, 255, 210, 69, 155, 119, 153, 8, 164, 171, 97, 173, 166, 68, 207, 170, 50, 92, 17, 2, 32, 182, 46, 136, 189, 187, 148, 206, 205, 127, 179, 32, 31, 77, 12, 141, 19, 126, 5, 82, 221, 47, 87, 206, 198, 146, 226, 128, 144, 64, 124, 49, 129, 205, 48, 19, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 21, 49, 6, 4, 4, 1, 0, 0, 0, 48, 87, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 20, 49, 74, 30, 72, 0, 98, 0, 53, 0, 55, 0, 56, 0, 48, 0, 53, 0, 52, 0, 52, 0, 45, 0, 52, 0, 56, 0, 99, 0, 48, 0, 45, 0, 52, 0, 55, 0, 56, 0, 52, 0, 45, 0, 57, 0, 50, 0, 98, 0, 52, 0, 45, 0, 56, 0, 99, 0, 49, 0, 101, 0, 102, 0, 56, 0, 54, 0, 101, 0, 54, 0, 100, 0, 54, 0, 54, 48, 93, 6, 9, 43, 6, 1, 4, 1, 130, 55, 17, 1, 49, 80, 30, 78, 0, 77, 0, 105, 0, 99, 0, 114, 0, 111, 0, 115, 0, 111, 0, 102, 0, 116, 0, 32, 0, 83, 0, 116, 0, 114, 0, 111, 0, 110, 0, 103, 0, 32, 0, 67, 0, 114, 0, 121, 0, 112, 0, 116, 0, 111, 0, 103, 0, 114, 0, 97, 0, 112, 0, 104, 0, 105, 0, 99, 0, 32, 0, 80, 0, 114, 0, 111, 0, 118, 0, 105, 0, 100, 0, 101, 0, 114, 48, 130, 3, 199, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 6, 160, 130, 3, 184, 48, 130, 3, 180, 2, 1, 0, 48, 130, 3, 173, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 109, 84, 234, 217, 158, 100, 175, 217, 2, 2, 7, 208, 128, 130, 3, 128, 90, 89, 57, 156, 113, 214, 108, 227, 208, 52, 188, 14, 47, 164, 163, 155, 102, 18, 248, 55, 200, 122, 232, 224, 246, 212, 57, 2, 121, 60, 244, 110, 223, 203, 184, 67, 115, 179, 191, 220, 45, 209, 143, 220, 17, 53, 61, 179, 105, 25, 150, 102, 108, 168, 140, 67, 141, 86, 165, 159, 139, 29, 23, 173, 40, 81, 79, 245, 12, 154, 11, 154, 208, 199, 34, 25, 46, 112, 30, 175, 71, 124, 20, 64, 128, 150, 156, 241, 198, 55, 103, 242, 169, 160, 232, 138, 160, 189, 30, 66, 73, 134, 28, 1, 32, 19, 151, 249, 75, 179, 252, 0, 244, 116, 209, 35, 116, 199, 171, 120, 18, 234, 17, 47, 70, 115, 154, 76, 170, 36, 166, 140, 190, 168, 99, 169, 130, 200, 220, 55, 38, 56, 145, 4, 119, 149, 184, 242, 214, 46, 181, 101, 25, 97, 102, 39, 240, 123, 83, 87, 69, 107, 159, 69, 136, 101, 88, 45, 2, 254, 111, 255, 202, 184, 213, 237, 98, 120, 235, 247, 134, 85, 155, 103, 232, 99, 218, 169, 219, 55, 232, 126, 133, 172, 179, 210, 82, 40, 42, 101, 157, 213, 42, 217, 88, 111, 101, 88, 202, 30, 212, 117, 225, 169, 164, 105, 225, 128, 251, 66, 148, 38, 7, 166, 125, 120, 135, 158, 139, 222, 245, 170, 5, 8, 102, 51, 193, 173, 136, 141, 183, 233, 168, 185, 208, 198, 238, 176, 63, 6, 167, 53, 27, 15, 226, 47, 37, 154, 1, 126, 252, 77, 83, 5, 21, 221, 166, 171, 9, 177, 181, 85, 83, 6, 154, 164, 24, 206, 48, 88, 243, 82, 103, 80, 9, 1, 169, 225, 255, 75, 10, 150, 17, 242, 49, 110, 194, 114, 9, 69, 15, 183, 20, 33, 43, 113, 79, 131, 115, 102, 230, 138, 139, 191, 29, 249, 175, 222, 231, 87, 98, 213, 108, 107, 30, 10, 126, 114, 3, 81, 67, 192, 26, 125, 31, 87, 83, 75, 113, 254, 122, 143, 227, 104, 67, 58, 155, 93, 177, 158, 84, 50, 50, 197, 125, 17, 252, 87, 194, 176, 160, 140, 220, 47, 209, 188, 158, 89, 167, 166, 241, 212, 44, 108, 208, 52, 39, 128, 198, 157, 93, 56, 154, 184, 136, 9, 48, 93, 239, 244, 140, 204, 139, 91, 181, 160, 117, 230, 142, 233, 205, 18, 178, 212, 103, 148, 188, 201, 106, 70, 172, 212, 215, 145, 62, 166, 139, 165, 241, 178, 100, 150, 119, 234, 44, 107, 123, 73, 105, 82, 222, 254, 40, 49, 198, 167, 88, 165, 176, 187, 124, 161, 97, 133, 188, 159, 234, 90, 110, 99, 121, 191, 123, 254, 86, 108, 164, 231, 225, 227, 209, 237, 155, 26, 148, 241, 91, 253, 18, 68, 137, 175, 184, 4, 160, 176, 109, 72, 177, 84, 229, 115, 95, 98, 155, 117, 96, 54, 226, 141, 163, 109, 58, 189, 67, 15, 246, 151, 136, 169, 88, 46, 160, 102, 113, 226, 92, 122, 209, 140, 52, 105, 235, 254, 127, 131, 235, 246, 146, 67, 24, 97, 196, 193, 248, 246, 214, 55, 52, 254, 214, 193, 77, 192, 91, 45, 17, 204, 77, 193, 211, 166, 152, 214, 79, 173, 167, 11, 49, 253, 9, 167, 249, 168, 228, 198, 164, 61, 209, 118, 5, 170, 128, 128, 217, 130, 90, 64, 13, 79, 191, 142, 29, 211, 101, 225, 173, 190, 124, 132, 149, 107, 178, 129, 102, 135, 23, 109, 5, 75, 207, 185, 21, 227, 184, 41, 92, 83, 180, 164, 77, 140, 184, 216, 66, 25, 104, 165, 158, 95, 234, 78, 153, 215, 26, 253, 100, 115, 101, 149, 58, 5, 49, 204, 213, 35, 188, 200, 201, 74, 248, 94, 143, 231, 215, 59, 145, 54, 28, 197, 156, 197, 131, 80, 114, 212, 236, 133, 25, 178, 57, 28, 241, 237, 56, 220, 195, 19, 234, 13, 122, 109, 81, 205, 163, 204, 122, 82, 10, 166, 44, 44, 74, 229, 251, 200, 42, 128, 223, 29, 182, 95, 180, 3, 100, 235, 56, 34, 157, 84, 23, 142, 6, 147, 187, 145, 97, 185, 6, 254, 48, 110, 125, 41, 239, 163, 169, 196, 145, 120, 115, 213, 57, 85, 118, 121, 211, 14, 56, 54, 34, 171, 101, 37, 190, 232, 55, 64, 238, 119, 150, 67, 136, 116, 193, 175, 100, 38, 2, 163, 28, 27, 250, 88, 46, 56, 18, 20, 68, 115, 111, 172, 112, 239, 55, 213, 5, 2, 91, 134, 98, 53, 99, 90, 6, 18, 199, 74, 241, 162, 85, 182, 134, 42, 233, 38, 192, 248, 200, 165, 194, 85, 232, 129, 170, 36, 155, 44, 14, 31, 234, 191, 172, 204, 146, 151, 24, 214, 14, 174, 163, 88, 15, 198, 119, 104, 198, 20, 139, 7, 199, 232, 144, 30, 129, 243, 234, 148, 231, 227, 87, 76, 81, 160, 149, 26, 233, 108, 133, 127, 193, 82, 233, 19, 179, 62, 210, 244, 118, 249, 202, 245, 5, 71, 37, 53, 249, 30, 0, 43, 190, 110, 96, 184, 239, 176, 52, 55, 109, 110, 111, 53, 193, 192, 200, 180, 5, 88, 32, 174, 106, 17, 23, 127, 190, 166, 34, 57, 161, 146, 255, 197, 222, 2, 107, 227, 61, 215, 36, 253, 1, 122, 234, 234, 156, 79, 48, 59, 48, 31, 48, 7, 6, 5, 43, 14, 3, 2, 26, 4, 20, 207, 75, 252, 75, 27, 80, 128, 157, 13, 40, 231, 80, 100, 232, 103, 176, 94, 95, 145, 147, 4, 20, 48, 158, 42, 31, 56, 126, 217, 230, 42, 16, 115, 84, 206, 98, 189, 26, 78, 38, 171, 12, 2, 2, 7, 208 };
        public static byte[] certificateRawBytes3 = new byte[] { 48, 130, 10, 44, 2, 1, 3, 48, 130, 9, 232, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 9, 217, 4, 130, 9, 213, 48, 130, 9, 209, 48, 130, 5, 250, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 160, 130, 5, 235, 4, 130, 5, 231, 48, 130, 5, 227, 48, 130, 5, 223, 6, 11, 42, 134, 72, 134, 247, 13, 1, 12, 10, 1, 2, 160, 130, 4, 254, 48, 130, 4, 250, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 201, 182, 155, 228, 101, 5, 93, 99, 2, 2, 7, 208, 4, 130, 4, 216, 10, 49, 25, 129, 251, 253, 104, 241, 120, 23, 72, 166, 23, 144, 117, 109, 170, 226, 13, 202, 202, 135, 74, 114, 79, 208, 250, 193, 195, 26, 118, 85, 45, 17, 43, 75, 29, 44, 52, 18, 255, 66, 137, 221, 33, 65, 186, 142, 90, 33, 2, 4, 177, 170, 58, 244, 60, 82, 40, 77, 64, 168, 77, 2, 29, 36, 210, 107, 213, 47, 160, 181, 82, 202, 221, 5, 202, 46, 222, 27, 228, 181, 14, 128, 2, 65, 57, 13, 128, 211, 53, 168, 207, 150, 76, 135, 1, 84, 243, 72, 151, 205, 184, 164, 221, 138, 107, 162, 163, 248, 8, 107, 102, 223, 173, 12, 192, 119, 77, 154, 224, 213, 198, 90, 183, 161, 188, 4, 221, 191, 76, 179, 87, 158, 140, 179, 198, 253, 87, 148, 162, 94, 186, 171, 36, 251, 214, 44, 164, 240, 131, 52, 204, 192, 163, 166, 108, 150, 108, 85, 138, 181, 45, 159, 7, 106, 47, 1, 189, 81, 104, 80, 66, 21, 133, 111, 226, 238, 73, 126, 191, 61, 169, 123, 210, 37, 228, 121, 231, 118, 134, 176, 15, 206, 137, 99, 253, 172, 21, 14, 249, 32, 226, 122, 101, 41, 98, 23, 35, 183, 23, 66, 102, 249, 175, 41, 103, 161, 211, 57, 208, 146, 182, 229, 55, 1, 251, 212, 253, 83, 119, 224, 153, 22, 198, 147, 9, 99, 101, 214, 142, 202, 197, 187, 197, 21, 199, 186, 231, 143, 116, 4, 253, 70, 238, 220, 98, 236, 110, 20, 153, 134, 52, 40, 245, 167, 76, 28, 24, 242, 40, 57, 97, 227, 223, 239, 249, 14, 34, 40, 53, 145, 35, 248, 37, 251, 84, 65, 255, 0, 143, 54, 73, 38, 8, 99, 160, 70, 49, 7, 238, 31, 238, 62, 180, 125, 71, 166, 96, 52, 37, 134, 229, 198, 150, 206, 102, 241, 51, 240, 186, 120, 74, 55, 114, 46, 190, 85, 15, 89, 4, 126, 89, 247, 120, 241, 119, 110, 225, 126, 99, 143, 179, 7, 117, 242, 225, 197, 247, 130, 105, 59, 129, 127, 222, 207, 20, 108, 241, 149, 124, 156, 175, 187, 105, 79, 23, 245, 131, 106, 12, 111, 228, 124, 128, 147, 204, 21, 122, 93, 52, 251, 197, 119, 204, 195, 111, 8, 43, 226, 50, 39, 66, 179, 84, 108, 71, 35, 10, 35, 129, 96, 0, 110, 5, 92, 223, 77, 49, 237, 184, 153, 163, 111, 29, 241, 52, 62, 190, 157, 22, 40, 26, 6, 231, 156, 90, 210, 16, 183, 154, 208, 244, 113, 213, 110, 121, 56, 131, 240, 136, 77, 171, 87, 215, 87, 134, 188, 14, 104, 217, 35, 218, 215, 36, 50, 5, 72, 137, 63, 242, 29, 0, 28, 127, 172, 190, 166, 239, 100, 172, 172, 23, 131, 237, 32, 73, 92, 157, 178, 82, 87, 0, 131, 53, 226, 176, 125, 221, 33, 75, 201, 164, 24, 253, 23, 130, 41, 129, 13, 223, 10, 22, 231, 99, 220, 171, 0, 205, 239, 89, 47, 224, 139, 181, 201, 54, 125, 105, 206, 168, 62, 145, 182, 114, 209, 134, 48, 0, 90, 143, 163, 161, 117, 167, 74, 6, 229, 92, 10, 163, 27, 49, 25, 139, 44, 140, 35, 107, 46, 74, 124, 180, 88, 92, 49, 27, 53, 93, 20, 105, 35, 8, 242, 28, 106, 106, 80, 220, 138, 169, 89, 154, 9, 57, 173, 83, 67, 136, 119, 200, 78, 152, 47, 100, 166, 119, 23, 208, 46, 57, 213, 6, 18, 21, 29, 185, 174, 154, 245, 154, 122, 53, 155, 28, 110, 139, 20, 236, 97, 97, 155, 65, 128, 45, 27, 95, 117, 126, 150, 87, 70, 37, 209, 65, 178, 27, 17, 59, 180, 176, 172, 104, 184, 144, 2, 238, 219, 210, 250, 168, 125, 24, 113, 36, 28, 105, 107, 34, 241, 175, 251, 71, 110, 72, 64, 15, 194, 39, 105, 18, 168, 60, 13, 50, 62, 191, 196, 255, 5, 106, 100, 158, 3, 45, 148, 238, 239, 64, 3, 21, 198, 50, 165, 107, 245, 195, 103, 138, 108, 153, 48, 105, 193, 106, 31, 82, 126, 202, 144, 215, 64, 138, 23, 160, 13, 233, 88, 199, 77, 46, 231, 124, 237, 26, 24, 206, 53, 162, 53, 224, 41, 178, 17, 44, 148, 22, 151, 192, 140, 121, 12, 68, 31, 238, 82, 24, 21, 116, 219, 172, 138, 179, 91, 10, 54, 235, 19, 49, 145, 67, 5, 55, 171, 23, 196, 237, 252, 107, 197, 18, 207, 48, 75, 229, 197, 152, 60, 33, 246, 97, 231, 28, 163, 222, 213, 8, 169, 178, 54, 217, 174, 208, 181, 173, 59, 174, 100, 65, 137, 130, 47, 42, 59, 176, 209, 225, 254, 59, 193, 40, 110, 195, 139, 166, 11, 44, 134, 251, 243, 46, 200, 174, 96, 21, 244, 10, 226, 244, 40, 115, 30, 241, 143, 100, 247, 0, 227, 248, 239, 78, 249, 80, 0, 35, 73, 107, 250, 156, 21, 36, 138, 203, 235, 84, 154, 46, 184, 12, 139, 246, 255, 89, 162, 249, 107, 237, 55, 148, 123, 197, 148, 254, 90, 201, 255, 249, 219, 170, 138, 212, 179, 175, 93, 42, 44, 195, 71, 108, 77, 187, 145, 147, 82, 154, 29, 166, 90, 35, 124, 157, 68, 102, 136, 235, 143, 100, 41, 248, 103, 139, 50, 56, 108, 233, 65, 228, 42, 152, 157, 102, 176, 56, 249, 86, 198, 236, 75, 61, 206, 240, 255, 55, 20, 222, 105, 19, 221, 7, 66, 194, 20, 186, 152, 120, 90, 68, 231, 17, 37, 248, 172, 75, 172, 129, 209, 220, 180, 123, 65, 32, 215, 146, 245, 5, 28, 255, 213, 35, 10, 59, 141, 15, 111, 5, 176, 189, 54, 209, 38, 234, 89, 52, 203, 128, 17, 147, 202, 171, 186, 8, 10, 89, 223, 83, 119, 28, 102, 11, 108, 70, 235, 43, 6, 146, 152, 132, 106, 192, 10, 80, 242, 88, 117, 8, 80, 13, 224, 120, 56, 19, 165, 190, 45, 124, 193, 38, 185, 40, 115, 222, 149, 243, 106, 169, 170, 55, 79, 87, 59, 203, 110, 62, 3, 100, 130, 147, 171, 79, 9, 227, 36, 39, 134, 231, 253, 179, 7, 23, 19, 206, 64, 56, 32, 175, 127, 46, 234, 192, 238, 190, 122, 35, 52, 162, 176, 223, 174, 205, 233, 88, 60, 32, 77, 154, 144, 15, 148, 126, 78, 53, 37, 247, 129, 104, 215, 9, 0, 43, 198, 64, 47, 115, 102, 232, 31, 49, 70, 219, 23, 54, 242, 73, 110, 145, 16, 206, 59, 91, 179, 172, 134, 143, 20, 22, 78, 167, 102, 59, 91, 29, 206, 197, 73, 245, 245, 219, 59, 42, 198, 105, 195, 50, 23, 196, 133, 185, 8, 30, 197, 92, 213, 48, 194, 217, 133, 65, 229, 16, 70, 231, 232, 230, 167, 77, 127, 233, 148, 87, 181, 62, 1, 159, 32, 27, 110, 167, 31, 111, 1, 196, 242, 7, 123, 241, 89, 221, 132, 252, 185, 175, 112, 51, 108, 228, 189, 207, 189, 89, 23, 65, 205, 7, 53, 171, 220, 245, 242, 85, 187, 55, 153, 8, 255, 1, 171, 160, 187, 109, 130, 245, 4, 26, 1, 83, 223, 10, 221, 61, 65, 101, 0, 94, 136, 47, 2, 209, 7, 143, 65, 216, 32, 71, 168, 155, 198, 236, 170, 205, 64, 49, 129, 205, 48, 19, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 21, 49, 6, 4, 4, 1, 0, 0, 0, 48, 87, 6, 9, 42, 134, 72, 134, 247, 13, 1, 9, 20, 49, 74, 30, 72, 0, 49, 0, 99, 0, 57, 0, 48, 0, 53, 0, 48, 0, 51, 0, 97, 0, 45, 0, 55, 0, 56, 0, 52, 0, 57, 0, 45, 0, 52, 0, 51, 0, 54, 0, 99, 0, 45, 0, 56, 0, 54, 0, 97, 0, 52, 0, 45, 0, 49, 0, 51, 0, 50, 0, 97, 0, 54, 0, 53, 0, 100, 0, 52, 0, 97, 0, 48, 0, 48, 0, 55, 48, 93, 6, 9, 43, 6, 1, 4, 1, 130, 55, 17, 1, 49, 80, 30, 78, 0, 77, 0, 105, 0, 99, 0, 114, 0, 111, 0, 115, 0, 111, 0, 102, 0, 116, 0, 32, 0, 83, 0, 116, 0, 114, 0, 111, 0, 110, 0, 103, 0, 32, 0, 67, 0, 114, 0, 121, 0, 112, 0, 116, 0, 111, 0, 103, 0, 114, 0, 97, 0, 112, 0, 104, 0, 105, 0, 99, 0, 32, 0, 80, 0, 114, 0, 111, 0, 118, 0, 105, 0, 100, 0, 101, 0, 114, 48, 130, 3, 207, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 6, 160, 130, 3, 192, 48, 130, 3, 188, 2, 1, 0, 48, 130, 3, 181, 6, 9, 42, 134, 72, 134, 247, 13, 1, 7, 1, 48, 28, 6, 10, 42, 134, 72, 134, 247, 13, 1, 12, 1, 3, 48, 14, 4, 8, 200, 59, 127, 243, 171, 147, 157, 127, 2, 2, 7, 208, 128, 130, 3, 136, 99, 75, 113, 248, 97, 190, 186, 177, 98, 176, 186, 91, 120, 170, 132, 193, 62, 121, 23, 129, 255, 220, 247, 43, 159, 29, 163, 114, 13, 93, 143, 171, 102, 66, 252, 74, 99, 253, 38, 53, 56, 73, 69, 51, 98, 128, 200, 232, 11, 162, 4, 170, 137, 109, 228, 185, 27, 6, 195, 120, 253, 174, 180, 67, 183, 248, 105, 101, 6, 173, 66, 251, 129, 249, 90, 140, 192, 43, 14, 201, 78, 111, 140, 115, 138, 216, 199, 45, 66, 89, 55, 144, 91, 37, 72, 194, 78, 193, 34, 16, 13, 88, 91, 183, 96, 143, 202, 15, 164, 215, 97, 222, 124, 248, 92, 221, 184, 103, 54, 132, 64, 114, 209, 116, 102, 168, 236, 8, 132, 143, 98, 218, 86, 110, 79, 45, 73, 156, 55, 35, 162, 83, 219, 200, 223, 144, 56, 64, 170, 137, 193, 157, 107, 47, 58, 38, 128, 142, 18, 184, 44, 80, 182, 80, 26, 8, 203, 161, 224, 254, 247, 223, 56, 124, 23, 187, 123, 63, 254, 147, 193, 77, 209, 101, 76, 46, 70, 118, 57, 172, 177, 35, 15, 49, 189, 115, 28, 117, 133, 166, 225, 246, 19, 133, 195, 70, 30, 237, 25, 215, 141, 19, 115, 48, 104, 39, 41, 140, 104, 38, 32, 215, 222, 153, 232, 74, 16, 23, 233, 21, 237, 145, 161, 62, 225, 143, 30, 219, 183, 12, 62, 24, 141, 28, 73, 49, 203, 92, 39, 133, 152, 143, 176, 197, 87, 47, 128, 228, 128, 169, 5, 121, 168, 15, 141, 217, 179, 79, 128, 150, 131, 78, 224, 238, 184, 141, 249, 123, 211, 241, 236, 103, 45, 7, 211, 163, 108, 216, 104, 200, 208, 134, 78, 196, 96, 118, 184, 78, 62, 88, 221, 5, 155, 249, 176, 207, 238, 2, 147, 229, 18, 77, 69, 189, 13, 237, 212, 224, 12, 61, 45, 254, 130, 135, 234, 41, 51, 142, 94, 24, 174, 55, 247, 56, 115, 206, 214, 161, 85, 51, 74, 248, 16, 219, 70, 91, 82, 26, 150, 59, 215, 39, 41, 85, 211, 77, 49, 23, 52, 59, 127, 20, 22, 130, 40, 253, 112, 123, 6, 15, 253, 98, 212, 24, 154, 126, 134, 216, 76, 76, 110, 212, 70, 62, 213, 115, 53, 158, 209, 152, 92, 178, 254, 24, 224, 121, 208, 19, 114, 250, 255, 80, 163, 61, 237, 48, 108, 51, 36, 41, 84, 97, 175, 131, 106, 150, 73, 176, 67, 114, 173, 244, 158, 235, 127, 19, 85, 179, 74, 144, 206, 126, 50, 39, 173, 112, 232, 108, 43, 9, 193, 167, 162, 63, 242, 218, 47, 26, 121, 237, 13, 240, 7, 107, 193, 202, 117, 224, 208, 128, 174, 137, 242, 221, 209, 37, 88, 184, 254, 69, 10, 39, 163, 158, 55, 249, 111, 238, 9, 78, 154, 184, 139, 227, 198, 39, 127, 222, 220, 77, 101, 128, 31, 210, 138, 48, 36, 26, 80, 160, 167, 181, 185, 196, 195, 208, 129, 23, 17, 157, 101, 157, 45, 173, 249, 158, 173, 245, 103, 88, 24, 170, 174, 203, 92, 69, 211, 167, 1, 221, 73, 99, 16, 148, 44, 97, 190, 28, 144, 252, 87, 10, 13, 95, 83, 211, 86, 14, 176, 27, 77, 236, 68, 10, 245, 185, 82, 154, 22, 207, 1, 163, 21, 214, 183, 201, 254, 91, 176, 171, 176, 223, 193, 128, 33, 100, 34, 32, 146, 156, 207, 247, 97, 250, 244, 41, 140, 253, 230, 56, 62, 90, 190, 41, 225, 50, 221, 134, 219, 63, 4, 81, 210, 106, 154, 6, 186, 97, 227, 13, 79, 111, 18, 204, 184, 246, 107, 194, 105, 65, 30, 179, 125, 196, 248, 96, 216, 77, 160, 27, 93, 42, 23, 6, 246, 13, 178, 22, 46, 67, 83, 224, 116, 91, 136, 223, 246, 213, 250, 207, 252, 197, 217, 139, 118, 245, 91, 236, 66, 208, 138, 154, 75, 107, 109, 151, 202, 68, 240, 212, 67, 20, 99, 109, 248, 139, 224, 206, 84, 152, 224, 170, 51, 137, 117, 64, 222, 91, 239, 71, 72, 162, 247, 243, 187, 164, 53, 123, 99, 186, 208, 39, 12, 160, 61, 78, 19, 205, 15, 250, 185, 238, 102, 144, 146, 127, 94, 250, 121, 240, 122, 240, 156, 182, 119, 143, 16, 120, 138, 161, 170, 79, 93, 169, 100, 149, 168, 76, 173, 8, 163, 172, 89, 153, 86, 73, 117, 100, 123, 115, 69, 135, 50, 29, 33, 66, 82, 71, 118, 231, 185, 151, 133, 179, 56, 251, 225, 76, 36, 189, 43, 39, 202, 168, 46, 25, 181, 127, 109, 78, 121, 80, 82, 137, 32, 196, 116, 100, 197, 0, 222, 88, 92, 165, 89, 146, 146, 239, 137, 28, 164, 44, 27, 162, 74, 37, 195, 54, 78, 18, 210, 106, 39, 162, 157, 145, 228, 70, 101, 195, 128, 222, 235, 117, 238, 29, 11, 180, 80, 104, 240, 118, 44, 231, 98, 239, 211, 64, 167, 59, 209, 26, 103, 168, 95, 187, 243, 178, 55, 31, 26, 230, 135, 67, 205, 232, 23, 89, 177, 223, 32, 105, 80, 186, 195, 82, 86, 144, 216, 110, 222, 223, 91, 20, 100, 73, 139, 128, 153, 150, 210, 140, 18, 197, 42, 121, 160, 77, 194, 37, 252, 68, 94, 32, 28, 245, 145, 29, 232, 182, 213, 246, 68, 175, 48, 59, 48, 31, 48, 7, 6, 5, 43, 14, 3, 2, 26, 4, 20, 121, 31, 55, 196, 109, 33, 122, 128, 32, 212, 84, 89, 7, 193, 97, 193, 144, 148, 24, 59, 4, 20, 161, 35, 154, 3, 112, 148, 126, 41, 44, 75, 51, 43, 182, 209, 49, 49, 210, 122, 30, 103, 2, 2, 7, 208 };
        public X509Certificate2 certificate1 = new X509Certificate2(certificateRawBytes1, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        public X509Certificate2 certificate2 = new X509Certificate2(certificateRawBytes2, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        public X509Certificate2 certificate3 = new X509Certificate2(certificateRawBytes3, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.UserKeySet);
        public CertificateFixture()
        {
            AddCertificateToStore(certificate1, StoreLocation.CurrentUser);
            AddCertificateToStore(certificate2, StoreLocation.CurrentUser);
            AddCertificateToStore(certificate3, StoreLocation.CurrentUser);
            if (IsAdmin)
            {
                AddCertificateToStore(certificate3, StoreLocation.LocalMachine);
            }
        }

        public void Dispose()
        {
            // Do Not remove Certificates to provide concurrency support
            // RemoveCertificate(certificate1, StoreLocation.CurrentUser);
            // RemoveCertificate(certificate2, StoreLocation.CurrentUser);
        }

        public static void AddCertificateToStore(X509Certificate2 certificate, StoreLocation certificateStoreLocation)
        {
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, certificateStoreLocation);
                certStore.Open(OpenFlags.ReadWrite);
                if (!certStore.Certificates.Contains(certificate))
                {
                    certStore.Add(certificate);
                }

            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }
        }

        public static void RemoveCertificate(X509Certificate2 certificate, StoreLocation certificateStoreLocation)
        {
            X509Store certStore = null;
            try
            {
                certStore = new X509Store(StoreName.My, certificateStoreLocation);
                certStore.Open(OpenFlags.ReadWrite);
                certStore.Remove(certificate);
            }
            finally
            {
                if (certStore != null)
                {
                    certStore.Close();
                }
            }
        }
    }
}
