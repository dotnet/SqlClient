// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AsyncCancelledConnectionsTest
    {
        /// <summary>
        /// How many attempts to poison the connection pool we will try.
        /// </summary>
        private const int NumberOfTasks = 100;

        /// <summary>
        /// Number of normal requests for each attempt
        /// </summary>
        private const int NumberOfNonPoisoned = 10;

        private bool _continue = true;
        private Random _random;

        // Disabled on Azure since this test fails on concurrent runs on same database.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CancelAsyncConnections(bool useMars)
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.MultipleActiveResultSets = useMars;

            SqlConnection.ClearAllPools();

            _random = new Random(4);

            // Act
            Task[] tasks = new Task[NumberOfTasks];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = DoManyAsync(builder);
            }

            await Task.WhenAll(tasks);

            // Assert - If test runs to completion, it is successful
        }

        // This is the main body that our Tasks run
        private async Task DoManyAsync(SqlConnectionStringBuilder connectionStringBuilder)
        {
            string connectionString = connectionStringBuilder.ToString();

            using SqlConnection connection = new SqlConnection(connectionString);
            if (connectionStringBuilder.MultipleActiveResultSets)
            {
                await connection.OpenAsync();
            }

            // First poison
            await DoOneAsync(connection, connectionString, poison: true);

            for (int i = 0; i < NumberOfNonPoisoned && _continue; i++)
            {
                // now run some without poisoning
                await DoOneAsync(connection, connectionString, poison: false);
            }
        }

        private async Task DoOneAsync(SqlConnection marsConnection, string connectionString, bool poison)
        {
            // This will do our work, open a connection, and run a query (that returns 4 results sets)
            // if we are poisoning we will
            //   1 - Interject some sleeps in the sql statement so that it will run long enough that we can cancel it
            //   2 - Set up a time bomb task that will cancel the command a random amount of time later

            try
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < 4; i++)
                {
                    builder.AppendLine("SELECT name FROM sys.tables");
                    if (poison && i < 3)
                    {
                        builder.AppendLine("WAITFOR DELAY '00:00:01'");
                    }
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    if (marsConnection != null && marsConnection.State == System.Data.ConnectionState.Open)
                    {
                        await RunCommand(marsConnection, builder.ToString(), poison);
                    }
                    else
                    {
                        await connection.OpenAsync();
                        await RunCommand(connection, builder.ToString(), poison);
                    }
                }
            }
            catch (Exception ex) when (poison && IsExpectedCancellation(ex))
            {
                // Expected cancellation from the time bomb when poisoning.
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("The MARS TDS header contained errors."))
                {
                    _continue = false;
                }

                throw;
            }
        }

        private static bool IsExpectedCancellation(Exception ex)
        {
            switch (ex)
            {
                case OperationCanceledException:
                    return true;
                case SqlException sqlEx:
                    return sqlEx.Message.Contains("operation cancelled", StringComparison.OrdinalIgnoreCase) ||
                           sqlEx.Message.Contains("operation canceled", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private async Task RunCommand(SqlConnection connection, string commandText, bool poison)
        {
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = commandText;

            Task timeBombTask = null;
            try
            {
                // Set up us the (time) bomb
                if (poison)
                {
                    timeBombTask = TimeBombAsync(command);
                }

                // Attempt to read all the data
                using SqlDataReader reader = await command.ExecuteReaderAsync();
                try
                {
                    do
                    {
                        while (await reader.ReadAsync() && _continue)
                        {
                            // Discard results
                        }
                    }
                    while (await reader.NextResultAsync() && _continue);
                }
                catch (SqlException) when (poison)
                {
                    // This looks a little strange, we failed to read above so this should
                    // fail too. But consider the case where this code is elsewhere (in the
                    // Dispose method of a class holding this logic)
                    await reader.FlushAllResultsAsync(flushResults: false);

                    throw;
                }
            }
            finally
            {
                // Make sure to clean up our time bomb
                // It is unlikely, but the timebomb may get delayed in the task queue, and we don't
                // want it running after we dispose the command.
                if (timeBombTask != null)
                {
                    await timeBombTask;
                }
            }
        }

        private async Task TimeBombAsync(SqlCommand command)
        {
            // Sleep a random amount between 100 and 3000 ms.
            int delayMs;
            lock (_random)
            {
                delayMs = _random.Next(100, 3000);
            }
            await Task.Delay(delayMs);
            
            // Cancel the command
            command.Cancel();
        }
    }
}
