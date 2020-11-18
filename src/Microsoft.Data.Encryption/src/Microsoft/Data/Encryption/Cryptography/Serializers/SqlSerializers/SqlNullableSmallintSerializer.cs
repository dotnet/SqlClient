using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableSmallintSerializer : Serializer<short?>
    {
        private static readonly SqlSmallintSerializer serializer = new SqlSmallintSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallInt_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override short? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (short?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(short? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
