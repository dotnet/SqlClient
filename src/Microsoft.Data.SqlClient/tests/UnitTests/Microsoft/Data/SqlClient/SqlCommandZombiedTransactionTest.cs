// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Data.SqlClient.Connection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Regression tests for GitHub issue #4855: a command bound to a transaction that the server
/// has already aborted must not run, where it would auto-commit.
/// </summary>
public class SqlCommandZombiedTransactionTest
{
    /// <summary>
    /// A new command constructed with a server-aborted transaction fails instead of executing.
    /// </summary>
    [Fact]
    public void Execute_NewCommandOnServerAbortedTransaction_Throws()
    {
        SqlTransaction transaction = CreateLiveTransaction();
        transaction.InternalTransaction.Completed(TransactionState.Aborted);
        Assert.NotNull(transaction.InternalTransaction);
        Assert.Null(transaction.Connection);

        using SqlCommand command = new SqlCommand("INSERT INTO T DEFAULT VALUES", connection: null, transaction);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

        Assert.Contains("no longer usable", exception.Message);
    }

    /// <summary>
    /// The command that was already bound when the server aborted the transaction fails on its next execution.
    /// </summary>
    [Fact]
    public void Execute_CommandBoundBeforeServerAbort_Throws()
    {
        SqlTransaction transaction = CreateLiveTransaction();
        using SqlCommand command = new SqlCommand("INSERT INTO T DEFAULT VALUES")
        {
            Transaction = transaction
        };

        transaction.InternalTransaction.Completed(TransactionState.Aborted);
        Assert.NotNull(transaction.InternalTransaction);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

        Assert.Contains("no longer usable", exception.Message);
    }

    /// <summary>
    /// Reading <see cref="SqlCommand.Transaction"/> still reports null, and the next execute is rejected.
    /// </summary>
    [Fact]
    public void Execute_AfterReadingTransactionProperty_Throws()
    {
        SqlTransaction transaction = CreateLiveTransaction();
        using SqlCommand command = new SqlCommand("INSERT INTO T DEFAULT VALUES")
        {
            Transaction = transaction
        };

        transaction.InternalTransaction.Completed(TransactionState.Aborted);
        Assert.Null(command.Transaction);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

        Assert.Contains("no longer usable", exception.Message);
    }

    /// <summary>
    /// A clone of a command bound to a server-aborted transaction fails instead of executing.
    /// </summary>
    [Fact]
    public void Execute_CloneOfCommandBoundToServerAbortedTransaction_Throws()
    {
        SqlTransaction transaction = CreateLiveTransaction();
        using SqlCommand command = new SqlCommand("INSERT INTO T DEFAULT VALUES")
        {
            Transaction = transaction
        };

        transaction.InternalTransaction.Completed(TransactionState.Aborted);

        using SqlCommand clone = command.Clone();
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => clone.ExecuteNonQuery());

        Assert.Contains("no longer usable", exception.Message);
    }

    /// <summary>
    /// After the caller rolls back the aborted transaction, a later command is no longer rejected as unusable.
    /// </summary>
    [Fact]
    public void Execute_AfterRollbackOfServerAbortedTransaction_IsNotRejectedAsZombied()
    {
        SqlTransaction transaction = CreateLiveTransaction();
        using SqlCommand command = new SqlCommand("INSERT INTO T DEFAULT VALUES", connection: null, transaction);
        transaction.InternalTransaction.Completed(TransactionState.Aborted);
        transaction.Rollback();
        Assert.Null(command.Transaction);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

        Assert.Contains("Connection property has not been initialized", exception.Message);
    }

    /// <summary>
    /// Builds a live <see cref="SqlTransaction"/> without opening a server connection.
    /// </summary>
    private static SqlTransaction CreateLiveTransaction()
    {
        SqlConnection connection = new SqlConnection();
        SqlConnectionInternal stub = CreateConnectionStub();

        // An open connection's inner connection is SqlConnectionInternal. That is what makes a
        // server abort defer zombification until the caller rolls the transaction back.
        FieldInfo innerConnection = typeof(SqlConnection).GetField("_innerConnection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        innerConnection.SetValue(connection, stub);

        SqlTransaction transaction = new SqlTransaction(
            stub,
            connection,
            System.Data.IsolationLevel.ReadCommitted,
            internalTransaction: null);

        Assert.NotNull(transaction.Connection);
        return transaction;
    }

    /// <summary>
    /// Allocates a connection stub without running its constructor, which opens a server connection.
    /// </summary>
    private static SqlConnectionInternal CreateConnectionStub() =>
#if NET
        (SqlConnectionInternal)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SqlConnectionInternal));
#else
        (SqlConnectionInternal)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SqlConnectionInternal));
#endif
}
