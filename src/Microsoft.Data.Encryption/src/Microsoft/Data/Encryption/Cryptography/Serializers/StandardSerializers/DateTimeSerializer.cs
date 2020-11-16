using System;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class DateTimeSerializer : Serializer<DateTime>
    {
        /// <inheritdoc/>
        public override string Identifier => "DateTime";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override DateTime Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(sizeof(long), nameof(bytes));

            return DateTime.FromBinary(ToInt64(bytes, 0));
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(DateTime value) => GetBytes(value.ToBinary());
    }
}
