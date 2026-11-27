// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SqlServer.TDS.EndPoint
{
    /// <summary>
    /// Identifies failures at the physical transport boundary, not in protocol handlers or logging.
    /// </summary>
    internal sealed class TransportStream : PlaceholderStream
    {
        internal TransportStream(Stream stream) : base(stream, false)
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Execute(() => CheckRead(InnerStream.Read(buffer, offset, count), count));

        public override void Write(byte[] buffer, int offset, int count)
            => Execute(() => InnerStream.Write(buffer, offset, count));

        public override void Flush() => Execute(() => InnerStream.Flush());

        public override void Close() => Execute(() => base.Close());

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                return CheckRead(await InnerStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false), count);
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                throw new TransportFailureException(ex);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                await InnerStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                throw new TransportFailureException(ex);
            }
        }

        internal static T Execute<T>(Func<T> operation)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                throw new TransportFailureException(ex);
            }
        }

        internal static void Execute(Action operation)
            => Execute(() => { operation(); return true; });

        private static bool IsIoFailure(Exception exception)
            => exception is IOException || exception is SocketException || exception is ObjectDisposedException;

        private static int CheckRead(int read, int count)
        {
            if (read == 0 && count != 0)
            {
                throw new EndOfStreamException("The peer closed the transport.");
            }
            return read;
        }
    }

    /// <summary>
    /// Carries a physical transport failure through TDS and TLS stream layers.
    /// </summary>
    internal sealed class TransportFailureException : IOException
    {
        internal TransportFailureException(Exception innerException)
            : base("TDS transport failed.", innerException)
        {
        }

        internal static bool IsExpected(Exception exception, bool stopping)
        {
            // SslStream may wrap the marked transport failure in another IOException.
            while (exception is IOException)
            {
                if (exception is TransportFailureException)
                {
                    return stopping || !(exception.InnerException is ObjectDisposedException);
                }
                exception = exception.InnerException;
            }
            return false;
        }
    }
}
