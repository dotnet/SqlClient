using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableTimeSpanSerializer : Serializer<TimeSpan?>
    {
        private static readonly TimeSpanSerializer serializer = new TimeSpanSerializer();

        /// <inheritdoc/>
        public override string Identifier => "Time_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override TimeSpan? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (TimeSpan?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(TimeSpan? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
