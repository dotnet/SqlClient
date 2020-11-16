using System;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    [CLSCompliant(false)]
    public class UInt16Serializer : Serializer<ushort>
    {
        /// <inheritdoc/>
        public override string Identifier => "UInt16";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 2.
        /// </exception>
        public override ushort Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(ushort), nameof(bytes));

            return ToUInt16(bytes, 0);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 2.
        /// </returns>
        public override byte[] Serialize(ushort value) => GetBytes(value);
    }
}
