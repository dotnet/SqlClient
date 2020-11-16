using System;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class BooleanSerializer : Serializer<bool>
    {
        /// <inheritdoc/>
        public override string Identifier => "Boolean";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 1.
        /// </exception>
        public override bool Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(bool), nameof(bytes));

            return ToBoolean(bytes, 0);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 1.
        /// </returns>
        public override byte[] Serialize(bool value) => GetBytes(value);
    }
}
