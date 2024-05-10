using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.SqlClientX.TDS.Objects;
using simplesqlclient;

namespace Microsoft.Data.SqlClient.SqlClientX.TDS
{
    internal class StreamExecutionState
    {
        internal Encoding _defaultEncoding = null;
        internal NullBitmap _nullBitMapInfo;
        public _SqlMetaDataSet LastReadMetadata { get; internal set; }
        public ulong LongLen { get; set; }
        public ulong LongLenLeft { get; internal set; }
    }
}
