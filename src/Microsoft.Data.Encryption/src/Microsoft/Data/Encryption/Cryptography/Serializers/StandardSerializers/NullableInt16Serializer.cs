using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableInt16Serializer : Serializer<short?>
    {
        private static readonly Int16Serializer serializer = new Int16Serializer();

        /// <inheritdoc/>
        public override string Identifier => "Int16_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 2.
        /// </exception>
        public override short? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (short?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 2.
        /// </returns>
        public override byte[] Serialize(short? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
