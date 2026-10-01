// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [Trait("Set", "2")]
    public class DataReaderCancellationTest
    {
        /// <summary>
        /// Upper bound for any single blocking step in these tests. A cancellation that works
        /// completes in well under a second, so this only ever elapses when the driver regresses.
        /// </summary>
        private static readonly System.TimeSpan WatchdogTimeout = System.TimeSpan.FromSeconds(45);

        /// <summary>
        /// Requests cancellation without ever blocking the calling thread indefinitely.
        ///
        /// SqlCommand registers a *synchronous* cancellation callback that sends the TDS attention
        /// signal, so CancellationTokenSource.Cancel() runs that work on whichever thread calls it.
        /// When attention cannot be sent (the regression these tests cover) Cancel() never returns.
        /// A test must therefore never call Cancel() on its own thread: doing so hangs the xUnit
        /// host, which aborts the entire run and reports every remaining test in the leg as an
        /// error instead of surfacing one clear failure.
        /// </summary>
        private static async Task CancelWithoutBlockingAsync(CancellationTokenSource cts, string context)
        {
            Task cancelTask = Task.Run(() => cts.Cancel());

            if (await Task.WhenAny(cancelTask, Task.Delay(WatchdogTimeout)) != cancelTask)
            {
                // Abandon the stuck call, but observe it so its fault cannot resurface later.
                ObserveWhenComplete(cancelTask);
                Assert.Fail(
                    $"CancellationTokenSource.Cancel() did not return within {WatchdogTimeout.TotalSeconds}s ({context}). " +
                    "The attention signal could not be sent, so Cancel() blocked its caller.");
            }

            await cancelTask;
        }

        /// <summary>
        /// Disposes an object on a background thread and waits only up to the watchdog. Close/Dispose
        /// drain the connection, which blocks for as long as the server stays busy when cancellation
        /// has regressed, so cleanup must never be allowed to hang the host either.
        /// </summary>
        private static async Task DisposeWithoutBlockingAsync(System.IDisposable disposable)
        {
            Task disposeTask = Task.Run(() =>
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Cleanup failures must not mask the assertion that is being reported.
                }
            });

            if (await Task.WhenAny(disposeTask, Task.Delay(WatchdogTimeout)) != disposeTask)
            {
                ObserveWhenComplete(disposeTask);
            }
        }

        /// <summary>
        /// Observes a task's exception once it eventually completes, so an abandoned task cannot
        /// surface as an unobserved task exception inside an unrelated test.
        /// </summary>
        private static void ObserveWhenComplete(Task task)
        {
            if (task.IsCompleted)
            {
                _ = task.Exception;
            }
            else
            {
                _ = task.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Test ensures cancellation token is registered before ReadAsync starts processing results from TDS Stream,
        /// such that when Cancel is triggered, the token is capable of canceling reading further results.
        /// Synapse: Incompatible query.
        /// </summary>
        /// <returns>Async Task</returns>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationTokenIsRespected_ReadAsync()
        {
            const string longRunningQuery = @"
with TenRows as (select Value from (values (1), (2), (3), (4), (5), (6), (7), (8), (9), (10)) as TenRows (Value)),
    ThousandRows as (select A.Value as A, B.Value as B, C.Value as C from TenRows as A, TenRows as B, TenRows as C)
select *
from ThousandRows as A, ThousandRows as B, ThousandRows as C;";

            using (var source = new CancellationTokenSource())
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync(source.Token);

                Stopwatch stopwatch = Stopwatch.StartNew();
                await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    using (var command = new SqlCommand(longRunningQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync(source.Token))
                    {
                        while (await reader.ReadAsync(source.Token))
                        {
                            source.Cancel();
                        }
                    }
                });
                Assert.True(stopwatch.ElapsedMilliseconds < 10000, "Cancellation did not trigger on time.");
            }
        }

        /// <summary>
        /// Test ensures cancellation token is registered before ReadAsync starts processing results from TDS Stream,
        /// such that when Cancel is triggered, the token is capable of canceling reading further results.
        /// Synapse: Incompatible query & Parallel query execution on the same connection is not supported.
        /// </summary>
        /// <returns>Async Task</returns>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancelledCancellationTokenIsRespected_ReadAsync()
        {
            const string longRunningQuery = @"
with TenRows as (select Value from (values (1), (2), (3), (4), (5), (6), (7), (8), (9), (10)) as TenRows (Value)),
    ThousandRows as (select A.Value as A, B.Value as B, C.Value as C from TenRows as A, TenRows as B, TenRows as C)
select *
from ThousandRows as A, ThousandRows as B, ThousandRows as C;";

            using (var source = new CancellationTokenSource())
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync(source.Token);

                Stopwatch stopwatch = Stopwatch.StartNew();
                await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    using (var command = new SqlCommand(longRunningQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync(source.Token))
                    {
                        source.Cancel();
                        while (await reader.ReadAsync(source.Token))
                        { }
                    }
                });
                Assert.True(stopwatch.ElapsedMilliseconds < 10000, "Cancellation did not trigger on time.");
            }
        }
        /// <summary>
        /// Validates that async cancellation sends a TDS attention signal to SQL Server
        /// when the server has sent partial results (RAISERROR WITH NOWAIT at severity 10)
        /// followed by a blocking operation (WAITFOR). Without the fix for GitHub issue #4424,
        /// cancellation would hang until WAITFOR completed naturally.
        /// Synapse: Incompatible query.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationSendsAttention_WhenPartialResultsReceived()
        {
            // Severity 10 informational message flushed via NOWAIT sends a partial TDS
            // response, then WAITFOR blocks for 60s. Cancellation should send attention
            // and abort within seconds.
            const string query = @"
RAISERROR('partial result', 10, 1) WITH NOWAIT;
WAITFOR DELAY '00:01:00';
SELECT 1 AS Result;";

            using (var cts = new CancellationTokenSource())
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 90;

                    // Subscribe BEFORE dispatching the command so the RAISERROR ... WITH NOWAIT
                    // informational token cannot arrive before we are listening.
                    var infoMessageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    connection.InfoMessage += (_, __) => infoMessageReceived.TrySetResult(true);

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    // Start ExecuteReaderAsync without awaiting so we can trigger
                    // cancellation from a separate thread once the query is in flight.
                    // The 60-second WAITFOR gives a wide window during which
                    // cancellation must send a TDS attention signal to the server.
                    Task<SqlDataReader> execTask = command.ExecuteReaderAsync(cts.Token);

                    // Cancel only after the server has flushed the RAISERROR NOWAIT packet so we know we're in the
                    // "partial results received" state that regressed in #4424.
                    Task cancelTask = Task.Run(async () =>
                    {
                        await Task.WhenAny(infoMessageReceived.Task, Task.Delay(System.TimeSpan.FromSeconds(10)));
                        await CancelWithoutBlockingAsync(cts, "partial results received");
                    });

                    // Cancellation during async read may surface as either
                    // OperationCanceledException or SqlException (attention ack).
                    System.Exception caughtException = null;
                    try
                    {
                        using (var reader = await execTask)
                        {
                            // If we reach here, cancellation failed to abort ExecuteReaderAsync while it was waiting
                            // for metadata after a partial response (e.g., RAISERROR WITH NOWAIT).
                            Assert.Fail("ExecuteReaderAsync should have been cancelled before returning a reader.");
                        }
                    }
                    catch (System.OperationCanceledException ex)
                    {
                        caughtException = ex;
                    }
                    catch (SqlException ex)
                    {
                        // Attention acknowledgment from server manifests as SqlException
                        caughtException = ex;
                    }

                    await cancelTask;
                    stopwatch.Stop();

                    Assert.NotNull(caughtException);
                    // Fail loudly if the InfoMessage never arrived: without it we silently
                    // degrade to a fixed-timer cancellation and no longer prove that
                    // cancellation happened in the "partial results received" state.
                    Assert.True(infoMessageReceived.Task.IsCompleted,
                        "InfoMessage from RAISERROR ... WITH NOWAIT was never received; " +
                        "the test did not exercise the partial-results cancellation path.");
                    // Ensure the CTS actually fired — guards against false positives
                    // from unrelated SqlExceptions.
                    Assert.True(cts.IsCancellationRequested,
                        "CancellationTokenSource was not cancelled; exception may be unrelated to cancellation.");
                    // The key assertion: cancellation should complete well before the
                    // 60-second WAITFOR. Allow up to 30 seconds for CI variability.
                    Assert.True(stopwatch.ElapsedMilliseconds < 30000,
                        $"Cancellation took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms. " +
                        "Attention signal may not have been sent to the server.");
                }
            }
        }

        /// <summary>
        /// Validates that cancellation during ExecuteReaderAsync itself sends a TDS attention
        /// signal when the server is blocked before returning any result set metadata.
        /// With WAITFOR as the first statement, ExecuteReaderAsync should never return a
        /// reader — cancellation must abort the operation during the await.
        /// Synapse: Incompatible query.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationDuringExecuteReaderAsync_SendsAttention()
        {
            // WAITFOR as the first statement means no metadata is returned until it
            // completes. ExecuteReaderAsync will be blocked in the async completion path.
            const string query = "WAITFOR DELAY '00:01:00'; SELECT 1 AS Result;";

            using (var cts = new CancellationTokenSource())
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 90;
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    // Start ExecuteReaderAsync without awaiting so we can trigger
                    // cancellation from a separate thread once the query is in flight.
                    // WAITFOR as the first statement blocks for 60s, giving a wide
                    // window during which cancellation must send TDS attention.
                    Task<SqlDataReader> execTask = command.ExecuteReaderAsync(cts.Token);

                    // Cancel from another thread after briefly yielding to ensure the
                    // async operation has been dispatched and reached the server-side
                    // WAITFOR. This avoids the flakiness of a preemptive timer that
                    // could fire before the query is actually in flight.
                    Task cancelTask = Task.Run(async () =>
                    {
                        await Task.Delay(System.TimeSpan.FromMilliseconds(500));
                        await CancelWithoutBlockingAsync(cts, "WAITFOR before any results");
                    });

                    System.Exception caughtException = null;
                    try
                    {
                        // ExecuteReaderAsync should be cancelled via attention before
                        // a reader is ever returned.
                        using (var reader = await execTask)
                        {
                            // If we reach here, cancellation failed to abort ExecuteReaderAsync.
                            Assert.Fail("ExecuteReaderAsync should have been cancelled before returning a reader.");
                        }
                    }
                    catch (System.OperationCanceledException ex)
                    {
                        caughtException = ex;
                    }
                    catch (SqlException ex)
                    {
                        caughtException = ex;
                    }

                    await cancelTask;
                    stopwatch.Stop();

                    Assert.NotNull(caughtException);
                    Assert.True(cts.IsCancellationRequested,
                        "CancellationTokenSource was not cancelled; exception may be unrelated to cancellation.");
                    Assert.True(stopwatch.ElapsedMilliseconds < 30000,
                        $"Cancellation took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms. " +
                        "Attention signal may not have been sent during ExecuteReaderAsync.");
                }
            }
        }

        /// <summary>
        /// Validates that cancelling an infinite WHILE loop via CancellationToken does not
        /// hang forever. This is the exact repro from GitHub issue #44.
        ///
        /// Every blocking step here is bounded by a watchdog. When cancellation regresses this
        /// test must report a single clean failure; if it were allowed to block it would hang the
        /// xUnit host, abort the run, and mass-fail every remaining test in the CI leg.
        /// Synapse: Incompatible query.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationOfInfiniteWhileLoop_DoesNotHang()
        {
            // Infinite loop that never completes — only cancellation via attention can stop it.
            const string query = @"
WHILE 1 = 1
BEGIN
    DECLARE @x INT = 1
END";

            using (var cts = new CancellationTokenSource())
            {
                var connection = new SqlConnection(DataTestUtility.TCPConnectionString);
                Task execTask = null;

                try
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        // Backstop: if attention never reaches the server the command still gives
                        // up instead of leaving the loop spinning on a shared CI server forever.
                        command.CommandTimeout = 60;

                        Stopwatch stopwatch = Stopwatch.StartNew();

                        // Start ExecuteNonQueryAsync without awaiting so we can trigger
                        // cancellation once the query is in flight. The infinite WHILE loop
                        // guarantees the server stays busy until attention aborts it.
                        execTask = command.ExecuteNonQueryAsync(cts.Token);

                        // Give the batch time to reach the server-side loop. This avoids the
                        // flakiness of cancelling before the query is actually in flight.
                        await Task.Delay(System.TimeSpan.FromMilliseconds(500));

                        await CancelWithoutBlockingAsync(cts, "infinite WHILE loop");

                        System.Exception caughtException = null;
                        try
                        {
                            if (await Task.WhenAny(execTask, Task.Delay(WatchdogTimeout)) != execTask)
                            {
                                // Do NOT attempt graceful cleanup here: command.Cancel() and
                                // connection.Close() both need the state object monitor that is
                                // already starved, so they would block forever. Abandon and fail.
                                ObserveWhenComplete(execTask);
                                Assert.Fail(
                                    $"ExecuteNonQueryAsync did not complete within {WatchdogTimeout.TotalSeconds}s. " +
                                    "Cancellation via attention signal failed.");
                            }

                            await execTask; // Propagate any exception
                            Assert.Fail("ExecuteNonQueryAsync should have been cancelled.");
                        }
                        catch (System.OperationCanceledException ex)
                        {
                            caughtException = ex;
                        }
                        catch (SqlException ex)
                        {
                            caughtException = ex;
                        }

                        stopwatch.Stop();

                        Assert.NotNull(caughtException);
                        Assert.True(cts.IsCancellationRequested,
                            "CancellationTokenSource was not cancelled; exception may be unrelated to cancellation.");
                        // Must complete well within 30s — without the fix this hangs forever.
                        Assert.True(stopwatch.ElapsedMilliseconds < 30000,
                            $"Cancellation took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms. " +
                            "Attention signal may not have been sent for infinite WHILE loop.");
                    }

                    // Verify the connection is still usable after cancellation.
                    using (var verifyCmd = new SqlCommand("SELECT 1", connection))
                    {
                        object result = await verifyCmd.ExecuteScalarAsync();
                        Assert.Equal(1, (int)result);
                    }
                }
                finally
                {
                    if (execTask is not null)
                    {
                        ObserveWhenComplete(execTask);
                    }

                    // Close/Dispose drains the connection, which blocks while the server is still
                    // running the loop, so it must be bounded like every other step.
                    await DisposeWithoutBlockingAsync(connection);
                }
            }
        }

        /// <summary>
        /// Regression test for the CreateLocalCompletionTask "internal end" path, which still
        /// takes lock (_stateObj) while calling endFunc. That continuation fires as soon as the
        /// first packet arrives, not once the whole result is buffered, so when a query flushes
        /// partial results (RAISERROR WITH NOWAIT) and then blocks (WAITFOR), endFunc performs a
        /// blocking read while holding the monitor. TdsParserStateObject.Cancel() needs the same
        /// monitor to send the TDS attention signal, so cancellation is dropped and the command
        /// runs to completion.
        ///
        /// In production this path is taken when column encryption is enabled and the parameter
        /// metadata came from the cache. Here it is forced with the DEBUG-only
        /// _forceInternalEndQuery hook so the regression is covered without an Always Encrypted
        /// setup. Against a Release build of the driver the hook is absent and the test reports as
        /// skipped, so the lost coverage is visible in the run summary. In that configuration the
        /// Always Encrypted variant in ApiShould is the only coverage for this path.
        /// Synapse: Incompatible query.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsForceInternalEndQuerySupported))]
        public static async Task CancellationOnInternalEndExecutePath_SendsAttention()
        {
            // Partial results must arrive before the blocking statement. That is what completes
            // localCompletion early and gets the internal-end continuation into the monitor while
            // the server is still busy.
            const string query = @"
RAISERROR('partial result', 10, 1) WITH NOWAIT;
WAITFOR DELAY '00:01:00';
SELECT 1 AS Result;";

            CommandHelper.s_forceInternalEndQuery.SetValue(null, true);
            try
            {
                using (var cts = new CancellationTokenSource())
                using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 120;

                        // Subscribe BEFORE dispatching so the RAISERROR ... WITH NOWAIT
                        // informational token cannot arrive before we are listening.
                        var infoMessageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        connection.InfoMessage += (_, __) => infoMessageReceived.TrySetResult(true);

                        Stopwatch stopwatch = Stopwatch.StartNew();
                        Task<SqlDataReader> execTask = command.ExecuteReaderAsync(cts.Token);

                        // Let the RAISERROR NOWAIT packet land so the internal-end continuation is
                        // inside endFunc, and therefore inside lock (_stateObj), before we cancel.
                        await Task.WhenAny(infoMessageReceived.Task, Task.Delay(System.TimeSpan.FromSeconds(10)));

                        long cancelledAtMs = stopwatch.ElapsedMilliseconds;
                        await CancelWithoutBlockingAsync(cts, "internal-end execute path");

                        System.Exception caughtException = null;
                        bool readerReturned = false;
                        try
                        {
                            using (var reader = await execTask)
                            {
                                readerReturned = true;
                            }
                        }
                        catch (System.OperationCanceledException ex)
                        {
                            caughtException = ex;
                        }
                        catch (SqlException ex)
                        {
                            caughtException = ex;
                        }

                        stopwatch.Stop();
                        long latency = stopwatch.ElapsedMilliseconds - cancelledAtMs;

                        Assert.False(readerReturned,
                            $"ExecuteReaderAsync returned a reader {latency}ms after cancellation was requested. " +
                            "The internal-end path held lock (_stateObj) across a blocking read, so " +
                            "TdsParserStateObject.Cancel() could not send the attention signal.");
                        Assert.NotNull(caughtException);
                        // Fail loudly if the InfoMessage never arrived: without it we silently
                        // degrade to a fixed-timer cancellation and no longer prove that
                        // cancellation happened in the "partial results received" state.
                        Assert.True(infoMessageReceived.Task.IsCompleted,
                            "InfoMessage from RAISERROR ... WITH NOWAIT was never received; " +
                            "the test did not exercise the partial-results cancellation path.");
                        Assert.True(latency < 30000,
                            $"Cancellation took {latency}ms, expected < 30000ms. " +
                            "Attention signal was not delivered on the internal-end path.");
                    }
                }
            }
            finally
            {
                CommandHelper.s_forceInternalEndQuery.SetValue(null, false);
            }
        }

        /// <summary>
        /// ExecuteXmlReaderAsync gets the same lock (_stateObj) removal in SqlCommand.Xml.cs as the
        /// reader and non-query paths, and reaches it the same way: BeginExecuteXmlReaderInternalReadStage
        /// completes the task from _stateObj.ReadSni, so a batch that flushes partial results and then
        /// blocks puts EndExecuteXmlReaderAsync into a blocking read. Without the fix the monitor is held
        /// across that read and TdsParserStateObject.Cancel() cannot send the attention signal.
        /// Synapse: Incompatible query.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationDuringExecuteXmlReaderAsync_SendsAttention()
        {
            // RAISERROR ... WITH NOWAIT flushes a packet before WAITFOR blocks, which is the
            // partial-results state that regressed in #4424. FOR XML RAW makes the batch valid
            // for ExecuteXmlReaderAsync (FOR XML AUTO would require a table in the FROM clause).
            const string query = @"
RAISERROR('partial result', 10, 1) WITH NOWAIT;
WAITFOR DELAY '00:01:00';
SELECT 1 AS Result FOR XML RAW;";

            using (var cts = new CancellationTokenSource())
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 120;

                    // Subscribe BEFORE dispatching so the RAISERROR ... WITH NOWAIT
                    // informational token cannot arrive before we are listening.
                    var infoMessageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    connection.InfoMessage += (_, __) => infoMessageReceived.TrySetResult(true);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    Task<System.Xml.XmlReader> execTask = command.ExecuteXmlReaderAsync(cts.Token);

                    await Task.WhenAny(infoMessageReceived.Task, Task.Delay(System.TimeSpan.FromSeconds(10)));

                    long cancelledAtMs = stopwatch.ElapsedMilliseconds;
                    await CancelWithoutBlockingAsync(cts, "ExecuteXmlReaderAsync with partial results");

                    System.Exception caughtException = null;
                    bool readerReturned = false;
                    try
                    {
                        using (System.Xml.XmlReader reader = await execTask)
                        {
                            readerReturned = true;
                        }
                    }
                    catch (System.OperationCanceledException ex)
                    {
                        caughtException = ex;
                    }
                    catch (SqlException ex)
                    {
                        caughtException = ex;
                    }

                    stopwatch.Stop();
                    long latency = stopwatch.ElapsedMilliseconds - cancelledAtMs;

                    Assert.False(readerReturned,
                        $"ExecuteXmlReaderAsync returned a reader {latency}ms after cancellation was requested. " +
                        "The attention signal was not delivered on the XML path.");
                    Assert.NotNull(caughtException);
                    Assert.True(infoMessageReceived.Task.IsCompleted,
                        "InfoMessage from RAISERROR ... WITH NOWAIT was never received; " +
                        "the test did not exercise the partial-results cancellation path.");
                    Assert.True(cts.IsCancellationRequested,
                        "CancellationTokenSource was not cancelled; exception may be unrelated to cancellation.");
                    Assert.True(latency < 30000,
                        $"Cancellation took {latency}ms, expected < 30000ms. " +
                        "Attention signal may not have been sent during ExecuteXmlReaderAsync.");
                }
            }
        }
    }
}
