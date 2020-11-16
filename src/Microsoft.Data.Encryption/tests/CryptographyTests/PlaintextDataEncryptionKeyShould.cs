using Microsoft.Data.Encryption.Cryptography;
using Xunit;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests
{
    public class PlaintextDataEncryptionKeyShould
    {
        [Fact]
        public void PerformEqualityCorrectly()
        {
            DataEncryptionKey encryptionKey1 = new PlaintextDataEncryptionKey("CEK", plaintextEncryptionKeyBytes);
            DataEncryptionKey encryptionKey2 = new PlaintextDataEncryptionKey("CEK", plaintextEncryptionKeyBytes);

            Assert.Equal(encryptionKey1, encryptionKey2);
        }

        [Fact]
        public void PerformHashCodeCorrectly()
        {
            DataEncryptionKey encryptionKey1 = new PlaintextDataEncryptionKey("CEK", plaintextEncryptionKeyBytes);
            DataEncryptionKey encryptionKey2 = new PlaintextDataEncryptionKey("CEK", plaintextEncryptionKeyBytes);

            Assert.Equal(encryptionKey1.GetHashCode(), encryptionKey2.GetHashCode());
        }

        [Fact]
        public void CacheEncryptionKeyCorrectlyWhenCallingGetOrCreate()
        {

            byte[] plaintextKey1 = { 26, 60, 114, 103, 139, 37, 229, 66, 170, 179, 244, 229, 233, 102, 44, 186, 234, 9, 5, 211, 216, 143, 103, 144, 252, 254, 96, 111, 233, 1, 149, 240 };
            byte[] plaintextKey2 = { 26, 60, 114, 103, 139, 37, 229, 66, 170, 179, 244, 229, 233, 102, 44, 186, 234, 9, 5, 211, 216, 143, 103, 144, 252, 254, 96, 111, 233, 1, 149, 240 };
            byte[] plaintextKey3 = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

            DataEncryptionKey encryptionkey1 = PlaintextDataEncryptionKey.GetOrCreate("EK", plaintextKey1);
            DataEncryptionKey encryptionkey2 = PlaintextDataEncryptionKey.GetOrCreate("EK", plaintextKey1);

            Assert.Same(encryptionkey1, encryptionkey2);

            DataEncryptionKey encryptionkey3 = PlaintextDataEncryptionKey.GetOrCreate("EK", plaintextKey1);
            DataEncryptionKey encryptionkey4 = PlaintextDataEncryptionKey.GetOrCreate("EK", plaintextKey2);

            Assert.Same(encryptionkey3, encryptionkey4);

            DataEncryptionKey encryptionkey5 = PlaintextDataEncryptionKey.GetOrCreate("EK", plaintextKey1);
            DataEncryptionKey encryptionkey6 = PlaintextDataEncryptionKey.GetOrCreate("Not_EK", plaintextKey1);

            Assert.NotSame(encryptionkey5, encryptionkey6);

            DataEncryptionKey encryptionkey7 = PlaintextDataEncryptionKey.GetOrCreate("EK", plaintextKey1);
            DataEncryptionKey encryptionkey8 = PlaintextDataEncryptionKey.GetOrCreate("EK", plaintextKey3);

            Assert.NotSame(encryptionkey7, encryptionkey8);
        }
    }
}
