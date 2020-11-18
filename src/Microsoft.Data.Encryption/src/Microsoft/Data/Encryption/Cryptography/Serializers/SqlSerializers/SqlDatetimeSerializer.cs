using System;
using System.Data.SqlTypes;
using System.Linq;
using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlDatetimeSerializer : Serializer<DateTime>
    {
        private static readonly DateTime MinValue = DateTime.Parse("1753-01-01 00:00:00");

        /// <inheritdoc/>
        public override string Identifier => "SQL_DateTime";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override DateTime Deserialize(byte[] bytes)
        {
            const int SizeOfDate = 4;
            const int SizeOfTime = 4;
            const int SizeOfDateTime = SizeOfDate + SizeOfTime;

            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(SizeOfDateTime, nameof(bytes));

            int dayTicks = ToInt32(bytes.Take(SizeOfDate).ToArray(), 0);
            int timeTicks = ToInt32(bytes.Skip(SizeOfDate).Take(SizeOfTime).ToArray(), 0);
            SqlDateTime sqlDateTime = new SqlDateTime(dayTicks, timeTicks);
            return sqlDateTime.Value;
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(DateTime value)
        {
            if (value < MinValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            SqlDateTime sqlDateTime = new SqlDateTime(value);
            byte[] dayTicks = GetBytes(sqlDateTime.DayTicks);
            byte[] timeTicks = GetBytes(sqlDateTime.TimeTicks);
            return dayTicks.Concat(timeTicks).ToArray();
        }
    }
}
