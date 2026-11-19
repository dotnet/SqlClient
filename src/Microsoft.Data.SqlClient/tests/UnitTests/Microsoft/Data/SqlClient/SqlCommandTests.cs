// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies batch command lookup for SQL errors and info messages, cancellation handling when
/// asynchronous execution ends, asynchronous query metadata cache dispatch, and bounded retry
/// routing for enclave package failures.
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
    /// A retryable enclave package fault before the read stage must invalidate cached metadata and
    /// retry once, completing with the retry's result.
    /// </summary>
    [Fact]
    public async Task
        CreateLocalCompletionTask_RetryableWriteFaultRetrySucceeds_ReturnsRetryResult()
    {
        // Arrange
        using SqlCommand command = NewCachedRetryCommand();
        using SqlConnection connection = command.Connection;
        object expectedResult = new();
        RetryObserver retry = new(command, Task.FromResult(expectedResult));
        TaskCompletionSource<object> localCompletion = new();
        TaskCompletionSource<object> globalCompletion = new();
        AttachCachedExecutionRetry(command, localCompletion, globalCompletion, retry.Retry);

        // Act
        localCompletion.SetException(NewPackageFault());

        // Assert
        Assert.Same(expectedResult, await globalCompletion.Task);
        Assert.Equal(1, retry.Calls);
    }

    /// <summary>
    /// A retryable enclave package fault retries once; a failed retry faults the caller's
    /// completion with the retry's own exception instead of retrying again.
    /// </summary>
    [Fact]
    public async Task CreateLocalCompletionTask_RetryableWriteFaultRetryFails_ThrowsRetryFailure()
    {
        // Arrange
        using SqlCommand command = NewCachedRetryCommand();
        using SqlConnection connection = command.Connection;
        EnclaveDelegate.RetryableEnclaveQueryExecutionException retryFailure =
            new("retry failed", null);
        RetryObserver retry = new(command, Task.FromException<object>(retryFailure));
        TaskCompletionSource<object> localCompletion = new();
        TaskCompletionSource<object> globalCompletion = new();
        AttachCachedExecutionRetry(command, localCompletion, globalCompletion, retry.Retry);

        // Act
        localCompletion.SetException(NewPackageFault());

        // Assert
        var exception = await Assert.ThrowsAsync<
            EnclaveDelegate.RetryableEnclaveQueryExecutionException>(() => globalCompletion.Task);
        Assert.Same(retryFailure, exception);
        Assert.Equal(1, retry.Calls);
    }

    /// <summary>
    /// A retryable enclave package fault retries once; a canceled retry cancels the caller's
    /// completion.
    /// </summary>
    [Fact]
    public async Task CreateLocalCompletionTask_RetryableWriteFaultRetryCanceled_Cancels()
    {
        // Arrange
        using SqlCommand command = NewCachedRetryCommand();
        using SqlConnection connection = command.Connection;
        RetryObserver retry = new(command, Task.FromCanceled<object>(new CancellationToken(true)));
        TaskCompletionSource<object> localCompletion = new();
        TaskCompletionSource<object> globalCompletion = new();
        AttachCachedExecutionRetry(command, localCompletion, globalCompletion, retry.Retry);

        // Act
        localCompletion.SetException(NewPackageFault());

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => globalCompletion.Task);
        Assert.True(globalCompletion.Task.IsCanceled);
        Assert.Equal(1, retry.Calls);
    }

    /// <summary>
    /// An ordinary write failure keeps its exception and does not trigger a retry.
    /// </summary>
    [Fact]
    public async Task CreateLocalCompletionTask_NonRetryableWriteFault_ThrowsWithoutRetry()
    {
        // Arrange
        using SqlConnection connection = new(
            "Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
        using SqlCommand command = new("SELECT @p", connection);
        RetryObserver retry = new(command, Task.FromResult<object>(command));
        InvalidOperationException failure = new("write failed");
        TaskCompletionSource<object> localCompletion = new();
        TaskCompletionSource<object> globalCompletion = new();
        AttachCachedExecutionRetry(command, localCompletion, globalCompletion, retry.Retry);

        // Act
        localCompletion.SetException(failure);

        // Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => globalCompletion.Task);
        Assert.Same(failure, exception);
        Assert.Equal(0, retry.Calls);
    }

    /// <summary>
    /// A canceled write cancels the caller's completion without triggering a retry.
    /// </summary>
    [Fact]
    public async Task CreateLocalCompletionTask_CanceledWrite_CancelsWithoutRetry()
    {
        // Arrange
        using SqlConnection connection = new(
            "Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
        using SqlCommand command = new("SELECT @p", connection);
        RetryObserver retry = new(command, Task.FromResult<object>(command));
        TaskCompletionSource<object> localCompletion = new();
        TaskCompletionSource<object> globalCompletion = new();
        AttachCachedExecutionRetry(command, localCompletion, globalCompletion, retry.Retry);

        // Act
        localCompletion.SetCanceled();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => globalCompletion.Task);
        Assert.True(globalCompletion.Task.IsCanceled);
        Assert.Equal(0, retry.Calls);
    }

    /// <summary>
    /// Creates a command whose query metadata is cached, so a retry must invalidate it.
    /// </summary>
    /// <returns>A cached command with an unopened connection; the caller disposes both.</returns>
    private static SqlCommand NewCachedRetryCommand()
    {
        SqlCommand command =
            QueryMetadataCacheTestCommands.NewCachedCommand(new TestKeyStoreProvider());
        Assert.True(SqlQueryMetadataCache.GetInstance().TryGetCachedQueryMetadata(command, out _));
        return command;
    }

    /// <summary>Creates the fault raised when enclave package generation must be retried.</summary>
    /// <returns>A retryable enclave exception.</returns>
    private static EnclaveDelegate.RetryableEnclaveQueryExecutionException NewPackageFault() =>
        new("package generation failed", null);

    /// <summary>
    /// Wires the command's cache-hit completion path with an End delegate that fails the test if a
    /// failed write reaches the read stage.
    /// </summary>
    /// <param name="command">Command whose cached metadata may need invalidating.</param>
    /// <param name="localCompletion">Completion representing the initial write stage.</param>
    /// <param name="globalCompletion">Completion returned to the caller.</param>
    /// <param name="retry">Delegate observing the retry attempt.</param>
    private static void AttachCachedExecutionRetry(
        SqlCommand command,
        TaskCompletionSource<object> localCompletion,
        TaskCompletionSource<object> globalCompletion,
        Func<SqlCommand, CommandBehavior, AsyncCallback?, object?, int, bool, bool, IAsyncResult>
            retry)
    {
        MethodInfo method = typeof(SqlCommand).GetMethod(
            "CreateLocalCompletionTask", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Async retry method not found.");
        Func<SqlCommand, IAsyncResult, bool, string, object> end = (_, _, _, _) =>
            throw new InvalidOperationException("A failed write must not enter the read stage.");
        method.Invoke(command, new object?[]
        {
            CommandBehavior.Default, null, 30, true, true,
            globalCompletion, localCompletion, end, retry, nameof(SqlCommand.EndExecuteReader),
            ADP.TimerCurrent(), CancellationToken.None,
        });
    }

    /// <summary>
    /// Records retry attempts and checks that each one is an async-write retry of the original
    /// command that starts only after its cached metadata was invalidated.
    /// </summary>
    private sealed class RetryObserver
    {
        private readonly SqlCommand _command;
        private readonly Task<object> _result;

        /// <summary>Creates an observer that completes every retry with the given task.</summary>
        /// <param name="command">The command expected to be retried.</param>
        /// <param name="result">The task returned by the retry.</param>
        internal RetryObserver(SqlCommand command, Task<object> result)
        {
            _command = command;
            _result = result;
        }

        /// <summary>Gets the number of retry attempts.</summary>
        internal int Calls { get; private set; }

        /// <summary>Validates and records one retry attempt.</summary>
        internal IAsyncResult Retry(
            SqlCommand command,
            CommandBehavior behavior,
            AsyncCallback? callback,
            object? state,
            int timeout,
            bool isRetry,
            bool asyncWrite)
        {
            Calls++;
            Assert.Same(_command, command);
            Assert.Equal(CommandBehavior.Default, behavior);
            Assert.Null(callback);
            Assert.Null(state);
            Assert.InRange(timeout, 0, 30);
            Assert.True(isRetry);
            Assert.True(asyncWrite);
            Assert.False(
                SqlQueryMetadataCache.GetInstance().TryGetCachedQueryMetadata(command, out _));
            return _result;
        }
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
