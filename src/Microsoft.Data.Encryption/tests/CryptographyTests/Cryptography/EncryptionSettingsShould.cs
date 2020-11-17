using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using Xunit;

using static Microsoft.Data.CommonTestUtilities.DataTestUtility;

namespace Microsoft.Data.Encryption.CryptographyTests.Cryptography
{
    public class EncryptionSettingsShould
    {
        [Fact]
        public void DefaultToPlaintextWhenKeyIsNullInConstructor()
        {
            EncryptionSettings encryptionSettings = new EncryptionSettings<byte>(null, StandardSerializerFactory.Default.GetDefaultSerializer<byte>());

            Assert.Equal(EncryptionType.Plaintext, encryptionSettings.EncryptionType);
        }


        [Fact]
        public void DefaultToRandomizedWhenKeyIsNotNullInConstructor()
        {
            EncryptionSettings encryptionSettings = new EncryptionSettings<byte>(dataEncryptionKey, StandardSerializerFactory.Default.GetDefaultSerializer<byte>());

            Assert.Equal(EncryptionType.Randomized, encryptionSettings.EncryptionType);
        }

        [Fact]
        public void EqualsWorksAsExpected()
        {
            EncryptionSettings encryptionSettings = new EncryptionSettings<byte>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<byte>());

            Assert.False(encryptionSettings.Equals(new EncryptionSettings<bool>(dataEncryptionKey, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<bool>())));
            Assert.False(encryptionSettings.Equals(new EncryptionSettings<byte>(dataEncryptionKey, EncryptionType.Randomized, StandardSerializerFactory.Default.GetDefaultSerializer<byte>())));
            Assert.False(encryptionSettings.Equals(new EncryptionSettings<byte>(new ProtectedDataEncryptionKey("CEK", keyEncryptionKey), EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<byte>())));
            Assert.False(encryptionSettings.Equals(new EncryptionSettings<byte>(null, EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<byte>())));

            Assert.True(encryptionSettings.Equals(new EncryptionSettings<byte>(new ProtectedDataEncryptionKey("CEK", keyEncryptionKey, encryptedDataEncryptionKey), EncryptionType.Deterministic, StandardSerializerFactory.Default.GetDefaultSerializer<byte>())));
        }
    }
}
