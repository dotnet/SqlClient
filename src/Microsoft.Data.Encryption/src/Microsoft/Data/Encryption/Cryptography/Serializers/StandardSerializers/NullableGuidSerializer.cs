using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class NullableGuidSerializer : Serializer<Guid?>
    {
        private static readonly GuidSerializer serializer = new GuidSerializer();
        /// <inheritdoc/>
        public override string Identifier => "UniqueIdentifier_Nullable";

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 16.
        /// </exception>
        public override Guid? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (Guid?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 16.
        /// </returns>
        public override byte[] Serialize(Guid? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
