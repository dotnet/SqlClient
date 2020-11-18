using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableRealSerializer : Serializer<float?>
    {
        private static readonly SqlRealSerializer serializer = new SqlRealSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Real_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override float? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (float?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(float? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
