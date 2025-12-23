// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class DataReaderCancellationTest
    {
        // @TODO: Make this a commonly used long running query?
        // This query generates a billion results by performing a cross join on 10 rows 3 times (1k
        // records), then cross joining that set 3 times (1b records).
        private const string LongRunningQuery =
            @"WITH " +
            @"  TenRows AS ( " +
            @"    SELECT value " +
            @"    FROM ( " +
            @"      VALUES (1), (2), (3), (4), (5), (6), (7), (8), (9), (10) " +
            @"    ) AS TenRows (value) " +
            @"  ), " +
            @"  ThousandRows AS ( " +
            @"    SELECT A.value AS A, B.value AS B, C.value AS C " +
            @"    FROM " +
            @"      TenRows AS A, " +
            @"      TenRows AS B, " +
            @"      TenRows AS C, " +
            @"  ) " +
            @"SELECT * " +
            @"FROM " +
            @"  ThousandRows AS A, " +
            @"  ThousandRows AS B, " +
            @"  ThousandRows AS C";

        /// <summary>
        /// Test ensures cancellation token is registered before ReadAsync starts processing results from TDS Stream,
        /// such that when Cancel is triggered, the token is capable of canceling reading further results.
        /// Synapse: Incompatible query. 
        /// </summary>
        /// <returns>Async Task</returns>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static async Task CancellationTokenIsRespected_ReadAsync()
        {
            // Arrange
            using CancellationTokenSource source = new();

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync(source.Token);

            // - Set up command that returns millions of rows
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = LongRunningQuery;

            // Act
            Func<Task> action = async () =>
            {
                // - Execute query
                using SqlDataReader reader = await command.ExecuteReaderAsync(source.Token);

                // - Cancel after each record is read (should cancel after first record)
                while (await reader.ReadAsync(source.Token))
                {
                    source.Cancel();
                }
            };

            // Assert
            // - Action should throw task cancelled exception
            Stopwatch stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsAsync<TaskCanceledException>(action);
            stopwatch.Stop();

            // - Ensure exception was thrown within 10 seconds of execution
            Assert.True(stopwatch.ElapsedMilliseconds < 10000, "Cancellation did not trigger on time");
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
            // Arrange
            using CancellationTokenSource source = new();

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync(source.Token);

            // - Set up command that returns millions of rows
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = LongRunningQuery;

            // Act
            Func<Task> action = async () =>
            {
                // - Execute query
                using SqlDataReader reader = await command.ExecuteReaderAsync(source.Token);

                // - Cancel before reading
                source.Cancel();

                // - Read all results (should cancel before first record is read)
                await reader.FlushResultSetAsync(source.Token);
            };

            // Assert
            // - Action should throw task cancelled exception
            Stopwatch stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsAsync<TaskCanceledException>(action);
            stopwatch.Stop();

            // - Ensure exception was thrown within 10 seconds of execution
            Assert.True(stopwatch.ElapsedMilliseconds < 10000, "Cancellation did not trigger on time");
        }
    }
}
