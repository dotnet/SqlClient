using System;
using System.Linq;

using static System.Linq.Enumerable;
using static System.Text.Encoding;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlNcharSerializer : Serializer<string>
    {
        private const int MinSize = 1;
        private const int MaxSize = 4000;
        private const int DefaultSize = 30;
        private static readonly char Padding = ' ';

        /// <inheritdoc/>
        public override string Identifier => "SQL_NChar_Nullable";

        private int size;

        /// <summary>
        /// Gets or sets the maximum length of the data.
        /// </summary>
        /// <remarks>
        /// If not explicitly set, the size is defaulted to 30.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [1 - 4000] for this setting.
        /// </exception>
        public int Size
        {
            get => size;
            set
            {
                if (value < MinSize || value > MaxSize)
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }

                size = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlNcharSerializer"/> class.
        /// </summary>
        /// <param name="size">The maximum length of the data</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [1 - 4000] for this setting.
        /// </exception>
        public SqlNcharSerializer(int size = DefaultSize)
        {
            Size = size;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// If the deserialized value's length is less than the size, then the value will be padded on the
        /// left to match the size. Padding is achieved by using spaces.
        /// </remarks>
        /// <returns>
        /// An array of bytes.
        /// </returns>
        public override string Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? null : PadToLength(Unicode.GetString(bytes));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// If the value's length is greater than the size, then the value will be truncated on the left to match the size.
        /// </remarks>
        /// <returns>
        /// An array of bytes.
        /// </returns>
        public override byte[] Serialize(string value)
        {
            return value.IsNull() ? null : Unicode.GetBytes(TrimToLength(value));
        }

        private string TrimToLength(string value) => value.Length > Size ? new string(value.Take(Size).ToArray()) : value;

        private string PadToLength(string value) => value.Length < Size ? value.PadRight(Size, Padding) : value;
    }
}
