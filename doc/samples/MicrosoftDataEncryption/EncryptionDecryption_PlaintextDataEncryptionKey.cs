//<Snippet1>
using Microsoft.Data.Encryption.Cryptography;
using System;

namespace EncryptionDecryptionWithPlaintextDataEncryptionKey
{
    public class Program
    {
        public static void Main()
        {
            // Create some simple data elements
            string plaintextString = "MyString";
            long plaintextNumber = 4815162342;

            Console.WriteLine("**** Original Values ****");
            Console.WriteLine($"String: {plaintextString}");
            Console.WriteLine($"Number: {plaintextNumber}");

            // Generate a new plaintext encryption key
            PlaintextDataEncryptionKey encryptionKey = new PlaintextDataEncryptionKey("MyKey");

            var ciphertextString = plaintextString.Encrypt(encryptionKey).ToBase64String();
            var ciphertextNumber = plaintextNumber.Encrypt(encryptionKey).ToBase64String();

            Console.WriteLine("\n**** Encrypted Values ****");
            Console.WriteLine($"String: {ciphertextString}");
            Console.WriteLine($"Number: {ciphertextNumber}");

            string decryptedString = ciphertextString.FromBase64String().Decrypt<string>(encryptionKey);
            long decryptedNumber = ciphertextNumber.FromBase64String().Decrypt<long>(encryptionKey);

            Console.WriteLine("\n**** Decrypted Values ****");
            Console.WriteLine($"String: {decryptedString}");
            Console.WriteLine($"Number: {decryptedNumber}");

            Console.Clear();
        }
    }
}
//</Snippet1>
