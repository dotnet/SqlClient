// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies batch command lookup for SQL errors and info messages, cancellation handling when
/// asynchronous execution ends, and asynchronous query metadata cache dispatch.
/// </summary>
public class SqlCommandTests
{
    private const string CanceledExecutionConnectionString =
        "Data Source=async-ae-unit-test;Column Encryption Setting=Enabled";

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

    /// <summary>
    /// An async cache hit whose provider completes synchronously must still load the key on a
    /// worker, not inline on the caller's thread, and must publish the cached metadata.
    /// </summary>
    [Fact]
    public async Task PrepareForTransparentEncryption_AsyncCacheHit_LoadsKeyOnWorker()
    {
        // Arrange
        int callerThreadId = Environment.CurrentManagedThreadId;
        bool invokingPrepare = true;
        bool providerRanInline = false;
        TestKeyStoreProvider provider = new()
        {
            DecryptAsyncCallback = _ =>
            {
                providerRanInline =
                    invokingPrepare && Environment.CurrentManagedThreadId == callerThreadId;
                return Task.FromResult(QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 53));
            },
        };
        using SqlCommand command = QueryMetadataCacheTestCommands.NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;

        // Act
        object?[] arguments;
        try
        {
            arguments = InvokeAsyncPrepareForTransparentEncryption(command);
        }
        finally
        {
            invokingPrepare = false;
        }
        Task completion = Assert.IsAssignableFrom<Task>(arguments[3]);
        await completion;

        // Assert
        Assert.Equal(true, arguments[5]);
        Assert.False(providerRanInline);
        Assert.Equal(1, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
        Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
    }

    /// <summary>
    /// An async cache hit with a stale key must run the blocking describe fallback on a worker,
    /// not inline on the caller's thread, after evicting the stale entry.
    /// </summary>
    [Fact]
    public async Task PrepareForTransparentEncryption_AsyncCacheHitWithStaleKey_FallsBackOnWorker()
    {
        // Arrange
        int callerThreadId = Environment.CurrentManagedThreadId;
        bool invokingPrepare = true;
        bool providerRanInline = false;
        TestKeyStoreProvider provider = new()
        {
            DecryptAsyncCallback = _ =>
            {
                providerRanInline =
                    invokingPrepare && Environment.CurrentManagedThreadId == callerThreadId;
                return Task.FromException<byte[]>(new ArgumentException("stale key"));
            },
        };
        using SqlCommand command = QueryMetadataCacheTestCommands.NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;

        // Act
        object?[] arguments;
        try
        {
            arguments = InvokeAsyncPrepareForTransparentEncryption(command);
        }
        finally
        {
            invokingPrepare = false;
        }
        Task completion = Assert.IsAssignableFrom<Task>(arguments[3]);

        // Assert
        // The closed connection stops the describe fallback before any network I/O.
        await Assert.ThrowsAsync<InvalidOperationException>(() => completion);
        Assert.Equal(true, arguments[5]);
        Assert.False(providerRanInline);
        Assert.Equal(1, provider.DecryptAsyncCallCount);
        Assert.Null(command.Parameters[0].CipherMetadata);
        Assert.False(SqlQueryMetadataCache.GetInstance().TryGetCachedQueryMetadata(command, out _));
    }

