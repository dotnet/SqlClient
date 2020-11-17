using System;
using System.Linq;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class DecimalSerializer : Serializer<decimal>
    {
        /// <inheritdoc/>
        public override string Identifier => "Decimal";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 16.
        /// </exception>
        public override decimal Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(decimal), nameof(bytes));

            int[] bits = Enumerable.Range(0, 4)
                .Select(i => ToInt32(bytes, i * sizeof(int)))
                .ToArray();

            return new decimal(bits);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(decimal value) => decimal.GetBits(value).SelectMany(GetBytes).ToArray();
    }
}
