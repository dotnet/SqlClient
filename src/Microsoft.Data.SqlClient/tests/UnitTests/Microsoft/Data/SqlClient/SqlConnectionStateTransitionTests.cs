// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.ProviderBase;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Regression tests for connection state transitions that can be affected by concurrent state updates.
/// </summary>
public class SqlConnectionStateTransitionTests
{
    /// <summary>
    /// Verifies TryOpenInner does not surface an invalid cast when the inner state changes after TryOpenConnection.
    /// </summary>
    [Fact]
    public void TryOpenInner_WhenInnerConnectionChangesAfterTryOpen_ThrowsInvalidOperation()
    {
        using SqlConnection connection = new();
        connection.ForceNewConnection = false;
        bool initialized = connection.SetInnerConnectionFrom(new TestRaceDbConnectionInternal(), DbConnectionClosedNeverOpened.SingletonInstance);
        Assert.True(initialized);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => connection.TryOpenInner(null));
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Verifies TryOpenInner does not surface an invalid cast when the inner state changes after TryReplaceConnection.
    /// </summary>
    [Fact]
    public void TryOpenInner_WhenInnerConnectionChangesAfterTryReplace_ThrowsInvalidOperation()
    {
        using SqlConnection connection = new();
        connection.ForceNewConnection = true;
        bool initialized = connection.SetInnerConnectionFrom(new TestRaceDbConnectionInternal(), DbConnectionClosedNeverOpened.SingletonInstance);
        Assert.True(initialized);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => connection.TryOpenInner(null));
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Verifies evented state transitions reject non-transitional source states.
    /// </summary>
    [Fact]
    public void SetInnerConnectionEvent_WhenInnerConnectionIsNotTransitionState_ThrowsInvalidOperation()
    {
        using SqlConnection connection = new();
        bool initialized = connection.SetInnerConnectionFrom(DbConnectionClosedPreviouslyOpened.SingletonInstance, DbConnectionClosedNeverOpened.SingletonInstance);
        Assert.True(initialized);

        int initialCloseCount = connection.CloseCount;
        int eventCount = 0;
        connection.StateChange += (_, _) => eventCount++;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => connection.SetInnerConnectionEvent(new TestOpenDbConnectionInternal()));
        Assert.NotNull(exception);
        Assert.Equal(initialCloseCount, connection.CloseCount);
        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// Verifies direct transition-to-state writes reject non-transitional source states.
    /// </summary>
    [Fact]
    public void SetInnerConnectionTo_WhenInnerConnectionIsNotTransitionState_ThrowsInvalidOperation()
    {
        using SqlConnection connection = new();
        bool initialized = connection.SetInnerConnectionFrom(DbConnectionClosedPreviouslyOpened.SingletonInstance, DbConnectionClosedNeverOpened.SingletonInstance);
        Assert.True(initialized);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => connection.SetInnerConnectionTo(DbConnectionClosedNeverOpened.SingletonInstance));
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Verifies Connecting-to-Open event transitions raise a single open state-change notification.
    /// </summary>
    [Fact]
    public void SetInnerConnectionEvent_FromConnectingToOpen_RaisesOpenStateChange()
    {
        using SqlConnection connection = new();
        bool enteredConnecting = connection.SetInnerConnectionFrom(DbConnectionClosedConnecting.SingletonInstance, DbConnectionClosedNeverOpened.SingletonInstance);
        Assert.True(enteredConnecting);

        int initialCloseCount = connection.CloseCount;
        StateChangeEventArgs? stateChange = null;
        connection.StateChange += (_, e) => stateChange = e;

        connection.SetInnerConnectionEvent(new TestOpenDbConnectionInternal());

        Assert.NotNull(stateChange);
        Assert.Equal(ConnectionState.Closed, stateChange!.OriginalState);
        Assert.Equal(ConnectionState.Open, stateChange.CurrentState);
        Assert.Equal(initialCloseCount, connection.CloseCount);
    }

    /// <summary>
    /// Verifies OpenBusy-to-Closed event transitions raise a single closed state-change notification and increment close count.
    /// </summary>
    [Fact]
    public void SetInnerConnectionEvent_FromOpenBusyToClosed_RaisesClosedStateChangeAndIncrementsCloseCount()
    {
        using SqlConnection connection = new();
        TestOpenDbConnectionInternal openConnection = new();

        bool enteredOpen = connection.SetInnerConnectionFrom(openConnection, DbConnectionClosedNeverOpened.SingletonInstance);
        Assert.True(enteredOpen);
        bool enteredOpenBusy = connection.SetInnerConnectionFrom(DbConnectionOpenBusy.SingletonInstance, openConnection);
        Assert.True(enteredOpenBusy);

        int initialCloseCount = connection.CloseCount;
        StateChangeEventArgs? stateChange = null;
        connection.StateChange += (_, e) => stateChange = e;

        connection.SetInnerConnectionEvent(DbConnectionClosedPreviouslyOpened.SingletonInstance);

        Assert.NotNull(stateChange);
        Assert.Equal(ConnectionState.Open, stateChange!.OriginalState);
        Assert.Equal(ConnectionState.Closed, stateChange.CurrentState);
        Assert.Equal(initialCloseCount + 1, connection.CloseCount);
    }

