using System;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// Base class for all key store providers. A custom provider must derive from this
    /// class and override its member functions.
    /// </summary>
    public abstract class EncryptionKeyStoreProvider
    {
        /// <summary>
        /// A cache of key encryption keys (once they are unwrapped). This is useful for rapidly decrypting multiple data values.
        /// </summary>
        private LocalCache<string, byte[]> dataEncryptionKeyCache = new LocalCache<string, byte[]>() { TimeToLive = TimeSpan.FromHours(2) };

        /// <summary>
        /// A cache for storing the results of signature verification of key encryption key metadata.
        /// </summary>
        private LocalCache<Tuple<string, string, bool, string>, bool> keyEncryptionKeyMetadataSignatureVerificationCache = 
            new LocalCache<Tuple<string, string, bool, string>, bool>(maxSizeLimit: 2000) { TimeToLive = TimeSpan.FromDays(10) };

        /// <summary>
        /// Gets or sets the lifespan of the decrypted encrtption key in the cache.
        /// Once the timespan has elapsed, data is discarded and must be revalidated.
        /// </summary>
        public TimeSpan? DataEncryptionKeyCacheTimeToLive
        {
            get => dataEncryptionKeyCache.TimeToLive;
            set => dataEncryptionKeyCache.TimeToLive = value;
        }

        /// <summary>
        /// The unique name that identifies a particular implementation of the abstract <see cref="EncryptionKeyStoreProvider"/>.
        /// </summary>
        public abstract string ProviderName { get; }

        /// <summary>
        /// Unwraps the specified <paramref name="encryptedKey"/> of a data encryption key. The encrypted value is expected to be encrypted using 
        /// the key encryption key with the specified <paramref name="encryptionKeyId"/> and using the specified <paramref name="algorithm"/>.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="algorithm">The encryption algorithm.</param>
        /// <param name="encryptedKey">The ciphertext key.</param>
        /// <returns>The unwrapped data encryption key.</returns>
        public abstract byte[] UnwrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] encryptedKey);

        /// <summary>
        /// Wraps a data encryption key using the key encryption key with the specified <paramref name="encryptionKeyId"/> and using the specified <paramref name="algorithm"/>.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="algorithm">The encryption algorithm.</param>
        /// <param name="key">The plaintext key</param>
        /// <returns>The wrapped data encryption key.</returns>
        public abstract byte[] WrapKey(string encryptionKeyId, KeyEncryptionKeyAlgorithm algorithm, byte[] key);

        /// <summary>
        /// When implemented in a derived class, digitally signs the key encryption key metadata with the key encryption key referenced 
        /// by the <paramref name="encryptionKeyId"/> parameter. The input values used to generate the signature should be the specified values of 
        /// the <paramref name="encryptionKeyId"/> and <paramref name="allowEnclaveComputations"/> parameters.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="allowEnclaveComputations">Indicates whether the key encryption key supports enclave computations.</param>
        /// <returns>The signature of the key encryption key metadata.</returns>
        public abstract byte[] Sign(string encryptionKeyId, bool allowEnclaveComputations);

        /// <summary>
        /// When implemented in a derived class, this method is expected to verify the specified <paramref name="signature"/> is valid 
        /// for the key encryption key with the specified <paramref name="encryptionKeyId"/> and the specified enclave behavior.
        /// </summary>
        /// <param name="encryptionKeyId">The key Id tells the provider where to find the key.</param>
        /// <param name="allowEnclaveComputations">Indicates whether the key encryption key supports enclave computations.</param>
        /// <param name="signature">The signature of the key encryption key metadata.</param>
        /// <returns></returns>
        public abstract bool Verify(string encryptionKeyId, bool allowEnclaveComputations, byte[] signature);

        /// <summary>
        /// Returns the cached decrypted data encryption key, or unwraps the encrypted data encryption if not present.
        /// </summary>
        /// <param name="encryptedDataEncryptionKey">Encrypted Data Encryption Key</param>
        /// <param name="createItem">The delegate function that will decrypt the encrypted column encryption key.</param>
        /// <returns></returns>
        protected virtual byte[] GetOrCreateDataEncryptionKey(string encryptedDataEncryptionKey, Func<byte[]> createItem)
        {
            return dataEncryptionKeyCache.GetOrCreate(encryptedDataEncryptionKey, createItem);
        }

        /// <summary>
        /// Returns the cached signature verification result, or proceeds to verify if not present.
        /// </summary>
        /// <param name="keyInformation">The ProviderName, masterKeyPath, allowEnclaveComputations and hexidecimal signature.</param>
        /// <param name="createItem">The delegate function that will perform the verification.</param>
        /// <returns></returns>
        protected virtual bool GetOrCreateSignatureVerificationResult(Tuple<string, string, bool, string> keyInformation, Func<bool> createItem)
        {
            return keyEncryptionKeyMetadataSignatureVerificationCache.GetOrCreate(keyInformation, createItem);
        }
    }
}
