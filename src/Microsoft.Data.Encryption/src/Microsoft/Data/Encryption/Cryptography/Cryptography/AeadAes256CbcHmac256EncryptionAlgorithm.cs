using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using static Microsoft.Data.Encryption.Cryptography.DataEncryptionKey;
using static Microsoft.Data.Encryption.Cryptography.EncryptionType;
using static System.Security.Cryptography.CipherMode;
using static System.Security.Cryptography.PaddingMode;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// A component for information encryption purposes that uses block ciphers to encrypt and protect data.
    /// This class implements authenticated encryption algorithm with associated data as described in
    /// http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05. More specifically this implements
    /// AEAD_AES_256_CBC_HMAC_SHA256 algorithm.
    /// </summary>
    public class AeadAes256CbcHmac256EncryptionAlgorithm : DataProtector
    {
        /// <summary>
        /// Block size in bytes. AES uses 16 byte blocks.
        /// </summary>
        private const int BlockSizeInBytes = 16;

        /// <summary>
        /// Variable indicating whether this algorithm should work in Deterministic mode or Randomized mode.
        /// For deterministic encryption, we derive an IV from the plaintext data.
        /// For randomized encryption, we generate a cryptographically random IV.
        /// </summary>
        private readonly bool isDeterministicEncryptionType;

        /// <summary>
        /// The Data Encryption Key is an encryption key is used to encrypt data.
        /// </summary>
        private readonly DataEncryptionKey dataEncryptionKey;

        /// <summary>
        /// Byte array with algorithm version used for authentication tag computation.
        /// </summary>
        private static readonly byte[] version = { 1 };


        private static readonly LocalCache<Tuple<DataEncryptionKey, EncryptionType>, AeadAes256CbcHmac256EncryptionAlgorithm> algorithmCache
            = new LocalCache<Tuple<DataEncryptionKey, EncryptionType>, AeadAes256CbcHmac256EncryptionAlgorithm>(maxSizeLimit: 1000);

        /// <summary>
        /// Returns a cached instance of the <see cref="AeadAes256CbcHmac256EncryptionAlgorithm"/> or, if not present, creates a new one.
        /// </summary>
        /// <param name="dataEncryptionKey">The encryption key that is used to encrypt data.</param>
        /// <param name="encryptionType">The type of encryption.</param>
        /// <returns>An <see cref="AeadAes256CbcHmac256EncryptionAlgorithm"/> object.</returns>
        public static AeadAes256CbcHmac256EncryptionAlgorithm GetOrCreate(DataEncryptionKey dataEncryptionKey, EncryptionType encryptionType)
        {
            dataEncryptionKey.ValidateNotNull(nameof(dataEncryptionKey));

            return algorithmCache.GetOrCreate(
                key: Tuple.Create(dataEncryptionKey, encryptionType),
                createItem: () => new AeadAes256CbcHmac256EncryptionAlgorithm(dataEncryptionKey, encryptionType)
            );
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AeadAes256CbcHmac256EncryptionAlgorithm"/> class.
        /// </summary>
        /// <param name="dataEncryptionKey">an encryption key is used to encrypt data</param>
        /// <param name="encryptionType">Determines whether this algorithm should work in Deterministic mode or Randomized mode.</param>
        public AeadAes256CbcHmac256EncryptionAlgorithm(DataEncryptionKey dataEncryptionKey, EncryptionType encryptionType)
        {
            this.dataEncryptionKey = dataEncryptionKey;
            isDeterministicEncryptionType = encryptionType == Deterministic;
        }

        /// <inheritdoc/>
        public override byte[] Encrypt(byte[] plaintext)
        {
            if (plaintext.IsNull())
            {
                return null;
            }

            Aes aes = Aes.Create();
            aes.Key = dataEncryptionKey.EncryptionKeyBytes;
            aes.Mode = CBC;
            aes.Padding = PKCS7;

            if (isDeterministicEncryptionType)
            {
                aes.IV = GetHMACWithSHA256(plaintext, dataEncryptionKey.IvKeyBytes).Take(BlockSizeInBytes).ToArray();
            }

            ICryptoTransform encryptor = aes.CreateEncryptor();
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            cryptoStream.Write(plaintext, 0, plaintext.Length);
            cryptoStream.FlushFinalBlock();
            byte[] ciphertext = memoryStream.ToArray();
            byte[] hmac = GenerateAuthenticationTag(aes.IV, ciphertext);

            return version.Concat(hmac).Concat(aes.IV).Concat(ciphertext).ToArray();
        }

        /// <inheritdoc/>
        public override byte[] Decrypt(byte[] ciphertext)
        {
            if (ciphertext.IsNull())
            {
                return null;
            }

            ValidateCiphertextLength(ciphertext);

            const int authenticationTagIndex = 1;
            const int authenticationTagLength = KeySizeInBytes;
            const int initializationVectorIndex = authenticationTagIndex + authenticationTagLength;
            const int initializationVectorLength = BlockSizeInBytes;
            const int encryptedDataIndex = initializationVectorIndex + initializationVectorLength;
            int encryptedDataLength = ciphertext.Length - encryptedDataIndex;

            byte[] authenticationTag = ciphertext.Skip(authenticationTagIndex).Take(authenticationTagLength).ToArray();
            byte[] initializationVector = ciphertext.Skip(initializationVectorIndex).Take(initializationVectorLength).ToArray();
            byte[] encryptedData = ciphertext.Skip(encryptedDataIndex).Take(encryptedDataLength).ToArray();

            ValidateAuthenticationTag(authenticationTag, initializationVector, encryptedData);

            SymmetricAlgorithm symmetricAlgorithm = Aes.Create();
            symmetricAlgorithm.Key = dataEncryptionKey.EncryptionKeyBytes;
            symmetricAlgorithm.Mode = CBC;
            symmetricAlgorithm.Padding = PKCS7;
            symmetricAlgorithm.IV = initializationVector;

            ICryptoTransform decryptor = symmetricAlgorithm.CreateDecryptor();
            MemoryStream memoryStream = new MemoryStream();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write);
            cryptoStream.Write(encryptedData, 0, encryptedData.Length);
            cryptoStream.FlushFinalBlock();

            return memoryStream.ToArray();
        }

        private byte[] GenerateAuthenticationTag(byte[] iv, byte[] ciphertext)
        {
            HMACSHA256 hmacSha256 = new HMACSHA256(dataEncryptionKey.MacKeyBytes);
            byte[] versionSize = { sizeof(byte) };
            byte[] buffer = version.Concat(iv).Concat(ciphertext).Concat(versionSize).ToArray();
            byte[] hmac = hmacSha256.ComputeHash(buffer);
            return hmac;
        }

        private static void ValidateCiphertextLength(byte[] ciphertext)
        {
            const int minimumCipherTextLength = sizeof(byte) + BlockSizeInBytes + BlockSizeInBytes + KeySizeInBytes;

            if (ciphertext.Length < minimumCipherTextLength)
            {
                throw new ArgumentException("Specified ciphertext has an unexpected length.");
            }
        }

        private void ValidateAuthenticationTag(byte[] authenticationTag, byte[] initializationVector, byte[] ciphertext)
        {
            byte[] computedAuthenticationTag = GenerateAuthenticationTag(initializationVector, ciphertext);

            if (!authenticationTag.SequenceEqual(computedAuthenticationTag))
            {
                throw new CryptographicException("Specified ciphertext has an invalid authentication tag.");
            }
        }
    }
}
