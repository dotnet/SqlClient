// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.Diagnostics;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Diagnostics;

/// <summary>
/// Verifies that the command diagnostic payloads carry the batch that produced them, both as a
/// typed property and as an entry in the <see cref="IReadOnlyList{T}"/> key/value view that
/// DiagnosticSource subscribers enumerate.
/// </summary>
public class SqlClientCommandPayloadTest
{
    private const string Operation = "ExecuteNonQuery";

    private static readonly Guid OperationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ConnectionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const long TransactionId = 42;
    private const long Timestamp = 1234567890;

    private static IReadOnlyList<SqlBatchCommand> CreateBatchCommands() =>
        new[]
        {
            new SqlBatchCommand("SELECT 1;"),
            new SqlBatchCommand("SELECT 2;"),
        };

    #region SqlClientCommandBefore

    /// <summary>
    /// Verifies that SqlClientCommandBefore.BatchCommands returns the list it was constructed with.
    /// </summary>
    [Fact]
    public void SqlClientCommandBefore_BatchCommands_ReturnsConstructorArgument()
    {
        IReadOnlyList<SqlBatchCommand> batchCommands = CreateBatchCommands();
        SqlClientCommandBefore payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, new SqlCommand(), batchCommands);

        Assert.Same(batchCommands, payload.BatchCommands);
    }

    /// <summary>
    /// Verifies that SqlClientCommandBefore.BatchCommands is null - present on the type, but
    /// null-valued - when the command is not executing as part of a SqlBatch.
    /// </summary>
    [Fact]
    public void SqlClientCommandBefore_BatchCommands_IsNullForNonBatchCommand()
    {
        SqlClientCommandBefore payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, new SqlCommand(), null);

        Assert.Null(payload.BatchCommands);
    }

    /// <summary>
    /// Verifies that the key/value view of SqlClientCommandBefore appends BatchCommands as the last
    /// entry and leaves the meaning of every pre-existing index unchanged.
    /// </summary>
    [Fact]
    public void SqlClientCommandBefore_KeyValueView_AppendsBatchCommandsAsLastEntry()
    {
        IReadOnlyList<SqlBatchCommand> batchCommands = CreateBatchCommands();
        SqlCommand command = new();
        SqlClientCommandBefore payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, command, batchCommands);

        KeyValuePair<string, object>[] expected =
        {
            new("OperationId", OperationId),
            new("Operation", Operation),
            new("Timestamp", Timestamp),
            new("ConnectionId", ConnectionId),
            new("TransactionId", TransactionId),
            new("Command", command),
            new("BatchCommands", batchCommands),
        };

        Assert.Equal(expected, payload);
    }

    #endregion

    #region SqlClientCommandAfter

    /// <summary>
    /// Verifies that SqlClientCommandAfter.BatchCommands returns the list it was constructed with.
    /// </summary>
    [Fact]
    public void SqlClientCommandAfter_BatchCommands_ReturnsConstructorArgument()
    {
        IReadOnlyList<SqlBatchCommand> batchCommands = CreateBatchCommands();
        SqlClientCommandAfter payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, new SqlCommand(), new Hashtable(), batchCommands);

        Assert.Same(batchCommands, payload.BatchCommands);
    }

    /// <summary>
    /// Verifies that SqlClientCommandAfter.BatchCommands is null - present on the type, but
    /// null-valued - when the command is not executing as part of a SqlBatch.
    /// </summary>
    [Fact]
    public void SqlClientCommandAfter_BatchCommands_IsNullForNonBatchCommand()
    {
        SqlClientCommandAfter payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, new SqlCommand(), new Hashtable(), null);

        Assert.Null(payload.BatchCommands);
    }

    /// <summary>
    /// Verifies that the key/value view of SqlClientCommandAfter appends BatchCommands as the last
    /// entry and leaves the meaning of every pre-existing index unchanged.
    /// </summary>
    [Fact]
    public void SqlClientCommandAfter_KeyValueView_AppendsBatchCommandsAsLastEntry()
    {
        IReadOnlyList<SqlBatchCommand> batchCommands = CreateBatchCommands();
        SqlCommand command = new();
        Hashtable statistics = new();
        SqlClientCommandAfter payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, command, statistics, batchCommands);

        KeyValuePair<string, object>[] expected =
        {
            new("OperationId", OperationId),
            new("Operation", Operation),
            new("Timestamp", Timestamp),
            new("ConnectionId", ConnectionId),
            new("TransactionId", TransactionId),
            new("Command", command),
            new("Statistics", statistics),
            new("BatchCommands", batchCommands),
        };

        Assert.Equal(expected, payload);
    }

    #endregion

    #region SqlClientCommandError

    /// <summary>
    /// Verifies that SqlClientCommandError.BatchCommands returns the list it was constructed with.
    /// </summary>
    [Fact]
    public void SqlClientCommandError_BatchCommands_ReturnsConstructorArgument()
    {
        IReadOnlyList<SqlBatchCommand> batchCommands = CreateBatchCommands();
        SqlClientCommandError payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, new SqlCommand(), new InvalidOperationException(), batchCommands);

        Assert.Same(batchCommands, payload.BatchCommands);
    }

    /// <summary>
    /// Verifies that SqlClientCommandError.BatchCommands is null - present on the type, but
    /// null-valued - when the command is not executing as part of a SqlBatch.
    /// </summary>
    [Fact]
    public void SqlClientCommandError_BatchCommands_IsNullForNonBatchCommand()
    {
        SqlClientCommandError payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, new SqlCommand(), new InvalidOperationException(), null);

        Assert.Null(payload.BatchCommands);
    }

    /// <summary>
    /// Verifies that the key/value view of SqlClientCommandError appends BatchCommands as the last
    /// entry and leaves the meaning of every pre-existing index unchanged.
    /// </summary>
    [Fact]
    public void SqlClientCommandError_KeyValueView_AppendsBatchCommandsAsLastEntry()
    {
        IReadOnlyList<SqlBatchCommand> batchCommands = CreateBatchCommands();
        SqlCommand command = new();
        InvalidOperationException exception = new();
        SqlClientCommandError payload = new(
            OperationId, Operation, Timestamp, ConnectionId, TransactionId, command, exception, batchCommands);

        KeyValuePair<string, object>[] expected =
        {
            new("OperationId", OperationId),
            new("Operation", Operation),
            new("Timestamp", Timestamp),
            new("ConnectionId", ConnectionId),
            new("TransactionId", TransactionId),
            new("Command", command),
            new("Exception", exception),
            new("BatchCommands", batchCommands),
        };

        Assert.Equal(expected, payload);
    }

    #endregion
}
