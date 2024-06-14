// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// Represents a TDS packet stream.
    /// </summary>
    internal interface ITdsStream
    {
        /// <summary>
        /// Manipulates the buffer size of the TDS stream by reallocating the buffer to the new size.
        /// Note: should be called once during a connection lifetime after login exchange.
        /// </summary>
        /// <param name="bufferSize">The value of the new buffer size.</param>
        public void SetPacketSize(int bufferSize);

        /// <summary>
        /// Replaces the underlying stream for a TDS stream. 
        /// This is useful while changing between SSL stream and non-SSL stream.
        /// </summary>
        /// <param name="stream"></param>
        public void ReplaceUnderlyingStream(Stream stream);
    }
}
