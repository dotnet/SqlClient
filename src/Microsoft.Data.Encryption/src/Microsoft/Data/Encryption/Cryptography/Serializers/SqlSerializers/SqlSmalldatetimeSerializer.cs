using System;
using System.Data.SqlTypes;
using System.Linq;
using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlSmalldatetimeSerializer : Serializer<DateTime>
    {
        private static readonly DateTime MinValue = DateTime.Parse("1900-01-01 00:00:00");
        private static readonly DateTime MaxValue = DateTime.Parse("2079-06-06 23:59:59");

        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallDateTime";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 4.
        /// </exception>
        public override DateTime Deserialize(byte[] bytes)
        {
            const int SizeOfDate = 2;
            const int SizeOfTime = 2;
            const int SizeOfDateTime = SizeOfDate + SizeOfTime;

            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(SizeOfDateTime, nameof(bytes));

            int dayTicks = ToUInt16(bytes.Take(SizeOfDate).ToArray(), 0);
            int timeTicks = ToUInt16(bytes.Skip(SizeOfDate).Take(SizeOfTime).ToArray(), 0) * SqlDateTime.SQLTicksPerMinute;
            SqlDateTime sqlDateTime = new SqlDateTime(dayTicks, timeTicks);
            return sqlDateTime.Value;
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 4.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(DateTime value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            SqlDateTime sqlDateTime = new SqlDateTime(value.AddSeconds(30));
            byte[] dayTicks = GetBytes((short)sqlDateTime.DayTicks);
            byte[] timeTicks = GetBytes((short)(sqlDateTime.TimeTicks / SqlDateTime.SQLTicksPerMinute));
            return dayTicks.Concat(timeTicks).ToArray();
        }
    }
}
