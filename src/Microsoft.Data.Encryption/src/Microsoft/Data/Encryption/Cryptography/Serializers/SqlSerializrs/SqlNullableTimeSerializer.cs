using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableTimeSerializer : Serializer<TimeSpan?>
    {
        private const int DefaultScale = 7;
        private readonly SqlTimeSerializer serializer;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Time_Nullable";

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
            get => serializer.Scale;
            set => serializer.Scale = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlNullableTimeSerializer"/> class.
        /// </summary>
        /// <param name="scale">The number of digits for the fractional part of the seconds.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public SqlNullableTimeSerializer(int scale = DefaultScale)
        {
            serializer = new SqlTimeSerializer(scale);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 5.
        /// </exception>
        public override TimeSpan? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (TimeSpan?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 5.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(TimeSpan? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
