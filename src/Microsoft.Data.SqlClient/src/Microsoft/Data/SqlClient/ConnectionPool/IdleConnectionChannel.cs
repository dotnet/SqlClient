// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

#nullable enable

namespace Microsoft.Data.SqlClient.ConnectionPool
{
    /// <summary>
    /// Wraps an unbounded <see cref="Channel{T}"/> of <see cref="CreateOutcome"/> values and tracks
    /// the number of connection-bearing outcomes it currently holds (i.e. idle connections).
    /// Unbounded channels do not support <see cref="ChannelReader{T}.Count"/>, so this class
    /// maintains the count via <see cref="Interlocked"/> operations on every read and write of a
    /// connection-bearing outcome. Bare-wake and error outcomes do not affect the count.
    /// </summary>
    internal sealed class IdleConnectionChannel
    {
        private readonly ChannelReader<CreateOutcome> _reader;
        private readonly ChannelWriter<CreateOutcome> _writer;
        private volatile int _count;

        internal IdleConnectionChannel()
        {
            var channel = Channel.CreateUnbounded<CreateOutcome>();
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
        internal bool Complete() => _writer.TryComplete();

        /// <summary>
        /// The number of connection-bearing outcomes (idle connections) currently in the channel.
        /// </summary>
        internal int Count => _count;

        /// <summary>
        /// Writes an outcome to the channel. Increments the idle count when the outcome carries a
        /// connection.
        /// </summary>
        /// <returns><see langword="true"/> if the value was written; otherwise <see langword="false"/>.</returns>
        internal bool TryWrite(CreateOutcome outcome)
        {
            if (_writer.TryWrite(outcome))
            {
                if (outcome.HasConnection)
                {
                    Interlocked.Increment(ref _count);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to read an outcome from the channel without blocking.
        /// Decrements the idle count when a connection-bearing outcome is read.
        /// </summary>
        internal bool TryRead(out CreateOutcome outcome)
        {
            if (_reader.TryRead(out outcome))
            {
                if (outcome.HasConnection)
                {
                    Interlocked.Decrement(ref _count);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Asynchronously reads an outcome from the channel.
        /// Decrements the idle count when a connection-bearing outcome is read.
        /// </summary>
        internal async ValueTask<CreateOutcome> ReadAsync(CancellationToken cancellationToken)
        {
            var outcome = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (outcome.HasConnection)
            {
                Interlocked.Decrement(ref _count);
            }

            return outcome;
        }
    }
}
