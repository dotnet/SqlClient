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
        /// <summary>
        /// Represents the type of the packet header that the stream is currently writing.
        /// </summary>
        TdsStreamPacketType? PacketHeaderType { get; set; }

        /// <summary>
        /// Writes a byte to the stream.
        /// </summary>
        /// <param name="value">The value to be written.</param>
        /// <param name="isAsync">Whether the operation should be performed asynchronosly.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        public ValueTask WriteByteAsync(byte value, bool isAsync, CancellationToken ct);

        /// <summary>
        /// Queues a cancellation request to the stream, which will be sent to the server.
        /// </summary>
        public void QueueCancellation();
    }
}
