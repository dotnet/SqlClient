using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableInt64Serializer : Serializer<long?>
    {
        private static readonly Int64Serializer serializer = new Int64Serializer();

        /// <inheritdoc/>
        public override string Identifier => "Int64_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override long? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (long?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(long? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
