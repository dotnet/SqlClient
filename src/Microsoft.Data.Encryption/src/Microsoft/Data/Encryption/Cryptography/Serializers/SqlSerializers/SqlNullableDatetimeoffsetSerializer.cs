using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableDatetimeoffsetSerializer : Serializer<DateTimeOffset?>
    {
        private const int DefaultScale = 7;
        private readonly SqlDatetimeoffsetSerializer serializer;

        /// <inheritdoc/>
        public override string Identifier => "SQL_DateTimeOffset_Nullable";

        /// <summary>
        /// Gets or sets the number of decimal places to which Value is resolved. The default is 7.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public int Scale
        {
            get => serializer.Scale;
            set => serializer.Scale = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlNullableDatetimeoffsetSerializer"/> class.
        /// </summary>
        /// <param name="scale">The number of decimal places to which Value is resolved.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public SqlNullableDatetimeoffsetSerializer(int scale = DefaultScale)
        {
            serializer = new SqlDatetimeoffsetSerializer(scale);
        }


        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 10.
        /// </exception>
        public override DateTimeOffset? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (DateTimeOffset?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 10.
        /// </returns>
        public override byte[] Serialize(DateTimeOffset? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
