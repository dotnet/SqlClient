using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class GuidSerializer : Serializer<Guid>
    {
        /// <inheritdoc/>
        public override string Identifier => "UniqueIdentifier";

        private const int SizeOfGuid = 16;

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 16.
        /// </exception>
        public override Guid Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(SizeOfGuid, nameof(bytes));

            return new Guid(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 16.
        /// </returns>
        public override byte[] Serialize(Guid value) => value.ToByteArray();
    }
}
