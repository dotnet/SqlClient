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
    public sealed class SqlVectorFloat32 : INullable, ISqlVector
    {
        #region Constants
        private const byte VecHeaderMagicNo = 0xA9;
        private const byte VecVersionNo = 0x01;
        private const byte VecTypeFloat32 = 0x00;

        #endregion

        #region Fields
        private readonly byte _elementSize = sizeof(float);
        private readonly int _elementCount;
        private readonly byte[] _rawBytes;
        private readonly byte _elementType = VecTypeFloat32;
        #endregion

        #region Constructors
        private SqlVectorFloat32()
        {
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
            _elementCount = GetElementCountFromRawBytes(rawBytes);
            var floatArray = new float[_elementCount];
            Buffer.BlockCopy(_rawBytes, 8, floatArray, 0, _elementCount * _elementSize);
            Values = new ReadOnlyMemory<float>(floatArray);
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor1/*' />
        public SqlVectorFloat32(int length)
        {
            if (length < 0)
                throw ADP.InvalidVectorColumnLength(nameof(length));
                
            _elementCount = length;
            _rawBytes = Array.Empty<byte>();
            Values = new ReadOnlyMemory<float>(Array.Empty<float>());
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ctor2/*' />
        public SqlVectorFloat32(ReadOnlyMemory<float> values)
        {
            Values = values;
            _elementCount = values.Length;
            _rawBytes = new byte[TdsEnums.VECTOR_HEADER_SIZE + _elementCount * _elementSize];
            InitializeVectorBytes(values);
        }
        #endregion

        #region Methods
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ToString/*' />
        public override string ToString()
        {
            if (IsNull || _rawBytes == null)
            {
                return SQLResource.NullString;
            }
            return JsonSerializer.Serialize(this.Values);
        }

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/ToArray/*' />
        public float[] ToArray()
        {
            if (IsNull || _rawBytes == null)
            {
                return Array.Empty<float>();
            }
            return Values.ToArray();
        }
        #endregion

        #region Properties
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/IsNull/*' />
        public bool IsNull => _rawBytes.Length == 0;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Null/*' />
        public static SqlVectorFloat32? Null => null;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Length/*' />
        public int Length => _elementCount;
        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Size/*' />
        public int Size => TdsEnums.VECTOR_HEADER_SIZE + _elementCount * _elementSize;

        /// <include file='../../../../doc/snippets/Microsoft.Data.SqlTypes/SqlVectorFloat32.xml' path='docs/members[@name="SqlVectorFloat32"]/Values/*' />
        public ReadOnlyMemory<float> Values { get; }
        #endregion

        #region ISqlVectorProperties
        byte ISqlVector.ElementType => _elementType;
        byte ISqlVector.ElementSize => _elementSize;
        byte[] ISqlVector.VectorPayload => _rawBytes;
        #endregion

        #region Helpers
        private void InitializeVectorBytes(ReadOnlyMemory<float> values)
        {
            //Refer to TDS section 2.2.5.5.7 for vector header format
            // +------------------------+-----------------+----------------------+------------------+----------------------------+--------------+
            // | Field                  | Size (bytes)    | Example Value         | Description                                                 |
            // +------------------------+-----------------+----------------------+--------------------------------------------------------------+
            // | Layout Format          | 1               | 0xA9                  | Magic number indicating vector layout format                |
            // | Layout Version         | 1               | 0x01                  | Version of the vector format                                |
            // | Number of Dimensions   | 2               | NN                    | Number of vector elements                                   |
            // | Dimension Type         | 1               | 0x00                     | Element type indicator (e.g. 0x00 for float32)           |
            // | Reserved               | 3               | 0x00 0x00 0x00        | Reserved for future use                                     |
            // | Stream of Values       | NN * sizeof(T)  | [float bytes...]      | Raw bytes for vector elements                               |
            // +------------------------+-----------------+----------------------+--------------------------------------------------------------+

            _rawBytes[0] = VecHeaderMagicNo;
            _rawBytes[1] = VecVersionNo;
            _rawBytes[2] = (byte)(_elementCount & 0xFF);
            _rawBytes[3] = (byte)((_elementCount >> 8) & 0xFF);
            _rawBytes[4] = VecTypeFloat32;
            _rawBytes[5] = 0x00;
            _rawBytes[6] = 0x00;
            _rawBytes[7] = 0x00;

            // Copy data
#if NETFRAMEWORK
            if (MemoryMarshal.TryGetArray(values, out ArraySegment<float> segment))
            {
                Buffer.BlockCopy(segment.Array, segment.Offset * sizeof(float), _rawBytes, TdsEnums.VECTOR_HEADER_SIZE, segment.Count * _elementSize);
            }
            else
            {
                Buffer.BlockCopy(values.ToArray(), 0, _rawBytes, TdsEnums.VECTOR_HEADER_SIZE, values.Length * _elementSize);
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
            
            if (rawBytes[0] != VecHeaderMagicNo || rawBytes[1] != VecVersionNo || rawBytes[4] != VecTypeFloat32)
                 return false;
            
            int elementCount = GetElementCountFromRawBytes(rawBytes);
            if (rawBytes.Length != TdsEnums.VECTOR_HEADER_SIZE + elementCount * _elementSize)
                return false;

            return true;
        }

        private int GetElementCountFromRawBytes(byte[] rawBytes)
        {
            return rawBytes[2] | (rawBytes[3] << 8);
        }
        #endregion
    }
}
