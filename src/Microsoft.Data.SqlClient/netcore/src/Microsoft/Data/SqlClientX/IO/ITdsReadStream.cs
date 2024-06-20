// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// An interface to model read stream operations for Tds specific capabilities.
    /// </summary>
    internal interface ITdsReadStream : ITdsStream
    {
        /// <summary>
        /// The packet header type of the current read operation.
        /// </summary>
        byte ReadPacketHeaderType { get; }

        /// <summary>
        /// The Packet status of current read operation.
        /// </summary>
        byte ReadPacketStatus { get; }

        /// <summary>
        /// Exposes the SPID of the connection on which this stream is operating.
        /// </summary>
        int Spid { get; }

        /// <summary>
        /// Peeks the next byte in the stream, without consuming it.
        /// The next call to read will return the same byte but it 
        /// will consume it.
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        ValueTask<byte> PeekByteAsync(bool isAsync, CancellationToken ct);

        /// <summary>
        /// Reads a byte from the stream.
        /// </summary>
        /// <param name="isAsync">Indicates if the operation should be async.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<byte> ReadByteAsync(bool isAsync, CancellationToken cancellationToken);

        /// <summary>
        /// A convenience method to skip the bytes in the stream,
        /// by allowing buffer manipulation, instead of making the consumer
        /// allocate buffers to read and discard the bytes.
        /// </summary>
        /// <param name="skipCount">Number of bytes to skip</param>
        /// <param name="isAsync">If the method should be called Asynchronously.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns></returns>
        public ValueTask SkipReadBytesAsync(int skipCount, bool isAsync, CancellationToken ct);
    }
}
