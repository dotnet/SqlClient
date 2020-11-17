using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableIntSerializer : Serializer<int?>
    {
        private static readonly SqlIntSerializer serializer = new SqlIntSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Int_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override int? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (int?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(int? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
