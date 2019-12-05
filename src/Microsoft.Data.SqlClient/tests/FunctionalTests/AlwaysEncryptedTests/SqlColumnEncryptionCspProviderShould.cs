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
    public class SqlColumnEncryptionCspProviderWindowsShould : IClassFixture<CspFixture>
    {
        private const string MASTER_KEY_PATH = "Microsoft Enhanced RSA and AES Cryptographic Provider/KeyName";
        private const string ENCRYPTION_ALGORITHM = "RSA_OAEP";
        private const string DUMMY_KEY = "ASKLSAVASLDJAS";

        [Theory]
        [InvalidDecryptionParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowExceptionWithInvalidParameterWhileDecryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCspProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.DecryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, bytes));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Theory]
        [InvalidEncryptionParameters]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowExceptionWithInvalidParameterWhileEncryptingColumnEncryptionKey(string errorMsg, Type exceptionType, string masterKeyPath, string encryptionAlgorithm, byte[] bytes)
        {
            var provider = new SqlColumnEncryptionCspProvider();
            Exception ex = Assert.Throws(exceptionType, () => provider.EncryptColumnEncryptionKey(masterKeyPath, encryptionAlgorithm, bytes));
            Assert.Matches(errorMsg, ex.Message);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowNotSupportedExceptionWhenCallingSignColumnMasterKeyMetadata()
        {
            var provider = new SqlColumnEncryptionCspProvider();
            Assert.Throws<NotSupportedException>(() => provider.SignColumnMasterKeyMetadata(MASTER_KEY_PATH, true));
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void ThrowNotSupportedExceptionWhenCallingVerifyColumnMasterKeyMetadata()
        {
            var provider = new SqlColumnEncryptionCspProvider();
            Assert.Throws<NotSupportedException>(() => provider.VerifyColumnMasterKeyMetadata(MASTER_KEY_PATH, true, GenerateTestEncryptedBytes(1, 0, 256, 256)));
        }

        [Theory]
        [InlineData("RSA_OAEP")]
        [InlineData("rsa_oaep")]
        [InlineData("RsA_oAeP")]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void AcceptEncryptionAlgorithmRegardlessOfCase(string algorithm)
        {
            var provider = new SqlColumnEncryptionCspProvider();
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
            var provider = new SqlColumnEncryptionCspProvider();
            var columnEncryptionKey = new byte[dataSize];
            var randomNumberGenerator = new Random();
            randomNumberGenerator.NextBytes(columnEncryptionKey);
            byte[] encryptedData = provider.EncryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, columnEncryptionKey);
            byte[] decryptedData = provider.DecryptColumnEncryptionKey(MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, encryptedData);
            Assert.Equal(columnEncryptionKey, decryptedData);
        }

        public class InvalidDecryptionParameters : DataAttribute
        {
            private const string TCE_NullCspPath = @"Internal error. Column master key path cannot be null. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCspPath = @"Internal error. Invalid column master key path: ''. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Internal error. Encrypted column encryption key cannot be null.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Internal error. Empty encrypted column encryption key specified.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Internal error. Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Internal error. Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidCspPath = @"Internal error. Invalid column master key path: 'KeyName'. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCspName = @"Internal error. Empty Microsoft cryptographic service provider \(CSP\) name specified in column master key path: '/KeyName'. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCspKeyId = @"Internal error. Empty key identifier specified in column master key path: 'MSSQL_CSP_PROVIDER/'. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCspKey = @"Internal error. Invalid Microsoft cryptographic service provider \(CSP\) name: 'MSSQL_CSP_PROVIDER'. Verify that the CSP provider name in column master key path: 'MSSQL_CSP_PROVIDER/KeyName' is valid and installed on the machine.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidAlgorithmVersion = @"Specified encrypted column encryption key contains an invalid encryption algorithm version '02'. Expected version is '01'.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidCiphertextLengthInEncryptedCEK = @"The specified encrypted column encryption key's ciphertext length: 128 does not match the ciphertext length: 256 when using column master key \(asymmetric key\) in 'Microsoft Enhanced RSA and AES Cryptographic Provider/KeyName'. The encrypted column encryption key may be corrupt, or the specified Microsoft Cryptographic Service provider \(CSP\) path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignatureInEncryptedCEK = @"The specified encrypted column encryption key's signature length: 128 does not match the signature length: 256 when using column master key \(asymmetric key\) in 'Microsoft Enhanced RSA and AES Cryptographic Provider/KeyName'. The encrypted column encryption key may be corrupt, or the specified Microsoft cryptographic service provider \(CSP\) path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private const string TCE_InvalidSignature = @"The specified encrypted column encryption key signature does not match the signature computed with the column master key \(asymmetric key\) in 'Microsoft Enhanced RSA and AES Cryptographic Provider/KeyName'. The encrypted column encryption key may be corrupt, or the specified path may be incorrect.\s+\(?Parameter (name: )?'?encryptedColumnEncryptionKey('\))?";
            private string TCE_InvalidCspKeyId = $@"Internal error. Invalid key identifier: 'KeyName/{DUMMY_KEY}'. Verify that the key identifier in column master key path: 'Microsoft Enhanced RSA and AES Cryptographic Provider/KeyName/{DUMMY_KEY}' is valid and exists in the CSP.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCspPath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCspPath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), MASTER_KEY_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), MASTER_KEY_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCspPath, typeof(ArgumentException), "KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCspName, typeof(ArgumentException), "/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCspKeyId, typeof(ArgumentException), "MSSQL_CSP_PROVIDER/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCspKeyId, typeof(ArgumentException), $"{MASTER_KEY_PATH}/{DUMMY_KEY}", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCspKey, typeof(ArgumentException), "MSSQL_CSP_PROVIDER/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidAlgorithmVersion, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(2, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCiphertextLengthInEncryptedCEK, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 128, 256) };
                yield return new Object[] { TCE_InvalidSignatureInEncryptedCEK, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 128) };
                yield return new Object[] { TCE_InvalidSignature, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }

        public class InvalidEncryptionParameters : DataAttribute
        {
            private const string TCE_NullCspPath = @"Column master key path cannot be null. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCspPath = @"Invalid column master key path: ''. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_NullEncryptedColumnEncryptionKey = @"Column encryption key cannot be null.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_EmptyEncryptedColumnEncryptionKey = @"Empty column encryption key specified.\s+\(?Parameter (name: )?'?columnEncryptionKey('\))?";
            private const string TCE_NullKeyEncryptionAlgorithm = @"Key encryption algorithm cannot be null.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidKeyEncryptionAlgorithm = @"Invalid key encryption algorithm specified: ''. Expected value: 'RSA_OAEP'.\s+\(?Parameter (name: )?'?encryptionAlgorithm('\))?";
            private const string TCE_InvalidCspPath = @"Invalid column master key path: 'KeyName'. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCspName = @"Empty Microsoft cryptographic service provider \(CSP\) name specified in column master key path: '/KeyName'. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_EmptyCspKeyId = @"Empty key identifier specified in column master key path: 'MSSQL_CSP_PROVIDER/'. Use the following format for a key stored in a Microsoft cryptographic service provider \(CSP\): <CSP Provider Name>\/<Key Identifier>.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private const string TCE_InvalidCspKey = @"Invalid Microsoft cryptographic service provider \(CSP\) name: 'MSSQL_CSP_PROVIDER'. Verify that the CSP provider name in column master key path: 'MSSQL_CSP_PROVIDER/KeyName' is valid and installed on the machine.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";
            private string TCE_InvalidCspKeyId = $@"Invalid key identifier: 'KeyName/{DUMMY_KEY}'. Verify that the key identifier in column master key path: '{MASTER_KEY_PATH}/{DUMMY_KEY}' is valid and exists in the CSP.\s+\(?Parameter (name: )?'?masterKeyPath('\))?";

            public override IEnumerable<Object[]> GetData(MethodInfo testMethod)
            {
                yield return new Object[] { TCE_NullCspPath, typeof(ArgumentNullException), null, ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCspPath, typeof(ArgumentException), "", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_NullEncryptedColumnEncryptionKey, typeof(ArgumentNullException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, null };
                yield return new Object[] { TCE_EmptyEncryptedColumnEncryptionKey, typeof(ArgumentException), MASTER_KEY_PATH, ENCRYPTION_ALGORITHM, new byte[] { } };
                yield return new Object[] { TCE_NullKeyEncryptionAlgorithm, typeof(ArgumentNullException), MASTER_KEY_PATH, null, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidKeyEncryptionAlgorithm, typeof(ArgumentException), MASTER_KEY_PATH, "", GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCspPath, typeof(ArgumentException), "KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCspName, typeof(ArgumentException), "/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_EmptyCspKeyId, typeof(ArgumentException), "MSSQL_CSP_PROVIDER/", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCspKeyId, typeof(ArgumentException), $"{MASTER_KEY_PATH}/{DUMMY_KEY}", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
                yield return new Object[] { TCE_InvalidCspKey, typeof(ArgumentException), "MSSQL_CSP_PROVIDER/KeyName", ENCRYPTION_ALGORITHM, GenerateTestEncryptedBytes(1, 0, 256, 256) };
            }
        }
    }

    public class SqlColumnEncryptionCspProviderUnixShould
    {
        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        public void ThrowPlatformNotSupportedExceptionInUnix()
        {
            var provider = new SqlColumnEncryptionCspProvider();
            Assert.Throws<PlatformNotSupportedException>(() => provider.EncryptColumnEncryptionKey("", "", new byte[] { }));
            Assert.Throws<PlatformNotSupportedException>(() => provider.DecryptColumnEncryptionKey("", "", new byte[] { }));
            Assert.Throws<PlatformNotSupportedException>(() => provider.SignColumnMasterKeyMetadata("", false));
            Assert.Throws<PlatformNotSupportedException>(() => provider.VerifyColumnMasterKeyMetadata("", false, new byte[] { }));
        }
    }

    public class CspFixture : IDisposable
    {
        private const string containerName = "KeyName";
        private const int KEY_SIZE = 2048;

        public CspFixture()
        {
            AddKeyToCsp(containerName);
        }

        public void Dispose()
        {
            // Do Not remove Key for concurrency.
            // RemoveKeyFromCsp(containerName);
        }

        public static void AddKeyToCsp(string containerName)
        {
            CspParameters cspParams = new CspParameters();
            cspParams.KeyContainerName = containerName;
            RSACryptoServiceProvider rsaAlg = new RSACryptoServiceProvider(KEY_SIZE, cspParams);
            rsaAlg.PersistKeyInCsp = true;
        }

        public static void RemoveKeyFromCsp(string containerName)
        {
            CspParameters cspParams = new CspParameters();
            cspParams.KeyContainerName = containerName;
            RSACryptoServiceProvider rsaAlg = new RSACryptoServiceProvider(cspParams);
            rsaAlg.PersistKeyInCsp = false;
            rsaAlg.Clear();
        }
    }
}
