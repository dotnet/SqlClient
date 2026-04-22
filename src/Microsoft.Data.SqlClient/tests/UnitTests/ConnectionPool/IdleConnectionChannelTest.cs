// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    public class IdleConnectionChannelTest
    {
        #region TryWrite

        [Fact]
        public void TryWrite_NonNullConnection_IncrementsCount()
        {
            var channel = new IdleConnectionChannel();

            Assert.True(channel.TryWrite(new StubDbConnectionInternal()));
            Assert.Equal(1, channel.Count);
        }

        [Fact]
        public void TryWrite_NullConnection_DoesNotIncrementCount()
        {
            var channel = new IdleConnectionChannel();

            Assert.True(channel.TryWrite(null));
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public void TryWrite_MultipleConnections_TracksCountCorrectly()
        {
            var channel = new IdleConnectionChannel();

            channel.TryWrite(new StubDbConnectionInternal());
            channel.TryWrite(new StubDbConnectionInternal());
            channel.TryWrite(null);
            channel.TryWrite(new StubDbConnectionInternal());

            Assert.Equal(3, channel.Count);
        }

        #endregion

        #region TryRead

        [Fact]
        public void TryRead_NonNullConnection_DecrementsCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(new StubDbConnectionInternal());
            Assert.Equal(1, channel.Count);

            Assert.True(channel.TryRead(out var connection));
            Assert.NotNull(connection);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public void TryRead_NullConnection_DoesNotDecrementCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(new StubDbConnectionInternal());
            channel.TryWrite(null);
            Assert.Equal(1, channel.Count);

            // Read the non-null connection first (FIFO)
            Assert.True(channel.TryRead(out var first));
            Assert.NotNull(first);
            Assert.Equal(0, channel.Count);

            // Read the null
            Assert.True(channel.TryRead(out var second));
            Assert.Null(second);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public void TryRead_EmptyChannel_ReturnsFalse()
        {
            var channel = new IdleConnectionChannel();

            Assert.False(channel.TryRead(out var connection));
            Assert.Null(connection);
            Assert.Equal(0, channel.Count);
        }

        #endregion

        #region ReadAsync

        [Fact]
        public async Task ReadAsync_NonNullConnection_DecrementsCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(new StubDbConnectionInternal());
            Assert.Equal(1, channel.Count);

            var connection = await channel.ReadAsync(CancellationToken.None);

            Assert.NotNull(connection);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public async Task ReadAsync_NullConnection_DoesNotDecrementCount()
        {
            var channel = new IdleConnectionChannel();
            channel.TryWrite(new StubDbConnectionInternal());
            channel.TryWrite(null);
            Assert.Equal(1, channel.Count);

            // First read returns the non-null connection (FIFO)
            var first = await channel.ReadAsync(CancellationToken.None);
            Assert.NotNull(first);
            Assert.Equal(0, channel.Count);

            // Second read returns null
            var second = await channel.ReadAsync(CancellationToken.None);
            Assert.Null(second);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public async Task ReadAsync_WaitsForWrite()
        {
            var channel = new IdleConnectionChannel();
            var expected = new StubDbConnectionInternal();

            var readTask = channel.ReadAsync(CancellationToken.None);
            Assert.False(readTask.IsCompleted);

            channel.TryWrite(expected);

            var connection = await readTask;
            Assert.Same(expected, connection);
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

        #region Mixed operations

        [Fact]
        public void WriteAndReadSequence_CountStaysConsistent()
        {
            var channel = new IdleConnectionChannel();

            // Write 3
            channel.TryWrite(new StubDbConnectionInternal());
            channel.TryWrite(new StubDbConnectionInternal());
            channel.TryWrite(new StubDbConnectionInternal());
            Assert.Equal(3, channel.Count);

            // Read 2
            channel.TryRead(out _);
            channel.TryRead(out _);
            Assert.Equal(1, channel.Count);

            // Write 1 more
            channel.TryWrite(new StubDbConnectionInternal());
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
                    channel.TryWrite(new StubDbConnectionInternal());
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
