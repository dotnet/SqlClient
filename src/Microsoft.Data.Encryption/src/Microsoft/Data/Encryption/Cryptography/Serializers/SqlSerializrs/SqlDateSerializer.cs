using System;
using System.Linq;
using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlDateSerializer : Serializer<DateTime>
    {
        private const int SizeOfDate = 3;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Date";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 3.
        /// </exception>
        public override DateTime Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(SizeOfDate, nameof(bytes));

            byte[] padding = { 0 };
            byte[] bytesWithPadding = bytes.Concat(padding).ToArray();
            int days = ToInt32(bytesWithPadding, 0);
            return DateTime.MinValue.AddDays(days);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 3.
        /// </returns>
        public override byte[] Serialize(DateTime value)
        {
            int days = value.Subtract(DateTime.MinValue).Days;
            return GetBytes(days).Take(SizeOfDate).ToArray();
        }
    }
}
