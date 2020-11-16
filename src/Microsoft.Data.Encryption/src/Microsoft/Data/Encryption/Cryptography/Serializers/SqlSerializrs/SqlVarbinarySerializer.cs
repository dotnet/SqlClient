using System;
using System.Linq;

using static System.Linq.Enumerable;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public sealed class SqlVarbinarySerializer : Serializer<byte[]>
    {
        private const int Max = -1;
        private const int DefaultSize = 30;
        private const int MinSize = 1;
        private const int MaxSize = 8000;

        /// <inheritdoc/>
        public override string Identifier => "SQL_VarBinary";

        private int size;

        /// <summary>
        /// Gets or sets the maximum length of the data.
        /// </summary>
        /// <remarks>
        /// If not explicitly set, the size is defaulted to 30.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [-1, 1 - 8000] for this setting.
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
        /// Initializes a new instance of the <see cref="SqlVarbinarySerializer"/> class.
        /// </summary>
        /// <param name="size">The maximum length of the data</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when set to a value that is out of the valid range [-1, 1 - 8000] for this setting.
        /// </exception>
        public SqlVarbinarySerializer(int size = DefaultSize)
        {
            Size = size;
        }

        /// <inheritdoc/>
        /// <returns>
        /// An array of bytes.
        /// </returns>
        public override byte[] Deserialize(byte[] bytes) => bytes;

        /// <inheritdoc/>
        /// <remarks>
        /// If the value's length is greater than the size, then the value will be truncated on the left to match the size.
        /// </remarks>
        /// <returns>
        /// An array of bytes.
        /// </returns>
        public override byte[] Serialize(byte[] value)
        {
            if (value.IsNull())
            {
                return null;
            }

            if (size != Max)
            {
                return TrimToLength(value);
            }

            return value;
        }

        private byte[] TrimToLength(byte[] value) => value.Length > Size ? value.Take(Size).ToArray() : value;
    }
}
