using System;
using System.Linq;
using System.Text;

namespace Microsoft.Data.Encryption.AzureKeyVaultProvider
{
    /// <summary>
    /// Key format is (version + keyPathLength + ciphertextLength + ciphertext + keyPath + signature)
    /// </summary>
    internal class EncryptedColumnEncryptionKey
    {
        private const int versionBitIndex = 0;
        private const int keyPathLengthIndex = 1;
        private const int cipherTextLengthIndex = 3;
        private const int keyPathIndex = 5;

        internal byte Version { get; private set; }
        internal int KeyPathLength { get; private set; }
        internal int CiphertextLength { get; private set; }
        internal byte[] Ciphertext { get; private set; }
        internal string KeyPath { get; private set; }
        internal byte[] Signature { get; private set; }
        internal byte[] Message { get; private set; }


        internal EncryptedColumnEncryptionKey(byte[] bytes)
        {
            Version = bytes[versionBitIndex];
            KeyPathLength = ParseKeyPathLength(bytes);
            KeyPath = ParseKeyPath(bytes, KeyPathLength);
            CiphertextLength = GetCipherTextLength(bytes);
            int ciphertextIndex = keyPathIndex + KeyPathLength;
            Ciphertext = ParseCiphertext(bytes, ciphertextIndex, CiphertextLength);
            int signatureIndex = ciphertextIndex + CiphertextLength;
            int signatureLength = bytes.Length - ciphertextIndex - CiphertextLength;
            Message = ParseMessage(bytes, signatureLength);
            Signature = ParseSignature(bytes, signatureIndex, signatureLength);
        }

        private static ushort ParseKeyPathLength(byte[] bytes)
        {
            return BitConverter.ToUInt16(bytes, keyPathLengthIndex);
        }

        private static ushort GetCipherTextLength(byte[] bytes)
        {
            return BitConverter.ToUInt16(bytes, cipherTextLengthIndex);
        }

        private static string ParseKeyPath(byte[] bytes, int keyPathLength)
        {
            return Encoding.Unicode.GetString(bytes.Skip(keyPathIndex).Take(keyPathLength).ToArray());
        }

        private static byte[] ParseCiphertext(byte[] bytes, int ciphertextIndex, int ciphertextLength)
        {
            return bytes.Skip(ciphertextIndex).Take(ciphertextLength).ToArray();
        }

        private static byte[] ParseSignature(byte[] bytes, int signatureIndex, int signatureLength)
        {
            return bytes.Skip(signatureIndex).Take(signatureLength).ToArray();
        }

        private static byte[] ParseMessage(byte[] bytes, int signtureLength)
        {
            int messageLength = bytes.Length - signtureLength;
            return bytes.Take(messageLength).ToArray();
        }
    }
}
