// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies command execution guards, batch command lookup for SQL errors and info messages, and
/// cancellation handling when asynchronous execution ends.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class SqlCommandTests
{
    private const string CanceledExecutionConnectionString =
        "Data Source=async-ae-unit-test;Column Encryption Setting=Enabled";

    /// <summary>A pending parameter write must keep the original connection attached.</summary>
    [Fact]
    public Task Connection_PendingAsyncWrite_Throws() =>
        AssertPendingWriteAsync(command =>
        {
            // Arrange
            SqlConnection original = command.Connection;

            // Act
            Exception? error = Record.Exception(
                () => command.Connection = null);
            command.Connection = original;

            // Assert
            Assert.Equal(SQL.CannotModifyPropertyAsyncOperationInProgress("Connection").Message,
                Assert.IsType<InvalidOperationException>(error).Message);
            return Task.CompletedTask;
        });

    /// <summary>A pending parameter write must not accept a replacement transaction.</summary>
    [Fact]
    public Task Transaction_PendingAsyncWrite_Throws() =>
        AssertPendingWriteAsync(command =>
        {
            // Arrange
            SqlConnection connection = command.Connection;
            // This unattached transaction has no server-side state to roll back.
            SqlTransaction transaction = new(
                (SqlConnectionInternal)connection.InnerConnection, connection,
                IsolationLevel.ReadCommitted, null);

            // Act
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => command.Transaction = transaction);

            // Assert
            Assert.Equal(SQL.CannotModifyPropertyAsyncOperationInProgress("Transaction").Message,
                error.Message);
            return Task.CompletedTask;
        });

    /// <summary>Sync execution must not reuse a command whose async write is pending.</summary>
    [Fact]
    public Task ExecuteReader_PendingAsyncWrite_Throws() =>
        AssertPendingWriteAsync(command =>
        {
            // Act
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => command.ExecuteReader());

            // Assert
            Assert.Equal(SQL.PendingBeginXXXExists().Message, error.Message);
            return Task.CompletedTask;
        });

    /// <summary>Another async execution must be rejected before it can disturb the first write.</summary>
    [Fact]
    public Task ExecuteReaderAsync_PendingAsyncWrite_Throws() =>
        AssertPendingWriteAsync(async command =>
        {
            // Act
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => command.ExecuteReaderAsync());

            // Assert
            Assert.Equal(SQL.PendingBeginXXXExists().Message, error.Message);
        });

    /// <summary>Exercises the public API while parameter streaming is deterministically suspended.</summary>
    /// <param name="assertion">The operation and assertions to run during the pending write.</param>
    /// <returns>Completion after the failed write has been observed and its guard released.</returns>
    private static async Task AssertPendingWriteAsync(Func<SqlCommand, Task> assertion)
    {
        // Arrange
        using TdsServer server = new();
        server.Start();
        using SqlConnection connection = new(new SqlConnectionStringBuilder
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        }.ConnectionString);
        connection.Open();
        using GatedReadStream stream = new();
        using SqlCommand command = new("SELECT @value", connection);
        command.Parameters.Add("@value", SqlDbType.VarBinary, -1).Value = stream;
        Task<SqlDataReader> execution = command.ExecuteReaderAsync();
        try
        {
            Assert.True(stream.ReadStarted);
            Assert.False(execution.IsCompleted);
            await assertion(command);
        }
        finally
        {
            // Cleanup
            stream.Fail();
            await Assert.ThrowsAsync<IOException>(() => execution);
        }

        // Assert
        command.Connection = null;
        Assert.Null(command.Connection);
    }

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
        // Arrange
        using SqlConnection connection = new(CanceledExecutionConnectionString);
        using SqlCommand command = new("SELECT 1", connection);
        Task canceled = CreateCanceledTask();

        // Act
        Exception exception = Record.Exception(() => command.EndExecuteReaderAsync(canceled));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    /// <summary>
    /// Key store cancellation after the describe phase releases the parser state must surface as
    /// cancellation, not as a null-lock failure, when the non-query execution ends.
    /// </summary>
    [Fact]
    public void EndExecuteNonQueryAsync_CanceledTask_ThrowsOperationCanceled()
    {
        // Arrange
        using SqlConnection connection = new(CanceledExecutionConnectionString);
        using SqlCommand command = new("SELECT 1", connection);
        Task canceled = CreateCanceledTask();

        // Act
        Exception exception = Record.Exception(() => command.EndExecuteNonQueryAsync(canceled));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    /// <summary>
    /// Key store cancellation after the describe phase releases the parser state must surface as
    /// cancellation, not as a null-lock failure, when the XML reader execution ends.
    /// </summary>
    [Fact]
    public void EndExecuteXmlReaderAsync_CanceledTask_ThrowsOperationCanceled()
    {
        // Arrange
        using SqlConnection connection = new(CanceledExecutionConnectionString);
        using SqlCommand command = new("SELECT 1", connection);
        Task canceled = CreateCanceledTask();

        // Act
        Exception exception = Record.Exception(() => command.EndExecuteXmlReaderAsync(canceled));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
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
    /// Creates an already canceled task, as happens when a key store operation is canceled after
    /// the parser state was released.
    /// </summary>
    /// <returns>A canceled task.</returns>
    private static Task CreateCanceledTask()
    {
        TaskCompletionSource<object> completion = new();
        completion.SetCanceled();
        return completion.Task;
    }
}
