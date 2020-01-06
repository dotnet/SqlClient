// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Xunit;
using Xunit.Sdk;
using static Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests.TestFixtures;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    public class SqlColumnEncryptionCngProviderWindowsShould : IClassFixture<CngFixture>
    {
        private const string MASTER_KEY_PATH = "Microsoft Software Key Storage Provider/KeyName";
        private const string INVALID_MASTER_KEY = "Microsoft Software Key Storage Provider/ASKLSAVASLDJAS";
        private const string ENCRYPTION_ALGORITHM = "RSA_OAEP";

        [Theory]
        [InvalidDecryptionParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowExceptionWithInvalidParameterWhileDecryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCngProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.DecryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, bytes));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Theory]
        [InvalidEncryptionParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowExceptionWithInvalidParameterWhileEncryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCngProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.EncryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, bytes));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowNotSupportedExceptionWhenCallingSignColumnMasterKeyMetadata()
        {
            var provider = new SqlColumnEncryptionCngProvider();
            Assert.Throws<NotSupportedException>(() => provider.SignColumnMasterKeyMetadata(MASTER_KEY_PATH, true));
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowNotSupportedExceptionWhenCallingVerifyColumnMasterKeyMetadata()
        {
            var provider = new SqlColumnEncryptionCngProvider();
            Assert.Throws<NotSupportedException>(() => provider.VerifyColumnMasterKeyMetadata(MASTER_KEY_PATH, true, GenerateTestEncryptedBytes(1, 0, 256, 256)));
        }

        [Theory]
        [InlineData("RSA_OAEP")]
        [InlineData("rsa_oaep")]
        [InlineData("RsA_oAeP")]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void AcceptEncryptionAlgorithmRegardlessOfCase(string algorithm)
        {
            var provider = new SqlColumnEncryptionCngProvider();
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
            var provider = new SqlColumnEncryptionCngProvider();
            var columnEncryptionKey = new byte[dataSize];
            var randomNumberGenerator = new Random();
            randomNumberGenerator.NextBytes(columnEncryptionKey);
            byte[] encryptedData = provider.EncryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, columnEncryptionKey);
            byte[] decryptedData = provider.DecryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, encryptedData);
            Assert.Equal(columnEncryptionKey, decryptedData);
        }

        public class InvalidDecryptionParameters : DataAttribute
        {
            private const string TCE_NullCngPath = @"Internal error. Column master key path cannot be null. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCngPath = @"Internal error. Invalid column master key path: ''. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Internal error. Encrypted column encryption key cannot be null.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Internal error. Empty encrypted column encryption key specified.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Internal error. Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Internal error. Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidCngPath = @"Internal error. Invalid column master key path: 'KeyName'. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCngName = @"Internal error. Empty Microsoft Cryptography API: Next Generation \(CNG\) provider name specified in column master key path: '/KeyName'. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCngKeyId = @"Internal error. Empty key identifier specified in column master key path: 'MSSQL_CNG_STORE/'. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCngKey = @"Internal error. An error occurred while opening the Microsoft Cryptography API: Next Generation \(CNG\) key: 'MSSQL_CNG_STORE/KeyName'. Verify that the CNG provider name 'MSSQL_CNG_STORE' is valid, installed on the machine, and the key 'KeyName' exists.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidAlgorithmVersion = @"Specified encrypted column encryption key contains an invalid encryption algorithm version '02'. Expected version is '01'.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidCiphertextLengthInEncryptedCEK = @"The specified encrypted column encryption key's ciphertext length: 128 does not match the ciphertext length: 256 when using column master key \(asymmetric key\) in 'Microsoft Software Key Storage Provider/KeyName'. The encrypted column encryption key may be corrupt, or the specified Microsoft Cryptography API: Next Generation \(CNG\) provider path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignatureInEncryptedCEK = @"The specified encrypted column encryption key's signature length: 128 does not match the signature length: 256 when using column master key \(asymmetric key\) in 'Microsoft Software Key Storage Provider/KeyName'. The encrypted column encryption key may be corrupt, or the specified Microsoft Cryptography API: Next Generation \(CNG\) provider path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignature = @"The specified encrypted column encryption key signature does not match the signature computed with the column master key \(asymmetric key\) in 'Microsoft Software Key Storage Provider/KeyName'. The encrypted column encryption key may be corrupt, or the specified path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidCngKeyId = @"Internal error. An error occurred while opening the Microsoft Cryptography API: Next Generation \(CNG\) key: 'Microsoft Software Key Storage Provider/ASKLSAVASLDJAS'. Verify that the CNG provider name 'Microsoft Software Key Storage Provider' is valid, installed on the machine, and the key 'ASKLSAVASLDJAS' exists.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCngPath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCngPath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), MASTER_KEY_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), MASTER_KEY_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCngPath, typeof(ArgumentException), "KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCngName, typeof(ArgumentException), "/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCngKeyId, typeof(ArgumentException), "MSSQL_CNG_STORE/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCngKey, typeof(ArgumentException), "MSSQL_CNG_STORE/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidAlgorithmVersion, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(2, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCiphertextLengthInEncryptedCEK, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 128, 256) };
                yield return new Object[] { TCE_InvalidSignatureInEncryptedCEK, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 128) };
                yield return new Object[] { TCE_InvalidSignature, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new object[] { TCE_InvalidCngKeyId, typeof(ArgumentException), INVALID_MASTER_KEY, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }

        public class InvalidEncryptionParameters : DataAttribute
        {
            private const string TCE_NullCertificatePath = @"Column master key path cannot be null. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCertificatePath = @"Invalid column master key path: ''. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Column encryption key cannot be null.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Empty column encryption key specified.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidCngPath = @"Invalid column master key path: 'KeyName'. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCngName = @"Empty Microsoft Cryptography API: Next Generation \(CNG\) provider name specified in column master key path: '/KeyName'. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCngKeyId = @"Empty key identifier specified in column master key path: 'MSSQL_CNG_STORE/'. Use the following format for a key stored in a Microsoft Cryptography API: Next Generation \(CNG\) provider: <CNG Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCngKey = @"An error occurred while opening the Microsoft Cryptography API: Next Generation \(CNG\) key: 'MSSQL_CNG_STORE/KeyName'. Verify that the CNG provider name 'MSSQL_CNG_STORE' is valid, installed on the machine, and the key 'KeyName' exists.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCngKeyId = @"An error occurred while opening the Microsoft Cryptography API: Next Generation \(CNG\) key: 'Microsoft Software Key Storage Provider/ASKLSAVASLDJAS'. Verify that the CNG provider name 'Microsoft Software Key Storage Provider' is valid, installed on the machine, and the key 'ASKLSAVASLDJAS' exists.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCertificatePath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCertificatePath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), MASTER_KEY_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), MASTER_KEY_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCngPath, typeof(ArgumentException), "KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCngName, typeof(ArgumentException), "/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCngKeyId, typeof(ArgumentException), "MSSQL_CNG_STORE/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCngKey, typeof(ArgumentException), "MSSQL_CNG_STORE/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCngKeyId, typeof(ArgumentException), INVALID_MASTER_KEY, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }
    }

    public class SqlColumnEncryptionCngProviderUnixShould
    {
        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public void ThrowPlatformNotSupportedExceptionInUnix()
        {
            var provider = new SqlColumnEncryptionCngProvider();
            Assert.Throws<PlatformNotSupportedException>(() => provider.EncryptColumnEncryptionKey("", "", new byte[] { }));
            Assert.Throws<PlatformNotSupportedException>(() => provider.DecryptColumnEncryptionKey("", "", new byte[] { }));
            Assert.Throws<PlatformNotSupportedException>(() => provider.SignColumnMasterKeyMetadata("", false));
            Assert.Throws<PlatformNotSupportedException>(() => provider.VerifyColumnMasterKeyMetadata("", false, new byte[] { }));
        }
    }

    public class CngFixture : IDisposable
    {
        private const string providerName = "Microsoft Software Key Storage Provider";
        private const string containerName = "KeyName";

        public CngFixture()
        {
            AddKeyToCng(providerName, containerName);
        }

        public void Dispose()
        {
            // Do Not remove Key for concurrency.
            // RemoveKeyFromCng(providerName, containerName);
        }

        public static void AddKeyToCng(string providerName, string containerName)
        {
            CngKeyCreationParameters keyParams = new CngKeyCreationParameters();

            keyParams.Provider = new CngProvider(providerName);
            keyParams.KeyCreationOptions = CngKeyCreationOptions.None;

            CngProperty keySizeProperty = new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None);
            keyParams.Parameters.Add(keySizeProperty);

            // Add Cng Key only if not exists.
            if (!CngKey.Exists(containerName))
            {
                CngKey mycngKey = CngKey.Create(CngAlgorithm.Rsa, containerName, keyParams);
            }
        }

        public static void RemoveKeyFromCng(string providerName, string containerName)
        {
            CngProvider cngProvider = new CngProvider(providerName);

            CngKey cngKey = CngKey.Open(containerName, cngProvider);
            cngKey.Delete();
        }
    }
}
