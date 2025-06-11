using System;
using System.Data.SqlTypes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;

#nullable enable
namespace Microsoft.Data.SqlTypes
{
    /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/SqlVectorFloat32/*' />
    public class SqlVectorFloat32 : INullable, ISqlVector
    {
        #region Constants
        private const byte VecHeaderMagicNo = 0xA9;
        private const byte VecVersionNo = 0x01;
        private const byte VecTypeIndicator = 0x00;
        #endregion

        #region Fields
        private readonly byte _elementSize;
        private readonly int _elementCount;
        private readonly byte[] _rawBytes;
        private readonly byte _elementType;
        #endregion

        #region Constructors
        private SqlVectorFloat32()
        {
            _elementType = (byte)MetaType.SqlVectorElementType.Float32;
            _elementSize = sizeof(float);
            _elementCount = 0;
            _rawBytes = Array.Empty<byte>();
        }

        internal SqlVectorFloat32(byte[] rawBytes)
        {
            if (!ValidateRawBytes(rawBytes))
            {
                throw ADP.InvalidVectorHeader();
            }
            _rawBytes = rawBytes;
            _elementCount = rawBytes[2] | (rawBytes[3] << 8);
            _elementType = rawBytes[4];
            _elementSize = sizeof(float);
        }
        
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor1/*' />
        public SqlVectorFloat32(int length)
        {
            if (length < 0)
                throw ADP.InvalidVectorColumnLength(nameof(length));

            _elementCount = length;
            _elementType = (byte)MetaType.SqlVectorElementType.Float32;
            _elementSize = sizeof(float);
            _rawBytes = Array.Empty<byte>();
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor2/*' />
        public SqlVectorFloat32(ReadOnlyMemory<float> values)
        : this()
        {
            if (values.IsEmpty)
            {
                throw ADP.EmptyVectorValues(nameof(values));
            }

            _elementCount = values.Length;
            _rawBytes = new byte[TdsEnums.VECTOR_HEADER_SIZE + _elementCount * sizeof(float)];
            InitializeVectorBytes(values);
        }
        #endregion

        #region Methods
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ToString/*' />
        public override string ToString()
        {
            if (IsNull || _rawBytes == null)
            {
                return "NULL";
            }
            return JsonSerializer.Serialize(this.Values);
        }
        #endregion

        #region Properties
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/IsNull/*' />
        public bool IsNull => _rawBytes.Length == 0;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Null/*' />
        public static SqlVectorFloat32? Null => null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Length/*' />
        public int Length => _elementCount;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Values/*' />
        public float[] Values 
        {
            get
            {
                if (_rawBytes.Length == 0)
                {
                    return Array.Empty<float>();
                }
                int elementCount = _rawBytes[2] | (_rawBytes[3] << 8);
#if NETFRAMEWORK
                // Allocate array and copy bytes into it
                float[] result = new float[elementCount];
                Buffer.BlockCopy(_rawBytes, 8, result, 0, elementCount * sizeof(float));
                return result;
#else
        ReadOnlySpan<byte> dataSpan = _rawBytes.AsSpan(8, elementCount * sizeof(float));
        return MemoryMarshal.Cast<byte, float>(dataSpan).ToArray();
#endif
            }
        }
        #endregion

        #region ISqlVectorProperties
        byte ISqlVector.ElementType => _elementType;
        byte ISqlVector.ElementSize => _elementSize;
        byte[] ISqlVector.VectorPayload => _rawBytes;
        #endregion

        #region Helpers
        private void InitializeVectorBytes(ReadOnlyMemory<float> values)
        {
            //Header Bytes
            _rawBytes[0] = VecHeaderMagicNo;
            _rawBytes[1] = VecVersionNo;
            _rawBytes[2] = (byte)(_elementCount & 0xFF);
            _rawBytes[3] = (byte)((_elementCount >> 8) & 0xFF);
            _rawBytes[4] = VecTypeIndicator;
            _rawBytes[5] = 0x00;
            _rawBytes[6] = 0x00;
            _rawBytes[7] = 0x00;

            // Copy data
#if NETFRAMEWORK
            if (MemoryMarshal.TryGetArray(values, out ArraySegment<float> segment))
            {
                Buffer.BlockCopy(segment.Array, segment.Offset * sizeof(float), _rawBytes, TdsEnums.VECTOR_HEADER_SIZE, segment.Count * sizeof(float));
            }
            else
            {
                Buffer.BlockCopy(values.ToArray(), 0, _rawBytes, TdsEnums.VECTOR_HEADER_SIZE, values.Length * sizeof(float));
            }
#else
            // Fast span-based copy
            var byteSpan = MemoryMarshal.AsBytes(values.Span);
            byteSpan.CopyTo(_rawBytes.AsSpan(TdsEnums.VECTOR_HEADER_SIZE));
#endif
        }

        private bool ValidateRawBytes(byte[] rawBytes)
        {
            if (rawBytes.Length == 0 || rawBytes.Length < TdsEnums.VECTOR_HEADER_SIZE)
                return false;
            
            if (rawBytes[0] != VecHeaderMagicNo || rawBytes[1] != VecVersionNo || rawBytes[4] != VecTypeIndicator)
                 return false;
            
            int elementCount = rawBytes[2] | (rawBytes[3] << 8);
            if (rawBytes.Length != TdsEnums.VECTOR_HEADER_SIZE + elementCount * sizeof(float))
                return false;

            return true;
        }
        #endregion
    }
}
