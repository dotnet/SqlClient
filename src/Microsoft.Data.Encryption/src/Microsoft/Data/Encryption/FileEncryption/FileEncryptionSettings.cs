using Microsoft.Data.Encryption.Cryptography;
using Microsoft.Data.Encryption.Cryptography.Serializers;
using System;

namespace Microsoft.Data.Encryption.FileEncryption
{
    /// <inheritdoc/>
    public class FileEncryptionSettings<T> : FileEncryptionSettings
    {
        /// <summary>
        /// Gets the <see cref="Serializer"/> used for serializing and deserializing data objects to and from an array of bytes.
        /// </summary>
        public Serializer<T> Serializer { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileEncryptionSettings{T}"/> class.
        /// </summary>
        /// <param name="dataEncryptionKey">An encryption key is used to encrypt and decrypt data.</param>
        /// <param name="serializer">A serializer is used for serializing and deserializing data objects to and from an array of bytes.</param>
        public FileEncryptionSettings(ProtectedDataEncryptionKey dataEncryptionKey, Serializer<T> serializer)
            : this(dataEncryptionKey, GetDefaultEncryptionType(dataEncryptionKey), serializer) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileEncryptionSettings{T}"/> class.
        /// </summary>
        /// <param name="dataEncryptionKey">An encryption key is used to encrypt and decrypt data.</param>
        /// <param name="encryptionType">The type of encryption.</param>
        /// <param name="serializer">A serializer is used for serializing and deserializing data objects to and from an array of bytes.</param>
        public FileEncryptionSettings(ProtectedDataEncryptionKey dataEncryptionKey, EncryptionType encryptionType, Serializer<T> serializer)
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

    /// <inheritdoc/>
    public abstract class FileEncryptionSettings : EncryptionSettings
    {
        /// <summary>
        /// Gets or sets the <see cref="ProtectedDataEncryptionKey"/>, protected by a <see cref="Cryptography.KeyEncryptionKey"/>, that is used to encrypt and decrypt data.
        /// </summary>
        public new ProtectedDataEncryptionKey DataEncryptionKey
        {
            get => (ProtectedDataEncryptionKey)base.DataEncryptionKey;
            protected set => base.DataEncryptionKey = value;
        }

        /// <inheritdoc/>
        internal object Clone()
        {
            Type genericType = GetType().GenericTypeArguments[0];
            Type settingsType = typeof(FileEncryptionSettings<>).MakeGenericType(genericType);
            return (FileEncryptionSettings)Activator.CreateInstance(
                settingsType,
                new object[] {
                    DataEncryptionKey,
                    EncryptionType,
                    GetSerializer()
                }
            );
        }

        internal static FileEncryptionSettings Create(Type genericType, params object[] parameters)
        {
            Type settingsType = typeof(FileEncryptionSettings<>).MakeGenericType(genericType);
            return (FileEncryptionSettings)Activator.CreateInstance(settingsType, parameters);
        }
    }
}
