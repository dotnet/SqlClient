using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlUniqueidentifierSerializer : Serializer<Guid>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_UniqueIdentifier";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 16.
        /// </exception>
        public override Guid Deserialize(byte[] bytes)
        {
            const int SizeOfGuid = 16;

            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(SizeOfGuid, nameof(bytes));

            return new Guid(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 16.
        /// </returns>
        public override byte[] Serialize(Guid value) => value.ToByteArray();
    }
}
