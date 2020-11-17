using System;
using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlFloatSerializer : Serializer<double>
    {
        /// <inheritdoc/>
        public override string Identifier => "SQL_Float";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override double Deserialize(byte[] bytes)
        {
            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(sizeof(double), nameof(bytes));

            return ToDouble(bytes, 0); 
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(double value) 
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            return GetBytes(value);
        }
    }
}
