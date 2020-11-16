using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableDoubleSerializer : Serializer<double?>
    {
        private static readonly DoubleSerializer serializer = new DoubleSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Float64_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override double? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (double?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(double? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
