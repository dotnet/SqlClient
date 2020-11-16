using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    [CLSCompliant(false)]
    public class NullableUInt64Serializer : Serializer<ulong?>
    {
        private static readonly UInt64Serializer serializer = new UInt64Serializer();

        /// <inheritdoc/>
        public override string Identifier => "UInt64_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override ulong? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (ulong?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(ulong? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
