using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class ByteSerializer : Serializer<byte>
    {
        /// <inheritdoc/>
        public override string Identifier => "Byte";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 1.
        /// </exception>
        public override byte Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(byte), nameof(bytes));

            return bytes[0];
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 1.
        /// </returns>
        public override byte[] Serialize(byte value) => new byte[] { value };
    }
}
