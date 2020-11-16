using System;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    [CLSCompliant(false)]
    public class UInt32Serializer : Serializer<uint>
    {
        /// <inheritdoc/>
        public override string Identifier => "UInt32";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override uint Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(uint), nameof(bytes));

            return ToUInt32(bytes, 0);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        public override byte[] Serialize(uint value) => GetBytes(value);
    }
}
