using Microsoft.Data.Encryption.Cryptography;
using System;
using Xunit;

namespace Microsoft.Data.Encryption.Cryptography.Tests
{
    public class EncryptionKeyStoreProviderShould
    {
        [Fact]
        public void CacheEncryptionKeyCorrectlyWhenCallingGetOrCreate()
        {
            TestProvider testProvider = new TestProvider();

            byte[] key1 = { 1, 2, 3 };
            byte[] key2 = { 1, 2, 3 };
            byte[] key3 = { 0, 1, 2, 3 };

            byte[] value1 = testProvider.UnwrapKey("path", KeyEncryptionKeyAlgorithm.RSA_OAEP, key1);
            byte[] value2 = testProvider.UnwrapKey("path", KeyEncryptionKeyAlgorithm.RSA_OAEP, key2);

            Assert.Same(value1, value2);

            byte[] value3 = testProvider.UnwrapKey("path", KeyEncryptionKeyAlgorithm.RSA_OAEP, key1);
            byte[] value4 = testProvider.UnwrapKey("path", KeyEncryptionKeyAlgorithm.RSA_OAEP, key3);

            Assert.NotSame(value3, value4);

            byte[] value5 = testProvider.UnwrapKey("path", KeyEncryptionKeyAlgorithm.RSA_OAEP, key2);
            byte[] value6 = testProvider.UnwrapKey("path", KeyEncryptionKeyAlgorithm.RSA_OAEP, key3);

            Assert.NotSame(value5, value6);
        }

        private class TestProvider : EncryptionKeyStoreProvider
        {
            private static byte encryptionKeyCount = 0;

            public override string ProviderName => "TestProvider";

            public override byte[] UnwrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
            {
                return GetOrCreateDataEncryptionKey(encryptedColumnEncryptionKey.ToHexString(), DecryptEncryptionKey);

                static byte[] DecryptEncryptionKey()
                {
                    return new byte[] { encryptionKeyCount++ };
                }
            }

            public override byte[] WrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] columnEncryptionKey)
            {
                throw new NotImplementedException();
            }

            public override byte[] Sign(string masterKeyPath, bool allowEnclaveComputations)
            {
                throw new NotImplementedException();
            }

            public override bool Verify(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
            {
                var key = Tuple.Create(ProviderName, masterKeyPath, allowEnclaveComputations, signature.ToHexString());
                return GetOrCreateSignatureVerificationResult(key, VerifyMasterKeyMetadata);

                static bool VerifyMasterKeyMetadata()
                {
                    return true;
                }
            }
        }
    }
}
