// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly SemaphoreSlim _items = new(0);
        private readonly CancellationTokenSource _completedCts = new();

        internal IdleConnectionChannel()
        {
            var channel = Channel.CreateUnbounded<DbConnectionInternal?>();
            _reader = channel.Reader;
            _writer = channel.Writer;
        }

        /// <summary>
        /// Marks the channel writer as complete. After completion, <see cref="TryWrite"/>
        /// returns <see langword="false"/> for any future writes, and any in-flight or future
        /// <see cref="ReadAsync"/> waiters will fault with <see cref="System.Threading.Channels.ChannelClosedException"/>
        /// once the channel is drained. Used by the connection pool to signal shutdown.
        /// </summary>
        /// <returns><see langword="true"/> if this call completed the channel; otherwise <see langword="false"/>
        /// (channel was already completed).</returns>
        internal bool Complete()
        {
            bool completed = _writer.TryComplete();
            _completedCts.Cancel();
            return completed;
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
                _items.Release();
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
            connection = null;
            if (_items.Wait(0) && _reader.TryRead(out connection))
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
            if (!_items.Wait(0))
            {
                using var linkedAsync = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _completedCts.Token);
                try
                {
                    await _items.WaitAsync(linkedAsync.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new ChannelClosedException();
                }
            }

            if (!_reader.TryRead(out var connection))
            {
                throw new ChannelClosedException();
            }

            if (connection is not null)
            {
                Interlocked.Decrement(ref _count);
            }

            return connection;
        }

        /// <summary>
        /// Synchronously blocks the calling thread until an item is available. The wait is
        /// released directly by the writing thread, so it never depends on a thread pool
        /// continuation the way sync-over-async on <see cref="ReadAsync"/> does.
        /// </summary>
        internal DbConnectionInternal? Read(CancellationToken cancellationToken)
        {
            if (!_items.Wait(0))
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _completedCts.Token);
                try
                {
                    _items.Wait(linked.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new ChannelClosedException();
                }
            }

            if (!_reader.TryRead(out var connection))
            {
                throw new ChannelClosedException();
            }

            if (connection is not null)
            {
                Interlocked.Decrement(ref _count);
            }

            return connection;
        }
    }
}
