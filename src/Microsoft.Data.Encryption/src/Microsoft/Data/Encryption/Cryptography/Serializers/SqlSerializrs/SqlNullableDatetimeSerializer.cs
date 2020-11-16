using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableDatetimeSerializer : Serializer<DateTime?>
    {
        private static readonly SqlDatetimeSerializer serializer = new SqlDatetimeSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_DateTime_Nullable";

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
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(DateTime? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
