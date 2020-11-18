using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableBitSerializer : Serializer<bool?>
    {
        private static readonly SqlBitSerializer serializer = new SqlBitSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Bit_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override bool? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (bool?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(bool? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
