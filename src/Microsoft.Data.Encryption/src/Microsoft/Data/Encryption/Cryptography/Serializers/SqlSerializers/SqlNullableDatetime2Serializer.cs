using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableDatetime2Serializer : Serializer<DateTime?>
    {
        private const int DefaultPrecision = 7;

        private readonly SqlDatetime2Serializer serializer;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Datetime2_Nullable";

        /// <summary>
        /// Gets or sets the maximum number of digits used to represent the Value. The default is 7.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public int Precision
        {
            get => serializer.Precision;
            set => serializer.Precision = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlDatetime2Serializer"/> class.
        /// </summary>
        /// <param name="precision">The number of decimal places to which Value is resolved</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - 7] for this setting.
        /// </exception>
        public SqlNullableDatetime2Serializer(int precision = DefaultPrecision)
        {
            serializer = new SqlDatetime2Serializer(precision);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override DateTime? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (DateTime?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        public override byte[] Serialize(DateTime? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