    /// <summary>
    /// An async cache hit whose parameters are all plaintext has no keys to load, so it completes
    /// inline without a worker task or a provider call.
    /// </summary>
    [Fact]
    public void PrepareForTransparentEncryption_AsyncCacheHitPlaintextOnly_CompletesInline()
    {
        // Arrange
        // Cache the query with a plaintext parameter.
        TestKeyStoreProvider provider = new();
        using SqlCommand command =
            QueryMetadataCacheTestCommands.NewCommandWithCipherMetadata(provider);
        using SqlConnection connection = command.Connection;
        command.Parameters[0].CipherMetadata = null;
        SqlQueryMetadataCache.GetInstance().AddQueryMetadata(
            command, ignoreQueriesWithReturnValueParams: true);

        // Act
        object?[] arguments = InvokeAsyncPrepareForTransparentEncryption(command);

        // Assert
        Assert.Null(arguments[3]);
        Assert.Equal(true, arguments[5]);
        Assert.Equal(0, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
        Assert.Null(command.Parameters[0].CipherMetadata);
    }

    /// <summary>
    /// A dispatched cache hit whose worker finishes before the entry checks it must still return
    /// its task, because the reader clears the in-progress guard only through that task.
    /// </summary>
    [Fact]
    public void CompletePreparation_DispatchedAlreadyCompleted_ReturnsTask()
    {
        // Act
        Task? returnTask = InvokeCompletePreparation(deferFailures: true, dispatched: true);

        // Assert
        Assert.NotNull(returnTask);
        Assert.Equal(TaskStatus.RanToCompletion, returnTask.Status);
    }

    /// <summary>
    /// Completed async preparation that was not dispatched finishes inline without a task, so
    /// the reader takes its synchronous execution branch.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletePreparation_NotDispatchedAlreadyCompleted_ReturnsNull(bool deferFailures)
    {
        // Act
        Task? returnTask = InvokeCompletePreparation(deferFailures, dispatched: false);

        // Assert
        Assert.Null(returnTask);
    }

    /// <summary>
    /// An async cache hit with plaintext parameters but cached enclave keys still completes on a
    /// worker, because execution would otherwise build the enclave package on the caller's thread.
    /// </summary>
    [Fact]
    public async Task PrepareForTransparentEncryption_AsyncCacheHitEnclaveKeys_UsesWorker()
    {
        // Arrange
        // Cache the query with a plaintext parameter and one enclave key.
        TestKeyStoreProvider provider = new();
        using SqlCommand command =
            QueryMetadataCacheTestCommands.NewCommandWithCipherMetadata(provider);
        using SqlConnection connection = command.Connection;
        command.Parameters[0].CipherMetadata = null;
        command.requiresEnclaveComputations = true;
        command.keysToBeSentToEnclave = new ConcurrentDictionary<int, SqlTceCipherInfoEntry>();
        command.keysToBeSentToEnclave[0] = new SqlTceCipherInfoEntry(0);
        SqlQueryMetadataCache.GetInstance().AddQueryMetadata(
            command, ignoreQueriesWithReturnValueParams: true);
        command.keysToBeSentToEnclave = null;

        // Act
        object?[] arguments = InvokeAsyncPrepareForTransparentEncryption(command);
        Task completion = Assert.IsAssignableFrom<Task>(arguments[3]);
        await completion;

        // Assert
        Assert.Equal(true, arguments[5]);
        Assert.Equal(0, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
        Assert.NotNull(command.keysToBeSentToEnclave);
        Assert.Single(command.keysToBeSentToEnclave);
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

    /// <summary>
    /// Invokes the private asynchronous encryption preparation entry point for a first attempt,
    /// as async execution does, without opening a connection.
    /// </summary>
    /// <param name="command">The command whose parameters may hit the query metadata cache.</param>
    /// <returns>The invocation arguments, including the returned task and used-cache flag.</returns>
    private static Task? InvokeCompletePreparation(bool deferFailures, bool dispatched)
    {
        MethodInfo method = typeof(SqlCommand).GetMethod(
            "CompletePreparation", BindingFlags.Static | BindingFlags.NonPublic)!;
        object[] arguments = { default(ValueTask), true, deferFailures, dispatched };
        return (Task?)method.Invoke(null, arguments);
    }

    private static object?[] InvokeAsyncPrepareForTransparentEncryption(SqlCommand command)
    {
        MethodInfo prepare = typeof(SqlCommand).GetMethod(
            "PrepareForTransparentEncryption", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Encryption preparation method not found.");
        object?[] arguments =
        {
            true, 30, new TaskCompletionSource<object>(), null, true, false, false, "ExecuteReader",
            CancellationToken.None,
        };
        prepare.Invoke(command, arguments);
        return arguments;
    }
}
