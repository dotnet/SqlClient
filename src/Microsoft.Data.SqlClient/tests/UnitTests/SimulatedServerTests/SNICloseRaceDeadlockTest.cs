// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Race variant of the SNIClose close-during-I/O regression tests
/// (ADO.Net #43847 / ICM 775308542).
///
/// <para>
/// The sibling <c>SNICloseDeadlockTest</c> closes over a <b>quiescent</b>
/// pending read: the server stays silent, so the close path simply cancels the
/// in-flight read and no completion callback is executing at close time. This
/// test instead <b>releases the server's response at the same instant the
/// connection is closed</b> (rendezvous on a <see cref="Barrier"/>), so the
/// read's completion callback fires concurrently with <c>SNIClose</c> - the
/// exact ordering hazard the deadlock is about. It repeats many iterations to
/// widen the race window.
/// </para>
///
/// <para>
/// The close runs on a dedicated long-running thread under a bounded wait: a
/// deadlock manifests as the wait timing out, which fails the test. A healthy
/// close completes promptly.
/// </para>
/// </summary>
public class SNICloseRaceDeadlockTest
{
    /// <summary>
    /// Upper bound for how long a non-deadlocked close should take. Generous so
    /// it only distinguishes "completed" from "deadlocked" on slow CI agents.
    /// </summary>
    private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Upper bound for waiting on the server to receive the client's batch, i.e.
    /// for the client's async read to become pending.
    /// </summary>
    private static readonly TimeSpan HandshakeBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A query engine that signals when a SQL batch arrives and then withholds
    /// the response until released, so the test controls exactly when the
    /// client's read completion fires.
    /// </summary>
    private sealed class ReleasableQueryEngine : QueryEngine
    {
        private readonly ManualResetEventSlim _batchReceived;
        private readonly ManualResetEventSlim _releaseResponse;

        public ReleasableQueryEngine(
            TdsServerArguments arguments,
            ManualResetEventSlim batchReceived,
            ManualResetEventSlim releaseResponse)
            : base(arguments)
        {
            _batchReceived = batchReceived;
            _releaseResponse = releaseResponse;
        }

        protected override TDSMessageCollection CreateQueryResponse(
            ITDSServerSession session,
            TDSSQLBatchToken batchRequest)
        {
            _batchReceived.Set();
            _releaseResponse.Wait();
            return base.CreateQueryResponse(session, batchRequest);
        }
    }

    /// <summary>
    /// Physical teardown must finish when response completion and connection close race.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResponseCompletionRacesClose_DoesNotDeadlock(bool disposeInsteadOfClose)
    {
        const int iterations = 100;

        for (int i = 0; i < iterations; i++)
        {
            // Arrange
            using CloseTraceListener trace = new();
            using ManualResetEventSlim batchReceived = new(false);
            using ManualResetEventSlim releaseResponse = new(false);

            TdsServerArguments arguments = new();
            using TdsServer server = new(
                new ReleasableQueryEngine(arguments, batchReceived, releaseResponse),
                arguments);
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
                // Disable pooling so Close()/Dispose() tears down the physical
                // connection (reaching SNIClose) instead of returning it to the pool.
                Pooling = false,
#if NETFRAMEWORK
                #pragma warning disable 618 // TransparentNetworkIPResolution is obsolete
                TransparentNetworkIPResolution = false,
                #pragma warning restore 618
#endif
            };

            SqlConnection connection = new(builder.ConnectionString);
            SqlCommand? command = null;
            Task<SqlDataReader>? readTask = null;
            bool closeAttempted = false;
            bool closedInTime = false;
            try
            {
                connection.Open();

                command = new("SELECT 1", connection);
                readTask = command.ExecuteReaderAsync();

                Assert.True(
                    batchReceived.Wait(HandshakeBudget),
                    $"iteration {i}: the server never received the SQL batch, so the " +
                    "async read was not in flight.");

                // Close on a dedicated, long-running thread, gated on a barrier;
                // release the server response at the same instant so the read's
                // completion callback fires concurrently with SNIClose.
                using Barrier gate = new(2);

                // Act
                closeAttempted = true;
                Task closeTask = Task.Factory.StartNew(
                    () =>
                    {
                        gate.SignalAndWait();
                        if (disposeInsteadOfClose)
                        {
                            connection.Dispose();
                        }
                        else
                        {
                            connection.Close();
                        }
                    },
                    TaskCreationOptions.LongRunning);

                gate.SignalAndWait();
                releaseResponse.Set();

                closedInTime = closeTask.Wait(CloseBudget);

                // Assert
                Assert.True(
                    closedInTime,
                    $"iteration {i}: {(disposeInsteadOfClose ? "Dispose()" : "Close()")} did not " +
                    $"complete within {CloseBudget.TotalSeconds:N0}s while a response completion " +
                    "raced close. This indicates the SNIClose deadlock (ADO.Net #43847 / ICM 775308542)." +
                    Environment.NewLine + trace);
            }
            finally
            {
                // Cleanup
                // Always release the (possibly still-stalled) server thread so
                // the using-scoped server can be disposed without blocking.
                releaseResponse.Set();

                if (readTask != null)
                {
                    try
                    {
                        readTask.Wait(CloseBudget);
                    }
                    catch
                    {
                        // The pending read is expected to fault or cancel once the
                        // connection is torn down. That is not what this test asserts.
                    }
                }

                command?.Dispose();

                // Dispose unless a close deadlock was actually detected (in which
                // case Dispose() could also block and defeat the regression signal).
                if (!closeAttempted || closedInTime)
                {
                    connection.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Records bounded close/read progress without enabling packet payload events.
    /// </summary>
    private sealed class CloseTraceListener : EventListener
    {
        private readonly ConcurrentQueue<string> _messages = new();

        /// <summary>
        /// Enables flow and managed-network tracing for the loopback connection.
        /// </summary>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)(2 | 4 | 128 | 2048));
            }
        }

        /// <summary>
        /// Retains recent progress and thread identities for a stalled iteration.
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
        /// Supplies the captured progress to the close watchdog assertion.
        /// </summary>
        public override string ToString() => string.Join(Environment.NewLine, _messages);
    }
}
