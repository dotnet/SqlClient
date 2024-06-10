using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SqlClientX.IO
{
    /// <summary>
    /// A Stream abstraction over the TDS protocol.
    /// The stream can be used to read and write TDS messages on a 
    /// SQL Server physical connection.
    /// </summary>
    internal class TdsStream : Stream
    {
        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public TdsStream(Stream underLyingStream) : base()
        {
        }

        /// <summary>
        /// When writing the packet, the caller needs to 
        /// specify the packet type. 
        /// TODO: Consider accepting an enum of packet types
        /// instead of the byte.
        /// </summary>
        /// <param name="packetType"></param>
        public void SetWritePacketType(TdsStreamPacketType packetType)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(Span<byte> buffer)
        {
            throw new NotImplementedException();
        }

        public override int Read(
            byte[] buffer, 
            int offset, 
            int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// We only support ValueTask based overloads for async IO.
        /// This overload makes sure that the method throws, instead 
        /// of using the unoptimized path.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Called explicitly by the consumers to flush the stream,
        /// which marks the TDS packet as the last packet in the message
        /// and sends it to the server.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override Task FlushAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Peeks the next byte in the stream, without consuming it.
        /// The next call to read will return the same byte but it 
        /// will consume it.
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ValueTask<byte> PeekByteAsync(bool isAsync,
            CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// A convenience method to skip the bytes in the stream,
        /// by allowing buffer manipulation, instead of making the consumer
        /// allocate buffers to read and discard the packets.
        /// </summary>
        /// <param name="skipCount"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ValueTask SkipReadBytesAsync(int skipCount,
            bool isAsync, 
            CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Needed to reset the stream.
        /// Useful in some cases for TDS implementation, where we 
        /// dont want to consume all the data in the stream, but 
        /// want to make it available for the next set of operations.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}
