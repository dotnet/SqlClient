// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// SSL encapsulated over TDS transport. During SSL handshake, SSL packets are
    /// transported in TDS packet type 0x12. Once SSL handshake has completed, SSL
    /// packets are sent transparently.
    /// </summary>
    internal sealed partial class SslOverTdsStream : Stream
    {
        private readonly Stream _stream;

        private int _packetBytes = 0;
        private bool _encapsulate;

        private const int PACKET_SIZE_WITHOUT_HEADER = TdsEnums.DEFAULT_LOGIN_PACKET_SIZE - TdsEnums.HEADER_LEN;
        private const int PRELOGIN_PACKET_TYPE = 0x12;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="stream">Underlying stream</param>
        public SslOverTdsStream(Stream stream)
        {
            _stream = stream;
            _encapsulate = true;
        }

        /// <summary>
        /// Finish SSL handshake. Stop encapsulating in TDS.
        /// </summary>
        public void FinishHandshake()
        {
            _encapsulate = false;
        }

        /// <summary>
        /// Set stream length. 
        /// </summary>
        /// <param name="value">Length</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Flush stream
        /// </summary>
        public override void Flush()
        {
            // Can sometimes get Pipe broken errors from flushing a PipeStream.
            // PipeStream.Flush() also doesn't do anything, anyway.
            if (!(_stream is PipeStream))
            {
                _stream.Flush();
            }
        }

        /// <summary>
        /// Get/set stream position
        /// </summary>
        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Seek in stream
        /// </summary>
        /// <param name="offset">Offset</param>
        /// <param name="origin">Origin</param>
        /// <returns>Position</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Check if stream can be read from
        /// </summary>
        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        /// <summary>
        /// Check if stream can be written to
        /// </summary>
        public override bool CanWrite
        {
            get { return _stream.CanWrite; }
        }

        /// <summary>
        /// Check if stream can be seeked
        /// </summary>
        public override bool CanSeek
        {
            get { return false; } // Seek not supported
        }

        /// <summary>
        /// Get stream length
        /// </summary>
        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }
    }
}
