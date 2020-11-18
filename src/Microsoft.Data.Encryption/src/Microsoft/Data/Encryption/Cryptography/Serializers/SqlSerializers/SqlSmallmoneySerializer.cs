using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlSmallmoneySerializer : Serializer<decimal>
    {        
        private const decimal MinValue = -214748.3648M;
        private const decimal MaxValue = 214748.3647M;
        private static readonly SqlMoneySerializer sqlMoneySerializer = new SqlMoneySerializer();

        /// <inheritdoc/>
        public override string Identifier => "SQL_SmallMoney";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length of <paramref name="bytes"/> is less than 8.
        /// </exception>
        public override decimal Deserialize(byte[] bytes)
        {
            return sqlMoneySerializer.Deserialize(bytes);
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

            return sqlMoneySerializer.Serialize(value);
        }
    }
}
