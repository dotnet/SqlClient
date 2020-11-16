using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableInt32Serializer : Serializer<int?>
    {
        private static readonly Int32Serializer serializer = new Int32Serializer();

        /// <inheritdoc/>
        public override string Identifier => "Int32_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override int? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (int?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        public override byte[] Serialize(int? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
