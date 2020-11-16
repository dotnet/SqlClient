using System;
using static System.Text.Encoding;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class StringSerializer : Serializer<string>
    {
        /// <inheritdoc/>
        public override string Identifier => "String";

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="bytes"/> is null.
        /// </exception>
        public override string Deserialize(byte[] bytes)
        {
            return bytes.IsNull() ? null : Unicode.GetString(bytes);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="value"/> is null.
        /// </exception>
        public override byte[] Serialize(string value)
        {
            return value.IsNull() ? null : Unicode.GetBytes(value);
        }
    }
}
