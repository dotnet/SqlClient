using System;
using System.Linq;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlDatetimeoffsetSerializer : Serializer<DateTimeOffset>
    {
        private const int MaxScale = 7;
        private const int MinScale = 0;
        private const int DefaultScale = 7;
        private readonly SqlDatetime2Serializer sqlDatetime2Serializer;

        /// <inheritdoc/>
        public override string Identifier => "SQL_DateTimeOffset";

        private int scale;

        /// <summary>
        /// Gets or sets the number of decimal places to which Value is resolved. The default is 7.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public int Scale
        {
            get => scale;
            set
            {
                if (value < MinScale || value > MaxScale)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }
                scale = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDatetimeoffsetSerializer"/> class.
        /// </summary>
        /// <param name="scale">The number of decimal places to which Value is resolved.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public SqlDatetimeoffsetSerializer(int scale = DefaultScale)
        {
            Scale = scale;
            sqlDatetime2Serializer = new SqlDatetime2Serializer(scale);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 10.
        /// </exception>
        public override DateTimeOffset Deserialize(byte[] bytes)
        {
            const int DateTimeIndex = 0;
            const int TimeSpanIndex = sizeof(long);
            const int DataSize = sizeof(long) + sizeof(short);

            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(DataSize, nameof(bytes));

            byte[] dateTimePart = bytes.Skip(DateTimeIndex).Take(sizeof(long)).ToArray();
            byte[] offsetPart = bytes.Skip(TimeSpanIndex).Take(sizeof(short)).ToArray();

            short minutes = ToInt16(offsetPart, 0);
            DateTime dateTime = sqlDatetime2Serializer.Deserialize(dateTimePart).AddMinutes(minutes);
            TimeSpan offset = new TimeSpan(0, minutes, 0);
            return new DateTimeOffset(dateTime, offset);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 10.
        /// </returns>
        public override byte[] Serialize(DateTimeOffset value)
        {
            byte[] datetimePart = sqlDatetime2Serializer.Serialize(value.UtcDateTime);
            short offsetMinutes = (short)value.Offset.TotalMinutes;
            byte[] offsetPart = GetBytes(offsetMinutes);
            return datetimePart.Concat(offsetPart).ToArray();
        }
    }
}
