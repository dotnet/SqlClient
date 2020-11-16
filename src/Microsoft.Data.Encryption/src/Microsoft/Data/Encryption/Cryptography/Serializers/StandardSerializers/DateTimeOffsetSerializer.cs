using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class DateTimeOffsetSerializer : Serializer<DateTimeOffset>
    {
        private static readonly DateTimeSerializer DateTimeSerializer = new DateTimeSerializer();
        private static readonly TimeSpanSerializer TimeSpanSerializer = new TimeSpanSerializer();

        /// <inheritdoc/>
        public override string Identifier => "DateTimeOffset";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 16.
        /// </exception>
        public override DateTimeOffset Deserialize(byte[] bytes)
        {
            const int DateTimeIndex = 0;
            const int TimeSpanIndex = sizeof(long);
            const int MinimumSize = sizeof(long) + sizeof(long);

            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateGreaterThanSize(MinimumSize, nameof(bytes));

            byte[] dateTimePart = bytes.Skip(DateTimeIndex).Take(sizeof(long)).ToArray();
            byte[] timeSpanPart = bytes.Skip(TimeSpanIndex).Take(sizeof(long)).ToArray();

            DateTime dateTime = DateTimeSerializer.Deserialize(dateTimePart);
            TimeSpan timeSpan = TimeSpanSerializer.Deserialize(timeSpanPart);

            return new DateTimeOffset(dateTime, timeSpan);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 16.
        /// </returns>
        public override byte[] Serialize(DateTimeOffset value)
        {
            IEnumerable<byte> dateTimePart = DateTimeSerializer.Serialize(value.DateTime);
            IEnumerable<byte> timeSpanPart = TimeSpanSerializer.Serialize(value.Offset);

            return dateTimePart.Concat(timeSpanPart).ToArray();
        }
    }
}
