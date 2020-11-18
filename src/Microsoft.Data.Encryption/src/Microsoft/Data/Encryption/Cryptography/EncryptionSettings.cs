using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;
using static Microsoft.Data.Encryption.Cryptography.EncryptionType;

namespace Microsoft.Data.Encryption.Cryptography
{
    /// <summary>
    /// Contains the settings to configure how encryption operations are performed on the data of
    /// arbitrary type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The data type on which these encryption settings will apply.</typeparam>
    public class EncryptionSettings<T> : EncryptionSettings
    {
        /// <summary>
        /// Used for serializing and deserializing data objects to and from an array of bytes.
        /// </summary>
        public Serializer<T> Serializer { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionSettings{T}"/> class.
        /// </summary>
        /// <param name="dataEncryptionKey">An encryption key is used to encrypt and decrypt data.</param>
        /// <param name="serializer">A serializer is used for serializing and deserializing data objects to and from an array of bytes.</param>
        public EncryptionSettings(DataEncryptionKey dataEncryptionKey, Serializer<T> serializer)
            : this(dataEncryptionKey, GetDefaultEncryptionType(dataEncryptionKey), serializer) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionSettings{T}"/> class.
        /// </summary>
        /// <param name="dataEncryptionKey">An encryption key is used to encrypt and decrypt data.</param>
        /// <param name="encryptionType">The type of encryption.</param>
        /// <param name="serializer">A serializer is used for serializing and deserializing data objects to and from an array of bytes.</param>
        public EncryptionSettings(DataEncryptionKey dataEncryptionKey, EncryptionType encryptionType, Serializer<T> serializer)
        {
            DataEncryptionKey = dataEncryptionKey;
            EncryptionType = encryptionType;
            Serializer = serializer;
        }

        /// <inheritdoc/>
        public override ISerializer GetSerializer()
        {
            return Serializer;
        }
    }

    /// <summary>
    /// Contains the settings to configure how encryption operations are performed on data.
    /// </summary>
    public abstract class EncryptionSettings : IEquatable<EncryptionSettings>
    {
        private DataEncryptionKey encryptionKey;

        /// <summary>
        /// Gets the <see cref="DataEncryptionKey"/>
        /// </summary>
        public virtual DataEncryptionKey DataEncryptionKey
        {
            get => encryptionKey;

            protected set
            {
                encryptionKey = value;
                if (DataEncryptionKey.IsNull())
                {
                    EncryptionType = Plaintext;
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="EncryptionType"/>
        /// </summary>
        public EncryptionType EncryptionType { get; protected set; } = Randomized;

        /// <summary>
        /// Gets the <see cref="ISerializer"/>
        /// </summary>
        /// <returns>returns the <see cref="ISerializer"/></returns>
        public abstract ISerializer GetSerializer();

        /// <summary>
        /// Gets the default <see cref="EncryptionType"/>
        /// </summary>
        /// <param name="dataEncryptionKey">An encryption key is used to encrypt and decrypt data.</param>
        /// <returns>Plaintet if the <paramref name="dataEncryptionKey"/> is null, <see cref="EncryptionType.Randomized"/> otherwise.</returns>
        protected static EncryptionType GetDefaultEncryptionType(DataEncryptionKey dataEncryptionKey)
        {
            return dataEncryptionKey.IsNull() ? Plaintext : Randomized;
        }

        /// <inheritdoc/>
        public bool Equals(EncryptionSettings other)
        {
            if (other == null)
            {
                return false;
            }

            if (DataEncryptionKey.IsNull() && !other.DataEncryptionKey.IsNull())
            {
                return false;
            }

            return DataEncryptionKey.Equals(other.DataEncryptionKey)
                && EncryptionType.Equals(other.EncryptionType)
                && GetSerializer().GetType().Equals(other.GetSerializer().GetType());
        }
    }
}
