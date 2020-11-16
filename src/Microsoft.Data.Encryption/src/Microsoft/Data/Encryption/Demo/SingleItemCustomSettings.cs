using System;
using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using static Microsoft.Data.Encryption.Demo.Program;

namespace Microsoft.Data.Encryption.Demo
{
    public static class SingleItemCustomSettings
    {
        public static void Demo()
        {
            // Declare custom settings
            var encryptionSettings = new EncryptionSettings<Guid>(
                dataEncryptionKey: encryptionKey,
                encryptionType: EncryptionType.Deterministic,
                serializer: StandardSerializerFactory.Default.GetDefaultSerializer<Guid>()
            );



            Console.WriteLine("**** Original Value ****");

            // Declare value to be encrypted
            var original = Guid.NewGuid();
            Console.WriteLine(original);



            Console.WriteLine("\n**** Encrypted Value ****");

            // Encrypt value and convert to HEX string
            var encryptedBytes = original.Encrypt<Guid>(encryptionSettings);
            var encryptedHexString = encryptedBytes.ToHexString();
            Console.WriteLine(encryptedHexString);



            Console.WriteLine("\n**** Decrypted Value ****");

            // Decrypt value back to original
            var bytesToDecrypt = encryptedHexString.FromHexString();
            var decryptedBytes = bytesToDecrypt.Decrypt<Guid>(encryptionSettings); //Type can be inferred here

            Console.WriteLine(decryptedBytes);

            Console.Clear();
        }
    }
}
