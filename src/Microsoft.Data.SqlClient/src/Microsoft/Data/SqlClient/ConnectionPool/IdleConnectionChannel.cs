// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Threading;
using System.Threading.Channels;
using Microsoft.Data.ProviderBase;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Wraps an unbounded <see cref="Channel{T}"/> of idle connections and tracks the number of
    /// non-null connections it contains. Unbounded channels do not support
    /// <see cref="ChannelReader{T}.Count"/>, so this class maintains the count via
    /// <see cref="Interlocked"/> operations on every read and write of a non-null value.
    /// </summary>
    internal sealed class IdleConnectionChannel
    {
        private readonly ChannelReader<DbConnectionInternal?> _reader;
        private readonly ChannelWriter<DbConnectionInternal?> _writer;
        private volatile int _count;

        internal IdleConnectionChannel()
        {
            var channel = Channel.CreateUnbounded<DbConnectionInternal?>();
            _reader = channel.Reader;
            //TODO: the channel should be completed on pool shutdown
            _writer = channel.Writer;
        }

        /// <summary>
        /// The number of non-null connections currently in the channel.
        /// </summary>
        internal int Count => _count;

        /// <summary>
        /// Writes a connection (or null wake-up signal) to the channel.
        /// Increments the idle count when <paramref name="connection"/> is not null.
        /// </summary>
        /// <returns><see langword="true"/> if the value was written; otherwise <see langword="false"/>.</returns>
        internal bool TryWrite(DbConnectionInternal? connection)
        {
            if (_writer.TryWrite(connection))
            {
                if (connection is not null)
                {
                    Interlocked.Increment(ref _count);
                }
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Tries to read a value from the channel without blocking.
        /// Decrements the idle count when a non-null connection is read.
        /// </summary>
        internal bool TryRead(out DbConnectionInternal? connection)
        {
            if (_reader.TryRead(out connection))
            {
                if (connection is not null)
                {
                    Interlocked.Decrement(ref _count);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Asynchronously reads a value from the channel.
        /// Decrements the idle count when a non-null connection is read.
        /// </summary>
        internal async ValueTask<DbConnectionInternal?> ReadAsync(CancellationToken cancellationToken)
        {
            var connection = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (connection is not null)
            {
                Interlocked.Decrement(ref _count);
            }

            return connection;
        }
    }
}
