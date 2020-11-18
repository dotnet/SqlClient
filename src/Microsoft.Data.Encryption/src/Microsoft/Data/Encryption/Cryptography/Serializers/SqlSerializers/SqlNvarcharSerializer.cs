using System;
using System.Linq;

using static System.Linq.Enumerable;
using static System.Text.Encoding;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlNvarcharSerializer : Serializer<string>
    {
        private const int Max = -1;
        private const int MinSize = 1;
        private const int MaxSize = 4000;
        private const int DefaultSize = 30;

        /// <inheritdoc/>
        public override string Identifier => "SQL_NVarChar_Nullable";

        private int size;

        /// <summary>
        /// Gets or sets the maximum length of the data.
        /// </summary>
        /// <remarks>
        /// If not explicitly set, the size is defaulted to 30.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [-1, 1 - 4000] for this setting.
        /// </exception>
        public int Size
        {
            get => size;
            set
            {
                if (value != Max && (value < MinSize || value > MaxSize))
                {
                    throw new ArgumentOutOfRangeException($"Parameter value {value} is out of range");
                }

                size = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlNvarcharSerializer"/> class.
        /// </summary>
        /// <param name="size">The maximum length of the data</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [-1, 1 - 4000] for this setting.
        /// </exception>
        public SqlNvarcharSerializer(int size = DefaultSize)
        {
            Size = size;
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes.
        /// </returns>
        public override string Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? null : Unicode.GetString(bytes);
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
            if (value.IsNull())
            {
                return null;
            }

            if (size != Max)
            {
                string trimmedValue = TrimToLength(value);
                return Unicode.GetBytes(trimmedValue);
            }

            return Unicode.GetBytes(value);
        }

        private string TrimToLength(string value) => value.Length > Size ? new string(value.Take(Size).ToArray()) : value;
    }
}
