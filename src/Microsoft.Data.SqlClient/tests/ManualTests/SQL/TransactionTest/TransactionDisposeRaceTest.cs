// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies that disposing a <see cref="SqlTransaction"/> does not throw when SqlClient closes the
    /// broken connection on a background thread while the dispose is rolling back.
    /// <para>
    /// After a fatal error on a pending async command (a timeout whose attention the server never
    /// acknowledges, or a network failure), SqlClient faults the command's task first and closes the
    /// connection afterwards on another thread. The caller's catch block or <c>using</c> scope disposes the
    /// transaction concurrently with that close, which zombies the internal transaction and disposes the
    /// internal connection. The dispose path checked the internal connection once and dereferenced it
    /// again afterwards, so it could throw <see cref="NullReferenceException"/>; and before the close ran,
    /// it threw <see cref="InvalidOperationException"/> for the broken connection. Either replaced the
    /// original error.
    /// </para>
    /// <para>
    /// The race window is a few instructions wide. These tests make it deterministic without any product
    /// test hook: the background close is held at the internal connection's close lock until the dispose
    /// has entered <c>SqlInternalTransaction.Rollback()</c>, observed through the driver's own
    /// <c>Microsoft.Data.SqlClient.EventSource</c> scope trace, and is then allowed to finish before the
    /// rollback continues.
    /// </para>
    /// </summary>
    [Trait("Set", "3")]
    public class TransactionDisposeRaceTest
    {
        /// <summary>
        /// Upper bound for the command to fail. A timeout without an attention acknowledgement takes
        /// <c>CommandTimeout</c> plus the driver's 5 second attention timeout.
        /// </summary>
        private static readonly TimeSpan FailureBudget = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Upper bound for the background close to finish once it is released.
        /// </summary>
        private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Disposes the transaction while SqlClient closes the connection after a command timeout that the
        /// server never acknowledged: the shape of an unresponsive server.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void Dispose_ConnectionClosedAfterUnacknowledgedTimeout_DoesNotThrow()
        {
            // Arrange
            // The proxy stops forwarding, so the command times out and its attention is never acknowledged.
            using ProxyServer proxy = ProxyServer.CreateAndStartProxy(DataTestUtility.TCPConnectionString, out string connectionString);
            using SqlConnection connection = OpenUnpooled(connectionString);
            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlCommand command = new("WAITFOR DELAY '00:00:30'", connection, transaction) { CommandTimeout = 1 };
            using BackgroundCloseGate gate = new(connection);
            Task pending = command.ExecuteNonQueryAsync();
            proxy.PauseCopying();

            // The driver faults the command, then starts closing the connection on another thread; the gate
            // holds that close.
            AggregateException failure = Assert.Throws<AggregateException>(() => pending.Wait(FailureBudget));
            SqlException timeout = Assert.IsType<SqlException>(failure.InnerException);
            Assert.Equal(-2, timeout.Number);

            // Act
            Exception exception = gate.DisposeDuringClose(transaction);

            // Assert
            Assert.True(gate.ClosedDuringRollback, "The background close did not run while the dispose was rolling back, so the race was not exercised.");
            Assert.Null(exception);
        }

        /// <summary>
        /// Disposes the transaction while SqlClient closes the connection after the network dropped under a
        /// pending async command.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void Dispose_ConnectionClosedAfterNetworkFailure_DoesNotThrow()
        {
            // Arrange
            using ProxyServer proxy = ProxyServer.CreateAndStartProxy(DataTestUtility.TCPConnectionString, out string connectionString);
            using SqlConnection connection = OpenUnpooled(connectionString);
            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlCommand command = new("WAITFOR DELAY '00:00:30'", connection, transaction);
            using BackgroundCloseGate gate = new(connection);
            Task pending = command.ExecuteNonQueryAsync();

            // The network drops while the command is pending. The driver faults the command, then starts
            // closing the connection on another thread; the gate holds that close.
            proxy.KillAllConnections();
            AggregateException failure = Assert.Throws<AggregateException>(() => pending.Wait(FailureBudget));
            Assert.IsType<SqlException>(failure.InnerException);

            // Act
            Exception exception = gate.DisposeDuringClose(transaction);

            // Assert
            Assert.True(gate.ClosedDuringRollback, "The background close did not run while the dispose was rolling back, so the race was not exercised.");
            Assert.Null(exception);
        }

        /// <summary>
        /// Disposes the transaction after a command timeout that the server never acknowledged, while the
        /// driver's close of the broken connection has not run yet.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void Dispose_CloseStillPendingAfterUnacknowledgedTimeout_DoesNotThrow()
        {
            // Arrange
            // The proxy stops forwarding, so the command times out and its attention is never acknowledged.
            using ProxyServer proxy = ProxyServer.CreateAndStartProxy(DataTestUtility.TCPConnectionString, out string connectionString);
            using SqlConnection connection = OpenUnpooled(connectionString);
            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlCommand command = new("WAITFOR DELAY '00:00:30'", connection, transaction) { CommandTimeout = 1 };
            using BackgroundCloseGate gate = new(connection);
            Task pending = command.ExecuteNonQueryAsync();
            proxy.PauseCopying();

            // The driver faults the command and breaks the connection; the gate holds its close.
            AggregateException failure = Assert.Throws<AggregateException>(() => pending.Wait(FailureBudget));
            SqlException timeout = Assert.IsType<SqlException>(failure.InnerException);
            Assert.Equal(-2, timeout.Number);

            // Act
            Exception exception = gate.DisposeBeforeClose(transaction);

            // Assert
            Assert.Null(exception);
            Assert.True(gate.ClosedAfterDispose, "The background close did not finish after the dispose.");
        }

        /// <summary>
        /// Disposes the transaction after the network dropped under a pending async command, while the
        /// driver's close of the broken connection has not run yet.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void Dispose_CloseStillPendingAfterNetworkFailure_DoesNotThrow()
        {
            // Arrange
            using ProxyServer proxy = ProxyServer.CreateAndStartProxy(DataTestUtility.TCPConnectionString, out string connectionString);
            using SqlConnection connection = OpenUnpooled(connectionString);
            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlCommand command = new("WAITFOR DELAY '00:00:30'", connection, transaction);
            using BackgroundCloseGate gate = new(connection);
            Task pending = command.ExecuteNonQueryAsync();

            // The network drops while the command is pending. The driver faults the command and breaks the
            // connection; the gate holds its close.
            proxy.KillAllConnections();
            AggregateException failure = Assert.Throws<AggregateException>(() => pending.Wait(FailureBudget));
            Assert.IsType<SqlException>(failure.InnerException);

            // Act
            Exception exception = gate.DisposeBeforeClose(transaction);

            // Assert
            Assert.Null(exception);
            Assert.True(gate.ClosedAfterDispose, "The background close did not finish after the dispose.");
        }

        /// <summary>
        /// Opens a connection that is not pooled, so its close disposes the internal connection.
        /// </summary>
        /// <param name="connectionString">The connection string to the proxy.</param>
        /// <returns>The open connection.</returns>
        private static SqlConnection OpenUnpooled(string connectionString)
        {
            SqlConnection connection = new(new SqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Orders the dispose and the driver's background close deterministically.
        /// <para>
        /// On construction it takes the lock that <c>DbConnectionInternal.CloseConnection</c> takes, so the
        /// driver's background close blocks before it touches the transaction. The dispose then runs on the
        /// thread that holds the lock, either before the close (<see cref="DisposeBeforeClose"/>) or during it
        /// (<see cref="DisposeDuringClose"/>): there, an <see cref="EventListener"/> on the driver's scope trace
        /// releases the lock when <c>SqlInternalTransaction.Rollback()</c> is entered, and waits for the close to
        /// finish before the rollback continues. The listener only acts on that thread, so other tests are not
        /// affected.
        /// </para>
        /// </summary>
        private sealed class BackgroundCloseGate : EventListener
        {
            private const string ProviderName = "Microsoft.Data.SqlClient.EventSource";

            /// <summary>
            /// The provider's <c>Scope</c> keyword, declared by the driver as <c>SqlClientEventSource.Keywords.Scope</c>.
            /// </summary>
            private const EventKeywords ScopeKeyword = (EventKeywords)4;

            private const string RollbackScope = "SqlInternalTransaction.Rollback | API";

            private readonly object _closeLock;
            private readonly ManualResetEventSlim _closed = new(false);
            private int _holdingThreadId;
            private bool _lockTaken;
            private bool _releaseOnRollback;

            /// <summary>
            /// Takes the close lock of <paramref name="connection"/>'s internal connection on the calling thread.
            /// </summary>
            /// <param name="connection">The open connection whose background close is gated.</param>
            public BackgroundCloseGate(SqlConnection connection)
            {
                connection.StateChange += (_, e) =>
                {
                    if (e.CurrentState == ConnectionState.Closed)
                    {
                        _closed.Set();
                    }
                };
                _closeLock = connection.GetInternalConnection();
                Monitor.Enter(_closeLock, ref _lockTaken);
                _holdingThreadId = Environment.CurrentManagedThreadId;
            }

            /// <summary>
            /// Whether the background close finished while the dispose was held inside the rollback.
            /// </summary>
            public bool ClosedDuringRollback { get; private set; }

            /// <summary>
            /// Whether the background close finished once the lock was released after the dispose.
            /// </summary>
            public bool ClosedAfterDispose { get; private set; }

            /// <summary>
            /// Disposes <paramref name="transaction"/> and lets the background close run to completion when the
            /// dispose enters the rollback.
            /// </summary>
            /// <param name="transaction">The transaction to dispose.</param>
            /// <returns>The exception the dispose threw, or <see langword="null"/>.</returns>
            public Exception DisposeDuringClose(SqlTransaction transaction)
            {
                Assert.Equal(_holdingThreadId, Environment.CurrentManagedThreadId);
                _releaseOnRollback = true;
                return Record.Exception(transaction.Dispose);
            }

            /// <summary>
            /// Disposes <paramref name="transaction"/> while the background close is still held, then lets the
            /// close run to completion.
            /// </summary>
            /// <param name="transaction">The transaction to dispose.</param>
            /// <returns>The exception the dispose threw, or <see langword="null"/>.</returns>
            public Exception DisposeBeforeClose(SqlTransaction transaction)
            {
                Assert.Equal(_holdingThreadId, Environment.CurrentManagedThreadId);
                Exception exception = Record.Exception(transaction.Dispose);
                ReleaseLock();
                ClosedAfterDispose = _closed.Wait(CloseBudget);
                return exception;
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == ProviderName)
                {
                    EnableEvents(eventSource, EventLevel.Verbose, ScopeKeyword);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (!_releaseOnRollback
                    || !_lockTaken
                    || Environment.CurrentManagedThreadId != _holdingThreadId
                    || eventData.Payload is not { Count: > 0 }
                    || eventData.Payload[0] is not string message
                    || !message.StartsWith(RollbackScope, StringComparison.Ordinal))
                {
                    return;
                }

                // Let the background close run to completion while the rollback is held here.
                ReleaseLock();
                ClosedDuringRollback = _closed.Wait(CloseBudget);
            }

            public override void Dispose()
            {
                base.Dispose();
                ReleaseLock();
                _closed.Dispose();
            }

            private void ReleaseLock()
            {
                if (_lockTaken && Monitor.IsEntered(_closeLock))
                {
                    _lockTaken = false;
                    Monitor.Exit(_closeLock);
                }
            }
        }
    }
}
