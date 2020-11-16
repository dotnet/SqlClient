using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableSingleSerializer : Serializer<float?>
    {
        private static readonly SingleSerializer serializer = new SingleSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Single_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override float? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (float?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        public override byte[] Serialize(float? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
