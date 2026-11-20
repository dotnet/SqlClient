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
    /// The shared preparation loads cached keys through the async provider even when the
    /// provider completes immediately.
    /// </summary>
    [Fact]
    public async Task PrepareForTransparentEncryption_AsyncCacheHit_UsesAsyncProvider()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            DecryptAsyncCallback = _ =>
                Task.FromResult(QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 53)),
        };
        using SqlCommand command = QueryMetadataCacheTestCommands.NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;

        // Act
        await InvokePrepareForTransparentEncryption(command, isAsync: true);

        // Assert
        Assert.Equal(1, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
        Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
    }

    /// <summary>
    /// Synchronous execution uses the same preparation sequence without calling async providers.
    /// </summary>
    [Fact]
    public void PrepareForTransparentEncryption_SyncCacheHit_UsesSyncProvider()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            PlaintextKey = QueryMetadataCacheTestCommands.NewPlaintextKey(seed: 59),
        };
        using SqlCommand command = QueryMetadataCacheTestCommands.NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;

        // Act
        InvokePrepareForTransparentEncryption(command, isAsync: false).GetAwaiter().GetResult();

        // Assert
        Assert.Equal(1, provider.DecryptCallCount);
        Assert.Equal(0, provider.DecryptAsyncCallCount);
        Assert.True(command.Parameters[0].CipherMetadata.IsAlgorithmInitialized());
    }

    /// <summary>
    /// A stale async cache entry falls through to discovery after clearing its cached metadata.
    /// </summary>
    [Fact]
    public async Task PrepareForTransparentEncryption_AsyncCacheHitWithStaleKey_FallsBackToDiscovery()
    {
        // Arrange
        TestKeyStoreProvider provider = new()
        {
            DecryptAsyncCallback = _ =>
                Task.FromException<byte[]>(new ArgumentException("stale key")),
        };
        using SqlCommand command = QueryMetadataCacheTestCommands.NewCachedCommand(provider);
        using SqlConnection connection = command.Connection;

        // Act
        Task preparation = InvokePrepareForTransparentEncryption(command, isAsync: true);

        // Assert
        // The closed connection stops the describe fallback before any network I/O.
        await Assert.ThrowsAsync<InvalidOperationException>(() => preparation);
        Assert.Equal(1, provider.DecryptAsyncCallCount);
        Assert.Null(command.Parameters[0].CipherMetadata);
        Assert.False(SqlQueryMetadataCache.GetInstance().TryGetCachedQueryMetadata(command, out _));
    }

    /// <summary>
    /// Plaintext cached parameters complete without key store calls through the shared path.
    /// </summary>
    [Fact]
    public async Task PrepareForTransparentEncryption_AsyncCacheHitPlaintextOnly_SkipsKeyStore()
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
        await InvokePrepareForTransparentEncryption(command, isAsync: true);

        // Assert
        Assert.Equal(0, provider.DecryptAsyncCallCount);
        Assert.Equal(0, provider.DecryptCallCount);
        Assert.Null(command.Parameters[0].CipherMetadata);
    }

    /// <summary>
    /// Cached enclave keys are published even when the parameters require no key store calls.
    /// </summary>
    [Fact]
    public async Task PrepareForTransparentEncryption_AsyncCacheHitEnclaveKeys_PublishesKeys()
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
        await InvokePrepareForTransparentEncryption(command, isAsync: true);

        // Assert
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
    /// Probes the query metadata cache and invokes shared preparation without opening a connection.
    /// </summary>
    /// <param name="command">The command whose parameters hit the query metadata cache.</param>
    /// <param name="isAsync">Whether preparation uses async provider calls.</param>
    /// <returns>The completion of encryption preparation.</returns>
    private static Task InvokePrepareForTransparentEncryption(SqlCommand command, bool isAsync)
    {
        MethodInfo probe = typeof(SqlCommand).GetMethod(
            "TryGetCachedQueryMetadata", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Cache probe method not found.");
        object?[] probeArguments = { false, null };
        Assert.True((bool)probe.Invoke(command, probeArguments)!);

        MethodInfo prepare = typeof(SqlCommand).GetMethod(
            "PrepareForTransparentEncryption", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Encryption preparation method not found.");
        return (Task)prepare.Invoke(
            command,
            new object?[]
            {
                probeArguments[1], null, 30, isAsync, false, "ExecuteReader", isAsync,
                CancellationToken.None,
            })!;
    }
}
