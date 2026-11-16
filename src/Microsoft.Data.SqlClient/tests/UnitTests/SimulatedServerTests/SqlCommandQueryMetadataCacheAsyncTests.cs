// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Verifies that async execution guards the command and its connection while the keys for
    /// cached query metadata are still loading.
    /// </summary>
    [Collection(SimulatedServerTestCollection.Name)]
    public sealed class SqlCommandQueryMetadataCacheAsyncTests
    {
        /// <summary>
        /// Changing the connection while cached keys load must be rejected, because execution
        /// resumes on the connection that was captured when it started.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_KeyLoadPending_ConnectionSetterThrows()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            using CancellationTokenSource cancellation = new();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync(cancellation.Token);
            await fixture.KeyLoadEntered;

            // Act
            Exception? error = Record.Exception(() => command.Connection = null);

            // Assert
            Assert.IsType<InvalidOperationException>(error);
            Assert.Same(fixture.Connection, command.Connection);

            // Cleanup
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Bounded(operation));
        }

        /// <summary>
        /// A second execution of the same command while cached keys load must be rejected.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_KeyLoadPending_SecondExecutionThrows()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            using CancellationTokenSource cancellation = new();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync(cancellation.Token);
            await fixture.KeyLoadEntered;

            // Act
            Task<SqlDataReader> second = command.ExecuteReaderAsync();

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(second));
            Assert.False(operation.IsCompleted);

            // Cleanup
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Bounded(operation));
        }

        /// <summary>
        /// Cancellation while cached keys load propagates and releases the guard.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_KeyLoadCanceled_ReleasesGuard()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            using CancellationTokenSource cancellation = new();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync(cancellation.Token);
            await fixture.KeyLoadEntered;

            // Act
            cancellation.Cancel();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Bounded(operation));
            Assert.False(IsInProgress(command));
            command.Connection = null;
            Assert.Equal(0, fixture.AsyncCount);
        }

        /// <summary>
        /// Closing and reopening the connection while cached keys load must stop execution on the
        /// new connection lifetime.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_ReopenedDuringKeyLoad_ThrowsClosedConnection()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;
            fixture.Connection.Close();
            fixture.Connection.Open();
            fixture.EnableColumnEncryption();

            // Act
            fixture.CompleteKeyLoad();

            // Assert
            InvalidOperationException error =
                await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));
            Assert.Equal(ADP.ClosedConnectionError().Message, error.Message);
            Assert.False(IsInProgress(command));
            Assert.Equal(0, fixture.AsyncCount);
        }

        /// <summary>
        /// A stale key must not start the fallback describe on a connection that was closed and
        /// reopened while the key was loading.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_ReopenedBeforeStaleFallback_ThrowsClosedConnection()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;
            fixture.Connection.Close();
            fixture.Connection.Open();
            fixture.EnableColumnEncryption();

            // Act
            fixture.FailKeyLoad();

            // Assert
            InvalidOperationException error =
                await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));
            Assert.Equal(ADP.ClosedConnectionError().Message, error.Message);
            Assert.False(IsInProgress(command));
            Assert.Equal(0, fixture.AsyncCount);
        }

        /// <summary>
        /// A reader opened while cached keys load must stop the stale-key fallback describe from
        /// sharing its session, and must remain usable.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_ReaderOpenedBeforeStaleFallback_ThrowsAndKeepsReader()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;
            using SqlCommand other = new("/* competing reader */", fixture.Connection);

            using (SqlDataReader reader = other.ExecuteReader())
            {
                // Act
                fixture.FailKeyLoad();

                // Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));
                Assert.False(reader.IsClosed);
            }

            // The connection and command are reusable once the competing reader closes.
            Assert.Equal(0, fixture.AsyncCount);
            Assert.False(IsInProgress(command));
            Assert.Equal(-1, await other.ExecuteNonQueryAsync());
        }

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private static async Task<T> Bounded<T>(Task<T> task)
        {
            await Bounded((Task)task);
            return await task;
        }

        private static async Task Bounded(Task task)
        {
            // Fail instead of hanging the run when the product never completes the task.
            if (await Task.WhenAny(task, Task.Delay(Timeout)) != task)
            {
                throw new TimeoutException();
            }

            await task;
        }

        private static bool IsInProgress(SqlCommand command) =>
            (bool)typeof(SqlCommand)
                .GetField(
                    "_enclaveAttestationInProgress",
                    BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(command)!;

        /// <summary>
        /// Opens a simulated-server connection that supports column encryption without enclaves,
        /// and gates the key store provider used by cached query metadata.
        /// </summary>
        private sealed class Fixture : IDisposable
        {
            private readonly TdsServer _server = new(new TdsServerArguments());
            private readonly TestKeyStoreProvider _provider = new();
            private readonly TaskCompletionSource<bool> _entered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<byte[]> _keyLoad =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal Fixture()
            {
                _server.Start();
                Connection = new SqlConnection(new SqlConnectionStringBuilder
                {
                    DataSource = $"localhost,{_server.EndPoint.Port}",
                    Encrypt = SqlConnectionEncryptOption.Optional,
                    Pooling = false,
                }.ConnectionString);
                Connection.Open();
                EnableColumnEncryption();

                _provider.DecryptAsyncCallback = cancellationToken =>
                {
                    cancellationToken.Register(() => _keyLoad.TrySetCanceled(cancellationToken));
                    _entered.TrySetResult(true);
                    return _keyLoad.Task;
                };
            }

            internal SqlConnection Connection { get; }

            internal Task KeyLoadEntered => Bounded(_entered.Task);

            internal int AsyncCount =>
                (int)typeof(SqlConnectionInternal)
                    .GetField("_asyncCommandCount", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(Connection.InnerConnection)!;

            /// <summary>
            /// Marks the open session as supporting column encryption without enclaves, so the
            /// simulated server need not negotiate it.
            /// </summary>
            internal void EnableColumnEncryption() =>
                ((SqlConnectionInternal)Connection.InnerConnection)
                    .Capabilities.ColumnEncryptionVersion = 1;

            internal SqlCommand CreateCachedCommand() =>
                QueryMetadataCacheTestCommands.NewCachedCommand(Connection, _provider);

            internal void CompleteKeyLoad() => _keyLoad.SetResult(_provider.PlaintextKey);

            /// <summary>
            /// Fails the pending key load. The driver wraps every provider failure as a stale key,
            /// which starts the describe parameter encryption fallback.
            /// </summary>
            internal void FailKeyLoad() =>
                _keyLoad.SetException(new InvalidOperationException("Stale key"));

            public void Dispose()
            {
                Connection.Dispose();
                _server.Dispose();
            }
        }
    }
}
