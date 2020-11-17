using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableCharSerializer : Serializer<char?>
    {
        private static readonly CharSerializer serializer = new CharSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Character_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 2.
        /// </exception>
        public override char? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (char?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 2.
        /// </returns>
        public override byte[] Serialize(char? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
