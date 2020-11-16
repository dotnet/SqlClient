using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    [CLSCompliant(false)]
    public class NullableUInt32Serializer : Serializer<uint?>
    {
        private static readonly UInt32Serializer serializer = new UInt32Serializer();

        /// <inheritdoc/>
        public override string Identifier => "UInt32_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override uint? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (uint?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        public override byte[] Serialize(uint? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
