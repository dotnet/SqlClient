// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Info;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies cancellation sends ATTENTION while async completion awaits more response data.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class CommandCancellationTests
{
    private static readonly TimeSpan WatchdogTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A partial INFO response must not prevent reader execution from sending ATTENTION.
    /// </summary>
    [Fact]
    public Task ExecuteReaderAsync_PartialResponse_CancellationSendsAttention() =>
        AssertCancellationSendsAttentionAsync(
            static (command, token) => command.ExecuteReaderAsync(token));

    /// <summary>
    /// A partial INFO response must not prevent nonquery execution from sending ATTENTION.
    /// </summary>
    [Fact]
    public Task ExecuteNonQueryAsync_PartialResponse_CancellationSendsAttention() =>
        AssertCancellationSendsAttentionAsync(
            static (command, token) => command.ExecuteNonQueryAsync(token));

    /// <summary>
    /// A partial INFO response must not prevent XML reader execution from sending ATTENTION.
    /// </summary>
    [Fact]
    public Task ExecuteXmlReaderAsync_PartialResponse_CancellationSendsAttention() =>
        AssertCancellationSendsAttentionAsync(
            static (command, token) => command.ExecuteXmlReaderAsync(token));

    /// <summary>
    /// Exercises partial-response cancellation through the supplied async command API.
    /// </summary>
    private static async Task AssertCancellationSendsAttentionAsync(
        Func<SqlCommand, CancellationToken, Task> execute)
    {
        // Arrange
        using CancellationTraceListener trace = new();
        using PartialResponseServer server = new();
        server.Start();

        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"127.0.0.1,{server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false,
            ConnectRetryCount = 0
        };
        using SqlConnection connection = new(builder.ConnectionString)
        {
            FireInfoMessageEventOnUserErrors = true
        };
        using SqlCommand command = new("SELECT 1", connection) { CommandTimeout = 60 };
        using CancellationTokenSource cts = new();
        var partialResponse = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.InfoMessage += (_, e) =>
        {
            if (e.Message == PartialResponseServer.PartialResult)
            {
                partialResponse.TrySetResult(true);
            }
        };

        Task? executeTask = null;
        Task? cancelTask = null;
        try
        {
            await connection.OpenAsync();

            // Act
            executeTask = execute(command, cts.Token);

            await CompleteWithinAsync(partialResponse.Task, "Partial response");
            Assert.False(executeTask.IsCompleted,
                "The command must still be awaiting response data.");

            cancelTask = Task.Run(() => cts.Cancel());
            await CompleteWithinAsync(server.AttentionReceived.Task, "ATTENTION reception");
            await CompleteWithinAsync(cancelTask, "Cancellation callbacks");

            // Assert
            Exception? exception = await Record.ExceptionAsync(
                () => CompleteWithinAsync(executeTask, "Command completion"));
            Assert.True(exception is OperationCanceledException or SqlException,
                $"Expected cancellation after ATTENTION, got {exception}.{Environment.NewLine}{trace}");
            if (exception is SqlException sqlException)
            {
                Assert.Contains("Operation cancelled by user.", sqlException.Message);
            }
        }
        finally
        {
            // Cleanup
            // Closing the peer unblocks reads even if the cancellation monitor regresses.
            server.Dispose();
            if (executeTask is not null)
            {
                _ = executeTask.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
            }
            if (cancelTask is not null)
            {
                _ = cancelTask.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
            }
        }
    }

    /// <summary>
    /// Fails promptly when protocol progress stalls and propagates completed operation failures.
    /// </summary>
    private static async Task CompleteWithinAsync(Task task, string context)
    {
        Assert.True(await Task.WhenAny(task, Task.Delay(WatchdogTimeout)) == task,
            $"{context} did not complete within {WatchdogTimeout.TotalSeconds}s.");
        await task;
    }

    /// <summary>
    /// Retains recent protocol progress when a cancellation watchdog expires on a CI platform.
    /// </summary>
    private sealed class CancellationTraceListener : EventListener
    {
        private readonly ConcurrentQueue<string> _messages = new();

        /// <summary>
        /// Enables the driver's existing flow traces without recording packet payloads.
        /// </summary>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)(2 | 128));
            }
        }

        /// <summary>
        /// Keeps a bounded, thread-safe history without blocking cancellation callbacks.
        /// </summary>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.Payload is not null)
            {
                _messages.Enqueue(
                    $"{Environment.CurrentManagedThreadId}: {string.Join(" | ", eventData.Payload)}");
                while (_messages.Count > 128)
                {
                    _messages.TryDequeue(out _);
                }
            }
        }

        /// <summary>
        /// Includes the captured protocol history in the failure message.
        /// </summary>
        public override string ToString() => string.Join(Environment.NewLine, _messages);
    }

    /// <summary>
    /// Flushes INFO and DONE_MORE without completing the batch, then acknowledges ATTENTION.
    /// </summary>
    private sealed class PartialResponseServer : TdsServer
    {
        internal const string PartialResult = "partial result";

        internal TaskCompletionSource<bool> AttentionReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Leaves the response incomplete so async command completion must wait for another packet.
        /// </summary>
        public override TDSMessageCollection OnSQLBatchRequest(
            ITDSServerSession session, TDSMessage message) =>
            new(new TDSMessage(TDSMessageType.Response,
                new TDSInfoToken(50000, 1, 10, PartialResult, "test"),
                new TDSDoneToken(TDSDoneTokenStatusType.More))
            {
                IsEndOfMessage = false
            });

        /// <summary>
        /// Records the actual ATTENTION request before returning the standard attention DONE token.
        /// </summary>
        public override TDSMessageCollection OnAttention(
            ITDSServerSession session, TDSMessage message)
        {
            AttentionReceived.TrySetResult(true);
            return base.OnAttention(session, message);
        }
    }
}
