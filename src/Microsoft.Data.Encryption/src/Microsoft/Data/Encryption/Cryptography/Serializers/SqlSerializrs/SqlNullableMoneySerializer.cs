using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableMoneySerializer : Serializer<decimal?>
    {
        private static readonly SqlMoneySerializer serializer = new SqlMoneySerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Money_Nullable";

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
