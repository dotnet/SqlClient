// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Stress test that verifies cancelling a command mid-stream does not
    /// corrupt the underlying connection, the connection pool, or (when MARS
    /// is enabled) the MARS session state machine.
    ///
    /// <para>
    /// For each of <see cref="NumberOfTasks"/> parallel tasks, the test:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     Runs a single "poisoned" command: a 4-batch query with
    ///     <c>WAITFOR DELAY '00:00:01'</c> between batches, paired with a
    ///     <see cref="TimeBombAsync"/> background task that fires
    ///     <see cref="SqlCommand.Cancel"/> after a random 100-3000 ms delay.
    ///     The streaming <see cref="SqlDataReader"/> is expected to throw
    ///     either <see cref="OperationCanceledException"/> or a
    ///     <see cref="SqlException"/> whose message contains
    ///     "operation cancelled/canceled"; that exception is swallowed.
    ///   </description></item>
    ///   <item><description>
    ///     Runs up to <see cref="NumberOfNonPoisoned"/> follow-up commands on
    ///     the same physical resources (the same MARS connection when
    ///     <c>useMars</c> is true, otherwise fresh pooled connections). These
    ///     must all complete cleanly; any failure here indicates the prior
    ///     cancellation corrupted shared state.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// The test asserts implicitly: any exception that is not the expected
    /// cancellation is rethrown and fails the test. The known regression
    /// signature for a desynchronized MARS framing buffer
    /// ("The MARS TDS header contained errors.") is treated as a hard stop
    /// via <c>_continue</c>.
    /// </para>
    ///
    /// <para>
    /// <b>What this test is not.</b> It is not a coverage test for
    /// <c>OpenAsync</c>, <c>Connect Timeout</c>, or pool queue-wait behavior.
    /// <c>OpenAsync</c> is treated as pre-work that must succeed so the
    /// cancellation/poisoning scenario can run. Because
    /// <see cref="NumberOfTasks"/> simultaneous opens race against an empty
    /// pool whose connection-creation path is serialized internally
    /// (<c>WaitHandleDbConnectionPool.WaitForPendingOpen</c>), the caller's
    /// per-open <c>Connect Timeout</c> budget must be generous enough to
    /// cover queue-wait + physical connect for the last open in the burst.
    /// The test therefore overrides <c>Connect Timeout</c> on the builder
    /// (see <see cref="ConnectTimeoutSeconds"/>) rather than relying on the
    /// default 15 s; bump that constant if slow CI agents are seeing
    /// pool-timeout failures on otherwise healthy runs.
    /// </para>
    /// </summary>
    [Trait("Set", "1")]
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

        /// <summary>
        /// Per-open <c>Connect Timeout</c> applied to every connection in
        /// this test. Sized to comfortably cover the serialized
        /// connection-creation queue depth produced by
        /// <see cref="NumberOfTasks"/> simultaneous opens on slow CI agents.
        /// Note: with strict timeout propagation through the pool, the
        /// caller's budget covers both pool queue wait and physical connect,
        /// so the default 15 s is too tight for this burst pattern.
        /// </summary>
        private const int ConnectTimeoutSeconds = 60;

        private bool _continue = true;
        private Random _random;

        /// <summary>
        /// Drives <see cref="NumberOfTasks"/> parallel <see cref="DoManyAsync"/>
        /// runs against the configured TCP test server and waits for all of
        /// them to complete. The theory matrix toggles MARS so that both the
        /// shared-connection (MARS) and per-call-connection (non-MARS) paths
        /// are exercised. The test passes if every task either succeeds or
        /// fails only with the expected cancellation exception.
        /// </summary>
        // Disabled on Azure since this test fails on concurrent runs on same database.
        // Disabled on Kerberos and Managed Instance pipelines due to environment-specific instability.
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup),
            nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotManagedInstance),
            nameof(DataTestUtility.IsNotKerberosTest))]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CancelAsyncConnections(bool useMars)
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.MultipleActiveResultSets = useMars;
            // The pool serializes physical connection creation, and with
            // strict Connect Timeout propagation through the pool, the
            // caller's budget must cover queue-wait time for the last open
            // in a NumberOfTasks-wide burst. Bump the default 15 s budget so
            // a slow CI agent doesn't time out legitimately-queued opens.
            builder.ConnectTimeout = ConnectTimeoutSeconds;

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

        /// <summary>
        /// Body run by each parallel task. When <see cref="SqlConnectionStringBuilder.MultipleActiveResultSets"/>
        /// is enabled, opens a single long-lived MARS connection that is
        /// reused by every <see cref="DoOneAsync"/> call in this task so that
        /// cancellation effects on the shared MARS session are observable.
        /// Then runs exactly one poisoned attempt followed by up to
        /// <see cref="NumberOfNonPoisoned"/> non-poisoned attempts (gated by
        /// <see cref="_continue"/>, which is cleared on a MARS-header
        /// corruption signature).
        /// </summary>
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

        /// <summary>
        /// Executes one 4-batch query and reads every result set. When
        /// <paramref name="poison"/> is true, the batches are interleaved
        /// with <c>WAITFOR DELAY '00:00:01'</c> so the command runs long
        /// enough for <see cref="TimeBombAsync"/> to cancel it mid-stream;
        /// the resulting cancellation exception is expected and swallowed.
        /// When <paramref name="poison"/> is false the command must complete
        /// cleanly - this is the assertion that prior cancellation did not
        /// corrupt shared state (the MARS session or the pooled connection).
        /// </summary>
        /// <param name="marsConnection">Shared MARS connection to reuse when
        /// open; otherwise a fresh per-call <see cref="SqlConnection"/> is
        /// opened from <paramref name="connectionString"/>.</param>
        /// <param name="connectionString">Connection string used for the
        /// non-MARS path.</param>
        /// <param name="poison">If true, schedules a time-bomb
        /// <see cref="SqlCommand.Cancel"/> and expects the cancellation
        /// exception; if false, the command must succeed.</param>
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

        /// <summary>
        /// Recognizes the two exception shapes that a mid-stream
        /// <see cref="SqlCommand.Cancel"/> can surface as: a managed
        /// <see cref="OperationCanceledException"/>, or a
        /// <see cref="SqlException"/> whose message reports the server-side
        /// "operation cancelled/canceled" error. Any other exception is, by
        /// design, treated as a real failure of the test.
        /// </summary>
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

        /// <summary>
        /// Issues <paramref name="commandText"/> on
        /// <paramref name="connection"/> via
        /// <see cref="SqlCommand.ExecuteReaderAsync()"/> and drains every
        /// row of every result set. When <paramref name="poison"/> is true a
        /// <see cref="TimeBombAsync"/> is started in parallel to cancel the
        /// command mid-read, and the inner <c>catch (SqlException)</c>
        /// deliberately tries to drain remaining result sets after the
        /// initial failure - this simulates the realistic dispose-on-error
        /// pattern where a caller may attempt cleanup reads on a reader that
        /// already faulted, and ensures it doesn't itself wedge the
        /// connection.
        /// </summary>
        private async Task RunCommand(SqlConnection connection, string commandText, bool poison)
        {
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = commandText;

            Task timeBombTask = null;
            try
            {
                // Set us up the (time) bomb
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
                    while (await reader.NextResultAsync())
                    {
                        // Discard all results
                    }

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

        /// <summary>
        /// Waits a random 100-3000 ms and then calls
        /// <see cref="SqlCommand.Cancel"/> on the supplied command. The
        /// randomized delay is intentional: it spreads cancellations across
        /// different points in the reader's lifecycle (pre-execute,
        /// mid-first-result, between result sets, etc.) to exercise more of
        /// the cancellation state machine across the
        /// <see cref="NumberOfTasks"/> parallel runs.
        /// </summary>
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
