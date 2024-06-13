// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
}
