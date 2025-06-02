using System;
using System.Data.SqlTypes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.SqlClient;

#nullable enable
namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/SqlVectorFloat32/*' />
    public class SqlVectorFloat32 : INullable, ISqlVector
    {
        #region Constructors

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor1/*' />
        public SqlVectorFloat32(int length)
        : this()
        {
            _elementCount = length;
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor2/*' />
        public SqlVectorFloat32(float[] values)
        : this()
        {
            if (values == null)
            {
                throw new ArgumentException($"Array cannot be null");
            }

            _elementCount = values.Length;
            _rawbytes = new byte[8 + _elementCount * sizeof(float)];
            initBytes(values);
        }

        #endregion

        #region Methods
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ToArray/*' />
        public float[] ToArray()
        {

            if (_rawbytes == null || _rawbytes.Length < 8)
                throw new ArgumentException("Invalid byte array");

            // Read element count from bytes 2 and 3 (little-endian)
            int elementCount = _rawbytes[2] | (_rawbytes[3] << 8);

            // Validate expected size
            int expectedLength = 8 + elementCount * sizeof(float);
            if (_rawbytes.Length < expectedLength)
                throw new ArgumentException("Byte array does not contain enough data");

#if NETFRAMEWORK
    float[] result = new float[elementCount];
    Buffer.BlockCopy(_rawbytes, 8, result, 0, elementCount * sizeof(float));
    return result;
#else
            ReadOnlySpan<byte> dataSpan = _rawbytes.AsSpan(8, elementCount * sizeof(float));
            return MemoryMarshal.Cast<byte, float>(dataSpan).ToArray();
#endif

        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ToString/*' />
        public override string ToString()
        {
            if (IsNull || _rawbytes == null)
            {
                return "NULL";
            }
            return JsonSerializer.Serialize(this.ToArray());
        }

        #endregion

        #region Properties
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/IsNull/*' />
        public bool IsNull => _rawbytes == null || _rawbytes.Length == 0;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Null/*' />
        public static SqlVectorFloat32? Null => null;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ElementCount/*' />
        public int Length => _elementCount;

        byte ISqlVector.ElementType => _elementType;
        byte ISqlVector.ElementSize => _elementSize;
        byte[] ISqlVector.VectorPayload
        {
            get
            {
                if (_rawbytes is null)
                {
                    throw new System.NullReferenceException(
                        $"SqlVectorFloat32 is null");
                }
                return _rawbytes;
            }
        }

        #endregion

        #region Helpers
        private void initBytes(float[] values)
        {
            // Prefix bytes
            _rawbytes[0] = 0xA9;
            _rawbytes[1] = 0x01;
            _rawbytes[2] = (byte)(_elementCount & 0xFF);
            _rawbytes[3] = (byte)((_elementCount >> 8) & 0xFF);

            // Set type indicator
            _rawbytes[4] = 0;

            // Remaining prefix bytes
            _rawbytes[5] = 0x00;
            _rawbytes[6] = 0x00;
            _rawbytes[7] = 0x00;

            // Copy data
#if NETFRAMEWORK
            // Manual block copy
            Buffer.BlockCopy(values, 0, _rawbytes, 8, values.Length * sizeof(float));
#else
            // Fast span-based copy
            var byteSpan = MemoryMarshal.AsBytes(values.AsSpan());
            byteSpan.CopyTo(_rawbytes.AsSpan(8));
#endif
        }

        // Acquire the name and size of each T element.
        private SqlVectorFloat32()
        {
            _elementType = (byte)MetaType.SqlVectorElementType.Float32;
            _elementSize = (byte)sizeof(float);
            _rawbytes = Array.Empty<byte>();
        }

        internal SqlVectorFloat32(byte[] rawbytes)
        {
            if (!ValidateRawBytes(rawbytes))
            {
                throw new ArgumentException(
                  $"Invalid vector data in {nameof(rawbytes)} ",
                  nameof(rawbytes));
            }
            _rawbytes = rawbytes;
            _elementCount = rawbytes[2] | (rawbytes[3] << 8);
            _elementType = rawbytes[4];
            _elementSize = (byte)MetaType.GetVectorElementSize(_elementType);
        }

        private bool ValidateRawBytes(byte[] rawbytes)
        {
            if (rawbytes == null || rawbytes.Length == 0)
            {
                return false;
            }
            // Check for valid prefix
            if (rawbytes[0] != 0xA9 || rawbytes[1] != 0x01)
            {
                return false;
            }
            return true;
        }

        #endregion

        #region Fields

        private readonly byte _elementSize;
        private readonly int _elementCount;
        private readonly byte[] _rawbytes;
        private readonly byte _elementType;

        #endregion
    }
}
