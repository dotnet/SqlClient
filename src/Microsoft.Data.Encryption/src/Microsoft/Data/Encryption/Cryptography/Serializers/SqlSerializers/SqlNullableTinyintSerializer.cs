using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableTinyintSerializer : Serializer<byte?>
    {
        private static readonly SqlTinyintSerializer serializer = new SqlTinyintSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_TinyInt_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override byte? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (byte?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(byte? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
