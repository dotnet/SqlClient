using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableDateTimeSerializer : Serializer<DateTime?>
    {
        private static readonly DateTimeSerializer serializer = new DateTimeSerializer();

        /// <inheritdoc/>
        public override string Identifier => "DateTime_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override DateTime? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (DateTime?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(DateTime? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
