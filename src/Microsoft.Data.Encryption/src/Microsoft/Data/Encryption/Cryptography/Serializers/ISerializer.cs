namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <summary>
    /// Contains the methods for serializing and deserializing data objects.
    /// </summary>
    public interface ISerializer 
    {
        /// <summary>
        /// The Identifier uniquely identifies a particular Serializer implementation.
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// Serializes the provided <paramref name="value"/>
        /// </summary>
        /// <param name="value">The value to be serialized</param>
        /// <returns>The serialized data as a byte array</returns>
        byte[] Serialize(object value);

        /// <summary>
        /// Deserializes the provided <paramref name="bytes"/>
        /// </summary>
        /// <param name="bytes">The data to be deserialized</param>
        /// <returns>The serialized data</returns>
        object Deserialize(byte[] bytes);
    }


    /// <summary>
    /// Contains the methods for serializing and deserializing data objects.
    /// </summary>
    /// <typeparam name="T">The type on which this will perform serialization operations.</typeparam>
    public abstract class Serializer<T> : ISerializer
    {
        /// <inheritdoc/>
        public abstract string Identifier { get; }

        /// <summary>
        /// Serializes the provided <paramref name="value"/>
        /// </summary>
        /// <param name="value">The value to be serialized</param>
        /// <returns>The serialized data as a byte array</returns>
        public abstract byte[] Serialize(T value);

        /// <summary>
        /// Deserializes the provided <paramref name="bytes"/>
        /// </summary>
        /// <param name="bytes">The data to be deserialized</param>
        /// <returns>The serialized data</returns>
        public abstract T Deserialize(byte[] bytes);

        byte[] ISerializer.Serialize(object value) => Serialize((T)value);

        object ISerializer.Deserialize(byte[] bytes) => Deserialize(bytes);
    }
}
