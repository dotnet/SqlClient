// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.SqlServer.TDS.Info;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies command completion and batch error attribution.</summary>
public class SqlCommandTests
{
    /// <summary>
    /// Closing during metadata initialization must abort execution, not drain its reader concurrently.
    /// </summary>
    [Fact]
    public Task ExecuteReaderAsync_CloseDuringInitialization_FaultsExecution() =>
        AssertCloseDuringInitializationAsync(
            static command => Task.Run(() => command.ExecuteReaderAsync()));

    /// <summary>
    /// Synchronous execution must also fail when its connection closes during initialization.
    /// </summary>
    [Fact]
    public Task ExecuteReader_CloseDuringInitialization_FaultsExecution() =>
        AssertCloseDuringInitializationAsync(
            static command => Task.Run(() => command.ExecuteReader()));

    /// <summary>Holds metadata parsing inside an INFO callback while closing the connection.</summary>
    /// <param name="execute">Starts command execution on a separate thread.</param>
    /// <returns>The bounded close and command-completion assertions.</returns>
    private static async Task AssertCloseDuringInitializationAsync(
        Func<SqlCommand, Task<SqlDataReader>> execute)
    {
        // Arrange
        TimeSpan watchdog = TimeSpan.FromSeconds(30);
        using TdsServerFixture fixture = new();
        fixture.TdsServer.OnSQLBatchCompleted = response =>
            response.Insert(0, new TDSInfoToken(50000, 1, 10, "initializing", "test"));
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{fixture.TdsServer.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        using SqlConnection connection = new(builder.ConnectionString)
        {
            FireInfoMessageEventOnUserErrors = true
        };
        using SqlCommand command = new("SELECT 1", connection);
        using ManualResetEventSlim resumeInitialization = new(false);
        TaskCompletionSource<bool> initializing = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool resumedInTime = false;
        connection.InfoMessage += (_, e) =>
        {
            if (e.Message == "initializing")
            {
                initializing.TrySetResult(true);
                resumedInTime = resumeInitialization.Wait(watchdog);
            }
        };
        await connection.OpenAsync();
        Task<SqlDataReader>? execution = null;
        try
        {
            // Act
            // Hold the parser inside reader initialization while another thread closes the connection.
            execution = execute(command);
            Assert.Same(initializing.Task,
                await Task.WhenAny(initializing.Task, Task.Delay(watchdog)));
            Task close = Task.Run(() => connection.Close());
            Assert.Same(close, await Task.WhenAny(close, Task.Delay(watchdog)));
            await close;
            resumeInitialization.Set();

            // Assert
            Assert.Same(execution, await Task.WhenAny(execution, Task.Delay(watchdog)));
            Exception? error = await Record.ExceptionAsync(async () => await execution);
            Assert.True(resumedInTime, "Initialization did not resume within the watchdog.");
            Assert.IsType<InvalidOperationException>(error);
        }
        finally
        {
            // Cleanup
            // Release both sides before disposing the connection if an assertion fails.
            resumeInitialization.Set();
            fixture.Dispose();
            if (execution is not null)
            {
                _ = execution.ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
            }
        }
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
}
