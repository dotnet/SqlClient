using Microsoft.Data.Encryption.Cryptography;
using Moq;
using Xunit;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests
{
    public class KeyEncryptionKeyShould
    {
        [Fact]
        public void PreformEqualityCorrectly()
        {
            KeyEncryptionKey masterKey1 = new KeyEncryptionKey("CMK", keyEncryptionKeyPath, azureKeyProvider);
            KeyEncryptionKey masterKey2 = new KeyEncryptionKey("CMK", keyEncryptionKeyPath, azureKeyProvider);

            Assert.Equal(masterKey1, masterKey2);
        }

        [Fact]
        public void PreformHashCodeCorrectly()
        {
            KeyEncryptionKey masterKey1 = new KeyEncryptionKey("CMK", keyEncryptionKeyPath, azureKeyProvider);
            KeyEncryptionKey masterKey2 = new KeyEncryptionKey("CMK", keyEncryptionKeyPath, azureKeyProvider);

            Assert.Equal(masterKey1.GetHashCode(), masterKey2.GetHashCode());
        }

        [Fact]
        public void LazilyGenerateSignature()
        {
            const string masterKeyPath = "Test_Path";
            const bool masterKeyIsEnclaveEnabled = true;

            Mock<EncryptionKeyStoreProvider> mockKeyStoreProvider = new Mock<EncryptionKeyStoreProvider>();
            KeyEncryptionKey masterKey = new KeyEncryptionKey("Test_MK", "Test_Path", mockKeyStoreProvider.Object, masterKeyIsEnclaveEnabled);

            mockKeyStoreProvider.Verify(m => m.Sign(masterKeyPath, masterKeyIsEnclaveEnabled), Times.Never);

            byte[] signature = masterKey.Signature;
            mockKeyStoreProvider.Verify(m => m.Sign(masterKeyPath, masterKeyIsEnclaveEnabled), Times.Once);

            signature = masterKey.Signature;
            mockKeyStoreProvider.Verify(m => m.Sign(masterKeyPath, masterKeyIsEnclaveEnabled), Times.Once);
        }

        [Fact]
        public void CacheMasterKeyCorrectlyWhenCallingGetOrCreate()
        {
            Mock<EncryptionKeyStoreProvider> mockKeyStoreProvider1 = new Mock<EncryptionKeyStoreProvider>();
            Mock<EncryptionKeyStoreProvider> mockKeyStoreProvider2 = new Mock<EncryptionKeyStoreProvider>();


            KeyEncryptionKey encryptionkey1 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider1.Object, true);
            KeyEncryptionKey encryptionkey2 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider1.Object, true);
            Assert.Same(encryptionkey1, encryptionkey2);

            KeyEncryptionKey encryptionkey3 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider1.Object, true);
            KeyEncryptionKey encryptionkey4 = KeyEncryptionKey.GetOrCreate(new string("Test_MK_Other"), new string("Test_Path"), mockKeyStoreProvider1.Object, true);
            Assert.NotSame(encryptionkey3, encryptionkey4);

            KeyEncryptionKey encryptionkey5 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider1.Object, true);
            KeyEncryptionKey encryptionkey6 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path_Other"), mockKeyStoreProvider1.Object, true);
            Assert.NotSame(encryptionkey5, encryptionkey6);

            KeyEncryptionKey encryptionkey7 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider1.Object, true);
            KeyEncryptionKey encryptionkey8 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider2.Object, true);
            Assert.NotSame(encryptionkey7, encryptionkey8);

            KeyEncryptionKey encryptionkey9 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider1.Object, true);
            KeyEncryptionKey encryptionkey10 = KeyEncryptionKey.GetOrCreate(new string("Test_MK"), new string("Test_Path"), mockKeyStoreProvider1.Object, false);
            Assert.NotSame(encryptionkey9, encryptionkey10);

            KeyEncryptionKey encryptionkey11 = KeyEncryptionKey.GetOrCreate(new string("Test_MK_Other"), new string("Test_Path_Other"), mockKeyStoreProvider2.Object, false);
            KeyEncryptionKey encryptionkey12 = KeyEncryptionKey.GetOrCreate(new string("Test_MK_Other"), new string("Test_Path_Other"), mockKeyStoreProvider2.Object, false);
            Assert.Same(encryptionkey11, encryptionkey12);
        }
    }
}
