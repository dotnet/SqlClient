using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// An internal interface to represent the TDS specific write stream operations.
    /// </summary>
    internal interface ITdsWriteStream : ITdsStream
    {
        TdsStreamPacketType PacketHeaderType { get; set; }

        public ValueTask WriteByteAsync(byte value, bool isAsync, CancellationToken ct);

        public void QueueCancellation();
    }

    internal interface ITdsStream
    {
        public void SetPacketSize(int bufferSize);

        public void ReplaceUnderlyingStream(Stream stream);
    }
}
