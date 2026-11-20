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
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
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
        /// Metadata cached for one database must not execute after the connection switches to
        /// another database while the keys load, because the cache key includes the database.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_DatabaseChangedDuringKeyLoad_Throws()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;
            fixture.Connection.ChangeDatabase(OtherDatabase);
            Assert.Equal(OtherDatabase, fixture.Connection.Database);

            // Act
            fixture.CompleteKeyLoad();

            // Assert
            InvalidOperationException error =
                await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));
            Assert.Equal(
                SQL.CannotModifyPropertyAsyncOperationInProgress(nameof(SqlConnection.Database))
                    .Message,
                error.Message);
            Assert.False(IsInProgress(command));
            Assert.Equal(0, fixture.AsyncCount);
        }

        /// <summary>
        /// A transaction begun on the connection while cached keys load must stop execution of a
        /// command that is not bound to it, as it would before execution starts.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_TransactionBegunDuringKeyLoad_Throws()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;

            // The simulated server has no transaction manager, so begin the API transaction
            // directly on the idle connection.
            SqlTransaction transaction = fixture.BeginTransaction();

            // Act
            fixture.CompleteKeyLoad();

            // Assert
            InvalidOperationException error =
                await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));
            Assert.Equal(
                ADP.TransactionRequired(nameof(SqlCommand.BeginExecuteReader)).Message,
                error.Message);
            Assert.False(IsInProgress(command));
            Assert.Equal(0, fixture.AsyncCount);

            // The command accepts the transaction once the failed execution has released it.
            command.Transaction = transaction;
            Assert.Same(transaction, command.Transaction);

            // Cleanup
            fixture.EndTransaction();
        }

        /// <summary>
        /// A bound transaction that completes while cached keys load must stop execution, so the
        /// command does not run outside the transaction it was bound to.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_TransactionCompletedDuringKeyLoad_Throws()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            SqlTransaction transaction = fixture.BeginTransaction();
            command.Transaction = transaction;
            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;

            // Complete the transaction as a commit acknowledged by the server would.
            transaction.InternalTransaction.Completed(TransactionState.Committed);
            Assert.Null(transaction.Connection);

            // Act
            fixture.CompleteKeyLoad();

            // Assert
            InvalidOperationException error =
                await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));
            Assert.Equal(ADP.TransactionZombied(transaction).Message, error.Message);
            Assert.False(IsInProgress(command));
            Assert.Equal(0, fixture.AsyncCount);
        }

        /// <summary>
        /// Reading <see cref="SqlCommand.Transaction"/> after the bound transaction completes while
        /// cached keys load must not clear the binding, so execution still stops.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_CompletedTransactionReadDuringKeyLoad_Throws()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            SqlTransaction transaction = fixture.BeginTransaction();
            command.Transaction = transaction;
            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;

            // Complete the transaction, then read it back as user code might.
            transaction.InternalTransaction.Completed(TransactionState.Committed);
            Assert.Null(command.Transaction);

            // Act
            fixture.CompleteKeyLoad();

            // Assert
            InvalidOperationException error =
                await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));
            Assert.Equal(ADP.TransactionZombied(transaction).Message, error.Message);
            Assert.False(IsInProgress(command));
            Assert.Null(command.Transaction);
            Assert.Equal(0, fixture.AsyncCount);
        }

        /// <summary>
        /// A stale key found after the connection switched databases must evict the entry that was
        /// probed, not the valid entry cached for the new database.
        /// </summary>
        [Fact]
        public async Task ExecuteReaderAsync_DatabaseChangedBeforeStaleKey_EvictsProbedEntry()
        {
            // Arrange
            using Fixture fixture = new();
            using SqlCommand command = fixture.CreateCachedCommand();
            string probedDatabase = fixture.Connection.Database;
            SqlQueryMetadataCache cache = SqlQueryMetadataCache.GetInstance();

            // Cache the same query for the other database, then switch back to probe the first.
            fixture.Connection.ChangeDatabase(OtherDatabase);
            cache.AddQueryMetadata(command, ignoreQueriesWithReturnValueParams: true);
            fixture.Connection.ChangeDatabase(probedDatabase);

            Task<SqlDataReader> operation = command.ExecuteReaderAsync();
            await fixture.KeyLoadEntered;
            fixture.Connection.ChangeDatabase(OtherDatabase);

            // Act
            fixture.FailKeyLoad();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Bounded(operation));

            // Assert
            Assert.True(cache.TryGetCachedQueryMetadata(command, out _));
            fixture.Connection.ChangeDatabase(probedDatabase);
            Assert.False(cache.TryGetCachedQueryMetadata(command, out _));
            Assert.False(IsInProgress(command));
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

        private const string OtherDatabase = "OtherDatabase";

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
            private readonly TdsServer _server = CreateServer();
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

            /// <summary>
            /// Records an API transaction on the open session, as a completed
            /// <see cref="SqlConnection.BeginTransaction()"/> would.
            /// </summary>
            internal SqlTransaction BeginTransaction()
            {
                SqlConnectionInternal inner = (SqlConnectionInternal)Connection.InnerConnection;
                SqlTransaction transaction =
                    new(inner, Connection, System.Data.IsolationLevel.ReadCommitted, null);
                inner.Parser.CurrentTransaction = transaction.InternalTransaction;
                return transaction;
            }

            /// <summary>
            /// Detaches the recorded transaction so closing the connection sends no rollback.
            /// </summary>
            internal void EndTransaction()
            {
                SqlConnectionInternal inner = (SqlConnectionInternal)Connection.InnerConnection;
                inner.Parser.CurrentTransaction = null;
            }

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

            private static TdsServer CreateServer()
            {
                TdsServerArguments arguments = new();
                return new TdsServer(new UseDatabaseQueryEngine(arguments), arguments);
            }
        }
    }
}
