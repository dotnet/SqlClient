using System;
using System.Linq;

using static System.BitConverter;
using static Microsoft.Data.Encryption.Cryptography.Serializers.SqlTimeSerializer;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlDatetime2Serializer : Serializer<DateTime>
    {
        private const int MaxPrecision = 7;
        private const int MinPrecision = 0;
        private const int DefaultPrecision = 7;
        private readonly SqlDateSerializer sqlDateSerializer = new SqlDateSerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_Datetime2";

        private int precision;

        /// <summary>
        /// Gets or sets the maximum number of digits used to represent the Value. The default is 7.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public int Precision
        {
            get { return precision; }
            set
            {
                if (value < MinPrecision || value > MaxPrecision)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }
                precision = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDatetime2Serializer"/> class.
        /// </summary>
        /// <param name="precision">The number of decimal places to which Value is resolved</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public SqlDatetime2Serializer(int precision = DefaultPrecision)
        {
            Precision = precision;
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override DateTime Deserialize(byte[] bytes)
        {
            const int SizeOfTimePart = 5;
            const int SizeOfDatePart = 3;
            const int SizeOfData = SizeOfTimePart + SizeOfDatePart;

            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(SizeOfData, nameof(bytes));

            byte[] padding = { 0, 0, 0 };
            byte[] timePart = bytes.Take(SizeOfTimePart).Concat(padding).ToArray();
            byte[] datePart = bytes.Skip(SizeOfTimePart).Take(SizeOfDatePart).ToArray();

            long timeTicks = ToInt64(timePart, 0);
            DateTime dateTime = sqlDateSerializer.Deserialize(datePart);
            return dateTime.AddTicks(timeTicks);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(DateTime value)
        {
            DateTime normalizedValue = NormalizeToScale(value, Precision);
            long time = normalizedValue.TimeOfDay.Ticks;
            byte[] timePart = GetBytes(time).Take(5).ToArray();
            byte[] datePart = sqlDateSerializer.Serialize(value);
            return timePart.Concat(datePart).ToArray();
        }

        private static DateTime NormalizeToScale(DateTime dateTime, int scale)
        {
            long normalizedTicksOffset = (dateTime.TimeOfDay.Ticks / precisionScale[scale] * precisionScale[scale]) - dateTime.TimeOfDay.Ticks;
            return dateTime.AddTicks(normalizedTicksOffset);
        }
    }
}
