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
    /// on a second session. Requires a test SQL Server and a sysadmin login; each case
    /// writes a severity-20 error to the server log.
    /// </summary>
    [Trait("Set", "1")]
    public sealed class MarsReceiveTeardownTest
    {
        /// <summary>
        /// Physical teardown must report normal command errors rather than faulting an
        /// unobserved MARS receive continuation, with or without connection pooling.
        /// Each delay is a separate case rather than a long-running stress loop.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsUsingManagedSNI),
            nameof(DataTestUtility.IsSysAdmin))]
        [InlineData(false, false, 1)]
        [InlineData(false, true, 1)]
        [InlineData(true, false, 1)]
        [InlineData(true, true, 1)]
        [InlineData(false, false, 29)]
        [InlineData(false, true, 29)]
        [InlineData(true, false, 29)]
        [InlineData(true, true, 29)]
        public async Task FatalError_WhileMarsReaderStreams_DoesNotFaultReceivePump(bool async, bool pooling, int delay)
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
                await TerminateStreamingConnection(connectionString, async, delay);

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
            Exception? terminateError = null;
            Exception? consumeError = null;
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
                // The streaming session can observe the server closing the connection before this
                // command parses its own fatal error. That session then reports the fatal error
                // and closes the connection, so this command can instead fail because the
                // connection is closed or complete without reading its error token.
                if (async)
                {
                    terminateError = await Record.ExceptionAsync(
                        () => terminate.ExecuteNonQueryAsync());
                }
                else
                {
                    terminateError = Record.Exception(() => terminate.ExecuteNonQuery());
                }
                AssertExpectedTeardownError(terminateError);
            }
            finally
            {
                Exception? closeError;
                Exception? readerError;
                try
                {
                    consumeError = consumer is null
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

            // Whichever session wins the race, one of them must report the fatal error.
            Assert.True(
                IsFatalError(terminateError) || IsFatalError(consumeError),
                "A session must report the fatal error that terminated the physical connection. " +
                $"Terminate error: {terminateError}; streaming error: {consumeError}");
        }

        /// <summary>
        /// Determines whether an exception reports an error that terminates the connection.
        /// </summary>
        /// <param name="exception">The observed application-side exception, if any.</param>
        /// <returns><see langword="true"/> if the exception has severity 20 or higher.</returns>
        private static bool IsFatalError(Exception? exception) =>
            exception is SqlException { Class: >= 20 };

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
}

#endif
