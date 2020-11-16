using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableBooleanSerializer : Serializer<bool?>
    {
        private static readonly BooleanSerializer serializer = new BooleanSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Boolean_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 1.
        /// </exception>
        public override bool? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (bool?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 1.
        /// </returns>
        public override byte[] Serialize(bool? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
