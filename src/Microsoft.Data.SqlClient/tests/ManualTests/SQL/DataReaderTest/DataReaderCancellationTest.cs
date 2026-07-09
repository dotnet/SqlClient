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
        /// when the server has sent partial results (RAISERROR WITH NOWAIT) followed by
        /// a blocking operation (WAITFOR). Without the fix for GitHub issue #4424,
        /// cancellation would hang until WAITFOR completed naturally.
        /// Synapse: Incompatible query.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationSendsAttention_WhenPartialResultsReceived()
        {
            // This query sends a partial response (RAISERROR WITH NOWAIT), then blocks
            // for 60 seconds. Cancellation should send attention and abort within seconds.
            const string query = @"
RAISERROR('partial result', 0, 1) WITH NOWAIT;
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
                            while (await reader.ReadAsync(cts.Token))
                            { }
                            // Advance to next result set (blocked by WAITFOR)
                            await reader.NextResultAsync(cts.Token);
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
                    // The key assertion: cancellation should complete well before the
                    // 60-second WAITFOR. Allow up to 30 seconds for CI variability.
                    Assert.True(stopwatch.ElapsedMilliseconds < 30000,
                        $"Cancellation took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms. " +
                        "Attention signal may not have been sent to the server.");
                }
            }
        }

        /// <summary>
        /// Validates that cancellation during ExecuteReaderAsync itself (not just ReadAsync)
        /// sends a TDS attention signal. This covers the case where the async completion path
        /// in EndExecuteReaderInternal is blocked on synchronous metadata consumption while
        /// the server is still processing (e.g., a long-running batch where initial metadata
        /// is delayed). This also covers the internal-end path used by Always Encrypted retry.
        /// Synapse: Incompatible query.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationDuringExecuteReaderAsync_SendsAttention()
        {
            // Use a query that blocks immediately so cancellation must fire during
            // ExecuteReaderAsync's async completion (before any reader is returned).
            const string query = "WAITFOR DELAY '00:01:00'; SELECT 1 AS Result;";

            using (var cts = new CancellationTokenSource(System.TimeSpan.FromSeconds(2)))
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 90;
                    Stopwatch stopwatch = Stopwatch.StartNew();

                    System.Exception caughtException = null;
                    try
                    {
                        // ExecuteReaderAsync itself should be cancelled via attention
                        using (var reader = await command.ExecuteReaderAsync(cts.Token))
                        {
                            await reader.ReadAsync(cts.Token);
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
                    Assert.True(stopwatch.ElapsedMilliseconds < 30000,
                        $"Cancellation took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms. " +
                        "Attention signal may not have been sent during ExecuteReaderAsync.");
                }
            }
        }
    }
}