    /// <summary>
    /// A test inner connection that forces a post-open state change to simulate race-sensitive transition windows.
    /// </summary>
    private sealed class TestRaceDbConnectionInternal : DbConnectionInternal
    {
        /// <summary>
        /// Initializes a closed test connection that permits connection-string updates.
        /// </summary>
        internal TestRaceDbConnectionInternal() : base(ConnectionState.Closed, true, true)
        {
        }

        /// <summary>
        /// Gets a placeholder server version for abstract member satisfaction.
        /// </summary>
        public override string ServerVersion => "0";

        /// <summary>
        /// Not supported in this test double.
        /// </summary>
        /// <param name="il">Requested isolation level.</param>
        /// <returns>Never returns; always throws.</returns>
        public override DbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();

        /// <summary>
        /// No-op for this test double.
        /// </summary>
        /// <param name="transaction">Transaction to enlist.</param>
        public override void EnlistTransaction(Transaction transaction)
        {
        }

        /// <summary>
        /// No-op reset for this test double.
        /// </summary>
        internal override void ResetConnection()
        {
        }

        /// <summary>
        /// No-op activation for this test double.
        /// </summary>
        /// <param name="transaction">Transaction supplied by the caller.</param>
        protected override void Activate(Transaction transaction)
        {
        }

        /// <summary>
        /// No-op deactivation for this test double.
        /// </summary>
        protected override void Deactivate()
        {
        }

        /// <summary>
        /// Simulates a race by replacing the inner connection with a closed singleton after open succeeds.
        /// </summary>
        /// <param name="outerConnection">Owning outer connection.</param>
        /// <param name="connectionFactory">Factory used to mutate inner state.</param>
        /// <param name="retry">Retry continuation.</param>
        /// <param name="userOptions">User connection options.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        internal override bool TryOpenConnection(
            DbConnection outerConnection,
            SqlConnectionFactory connectionFactory,
            TaskCompletionSource<DbConnectionInternal> retry,
            SqlConnectionOptions userOptions)
        {
            connectionFactory.SetInnerConnectionTo(outerConnection, DbConnectionClosedPreviouslyOpened.SingletonInstance);
            return true;
        }

        /// <summary>
        /// Simulates a race by replacing the inner connection with a closed singleton after replace succeeds.
        /// </summary>
        /// <param name="outerConnection">Owning outer connection.</param>
        /// <param name="connectionFactory">Factory used to mutate inner state.</param>
        /// <param name="retry">Retry continuation.</param>
        /// <param name="userOptions">User connection options.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        internal override bool TryReplaceConnection(
            DbConnection outerConnection,
            SqlConnectionFactory connectionFactory,
            TaskCompletionSource<DbConnectionInternal> retry,
            SqlConnectionOptions userOptions)
        {
            connectionFactory.SetInnerConnectionTo(outerConnection, DbConnectionClosedPreviouslyOpened.SingletonInstance);
            return true;
        }
    }

    /// <summary>
    /// A minimal open-state inner connection used as a non-transitional target in transition validation tests.
    /// </summary>
    private sealed class TestOpenDbConnectionInternal : DbConnectionInternal
    {
        /// <summary>
        /// Initializes an open test connection instance.
        /// </summary>
        internal TestOpenDbConnectionInternal() : base(ConnectionState.Open, true, false)
        {
        }

        /// <summary>
        /// Gets a placeholder server version for abstract member satisfaction.
        /// </summary>
        public override string ServerVersion => "0";

        /// <summary>
        /// Not supported in this test double.
        /// </summary>
        /// <param name="il">Requested isolation level.</param>
        /// <returns>Never returns; always throws.</returns>
        public override DbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();

        /// <summary>
        /// No-op for this test double.
        /// </summary>
        /// <param name="transaction">Transaction to enlist.</param>
        public override void EnlistTransaction(Transaction transaction)
        {
        }

        /// <summary>
        /// No-op reset for this test double.
        /// </summary>
        internal override void ResetConnection()
        {
        }

        /// <summary>
        /// No-op activation for this test double.
        /// </summary>
        /// <param name="transaction">Transaction supplied by the caller.</param>
        protected override void Activate(Transaction transaction)
        {
        }

        /// <summary>
        /// No-op deactivation for this test double.
        /// </summary>
        protected override void Deactivate()
        {
        }
    }
}
