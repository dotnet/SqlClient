using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    [CLSCompliant(false)]
    public class NullableSByteSerializer : Serializer<sbyte?>
    {
        private static readonly SByteSerializer serializer = new SByteSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SByte_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 1.
        /// </exception>
        public override sbyte? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (sbyte?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 1.
        /// </returns>
        public override byte[] Serialize(sbyte? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
