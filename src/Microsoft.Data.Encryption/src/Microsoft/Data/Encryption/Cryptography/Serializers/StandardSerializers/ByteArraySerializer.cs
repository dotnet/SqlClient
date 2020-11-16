using System;

namespace Microsoft.Data.Encryption.Cryptography.Serializers
{
    /// <inheritdoc/>
    public class ByteArraySerializer : Serializer<byte[]>
    {
        /// <inheritdoc/>
        public override string Identifier => "ByteArray";

        /// <inheritdoc/>
        public override byte[] Deserialize(byte[] bytes)
        {
            return bytes;
        }

        /// <inheritdoc/>
        public override byte[] Serialize(byte[] value)
        {
            return value;
        }
    }
}
