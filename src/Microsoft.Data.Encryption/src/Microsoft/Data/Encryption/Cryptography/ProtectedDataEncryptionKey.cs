using System;
using System.Security.Cryptography;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// A <see cref="DataEncryptionKey"/>, protected by a <see cref="Cryptography.KeyEncryptionKey"/>, that is used to encrypt and decrypt data.
    /// </summary>
    public class ProtectedDataEncryptionKey : DataEncryptionKey
    {
        /// <summary>
        /// A local cache to hold previously created <see cref="ProtectedDataEncryptionKey"/> objects for reuse.
        /// </summary>
        /// <remarks>
        /// The retension period is defined by the <see cref="TimeToLive"/> property.
        /// </remarks>
        private static readonly LocalCache<Tuple<string, KeyEncryptionKey, string>, ProtectedDataEncryptionKey> encryptionKeyCache
            = new LocalCache<Tuple<string, KeyEncryptionKey, string>, ProtectedDataEncryptionKey>() { TimeToLive = TimeSpan.FromHours(2) };

        /// <summary>
        /// Sets an absolute expiration time, relative to now.
        /// </summary>
        /// <remarks>The default is 2 hours.</remarks>
        public TimeSpan TimeToLive
        {
            get => encryptionKeyCache.TimeToLive.Value;
            set => encryptionKeyCache.TimeToLive = value;
        }

        /// <summary>
        /// Specifies the the <see cref="Cryptography.KeyEncryptionKey"/> used for encrypting and decrypting the <see cref="ProtectedDataEncryptionKey"/>.
        /// </summary>
        public KeyEncryptionKey KeyEncryptionKey { get; private set; }

        /// <summary>
        /// The encrypted <see cref="ProtectedDataEncryptionKey"/> value.
        /// </summary>
        public byte[] EncryptedValue { get; private set; }

        /// <summary>
        /// Returns a cached instance of the <see cref="ProtectedDataEncryptionKey"/> or, if not present, creates a new one
        /// </summary>
        /// <param name="name">The name by which the <see cref="ProtectedDataEncryptionKey"/> will be known.</param>
        /// <param name="keyEncryptionKey">Specifies the the <see cref="Cryptography.KeyEncryptionKey"/> used for encrypting and decrypting the <see cref="ProtectedDataEncryptionKey"/>.</param>
        /// <param name="encryptedKey">The encrypted <see cref="ProtectedDataEncryptionKey"/> value.</param>
        /// <returns>An <see cref="ProtectedDataEncryptionKey"/> object.</returns>
        public static ProtectedDataEncryptionKey GetOrCreate(string name, KeyEncryptionKey keyEncryptionKey, byte[] encryptedKey)
        {
            name.ValidateNotNullOrWhitespace(nameof(name));
            keyEncryptionKey.ValidateNotNull(nameof(keyEncryptionKey));
            encryptedKey.ValidateNotNull(nameof(encryptedKey));

            return encryptionKeyCache.GetOrCreate(
                key: Tuple.Create(name, keyEncryptionKey, encryptedKey.ToHexString()),
                createItem: () => new ProtectedDataEncryptionKey(name, keyEncryptionKey, encryptedKey)
            );
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectedDataEncryptionKey"/> class derived from
        /// generating an array of bytes with a cryptographically strong random sequence of values.
        /// </summary>
        /// <param name="name">The name by which the <see cref="ProtectedDataEncryptionKey"/> will be known.</param>
        /// <param name="keyEncryptionKey">Specifies the the <see cref="Cryptography.KeyEncryptionKey"/> used for encrypting and decrypting the <see cref="ProtectedDataEncryptionKey"/>.</param>
        public ProtectedDataEncryptionKey(string name, KeyEncryptionKey keyEncryptionKey) : this(name, keyEncryptionKey, GenerateNewColumnEncryptionKey(keyEncryptionKey)) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectedDataEncryptionKey"/> class derived from
        /// decrypting the <paramref name="encryptedKey"/>.
        /// </summary>
        /// <param name="name">The name by which the <see cref="ProtectedDataEncryptionKey"/> will be known.</param>
        /// <param name="keyEncryptionKey">Specifies the the <see cref="Cryptography.KeyEncryptionKey"/> used for encrypting and decrypting the <see cref="ProtectedDataEncryptionKey"/>.</param>
        /// <param name="encryptedKey">The encrypted <see cref="ProtectedDataEncryptionKey"/> value.</param>
        public ProtectedDataEncryptionKey(string name, KeyEncryptionKey keyEncryptionKey, byte[] encryptedKey) : base(name, keyEncryptionKey.DecryptEncryptionKey(encryptedKey))
        {
            name.ValidateNotNullOrWhitespace(nameof(name));
            keyEncryptionKey.ValidateNotNull(nameof(keyEncryptionKey));

            KeyEncryptionKey = keyEncryptionKey;
            EncryptedValue = encryptedKey;
        }

        private static byte[] GenerateNewColumnEncryptionKey(KeyEncryptionKey masterKey)
        {
            byte[] plainTextColumnEncryptionKey = new byte[32];
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(plainTextColumnEncryptionKey);

            return masterKey.EncryptEncryptionKey(plainTextColumnEncryptionKey);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is ProtectedDataEncryptionKey other))
            {
                return false;
            }

            if (KeyEncryptionKey.IsNull() && !other.KeyEncryptionKey.IsNull())
            {
                return false;
            }

            return Name.Equals(other.Name)
                && KeyEncryptionKey.Equals(other.KeyEncryptionKey)
                && RootKeyEquals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Tuple.Create(Name, KeyEncryptionKey, rootKeyHexString).GetHashCode();
    }
}
