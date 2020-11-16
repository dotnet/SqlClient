using System;
using Microsoft.Data.Encryption.Cryptography;
using static Microsoft.Data.Encryption.Demo.Program;

namespace Microsoft.Data.Encryption.Demo
{
    public static class SingleItemDefaultSettings
    {
        public static void Demo()
        {
            Console.WriteLine("\n**** Original Value ****");

            // Declare value to be encrypted
            DateTime original = DateTime.Now;
            Console.WriteLine(original);



            Console.WriteLine("\n**** Encrypted Value ****");

            // Encrypt value and convert to HEX string
            var encryptedBytes = original.Encrypt(encryptionKey);
            var encryptedHexString = encryptedBytes.ToHexString();
            Console.WriteLine(encryptedHexString);



            Console.WriteLine("\n**** Decrypted Value ****");

            // Decrypt value back to original
            var bytesToDecrypt = encryptedHexString.FromHexString();
            var decryptedBytes = bytesToDecrypt.Decrypt<DateTime>(encryptionKey);

            Console.WriteLine(decryptedBytes);

            Console.Clear();
        }
    }
}
