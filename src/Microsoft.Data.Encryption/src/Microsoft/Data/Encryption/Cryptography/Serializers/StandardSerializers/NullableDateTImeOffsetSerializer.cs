using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableDateTimeOffsetSerializer : Serializer<DateTimeOffset?>
    {
        private static readonly DateTimeOffsetSerializer serializer = new DateTimeOffsetSerializer();

        /// <inheritdoc/>
        public override string Identifier => "DateTimeOffset_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 16.
        /// </exception>
        public override DateTimeOffset? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (DateTimeOffset?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 16.
        /// </returns>
        public override byte[] Serialize(DateTimeOffset? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
