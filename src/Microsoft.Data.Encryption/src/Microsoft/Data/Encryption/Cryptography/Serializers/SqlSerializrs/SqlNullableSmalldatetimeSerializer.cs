using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableSmalldatetimeSerializer : Serializer<DateTime?>
    {
        private static readonly SqlSmalldatetimeSerializer serializer = new SqlSmalldatetimeSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallDateTime_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override DateTime? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (DateTime?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
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
