// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [Trait("Set", "2")]
    public class DataReaderCancellationTest
    {
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

                    // Schedule cancellation BEFORE ExecuteReaderAsync so it fires while
                    // the async completion path may still be consuming metadata or while
                    // ReadAsync/NextResultAsync is blocked waiting for the server.
                    cts.CancelAfter(System.TimeSpan.FromSeconds(2));

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    // Cancellation during async read may surface as either
                    // OperationCanceledException or SqlException (attention ack).
                    System.Exception caughtException = null;
                    try
                    {
                        using (var reader = await command.ExecuteReaderAsync(cts.Token))
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

                    stopwatch.Stop();

                    Assert.NotNull(caughtException);
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

                // Start cancellation timer AFTER connection is open to avoid
                // false positives if OpenAsync is slow.
                cts.CancelAfter(System.TimeSpan.FromSeconds(2));

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 90;
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    System.Exception caughtException = null;
                    try
                    {
                        // ExecuteReaderAsync should be cancelled via attention before
                        // a reader is ever returned.
                        using (var reader = await command.ExecuteReaderAsync(cts.Token))
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
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();
                cts.CancelAfter(System.TimeSpan.FromSeconds(2));

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 0; // No timeout — rely solely on cancellation

                    Stopwatch stopwatch = Stopwatch.StartNew();

                    System.Exception caughtException = null;
                    try
                    {
                        await command.ExecuteNonQueryAsync(cts.Token);
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
        }
    }
}
