// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Exercises GH#4679 with a streaming MARS session and a connection-terminating error
    /// on a second session. Requires a test SQL Server and a sysadmin login; each iteration
    /// writes a severity-20 error to the server log.
    /// </summary>
    [Trait("Set", "1")]
    [Collection(nameof(MarsReceiveTeardownCollection))]
    public sealed class MarsReceiveTeardownTest
    {
        /// <summary>
        /// Physical teardown must report normal command errors rather than faulting an
        /// unobserved MARS receive continuation, with or without connection pooling.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsUsingManagedSNI),
            nameof(DataTestUtility.IsSysAdmin))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task FatalError_WhileMarsReaderStreams_DoesNotFaultReceivePump(bool async, bool pooling)
        {
            string connectionString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                MultipleActiveResultSets = true,
                Pooling = pooling,
                ConnectRetryCount = 0,
                ApplicationName = nameof(MarsReceiveTeardownTest),
            }.ConnectionString;
            ConcurrentQueue<Exception> unobserved = new();
            EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, args) =>
            {
                foreach (Exception exception in args.Exception.Flatten().InnerExceptions)
                {
                    if (exception.StackTrace?.Contains("ManagedSni.SniMarsConnection") == true)
                    {
                        unobserved.Enqueue(exception);
                        args.SetObserved();
                    }
                }
            };

            TaskScheduler.UnobservedTaskException += handler;
            try
            {
                Random random = new(4679);
                for (int iteration = 0; iteration < 200 && unobserved.IsEmpty; iteration++)
                {
                    await TerminateStreamingConnection(connectionString, async, random.Next(1, 30));
                    if (iteration % 25 == 24)
                    {
                        CollectReceiveContinuations();
                    }
                }

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    CollectReceiveContinuations();
                    await Task.Delay(50);
                }

                Assert.Empty(unobserved);
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handler;
                using SqlConnection poolKey = new(connectionString);
                SqlConnection.ClearPool(poolKey);
            }
        }

        /// <summary>
        /// Starts two MARS sessions, terminates only their connection, and observes all
        /// application tasks so any unobserved exception belongs to the driver's receive pump.
        /// </summary>
        /// <param name="connectionString">Connection string for the test server.</param>
        /// <param name="async">Whether to use asynchronous command and reader APIs.</param>
        /// <param name="delay">Delay before terminating the streaming connection, in milliseconds.</param>
        /// <returns>A task that completes after both sessions and the connection are cleaned up.</returns>
        private static async Task TerminateStreamingConnection(string connectionString, bool async, int delay)
        {
            SqlConnection connection = new(connectionString);
            SqlDataReader? reader = null;
            Task<Exception?>? consumer = null;
            using SqlCommand stream = new(
                "SELECT TOP (200000) a.object_id, b.name, REPLICATE('x', 200) AS pad " +
                "FROM sys.all_objects a CROSS JOIN sys.all_columns b", connection);
            stream.CommandTimeout = 60;
            try
            {
                if (async)
                {
                    await connection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }

                reader = async ? await stream.ExecuteReaderAsync() : stream.ExecuteReader();
                Assert.True(async ? await reader.ReadAsync() : reader.Read());
                consumer = Task.Run(() => Record.ExceptionAsync(async () =>
                {
                    while (async ? await reader.ReadAsync() : reader.Read())
                    {
                    }
                }));
                await Task.Delay(delay);

                using SqlCommand terminate = new(
                    "RAISERROR('SqlClient MARS receive teardown regression', 20, 1) WITH LOG", connection);
                SqlException error;
                if (async)
                {
                    error = await Assert.ThrowsAsync<SqlException>(() => terminate.ExecuteNonQueryAsync());
                }
                else
                {
                    error = Assert.Throws<SqlException>(() => terminate.ExecuteNonQuery());
                }
                Assert.True(error.Class >= 20, "The second session must terminate the physical connection.");
            }
            finally
            {
                Exception? closeError;
                Exception? readerError;
                try
                {
                    Exception? consumeError = consumer is null
                        ? null
                        : await consumer.WaitAsync(TimeSpan.FromSeconds(65));
                    AssertExpectedTeardownError(consumeError);
                }
                finally
                {
                    closeError = Record.Exception(connection.Dispose);
                    readerError = reader is null ? null : Record.Exception(reader.Dispose);
                }
                AssertExpectedTeardownError(closeError);
                AssertExpectedTeardownError(readerError);
            }
        }

        /// <summary>
        /// Allows expected failures of commands on a broken connection, but rejects programming errors.
        /// </summary>
        /// <param name="exception">The observed application-side teardown exception, if any.</param>
        private static void AssertExpectedTeardownError(Exception? exception)
        {
            Assert.True(exception is null or SqlException or InvalidOperationException or IOException,
                $"Unexpected application-side teardown error: {exception}");
        }

        /// <summary>
        /// Finalizes abandoned continuation tasks so their failures reach the event handler.
        /// </summary>
        private static void CollectReceiveContinuations()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    /// <summary>
    /// Isolates the process-wide unobserved-exception handler and forced garbage collections.
    /// </summary>
    [CollectionDefinition(nameof(MarsReceiveTeardownCollection), DisableParallelization = true)]
    public sealed class MarsReceiveTeardownCollection
    {
    }
}

#endif
