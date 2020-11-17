using System;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// A <see cref="KeyEncryptionKey"/> represents a key-protecting key stored in an external key store. 
    /// The key protects (encrypts) one or more <see cref="DataEncryptionKey"/>.
    /// </summary>
    public class KeyEncryptionKey
    {
        private const KeyEncryptionKeyAlgorithm encryptionAlgorithm = KeyEncryptionKeyAlgorithm.RSA_OAEP;
        private readonly Lazy<byte[]> signature;

        /// <summary>
        /// A local cache to hold previously created <see cref="KeyEncryptionKey"/> objects for reuse.
        /// </summary>
        /// <remarks>
        /// The retension period is defined by the <see cref="TimeToLive"/> property.
        /// </remarks>
        private static readonly LocalCache<Tuple<string, string, EncryptionKeyStoreProvider, bool>, KeyEncryptionKey> keyEncryptionKeyCache =
            new LocalCache<Tuple<string, string, EncryptionKeyStoreProvider, bool>, KeyEncryptionKey>() { TimeToLive = TimeSpan.FromHours(2) };

        /// <summary>
        /// Sets an absolute expiration time, relative to now.
        /// </summary>
        /// <remarks>The default is 2 hours.</remarks>
        public TimeSpan TimeToLive
        {
            get => keyEncryptionKeyCache.TimeToLive.Value;
            set => keyEncryptionKeyCache.TimeToLive = value;
        }

        /// <summary>
        /// The name of the <see cref="KeyEncryptionKey"/>.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Specifies the key store provider. A key store provider is an object that has access to the <see cref="KeyEncryptionKey"/>.
        /// </summary>
        public EncryptionKeyStoreProvider KeyStoreProvider { get; private set; }

        /// <summary>
        /// The path of the key in the <see cref="KeyEncryptionKey"/> store. The format of the key path is specific 
        /// to the <see cref="EncryptionKeyStoreProvider"/> implementation.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Specifies that the <see cref="KeyEncryptionKey"/> is enclave-enabled. You can share all encryption keys,
        /// encrypted with the  <see cref="KeyEncryptionKey"/>, with a secure enclave and use them for computations inside the enclave.
        /// </summary>
        public bool IsEnclaveSupported { get; private set; }

        /// <summary>
        /// A binary result of digitally signing key path and the <see cref="IsEnclaveSupported"/>
        /// setting with the master key. The signature reflects whether <see cref="IsEnclaveSupported"/>
        /// is specified or not. The signature protects the signed values from being altered by unauthorized users.
        /// </summary>
        public byte[] Signature { get => signature.Value; }

        /// <summary>
        /// Returns a cached instance of the <see cref="KeyEncryptionKey"/> or, if not present, creates a new one.
        /// </summary>
        /// <param name="name">The name of the <see cref="KeyEncryptionKey"/>.</param>
        /// <param name="path">The path of the key in the <see cref="KeyEncryptionKey"/> store.</param>
        /// <param name="keyStoreProvider">A component that has access to the <see cref="KeyEncryptionKey"/>.</param>
        /// <param name="isEnclaveSupported">Specifies whether the <see cref="KeyEncryptionKey"/> is enclave-enabled.</param>
        /// <returns>A <see cref="KeyEncryptionKey"/> object.</returns>
        public static KeyEncryptionKey GetOrCreate(string name, string path, EncryptionKeyStoreProvider keyStoreProvider, bool isEnclaveSupported = false)
        {
            name.ValidateNotNullOrWhitespace(nameof(name));
            path.ValidateNotNullOrWhitespace(nameof(name));
            keyStoreProvider.ValidateNotNull(nameof(keyStoreProvider));

            return keyEncryptionKeyCache.GetOrCreate(
                key: Tuple.Create(name, path, keyStoreProvider, isEnclaveSupported),
                createItem: () => new KeyEncryptionKey(name, path, keyStoreProvider, isEnclaveSupported)
            );
        }

        /// <summary>
        /// Constructs a new master key.
        /// </summary>
        /// <param name="name">The name of the <see cref="KeyEncryptionKey"/>.</param>
        /// <param name="path">The path of the key in the <see cref="KeyEncryptionKey"/> store.</param>
        /// <param name="keyStoreProvider">A component that has access to the <see cref="KeyEncryptionKey"/>.</param>
        /// <param name="isEnclaveSupported">Specifies whether the <see cref="KeyEncryptionKey"/> is enclave-enabled.</param>
        public KeyEncryptionKey(string name, string path, EncryptionKeyStoreProvider keyStoreProvider, bool isEnclaveSupported = false)
        {
            name.ValidateNotNullOrWhitespace(nameof(name));
            path.ValidateNotNullOrWhitespace(nameof(name));
            keyStoreProvider.ValidateNotNull(nameof(keyStoreProvider));

            Name = name;
            Path = path;
            KeyStoreProvider = keyStoreProvider;
            IsEnclaveSupported = isEnclaveSupported;
            signature = new Lazy<byte[]>(() => KeyStoreProvider.Sign(encryptionKeyId: path, allowEnclaveComputations: isEnclaveSupported));
        }

        /// <summary>
        /// Encrypts an <see cref="DataEncryptionKey">EncryptionKey</see>
        /// </summary>
        /// <param name="plaintextEncryptionKey">The <see cref="DataEncryptionKey"/> to encrypt.</param>
        /// <returns>The encrypted <see cref="DataEncryptionKey"/></returns>
        public byte[] EncryptEncryptionKey(byte[] plaintextEncryptionKey)
        {
            return KeyStoreProvider.WrapKey(Path, encryptionAlgorithm, plaintextEncryptionKey);
        }

        /// <summary>
        /// Decrypts an <see cref="DataEncryptionKey"/>
        /// </summary>
        /// <param name="encryptedEncryptionKey">The <see cref="DataEncryptionKey"/> to decrypt.</param>
        /// <returns>The decrypted <see cref="DataEncryptionKey"/></returns>
        public byte[] DecryptEncryptionKey(byte[] encryptedEncryptionKey)
        {
            return KeyStoreProvider.UnwrapKey(Path, encryptionAlgorithm, encryptedEncryptionKey);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is KeyEncryptionKey other))
            {
                return false;
            }

            return Name.Equals(other.Name)
                && KeyStoreProvider.Equals(other.KeyStoreProvider)
                && Path.Equals(other.Path)
                && IsEnclaveSupported.Equals(other.IsEnclaveSupported);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Tuple.Create(Name, KeyStoreProvider, Path, IsEnclaveSupported).GetHashCode();
    }
}
