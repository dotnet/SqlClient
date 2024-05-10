using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.Streams;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS.Objects
{
    internal struct NullBitmap
    {
        private byte[] _nullBitmap;
        private int _columnsCount; // set to 0 if not used or > 0 for NBC rows

        internal async ValueTask Initialize(TdsReadStream reader, int columnsCount,
            bool isAsync,
            CancellationToken ct)
        {
            _columnsCount = columnsCount;
            // 1-8 columns need 1 byte
            // 9-16: 2 bytes, and so on
            int bitmapArrayLength = (columnsCount + 7) / 8;

            // allow reuse of previously allocated bitmap
            if (_nullBitmap == null || _nullBitmap.Length != bitmapArrayLength)
            {
                _nullBitmap = new byte[bitmapArrayLength];
            }

            // read the null bitmap compression information from TDS
            if (isAsync)
            {
                await reader.ReadAsync(_nullBitmap.AsMemory(), ct).ConfigureAwait(false);
            }
            else
            {
                reader.Read(_nullBitmap.AsSpan());
            }
        }

        internal bool ReferenceEquals(NullBitmap obj)
        {
            return object.ReferenceEquals(_nullBitmap, obj._nullBitmap);
        }

        internal NullBitmap Clone()
        {
            NullBitmap newBitmap = new NullBitmap();
            newBitmap._nullBitmap = _nullBitmap == null ? null : (byte[])_nullBitmap.Clone();
            newBitmap._columnsCount = _columnsCount;
            return newBitmap;
        }

        internal void Clean()
        {
            _columnsCount = 0;
            // no need to free _nullBitmap array - it is cached for the next row
        }

        /// <summary>
        /// If this method returns true, the value is guaranteed to be null. This is not true vice versa:
        /// if the bitmap value is false (if this method returns false), the value can be either null or non-null - no guarantee in this case.
        /// To determine whether it is null or not, read it from the TDS (per NBCROW design spec, for IMAGE/TEXT/NTEXT columns server might send
        /// bitmap = 0, when the actual value is null).
        /// </summary>
        internal bool IsGuaranteedNull(int columnOrdinal)
        {
            if (_columnsCount == 0)
            {
                // not an NBC row
                return false;
            }

            Debug.Assert(columnOrdinal >= 0 && columnOrdinal < _columnsCount, "Invalid column ordinal");

            byte testBit = (byte)(1 << (columnOrdinal & 0x7)); // columnOrdinal & 0x7 == columnOrdinal MOD 0x7
            byte testByte = _nullBitmap[columnOrdinal >> 3];
            return (testBit & testByte) != 0;
        }
    }
}
