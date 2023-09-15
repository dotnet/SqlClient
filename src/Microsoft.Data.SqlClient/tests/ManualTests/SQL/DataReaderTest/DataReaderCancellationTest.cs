// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
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
    }
}
