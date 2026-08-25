// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Diagnostics;

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
        private readonly ISqlClientMetrics _metrics;
        private volatile int _count;

        /// <param name="metrics">
        /// The metrics sink of the pool that owns this channel. Defaults to the process-wide
        /// instance so tests can construct a channel without a pool.
        /// </param>
        internal IdleConnectionChannel(ISqlClientMetrics? metrics = null)
        {
            var channel = Channel.CreateUnbounded<DbConnectionInternal?>();
            _reader = channel.Reader;
            _writer = channel.Writer;
            _metrics = metrics ?? SqlClientDiagnostics.Metrics;
        }

        /// <summary>
        /// Marks the channel writer as complete. After completion, <see cref="TryWrite"/>
        /// returns <see langword="false"/> for any future writes, and any in-flight or future
        /// <see cref="ReadAsync"/> waiters will fault with <see cref="System.Threading.Channels.ChannelClosedException"/>
        /// once the channel is drained. Used by the connection pool to signal shutdown.
        /// </summary>
        /// <returns><see langword="true"/> if this call completed the channel; otherwise <see langword="false"/>
        /// (channel was already completed).</returns>
        internal bool Complete() => _writer.TryComplete();

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
                    _metrics.EnterFreeConnection();
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
                    _metrics.ExitFreeConnection();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Asynchronously reads a value from the channel.
        /// Decrements the idle count when a non-null connection is read.
        /// </summary>
        /// <remarks>
        /// The <c>ConfigureAwait(false)</c> below is load bearing. Sync callers reach this method
        /// through <c>ChannelDbConnectionPool.ReadChannelSyncOverAsync</c>, which blocks on the
        /// returned operation. If this resumption captured a single-threaded
        /// <see cref="SynchronizationContext"/> (WPF, WinForms, legacy ASP.NET), completing it would
        /// require the very thread that is blocked waiting for it, deadlocking the caller.
        /// </remarks>
        internal async ValueTask<DbConnectionInternal?> ReadAsync(CancellationToken cancellationToken)
        {
            var connection = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (connection is not null)
            {
                Interlocked.Decrement(ref _count);
                _metrics.ExitFreeConnection();
            }

            return connection;
        }
    }
}
