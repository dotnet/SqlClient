// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies that a SqlBatch execution reaches a DiagnosticSource subscriber with the batch it
/// executed, that an ordinary SqlCommand execution reports no batch, and that the state SqlBatch
/// caches to make that possible is released on disposal.
/// </summary>
// Serializes execution with other SimulatedServerTests classes.  Required here because
// DiagnosticListener.AllListeners is process-wide state that these tests subscribe to.
[Collection(SimulatedServerTestCollection.Name)]
public class BatchDiagnosticsTests : IDisposable
{
    private const string WriteCommandBefore = "Microsoft.Data.SqlClient.WriteCommandBefore";
    private const string WriteCommandAfter = "Microsoft.Data.SqlClient.WriteCommandAfter";
    private const string WriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";

    private static readonly string[] BatchCommandTexts = { "SELECT 1;", "SELECT 2;", "SELECT 3;" };

    private readonly TdsServerFixture _fixture;
    private readonly string _connectionString;

    public BatchDiagnosticsTests()
    {
        _fixture = new TdsServerFixture();
        TdsServer server = _fixture.TdsServer;
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    public void Dispose() => _fixture.Dispose();

    /// <summary>
    /// Verifies that every SqlBatch execute path - sync and async - surfaces the batch's commands,
    /// in order and as a collection the subscriber cannot mutate, on the WriteCommandBefore
    /// payload.
    /// </summary>
    [Theory]
    [InlineData(nameof(SqlBatch.ExecuteNonQuery))]
    [InlineData(nameof(SqlBatch.ExecuteNonQueryAsync))]
    [InlineData(nameof(SqlBatch.ExecuteScalar))]
    [InlineData(nameof(SqlBatch.ExecuteScalarAsync))]
    [InlineData(nameof(SqlBatch.ExecuteReader))]
    [InlineData(nameof(SqlBatch.ExecuteReaderAsync))]
    public async Task Batch_CommandBeforePayload_CarriesBatchCommands(string executeMethod)
    {
        using CommandEventCollector collector = new();
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        using SqlBatch batch = CreateBatch(connection);

        // The simulated TDS server has no RPC handler, so a batch always faults at the transport
        // level.  WriteCommandBefore is emitted before the fault, which is the payload under test.
        await Assert.ThrowsAnyAsync<SqlException>(() => ExecuteAsync(batch, executeMethod));

        SqlClientCommandBefore payload = collector.SinglePayload<SqlClientCommandBefore>(WriteCommandBefore);
        Assert.Equal(BatchCommandTexts, payload.BatchCommands.Select(command => command.CommandText));

        // The payload must not hand the subscriber a mutable view of the live batch, which it could
        // downcast to and edit while the batch is executing.
        Assert.False(
            payload.BatchCommands is ICollection<SqlBatchCommand> { IsReadOnly: false },
            "BatchCommands must not be a writable collection.");
    }

    /// <summary>
    /// Verifies that a failed SqlBatch execution surfaces the batch's commands on the
    /// WriteCommandError payload.
    /// </summary>
    [Fact]
    public async Task Batch_CommandErrorPayload_CarriesBatchCommands()
    {
        using CommandEventCollector collector = new();
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        using SqlBatch batch = CreateBatch(connection);

        await Assert.ThrowsAnyAsync<SqlException>(() => ExecuteAsync(batch, nameof(SqlBatch.ExecuteNonQuery)));

        SqlClientCommandError payload = collector.SinglePayload<SqlClientCommandError>(WriteCommandError);
        Assert.Equal(BatchCommandTexts, payload.BatchCommands.Select(command => command.CommandText));
    }

    /// <summary>
    /// Verifies that disposing a SqlBatch releases the cached read-only view of its commands that
    /// an execution allocated, rather than leaving it holding a wrapper over the emptied list.
    /// </summary>
    [Fact]
    public void Batch_Dispose_ReleasesCachedBatchCommandsView()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        SqlBatch batch = CreateBatch(connection);

        // The view is allocated by the execute path, so it has to run before disposal is meaningful.
        Assert.ThrowsAny<SqlException>(() => batch.ExecuteNonQuery());
        Assert.NotNull(BatchAccessor.ReadOnlyCommands(batch));

        batch.Dispose();

        Assert.Null(BatchAccessor.ReadOnlyCommands(batch));
    }

    /// <summary>
    /// Verifies that an ordinary SqlCommand execution - which is not part of a SqlBatch - reports a
    /// null BatchCommands on both the WriteCommandBefore and WriteCommandAfter payloads.
    /// </summary>
    [Fact]
    public void PlainCommand_CommandPayloads_ReportNoBatchCommands()
    {
        using CommandEventCollector collector = new();
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        using SqlCommand command = new("SELECT 1;", connection);

        command.ExecuteNonQuery();

        Assert.Null(collector.SinglePayload<SqlClientCommandBefore>(WriteCommandBefore).BatchCommands);
        Assert.Null(collector.SinglePayload<SqlClientCommandAfter>(WriteCommandAfter).BatchCommands);
    }

    private static SqlBatch CreateBatch(SqlConnection connection)
    {
        SqlBatch batch = new(connection);
        foreach (string commandText in BatchCommandTexts)
        {
            batch.BatchCommands.Add(new SqlBatchCommand(commandText));
        }

        return batch;
    }

    private static async Task ExecuteAsync(SqlBatch batch, string executeMethod)
    {
        switch (executeMethod)
        {
            case nameof(SqlBatch.ExecuteNonQuery):
                batch.ExecuteNonQuery();
                break;
            case nameof(SqlBatch.ExecuteNonQueryAsync):
                await batch.ExecuteNonQueryAsync();
                break;
            case nameof(SqlBatch.ExecuteScalar):
                batch.ExecuteScalar();
                break;
            case nameof(SqlBatch.ExecuteScalarAsync):
                await batch.ExecuteScalarAsync();
                break;
            case nameof(SqlBatch.ExecuteReader):
                batch.ExecuteReader().Dispose();
                break;
            case nameof(SqlBatch.ExecuteReaderAsync):
                (await batch.ExecuteReaderAsync()).Dispose();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(executeMethod), executeMethod, null);
        }
    }

    /// <summary>
    /// Reflection wrapper over the private members of <see cref="SqlBatch"/>.  Nested so that the
    /// type initializer runs only for the tests that read them.
    /// </summary>
    private static class BatchAccessor
    {
        // Private implementation detail, so a rename would silently turn Batch_Dispose_Releases-
        // CachedBatchCommandsView into a no-op.  Fail loudly instead.
        private static readonly FieldInfo s_readOnlyCommands =
            typeof(SqlBatch).GetField("_readOnlyCommands", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SqlBatch no longer declares a field '_readOnlyCommands'.");

        internal static object? ReadOnlyCommands(SqlBatch batch) => s_readOnlyCommands.GetValue(batch);
    }

    /// <summary>
    /// Collects the payloads written to the SqlClient DiagnosticListener for the lifetime of the
    /// instance.
    /// </summary>
    private sealed class CommandEventCollector
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<KeyValuePair<string, object?>> _events = new();
        private readonly IDisposable _allListenersSubscription;
        private IDisposable? _listenerSubscription;

        public CommandEventCollector() =>
            _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);

        public T SinglePayload<T>(string eventName)
        {
            lock (_events)
            {
                return Assert.Single(_events.Where(e => e.Key == eventName).Select(e => e.Value).OfType<T>());
            }
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "SqlClientDiagnosticListener")
            {
                _listenerSubscription = listener.Subscribe(this);
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            lock (_events)
            {
                _events.Add(value);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allListenersSubscription.Dispose();
        }
    }
}
