using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class SqlNullableNumericSerializer : Serializer<decimal?>
    {
        private const int DefaultPrecision = 18;
        private const int DefaultScale = 0;
        private SqlNumericSerializer serializer;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Numeric_Nullable";

        /// <summary>
        /// Gets or sets the maximum number of digits used to represent the value. The default precision is 18.
        /// </summary>
        /// <remarks>
        /// The <see cref="Precision"/> represents the maximum total number of decimal digits to be stored. 
        /// This number includes both the left and the right sides of the decimal point. 
        /// The precision must be a value from 1 through the maximum precision of 38.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [1 - 38] for this setting.
        /// </exception>
        public int Precision
        {
            get => serializer.Precision;
            set => serializer.Precision = value;
        }

        /// <summary>
        /// Gets or sets the number of decimal places to which Value is resolved.
        /// </summary>
        /// <remarks>
        /// The number of decimal digits that are stored to the right of the decimal point. This number is subtracted from the <see cref="Precision"/>
        /// to determine the maximum number of digits to the left of the decimal point. <see cref="Scale"/> must be a value from 0 through <see cref="Precision"/>. 
        /// The default scale is 0 and so 0 &#8804; <see cref="Scale"/> &#8804; <see cref="Precision"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [0 - <see cref="Precision"/>] for this setting.
        /// </exception>
        public int Scale
        {
            get => serializer.Scale;
            set => serializer.Scale = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlNullableNumericSerializer"/> class.
        /// </summary>
        /// <param name="precision">The maximum number of digits used to represent the value. The default precision is 18.</param>
        /// <param name="scale">The number of decimal places to which Value is resolved. The default scale is 0.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="precision"/> is set to a value that is out of the valid range [1 - 38] or when <paramref name="scale"/> 
        /// is set to a value that is out of the valid range [0 -  <paramref name="precision"/>] for this setting.
        /// </exception>
        public SqlNullableNumericSerializer(int precision = DefaultPrecision, int scale = DefaultScale)
        {
            serializer = new SqlNumericSerializer(precision, scale);
        }


        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 17.
        /// </exception>
        public override decimal? Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? (decimal?)null : serializer.Deserialize(bytes);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 17.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(decimal? value)
        {
            return value.IsNull() ? null : serializer.Serialize(value.Value);
        }
    }
}
