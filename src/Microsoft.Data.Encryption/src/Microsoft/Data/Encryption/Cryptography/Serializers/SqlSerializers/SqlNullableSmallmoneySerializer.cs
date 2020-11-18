using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableSmallmoneySerializer : Serializer<decimal?>
    {
        private static readonly SqlSmallmoneySerializer serializer = new SqlSmallmoneySerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallMoney_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override decimal? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (decimal?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(decimal? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
