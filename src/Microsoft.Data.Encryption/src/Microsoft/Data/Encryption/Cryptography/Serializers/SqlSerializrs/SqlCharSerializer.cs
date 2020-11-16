using System;
using System.Linq;
using System.Text;
using static System.Text.Encoding;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlCharSerializer : SqlCodePageEncodingSerializer
    {
        private const int DefaultSize = 30;
        private const int MinSize = 1;
        private const int MaxSize = 8000;
        private static readonly char Padding = ' ';

        /// <summary>
        /// The default character encoding Windows-1252. It is also referred to as "ANSI".
        /// </summary>
        private const int DefaultEncodingCodePoint = 1252; 

        /// <inheritdoc/>
        public override string Identifier => "SQL_Char_Nullable";

        private int size;

        /// <summary>
        /// Gets or sets the maximum length of the data.
        /// </summary>
        /// <remarks>
        /// If not explicitly set, the size is defaulted to 30.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [1 - 8000] for this setting.
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

        private Encoding characterEncoding;

        /// <summary>
        /// Gets or sets the character encoding.
        /// </summary>
        /// <remarks>
        /// If not explicitly set, the encoding is defaulted to Windows-1252, which is also referred to as "ANSI".
        /// </remarks>
        public int CodePageCharacterEncoding
        {
            get => characterEncoding.CodePage;
            set => characterEncoding = GetEncoding(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCharSerializer"/> class.
        /// </summary>
        /// <param name="size">The maximum length of the data</param>
        /// <param name="codePageCharacterEncoding">The code page character encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [1 - 8000] for this setting.
        /// </exception>
        public SqlCharSerializer(int size = DefaultSize, int codePageCharacterEncoding = DefaultEncodingCodePoint)
        {
            Size = size;
            characterEncoding = GetEncoding(codePageCharacterEncoding);
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
            return bytes.IsNull() ? null : PadToLength(characterEncoding.GetString(bytes));
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
            return value.IsNull() ? null : characterEncoding.GetBytes(TrimToLength(value));
        }

        private string TrimToLength(string value) => value.Length > Size ? new string(value.Take(Size).ToArray()) : value;

        private string PadToLength(string value) => value.Length < Size ? value.PadRight(Size, Padding) : value;
    }
}
