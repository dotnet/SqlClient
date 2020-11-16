using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    [CLSCompliant(false)]
    public class NullableUInt16Serializer : Serializer<ushort?>
    {
        private static readonly UInt16Serializer serializer = new UInt16Serializer();

        /// <inheritdoc/>
        public override string Identifier => "UInt16_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 2.
        /// </exception>
        public override ushort? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (ushort?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 2.
        /// </returns>
        public override byte[] Serialize(ushort? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
