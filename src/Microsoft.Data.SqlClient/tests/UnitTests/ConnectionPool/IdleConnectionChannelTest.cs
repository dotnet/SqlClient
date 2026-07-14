// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.Common;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    public class IdleConnectionChannelTest
    {
        private static CreateOutcome Conn() => new CreateOutcome(new StubDbConnectionInternal());

        #region TryWrite

        [Fact]
        public void TryWrite_ConnectionOutcome_IncrementsCount()
        {
            var channel = new IdleConnectionChannel();

            Assert.True(channel.TryWrite(Conn()));
            Assert.Equal(1, channel.Count);
        }

        [Fact]
        public void TryWrite_BareWake_DoesNotIncrementCount()
        {
            var channel = new IdleConnectionChannel();

            Assert.True(channel.TryWrite(default));
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public void TryWrite_MultipleConnections_TracksCountCorrectly()
        {
            var channel = new IdleConnectionChannel();

            channel.TryWrite(Conn());
            channel.TryWrite(Conn());
            channel.TryWrite(default);
            channel.TryWrite(Conn());

            Assert.Equal(3, channel.Count);
        }

        #endregion

        #region TryRead

        [Fact]
        public void TryRead_ConnectionOutcome_DecrementsCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(Conn());
            Assert.Equal(1, channel.Count);

            Assert.True(channel.TryRead(out var outcome));
            Assert.NotNull(outcome.Connection);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public void TryRead_BareWake_DoesNotDecrementCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(Conn());
            channel.TryWrite(default);
            Assert.Equal(1, channel.Count);

            // Read the connection outcome first (FIFO)
            Assert.True(channel.TryRead(out var first));
            Assert.NotNull(first.Connection);
            Assert.Equal(0, channel.Count);

            // Read the bare wake
            Assert.True(channel.TryRead(out var second));
            Assert.Null(second.Connection);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public void TryRead_EmptyChannel_ReturnsFalse()
        {
            var channel = new IdleConnectionChannel();

            Assert.False(channel.TryRead(out var outcome));
            Assert.Null(outcome.Connection);
            Assert.Equal(0, channel.Count);
        }

        #endregion

        #region ReadAsync

        [Fact]
        public async Task ReadAsync_ConnectionOutcome_DecrementsCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(Conn());
            Assert.Equal(1, channel.Count);

            var outcome = await channel.ReadAsync(CancellationToken.None);

            Assert.NotNull(outcome.Connection);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public async Task ReadAsync_BareWake_DoesNotDecrementCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(Conn());
            channel.TryWrite(default);
            Assert.Equal(1, channel.Count);

            // First read returns the connection outcome (FIFO)
            var first = await channel.ReadAsync(CancellationToken.None);
            Assert.NotNull(first.Connection);
            Assert.Equal(0, channel.Count);

            // Second read returns the bare wake
            var second = await channel.ReadAsync(CancellationToken.None);
            Assert.Null(second.Connection);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public async Task ReadAsync_WaitsForWrite()
        {
            var channel = new IdleConnectionChannel();
            var expected = new StubDbConnectionInternal();

            var readTask = channel.ReadAsync(CancellationToken.None);
            Assert.False(readTask.IsCompleted);

            channel.TryWrite(new CreateOutcome(expected));

            var outcome = await readTask;
            Assert.Same(expected, outcome.Connection);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public async Task ReadAsync_Cancelled_ThrowsOperationCanceledException()
        {
            var channel = new IdleConnectionChannel();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => channel.ReadAsync(cts.Token).AsTask());
        }

        #endregion

        #region Complete

        [Fact]
        public void Complete_FirstCall_ReturnsTrue()
        {
            var channel = new IdleConnectionChannel();

            Assert.True(channel.Complete());
        }

        [Fact]
        public void Complete_SecondCall_ReturnsFalse()
        {
            var channel = new IdleConnectionChannel();
            Assert.True(channel.Complete());

            // Idempotent: a second Complete is a safe no-op and must not throw.
            Assert.False(channel.Complete());
        }

        [Fact]
        public void TryWrite_AfterComplete_ReturnsFalseAndDoesNotIncrementCount()
        {
            var channel = new IdleConnectionChannel();
            channel.Complete();

            Assert.False(channel.TryWrite(Conn()));
            Assert.False(channel.TryWrite(default));
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public void TryRead_AfterComplete_DrainsBufferedItems()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(Conn());
            channel.TryWrite(Conn());
            Assert.Equal(2, channel.Count);

            channel.Complete();

            // Completion only stops new writes; already-buffered items remain readable.
            Assert.True(channel.TryRead(out var first));
            Assert.NotNull(first.Connection);
            Assert.True(channel.TryRead(out var second));
            Assert.NotNull(second.Connection);
            Assert.Equal(0, channel.Count);

            // Once drained, further reads return false.
            Assert.False(channel.TryRead(out _));
        }

        [Fact]
        public async Task ReadAsync_AfterCompleteAndDrain_ThrowsChannelClosedException()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(Conn());
            channel.Complete();

            // Buffered item is still readable.
            var buffered = await channel.ReadAsync(CancellationToken.None);
            Assert.NotNull(buffered.Connection);

            // After the channel is drained, ReadAsync faults with ChannelClosedException.
            await Assert.ThrowsAsync<ChannelClosedException>(
                () => channel.ReadAsync(CancellationToken.None).AsTask());
        }

        [Fact]
        public async Task ReadAsync_PendingWaiter_FaultsOnComplete()
        {
            // FR-007: a caller already parked in ReadAsync when shutdown completes the
            // channel must be unblocked (not wait until its connection timeout).
            var channel = new IdleConnectionChannel();

            var readTask = channel.ReadAsync(CancellationToken.None);
            Assert.False(readTask.IsCompleted);

            channel.Complete();

            await Assert.ThrowsAsync<ChannelClosedException>(() => readTask.AsTask());
        }

        #endregion

        #region Mixed operations

        [Fact]
        public void WriteAndReadSequence_CountStaysConsistent()
        {
            var channel = new IdleConnectionChannel();

            // Write 3
            channel.TryWrite(Conn());
            channel.TryWrite(Conn());
            channel.TryWrite(Conn());
            Assert.Equal(3, channel.Count);

            // Read 2
            channel.TryRead(out _);
            channel.TryRead(out _);
            Assert.Equal(1, channel.Count);

            // Write 1 more
            channel.TryWrite(Conn());
            Assert.Equal(2, channel.Count);

            // Read remaining 2
            channel.TryRead(out _);
            channel.TryRead(out _);
            Assert.Equal(0, channel.Count);

            // Channel is empty
            Assert.False(channel.TryRead(out _));
            Assert.Equal(0, channel.Count);
        }

        #endregion

        #region Multi-threaded Tests

        [Fact]
        public async Task ConcurrentWriteAndRead_CountReturnsToZero()
        {
            var channel = new IdleConnectionChannel();
            var barrier = new Barrier(3);
            const int iterations = 1000;

            async Task WriteAndRead()
            {
                barrier.SignalAndWait();

                for (int i = 0; i < iterations; i++)
                {
                    channel.TryWrite(Conn());
                    await channel.ReadAsync(CancellationToken.None);
                }
            }

            await Task.WhenAll(
                Task.Run(WriteAndRead),
                Task.Run(WriteAndRead),
                Task.Run(WriteAndRead));

            Assert.Equal(0, channel.Count);
        }

        #endregion

        #region Helpers

        private class StubDbConnectionInternal : DbConnectionInternal
        {
            public override string ServerVersion => throw new NotImplementedException();

            public override ConnectionCapabilities Capabilities => throw new NotImplementedException();

            public override DbTransaction BeginTransaction(System.Data.IsolationLevel il)
                => throw new NotImplementedException();

            public override void EnlistTransaction(Transaction transaction) { }
            protected override void Activate(Transaction transaction) { }
            protected override void Deactivate() { }
            internal override void ResetConnection() { }
        }

        #endregion
    }
}
