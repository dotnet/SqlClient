using System;
using System.Security.Cryptography;

using static System.Text.Encoding;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// An encryption key that is used to encrypt and decrypt data.
    /// </summary>
    public abstract class DataEncryptionKey
    {
        private byte[] rootKeyBytes;
        private const int KeySizeInBits = 256;
        internal const int KeySizeInBytes = KeySizeInBits / 8;

        /// <summary>
        /// The name by which the <see cref="DataEncryptionKey"/> will be known.
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// The hexadecimal string representation of the root key used for equality comparisons.
        /// </summary>
        protected string rootKeyHexString;

        /// <summary>
        /// The root key used for encryption operations.
        /// </summary>
        internal byte[] RootKeyBytes
        {
            get => rootKeyBytes;
            set
            {
                rootKeyBytes = value;
                rootKeyHexString = value.ToHexString();
            }
        }

        /// <summary>
        /// The encryption key that is used for encryption and decryption.
        /// </summary>
        internal byte[] EncryptionKeyBytes { get; set; }

        /// <summary>
        /// The MAC key that is used to compute and validate an HMAC.
        /// </summary>
        internal byte[] MacKeyBytes { get; set; }

        /// <summary>
        /// The IV key that is used to compute a synthetic IV from a given plain text.
        /// </summary>
        internal byte[] IvKeyBytes { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataEncryptionKey"/> class.
        /// </summary>
        /// <param name="name">The name by which the <see cref="DataEncryptionKey"/> will be known.</param>
        /// <param name="rootKey">Specifies the root key used for encrypting and decrypting.</param>
        protected DataEncryptionKey(string name, byte[] rootKey)
        {
            name.ValidateNotNullOrWhitespace(nameof(name));
            rootKey.ValidateSize(KeySizeInBytes, nameof(rootKey));

            Name = name;

            string encryptionKeySalt = $"Microsoft SQL Server cell encryption key with encryption algorithm:AEAD_AES_256_CBC_HMAC_SHA256 and key length:{KeySizeInBits}";
            string macKeySalt = $"Microsoft SQL Server cell MAC key with encryption algorithm:AEAD_AES_256_CBC_HMAC_SHA256 and key length:{KeySizeInBits}";
            string ivKeySalt = $"Microsoft SQL Server cell IV key with encryption algorithm:AEAD_AES_256_CBC_HMAC_SHA256 and key length:{KeySizeInBits}";

            byte[] encryptionKeyBytes = GetHMACWithSHA256(Unicode.GetBytes(encryptionKeySalt), rootKey);
            byte[] macKeyBytes = GetHMACWithSHA256(Unicode.GetBytes(macKeySalt), rootKey);
            byte[] ivKeyBytes = GetHMACWithSHA256(Unicode.GetBytes(ivKeySalt), rootKey);

            RootKeyBytes = rootKey;
            EncryptionKeyBytes = encryptionKeyBytes;
            MacKeyBytes = macKeyBytes;
            IvKeyBytes = ivKeyBytes;
        }

        /// <summary>
        /// Computes a Hash-based Message Authentication Code (HMAC) by using the SHA256 hash function.
        /// </summary>
        /// <param name="plaintext">Plain text bytes whose hash has to be computed.</param>
        /// <param name="key">key used for the HMAC</param>
        /// <returns>HMAC value</returns>
        internal static byte[] GetHMACWithSHA256(byte[] plaintext, byte[] key)
        {
            using (HMACSHA256 hmacSha256 = new HMACSHA256(key))
            {
                return hmacSha256.ComputeHash(plaintext);
            }
        }

        /// <summary>
        /// Determines if the current <see cref="DataEncryptionKey"/>'s root key is equal to the specified <see cref="DataEncryptionKey"/>'s root key
        /// </summary>
        /// <param name="otherKey">The <see cref="DataEncryptionKey"/> to compare with the current <see cref="DataEncryptionKey"/></param>
        /// <returns></returns>
        public bool RootKeyEquals(DataEncryptionKey otherKey)
        { 
            return rootKeyHexString.Equals(otherKey.rootKeyHexString);
        }
    }
}
