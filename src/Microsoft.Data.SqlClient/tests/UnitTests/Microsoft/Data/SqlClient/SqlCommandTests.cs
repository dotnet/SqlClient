// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies batch command lookup for SQL errors and info messages, and asynchronous execution
/// cancellation state cleanup.
/// </summary>
public class SqlCommandTests
{
    /// <summary>Errors after the final DONEPROC have no remaining command to attribute.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GetBatchCommand_AfterLastDoneProc_ReturnsNull(int count)
    {
        // Arrange
        using SqlCommand command = CreateBatchCommand(count);
        TdsParser parser = new(false, false);
        for (int i = 0; i < count; i++)
        {
            command.OnDoneProc(parser._physicalStateObj);
        }

        // Act
        SqlBatchCommand? batchCommand = command.GetBatchCommand(command.GetCurrentBatchIndex());

        // Assert
        Assert.Equal(count, command.GetCurrentBatchIndex());
        Assert.Null(batchCommand);
    }

    /// <summary>Info messages after the final DONEPROC have no current command.</summary>
    [Fact]
    public void GetCurrentBatchCommand_AfterLastDoneProc_ReturnsNull()
    {
        // Arrange
        using SqlCommand command = CreateBatchCommand(1);
        command.OnDoneProc(new TdsParser(false, false)._physicalStateObj);

        // Act
        SqlBatchCommand? batchCommand = command.GetCurrentBatchCommand();

        // Assert
        Assert.Null(batchCommand);
    }

    /// <summary>Info messages during execution identify the executing command.</summary>
    [Fact]
    public void GetCurrentBatchCommand_DuringExecution_ReturnsBatchCommand()
    {
        // Arrange
        using SqlCommand command = CreateBatchCommand(2);
        SqlBatchCommand expected = GetBatchEntries(command)[1].batchCommand;
        command.OnDoneProc(new TdsParser(false, false)._physicalStateObj);

        // Act
        SqlBatchCommand batchCommand = command.GetCurrentBatchCommand();

        // Assert
        Assert.Same(expected, batchCommand);
    }

    /// <summary>Invalid error indexes must not hide the original error with a lookup failure.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void GetBatchCommand_IndexOutsideBatch_ReturnsNull(int index)
    {
        // Arrange
        using SqlCommand command = CreateBatchCommand(2);

        // Act
        SqlBatchCommand? batchCommand = command.GetBatchCommand(index);

        // Assert
        Assert.Null(batchCommand);
    }

    /// <summary>Ordinary commands do not have a batch entry, including for errors with a cause.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void GetBatchCommand_NonBatchCommand_ReturnsNull(int index)
    {
        // Arrange
        using SqlCommand command = new();

        // Act
        SqlBatchCommand? batchCommand = command.GetBatchCommand(index);

        // Assert
        Assert.Null(batchCommand);
    }

    /// <summary>Cleared batch state cannot supply a command for a later error.</summary>
    [Fact]
    public void GetBatchCommand_ClearedBatch_ReturnsNull()
    {
        // Arrange
        using SqlCommand command = CreateBatchCommand(1);
        command.SetBatchRPCMode(false);

        // Act
        SqlBatchCommand? batchCommand = command.GetBatchCommand(0);

        // Assert
        Assert.Null(batchCommand);
    }

    /// <summary>Valid error indexes still identify the exact command that failed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GetBatchCommand_IndexWithinBatch_ReturnsBatchCommand(int index)
    {
        // Arrange
        using SqlCommand command = CreateBatchCommand(2);
        List<_SqlRPC> entries = GetBatchEntries(command);
        SqlBatchCommand expected = entries[index].batchCommand;

        // Act
        SqlBatchCommand batchCommand = command.GetBatchCommand(index);

        // Assert
        Assert.Same(expected, batchCommand);
    }

    /// <summary>
    /// Key store cancellation after the describe phase releases the parser state must surface as
    /// cancellation, not as a null-lock failure, when the reader execution ends.
    /// </summary>
    [Fact]
    public void EndExecuteReaderAsync_CanceledTask_ThrowsOperationCanceled()
    {
        // Act
        Exception? inner = InvokeEndAsyncWithCanceledTask("EndExecuteReaderAsync");

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(inner);
    }

    /// <summary>
    /// Key store cancellation after the describe phase releases the parser state must surface as
    /// cancellation, not as a null-lock failure, when the non-query execution ends.
    /// </summary>
    [Fact]
    public void EndExecuteNonQueryAsync_CanceledTask_ThrowsOperationCanceled()
    {
        // Act
        Exception? inner = InvokeEndAsyncWithCanceledTask("EndExecuteNonQueryAsync");

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(inner);
    }

