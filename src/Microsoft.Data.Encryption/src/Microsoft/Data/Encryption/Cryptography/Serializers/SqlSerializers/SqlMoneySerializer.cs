using System;
using System.Data.SqlTypes;
using System.Linq;

using static System.BitConverter;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlMoneySerializer : Serializer<decimal>
    {
        private const byte Scale = 4;
        private const decimal MinValue = -922337203685477.5808M;
        private const decimal MaxValue = 922337203685477.5807M;

        /// <inheritdoc/>
        public override string Identifier => "SQL_Money";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override decimal Deserialize(byte[] bytes)
        {
            const int SizeOfData = 8;

            bytes.ValidateNotNull(nameof(bytes));
            bytes.ValidateSize(SizeOfData, nameof(bytes));

            uint low = ToUInt32(bytes, 4);
            int middle = ToInt32(bytes, 0);
            long longValue = ((long)middle << 32) + low;
            bool isNegative = longValue < 0;
            int sign = isNegative ? -1 : 1;
            long signedLongValue = longValue * sign;
            int signedLow = (int)(signedLongValue & uint.MaxValue);
            int signedMiddle = (int)(signedLongValue >> 32);

            return new decimal(signedLow, signedMiddle, 0, isNegative, Scale);
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes with length 8.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <paramref name="value"/> is out of range.
        /// </exception>
        public override byte[] Serialize(decimal value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
            }

            value = Normalize(value);
            int sign = value > -1 ? 1 : -1;
            int[] decimalBits = decimal.GetBits(value);
            long longValue = ((long)decimalBits[1] << 32) + (uint)decimalBits[0];
            long signedLongValue = longValue * sign;
            int low = (int)(signedLongValue >> 32);
            int mid = (int)signedLongValue;
            byte[] lowbytes = GetBytes(low);
            byte[] midBytes = GetBytes(mid);

            return lowbytes.Concat(midBytes).ToArray();
        }

        private decimal Normalize(decimal value) => new SqlMoney(value).Value;
    }
}
