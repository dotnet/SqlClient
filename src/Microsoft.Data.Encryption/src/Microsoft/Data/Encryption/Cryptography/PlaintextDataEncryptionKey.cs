using System;
using System.Security.Cryptography;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <inheritdoc/>
    public class PlaintextDataEncryptionKey : DataEncryptionKey
    {
        /// <summary>
        /// A local cache to hold previously created <see cref="PlaintextDataEncryptionKey"/> objects for reuse.
        /// </summary>
        /// <remarks>
        /// The retension period is defined by the <see cref="TimeToLive"/> property.
        /// </remarks>
        private static readonly LocalCache<Tuple<string, string>, PlaintextDataEncryptionKey> encryptionKeyCache
            = new LocalCache<Tuple<string, string>, PlaintextDataEncryptionKey>() { TimeToLive = TimeSpan.FromHours(2) };

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
        /// Returns a cached instance of the <see cref="PlaintextDataEncryptionKey"/> or, if not present, creates a new one
        /// </summary>
        /// <param name="name">The name by which the <see cref="PlaintextDataEncryptionKey"/> will be known.</param>
        /// <param name="unencryptedKey">The unencrypted <see cref="PlaintextDataEncryptionKey"/> value.</param>
        /// <returns>An <see cref="PlaintextDataEncryptionKey"/> object.</returns>
        public static PlaintextDataEncryptionKey GetOrCreate(string name, byte[] unencryptedKey)
        {
            name.ValidateNotNullOrWhitespace(nameof(name));
            unencryptedKey.ValidateNotNull(nameof(unencryptedKey));

            return encryptionKeyCache.GetOrCreate(
                key: Tuple.Create(name, unencryptedKey.ToHexString()),
                createItem: () => new PlaintextDataEncryptionKey(name, unencryptedKey)
            );
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectedDataEncryptionKey"/> class.
        /// </summary>
        /// <param name="name">The name by which the <see cref="PlaintextDataEncryptionKey"/> will be known.</param>
        /// <remarks>
        /// Generates a new 256 bit cryptographically strong random key.
        /// </remarks>
        public PlaintextDataEncryptionKey(string name) : base(name, GenerateNewColumnEncryptionKey()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectedDataEncryptionKey"/> class.
        /// </summary>
        /// <param name="name">The name by which the <see cref="PlaintextDataEncryptionKey"/> will be known.</param>
        /// <param name="unencryptedKey">The unencrypted <see cref="PlaintextDataEncryptionKey"/> value.</param>
        public PlaintextDataEncryptionKey(string name, byte[] unencryptedKey) : base(name, unencryptedKey) { }

        private static byte[] GenerateNewColumnEncryptionKey()
        {
            byte[] plainTextColumnEncryptionKey = new byte[32];
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(plainTextColumnEncryptionKey);

            return plainTextColumnEncryptionKey;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is PlaintextDataEncryptionKey other))
            {
                return false;
            }

            return Name.Equals(other.Name) && RootKeyEquals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Tuple.Create(Name, rootKeyHexString).GetHashCode();
    }
}