    /// <summary>
    /// Key store cancellation after the describe phase releases the parser state must surface as
    /// cancellation, not as a null-lock failure, when the XML reader execution ends.
    /// </summary>
    [Fact]
    public void EndExecuteXmlReaderAsync_CanceledTask_ThrowsOperationCanceled()
    {
        // Act
        Exception? inner = InvokeEndAsyncWithCanceledTask("EndExecuteXmlReaderAsync");

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(inner);
    }

    /// <summary>
    /// A missing or closed connection faults reader setup without retaining the caller's token,
    /// even though setup fails before the cleanup continuation is registered.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteReaderAsync_FailedSetup_ClearsCancellationToken(bool hasConnection)
    {
        // Arrange
        using SqlConnection connection = new();
        using SqlCommand command = new("SELECT 1", hasConnection ? connection : null);
        using CancellationTokenSource source = new();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => command.ExecuteReaderAsync(source.Token));

        // Assert
        Assert.Equal(CancellationToken.None, GetAsyncExecutionCancellationToken(command));
    }

    /// <summary>
    /// A missing or closed connection faults non-query setup without retaining the caller's token,
    /// even though setup fails before the cleanup continuation is registered.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteNonQueryAsync_FailedSetup_ClearsCancellationToken(bool hasConnection)
    {
        // Arrange
        using SqlConnection connection = new();
        using SqlCommand command = new("SELECT 1", hasConnection ? connection : null);
        using CancellationTokenSource source = new();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => command.ExecuteNonQueryAsync(source.Token));

        // Assert
        Assert.Equal(CancellationToken.None, GetAsyncExecutionCancellationToken(command));
    }

    /// <summary>
    /// A missing or closed connection faults XML reader setup without retaining the caller's
    /// token, even though setup fails before the cleanup continuation is registered.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteXmlReaderAsync_FailedSetup_ClearsCancellationToken(bool hasConnection)
    {
        // Arrange
        using SqlConnection connection = new();
        using SqlCommand command = new("SELECT 1", hasConnection ? connection : null);
        using CancellationTokenSource source = new();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => command.ExecuteXmlReaderAsync(source.Token));

        // Assert
        Assert.Equal(CancellationToken.None, GetAsyncExecutionCancellationToken(command));
    }

    /// <summary>Creates RPC entries without requiring a network connection.</summary>
    /// <param name="count">The number of entries to add.</param>
    /// <returns>A command owned by the test.</returns>
    private static SqlCommand CreateBatchCommand(int count)
    {
        SqlCommand command = new();
        command.SetBatchRPCMode(true, count);
        List<_SqlRPC> entries = GetBatchEntries(command);
        for (int i = 0; i < count; i++)
        {
            entries.Add(new _SqlRPC { batchCommand = new SqlBatchCommand("SELECT 1") });
        }
        command.SetBatchRPCModeReadyToExecute();
        return command;
    }

    /// <summary>Accesses the private RPC list to set up batch state without executing SQL.</summary>
    /// <param name="command">The command initialized in batch mode.</param>
    /// <returns>The command's RPC entries.</returns>
    private static List<_SqlRPC> GetBatchEntries(SqlCommand command) =>
        Assert.IsType<List<_SqlRPC>>(typeof(SqlCommand)
            .GetField("_RPCList", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(command));

    /// <summary>
    /// Invokes a private asynchronous End method with an already canceled task, as happens when a
    /// key store operation is canceled after the parser state was released.
    /// </summary>
    /// <param name="endMethod">The name of the private End method to invoke.</param>
    /// <returns>The exception thrown by the End method.</returns>
    private static Exception? InvokeEndAsyncWithCanceledTask(string endMethod)
    {
        using SqlConnection connection = new(
            "Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
        using SqlCommand command = new("SELECT 1", connection);
        TaskCompletionSource<object> completion = new();
        completion.SetCanceled();

        MethodInfo method = typeof(SqlCommand).GetMethod(
            endMethod, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Async End method not found.");
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(command, new object[] { completion.Task }));
        return exception.InnerException;
    }

    /// <summary>Reads the private token captured for the in-flight async execution.</summary>
    /// <param name="command">The command to inspect.</param>
    /// <returns>The captured cancellation token.</returns>
    private static CancellationToken GetAsyncExecutionCancellationToken(SqlCommand command) =>
        Assert.IsType<CancellationToken>(typeof(SqlCommand)
            .GetField(
                "_asyncExecutionCancellationToken", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(command));
}
