// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.TestUtilities.Fixtures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Xunit;
using Xunit.Sdk;
using static Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests.TestFixtures;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class SqlColumnEncryptionCertificateStoreProviderShould : IClassFixture<ColumnEncryptionCertificateFixture>
    {
        private const string PRIMARY_CERTIFICATE_PATH = "CurrentUser/My/{primary_thumbprint}";
        private const string SECONDARY_CERTIFICATE_PATH = "CurrentUser/My/{secondary_thumbprint}";
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

        private readonly ColumnEncryptionCertificateFixture _certFixture;

        public SqlColumnEncryptionCertificateStoreProviderShould(ColumnEncryptionCertificateFixture certFixture)
        {
            _certFixture = certFixture;

            // Disable the cache to avoid false failures.
            SqlConnection.ColumnEncryptionQueryMetadataCacheEnabled = false;
        }

        [Theory]
        [InvalidDecryptionParameters]
        public void ThrowExceptionWithInvalidParameterWhileDecryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            Exception ex = Assert.Throws(exceptionType,
                () => provider.DecryptColumnEncryptionKey(ReplaceKeyTokens(masterKeyPath), encryptionAlgorithm, bytes));
            Assert.Matches(ReplaceKeyTokens(errorMsg), ex.Message);
        }

        [Theory]
        [InvalidEncryptionParameters]
        public void ThrowExceptionWithInvalidParameterWhileEncryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.EncryptColumnEncryptionKey(ReplaceKeyTokens(masterKeyPath), encryptionAlgorithm, bytes));
            Assert.Matches(ReplaceKeyTokens(errorMsg), ex.Message);
        }

        [Theory]
        [InvalidSigningParameters]
        public void ThrowExceptionWithInvalidParameterWhileSigningColumnMasterKeyMetadata(string errorMsg, Type exceptionType, string masterKeyPath)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.SignColumnMasterKeyMetadata(masterKeyPath, true));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Theory]
        [InlineData("CurrentUser/My/{primary_thumbprint}")]
        [InlineData("CURRENTUSER/My/{primary_thumbprint}")]
        [InlineData("currentuser/My/{primary_thumbprint}")]
        public void SetStoreLocationAppropriatelyFromMasterKeyPathRegardlessOfCase(string masterKeyPath)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(ReplaceKeyTokens(masterKeyPath), ENCRYPTION_ALGORITHM, new byte[] { 1, 2, 3, 4, 5 });
            Assert.NotNull(ciphertext);
        }

        [Theory]
        [InlineData("CurrentUser/my/{primary_thumbprint}")]
        [InlineData("CurrentUser/MY/{primary_thumbprint}")]
        [InlineData("CurrentUser/My/{primary_thumbprint}")]
        public void SetStoreNameAppropriatelyFromMasterKeyPathRegardlessOfCase(string masterKeyPath)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(ReplaceKeyTokens(masterKeyPath), ENCRYPTION_ALGORITHM, new byte[] { 1, 2, 3, 4, 5 });
            Assert.NotNull(ciphertext);
        }

        [Theory]
        [InlineData("RSA_OAEP")]
        [InlineData("rsa_oaep")]
        [InlineData("RsA_oAeP")]
        public void AcceptEncryptionAlgorithmRegardlessOfCase(string algorithm)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), algorithm, new byte[] { 1, 2, 3, 4, 5 });
            Assert.NotNull(ciphertext);
        }

        [Theory]
        [InlineData(32)]
        [InlineData(64)]
        [InlineData(128)]
        public void EncryptKeyAndThenDecryptItSuccessfully(int dataSize)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            var columnEncryptionKey = new byte[dataSize];
            var randomNumberGenerator = new Random();
            randomNumberGenerator.NextBytes(columnEncryptionKey);

            byte[] encryptedData = provider.EncryptColumnEncryptionKey(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), ENCRYPTION_ALGORITHM, columnEncryptionKey);
            byte[] decryptedData = provider.DecryptColumnEncryptionKey(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), ENCRYPTION_ALGORITHM, encryptedData);
            Assert.Equal(columnEncryptionKey, decryptedData);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SignAndVerifyColumnMasterKeyMetadataSuccessfully(bool allowEnclaveComputations)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] signature = provider.SignColumnMasterKeyMetadata(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), allowEnclaveComputations);
            Assert.NotNull(signature);
            Assert.True(provider.VerifyColumnMasterKeyMetadata(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), allowEnclaveComputations, signature));
            Assert.False(provider.VerifyColumnMasterKeyMetadata(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), !allowEnclaveComputations, signature));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FailToVerifyColumnMasterKeyMetadataWithWrongCertificate(bool allowEnclaveComputations)
        {
            var provider = new SqlColumnEncryptionCertificateStoreProvider();

            byte[] signature = provider.SignColumnMasterKeyMetadata(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), allowEnclaveComputations);
            Assert.NotNull(signature);
            Assert.False(
                provider.VerifyColumnMasterKeyMetadata(ReplaceKeyTokens(SECONDARY_CERTIFICATE_PATH), allowEnclaveComputations, signature));
        }

        [Fact]
        public void EncryptAndDecryptDataSuccessfully()
        {
            var input = new byte[] { 1, 2, 3, 4, 5 };
            var provider = new SqlColumnEncryptionCertificateStoreProvider();
            byte[] ciphertext = provider.EncryptColumnEncryptionKey(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), ENCRYPTION_ALGORITHM,
                new byte[] { 1, 2, 3, 4, 5 });
            byte[] output =
                provider.DecryptColumnEncryptionKey(ReplaceKeyTokens(PRIMARY_CERTIFICATE_PATH), ENCRYPTION_ALGORITHM, ciphertext);
            Assert.Equal(input, output);
        }

        [Theory]
        [MemberData(
            nameof(CEKEncryptionReversalData)
#if NETFRAMEWORK
            // .NET Framework puts system enums in something called the Global
            // Assembly Cache (GAC), and xUnit refuses to serialize enums that
            // live there.  So for .NET Framework, we disable enumeration of the
            // test data to avoid warnings on the console when running tests.
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void TestCEKEncryptionReversal(StoreLocation certificateStoreLocation, String certificateStoreNameAndLocation)
        {
            Assert.True(!string.IsNullOrWhiteSpace(certificateStoreNameAndLocation));

            // Fetch the newly created cert.
            X509Certificate2 masterKeyCertificate = _certFixture.GetCertificate(certificateStoreLocation);

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
                    Assert.Fail("unexpected data type.");
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
        public void TestCustomKeyProviderListSetter()
        {
            lock (Utility.ClearSqlConnectionGlobalProvidersLock)
            {
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
                Utility.ClearSqlConnectionGlobalProviders();
            }
        }

        [Theory]
        [MemberData(
            nameof(ValidCertificatePathsData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void TestValidCertificatePaths(string certificateStoreNameAndLocation, object location)
        {
            StoreLocation certificateStoreLocation;

            // Certificate Store Location and Name.
            Assert.True(certificateStoreNameAndLocation != null);

            if (location != null)
            {
                // Certificate Store Location.
                certificateStoreLocation = (StoreLocation)location;
            }
            else
            {
                certificateStoreLocation = StoreLocation.CurrentUser;
            }

            // Fetch the newly created cert.
            X509Certificate2 masterKeyCertificate = _certFixture.GetCertificate(certificateStoreLocation);

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

        private string ReplaceKeyTokens(string keyPath)
        {
            return keyPath?.Replace("{primary_thumbprint}", _certFixture.PrimaryColumnEncryptionCertificate.Thumbprint)
                ?.Replace("{secondary_thumbprint}", _certFixture.SecondaryColumnEncryptionCertificate.Thumbprint)
                ?.Replace("{npk_thumbprint}", _certFixture.CertificateWithoutPrivateKey.Thumbprint);
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

        public static IEnumerable<object[]> CEKEncryptionReversalData()
        {
            yield return new object[2] { StoreLocation.CurrentUser, CurrentUserMyPathPrefix };
            // use localmachine cert path only when current user is Admin.
            if (ColumnEncryptionCertificateFixture.IsAdmin)
            {
                yield return new object[2] { StoreLocation.LocalMachine, LocalMachineMyPathPrefix };
            }
        }

        public static IEnumerable<object[]> ValidCertificatePathsData()
        {
            yield return new object[2] { CurrentUserMyPathPrefix, StoreLocation.CurrentUser };

            // use localmachine cert path (or a location in the cert path which defaults to localmachine) only when current user is Admin.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield return new object[2] { @"", null };
                yield return new object[2] { MyPathPrefix, null };

                if (ColumnEncryptionCertificateFixture.IsAdmin)
                {
                    yield return new object[2] { LocalMachineMyPathPrefix, StoreLocation.LocalMachine };
                }
            }
        }

        public class InvalidDecryptionParameters : DataAttribute
        {
            private const string TCE_NullCertificatePath_Windows = @"Internal error. Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullCertificatePath_Unix = @"Internal error. Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath_Windows = @"Internal error. Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath_Unix = @"Internal error. Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Internal error. Encrypted column encryption key cannot be null.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Internal error. Empty encrypted column encryption key specified.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Internal error. Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Internal error. Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_LargeCertificatePathLength = @"Internal error. Specified certificate path has 32768 bytes, which exceeds maximum length of 32767 bytes.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificatePath_Windows = @"Internal error. Invalid certificate path: 'CurrentUser/My/Thumbprint/extra'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificatePath_Unix = @"Internal error. Invalid certificate path: 'CurrentUser/My/Thumbprint/extra'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateLocation_Windows = @"Internal error. Invalid certificate location 'Invalid' in certificate path 'Invalid/My/Thumbprint'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateLocation_Unix = @"Internal error. Invalid certificate location 'Invalid' in certificate path 'Invalid/My/Thumbprint'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateStore = @"Internal error. Invalid certificate store 'Invalid' specified in certificate path 'CurrentUser/Invalid/Thumbprint'. Expected value: 'My'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_CertificateNotFound = @"Certificate with thumbprint 'JunkThumbprint' not found in certificate store 'My' in certificate location 'CurrentUser'. Verify the certificate path in the column master key definition in the database is correct, and the certificate has been imported correctly into the certificate location/store.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_CertificateWithNoPrivateKey = @"Certificate specified in key path 'CurrentUser/My/{npk_thumbprint}' does not have a private key to decrypt a column encryption key. Verify the certificate is imported correctly.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateSignature = @"Internal error. Empty certificate thumbprint specified in certificate path 'CurrentUser/My/'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidAlgorithmVersion = @"Specified encrypted column encryption key contains an invalid encryption algorithm version '02'. Expected version is '01'.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidCiphertextLengthInEncryptedCEK = @"The specified encrypted column encryption key's ciphertext length: 128 does not match the ciphertext length: 256 when using column master key \(certificate\) in 'CurrentUser/My/{primary_thumbprint}'. The encrypted column encryption key may be corrupt, or the specified certificate path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignatureInEncryptedCEK = @"The specified encrypted column encryption key's signature length: 128 does not match the signature length: 256 when using column master key \(certificate\) in 'CurrentUser/My/{primary_thumbprint}'. The encrypted column encryption key may be corrupt, or the specified certificate path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignature = @"The specified encrypted column encryption key signature does not match the signature computed with the column master key \(certificate\) in 'CurrentUser/My/{primary_thumbprint}'. The encrypted column encryption key may be corrupt, or the specified path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";

            private static readonly string TCE_NullCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_NullCertificatePath_Windows : TCE_NullCertificatePath_Unix;
            private static readonly string TCE_EmptyCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_EmptyCertificatePath_Windows : TCE_EmptyCertificatePath_Unix;
            private static readonly string TCE_InvalidCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_InvalidCertificatePath_Windows : TCE_InvalidCertificatePath_Unix;
            private static readonly string TCE_InvalidCertificateLocation = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_InvalidCertificateLocation_Windows : TCE_InvalidCertificateLocation_Unix;

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCertificatePath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCertificatePath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), PRIMARY_CERTIFICATE_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_LargeCertificatePathLength, typeof(ArgumentException), GenerateString(Int16.MaxValue + 1), ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificatePath, typeof(ArgumentException), "CurrentUser/My/Thumbprint/extra", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateLocation, typeof(ArgumentException), "Invalid/My/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateStore, typeof(ArgumentException), "CurrentUser/Invalid/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_CertificateNotFound, typeof(ArgumentException), "CurrentUser/My/JunkThumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_CertificateWithNoPrivateKey, typeof(ArgumentException), "CurrentUser/My/{npk_thumbprint}", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateSignature, typeof(ArgumentException), "CurrentUser/My/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidAlgorithmVersion, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(2, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCiphertextLengthInEncryptedCEK, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 128, 256) };
                yield return new Object[] { TCE_InvalidSignatureInEncryptedCEK, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 128) };
                yield return new Object[] { TCE_InvalidSignature, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }

        public class InvalidEncryptionParameters : DataAttribute
        {
            private const string TCE_NullCertificatePath_Windows = @"Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullCertificatePath_Unix = @"Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath_Windows = @"Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath_Unix = @"Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Column encryption key cannot be null.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Empty column encryption key specified.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_LargeCertificatePathLength = @"Specified certificate path has 32768 bytes, which exceeds maximum length of 32767 bytes.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificatePath_Windows = @"Invalid certificate path: 'CurrentUser/My/Thumbprint/extra'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificatePath_Unix = @"Invalid certificate path: 'CurrentUser/My/Thumbprint/extra'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateLocation_Windows = @"Invalid certificate location 'Invalid' in certificate path 'Invalid/My/Thumbprint'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateLocation_Unix = @"Invalid certificate location 'Invalid' in certificate path 'Invalid/My/Thumbprint'. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateStore = @"Invalid certificate store 'Invalid' specified in certificate path 'CurrentUser/Invalid/Thumbprint'. Expected value: 'My'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_CertificateNotFound = @"Certificate with thumbprint 'JunkThumbprint' not found in certificate store 'My' in certificate location 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_CertificateWithNoPrivateKey = @"Certificate specified in key path 'CurrentUser/My/{npk_thumbprint}' does not have a private key to encrypt a column encryption key. Verify the certificate is imported correctly.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCertificateSignature = @"Empty certificate thumbprint specified in certificate path 'CurrentUser/My/'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";

            private static readonly string TCE_NullCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_NullCertificatePath_Windows : TCE_NullCertificatePath_Unix;
            private static readonly string TCE_EmptyCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_EmptyCertificatePath_Windows : TCE_EmptyCertificatePath_Unix;
            private static readonly string TCE_InvalidCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_InvalidCertificatePath_Windows : TCE_InvalidCertificatePath_Unix;
            private static readonly string TCE_InvalidCertificateLocation = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_InvalidCertificateLocation_Windows : TCE_InvalidCertificateLocation_Unix;

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCertificatePath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCertificatePath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), PRIMARY_CERTIFICATE_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), PRIMARY_CERTIFICATE_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_LargeCertificatePathLength, typeof(ArgumentException), GenerateString(Int16.MaxValue + 1), ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificatePath, typeof(ArgumentException), "CurrentUser/My/Thumbprint/extra", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateLocation, typeof(ArgumentException), "Invalid/My/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateStore, typeof(ArgumentException), "CurrentUser/Invalid/Thumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_CertificateNotFound, typeof(ArgumentException), "CurrentUser/My/JunkThumbprint", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_CertificateWithNoPrivateKey, typeof(ArgumentException), "CurrentUser/My/{npk_thumbprint}", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCertificateSignature, typeof(ArgumentException), "CurrentUser/My/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }

        public class InvalidSigningParameters : DataAttribute
        {
            private const string TCE_NullCertificatePath_Windows = @"Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullCertificatePath_Unix = @"Certificate path cannot be null. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath_Windows = @"Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is either 'LocalMachine' or 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath_Unix = @"Invalid certificate path: ''. Use the following format: <certificate location>/<certificate store>/<certificate thumbprint>, where <certificate location> is 'CurrentUser'.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_LargeCertificatePathLength = @"Specified certificate path has 32768 bytes, which exceeds maximum length of 32767 bytes.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";

            private static readonly string TCE_NullCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_NullCertificatePath_Windows : TCE_NullCertificatePath_Unix;
            private static readonly string TCE_EmptyCertificatePath = Environment.OSVersion.Platform == PlatformID.Win32NT ? TCE_EmptyCertificatePath_Windows : TCE_EmptyCertificatePath_Unix;

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
}
