using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableDateSerializer : Serializer<DateTime?>
    {
        private static readonly SqlDateSerializer serializer = new SqlDateSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Date_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 3.
        /// </exception>
        public override DateTime? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (DateTime?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 3.
        /// </returns>
        public override byte[] Serialize(DateTime? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
