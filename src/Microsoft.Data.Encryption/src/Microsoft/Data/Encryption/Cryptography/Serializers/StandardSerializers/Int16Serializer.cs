using System;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class Int16Serializer : Serializer<short>
    {
        /// <inheritdoc/>
        public override string Identifier => "Int16";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 2.
        /// </exception>
        public override short Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(short), nameof(bytes));

            return ToInt16(bytes, 0);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 2.
        /// </returns>
        public override byte[] Serialize(short value) => GetBytes(value);
    }
}
