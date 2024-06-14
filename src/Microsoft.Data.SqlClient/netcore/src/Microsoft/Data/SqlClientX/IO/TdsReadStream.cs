// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.IO
{
    /// <summary>
    /// A stream which handles reading TDS messages from the wire, and extracting the data from the 
    /// packets in the message packets.
    /// </summary>
    internal class TdsReadStream : Stream, ITdsReadStream
    {

        #region Public Properties
        /// <inheritdoc />
        public override bool CanRead => throw new NotImplementedException();

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position 
        {
            get => throw new NotImplementedException(); 
            set => throw new NotImplementedException(); 
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public override void Flush() => throw new NotSupportedException();

        /// <inheritdoc />
        public ValueTask<byte> PeekByteAsync(bool isAsync, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ReplaceUnderlyingStream(Stream stream)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public void SetPacketSize(int bufferSize)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ValueTask SkipReadBytesAsync(int skipCount, bool isAsync, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
