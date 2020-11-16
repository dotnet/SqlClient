using System;
using System.Linq;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlTimeSerializer : Serializer<TimeSpan>
    {
        private const int MaxScale = 7;
        private const int MinScale = 0;
        private const int DefaultScale = 7;
        private const int MaxTimeLength = 5;
        private static readonly TimeSpan MinValue = TimeSpan.Parse("00:00:00.0000000");
        private static readonly TimeSpan MaxValue = TimeSpan.Parse("23:59:59.9999999");

        /// <inheritdoc/>
        public override string Identifier => "SQL_Time";

        private int scale;

        /// <summary>
        /// Gets or sets the number of digits for the fractional part of the seconds.
        /// </summary>
        /// <remarks>
        /// This can be an integer from 0 to 7. The default fractional scale is 7 (100ns).
        /// </remarks>
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
        /// Initializes a new instance of the <see cref="SqlTimeSerializer"/> class.
        /// </summary>
        /// <param name="scale">The number of digits for the fractional part of the seconds.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public SqlTimeSerializer(int scale = DefaultScale)
        {
            Scale = scale;
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 5.
        /// </exception>
        public override TimeSpan Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(MaxTimeLength, nameof(bytes));

            byte[] padding = { 0, 0, 0 };
            byte[] paddedBytes = bytes.Concat(padding).ToArray();
            long timeTicks = ToInt64(paddedBytes, 0);

            return new TimeSpan(timeTicks);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 5.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(TimeSpan value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            long time = value.Ticks / precisionScale[scale] * precisionScale[scale];

            return GetBytes(time).Take(MaxTimeLength).ToArray();
        }

        internal static readonly long[] precisionScale = {
            10000000,
            1000000,
            100000,
            10000,
            1000,
            100,
            10,
            1,
        };
    }
}
