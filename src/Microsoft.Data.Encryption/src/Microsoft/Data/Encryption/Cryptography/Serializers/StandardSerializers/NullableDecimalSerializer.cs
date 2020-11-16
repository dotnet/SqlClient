using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableDecimalSerializer : Serializer<decimal?>
    {
        private static readonly DecimalSerializer serializer = new DecimalSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Decimal_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 16.
        /// </exception>
        public override decimal? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (decimal?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 16.
        /// </returns>
        public override byte[] Serialize(decimal? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
