// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Live-server regression test for ADO.Net work item 43847 / ICM 775308542:
    /// "SNIClose can deadlock with in-flight async I/O during connection close".
    ///
    /// <para>
    /// This is the MARS-enabled counterpart to the in-process
    /// <c>SNICloseDeadlockTest</c> unit tests. The native <c>Dispose()</c> path
    /// in <c>TdsParserStateObjectNative</c> carries a long-standing "UNDONE"
    /// note about needing to block for pending callbacks on <b>logical</b>
    /// (MARS) connections during close. The in-process TDS test server does not
    /// support MARS, so exercising the SMUX logical-session close path requires
    /// a real SQL Server.
    /// </para>
    ///
    /// <para>
    /// The test enables MARS and starts an asynchronous read of a batch that the
    /// server deliberately withholds (<c>WAITFOR DELAY</c>). This guarantees the
    /// client's async read is genuinely in flight on a MARS logical session at
    /// the moment the connection is closed. The close is performed on a worker
    /// thread under a bounded wait: a deadlock manifests as the wait timing out,
    /// which fails the test. A healthy close aborts the in-flight command and
    /// returns promptly.
    /// </para>
    /// </summary>
    [Trait("Set", "1")]
    public class MarsCloseDeadlockTest
    {
        /// <summary>
        /// How long the server withholds the response. Must comfortably exceed
        /// <see cref="CloseBudget"/> so that a prompt close is attributable to
        /// the close path aborting the command, not to the query completing on
        /// its own.
        /// </summary>
        private const string StallDelay = "00:00:30";

        /// <summary>
        /// Upper bound for how long a non-deadlocked close should take. Close of
        /// a connection with an in-flight command is effectively instantaneous;
        /// this budget only distinguishes "completed" from "deadlocked" without
        /// hanging the run on a slow agent.
        /// </summary>
        private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(15);

        /// <summary>
        /// How long to wait to confirm the async read is genuinely pending (not
        /// completed) before closing. Small relative to <see cref="StallDelay"/>.
        /// </summary>
        private static readonly TimeSpan PendingConfirmDelay = TimeSpan.FromSeconds(3);

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false)]
        [InlineData(true)]
        public void CloseOrDispose_WithPendingMarsAsyncRead_DoesNotDeadlock(bool disposeInsteadOfClose)
        {
            // Enable MARS so the async read runs over a SMUX logical session, and
            // disable pooling so Close()/Dispose() tears down the physical
            // connection (reaching SNIClose) instead of returning it to the pool.
            string connectionString =
                new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
                {
                    MultipleActiveResultSets = true,
                    Pooling = false,
                }.ConnectionString;

            SqlConnection connection = new(connectionString);
            connection.Open();

            SqlCommand command =
                new($"WAITFOR DELAY '{StallDelay}'; SELECT 1;", connection);

            // Start an async read that will pend: the server withholds the
            // response for StallDelay, so this Task will not complete until we
            // either wait out the delay or tear down the connection.
            Task<SqlDataReader> readTask = command.ExecuteReaderAsync();

            // Confirm the read is genuinely in flight before we close: it must
            // not have completed and the connection must still be open.
            Assert.False(
                readTask.Wait(PendingConfirmDelay),
                "The async read completed before the server released the " +
                "response; the read was not in flight at close time.");
            Assert.Equal(ConnectionState.Open, connection.State);

            // Close/dispose the connection on a worker thread while the MARS
            // async read is in flight. This is the operation that can deadlock
            // in SNIClose.
            Task closeTask = Task.Run(() =>
            {
                if (disposeInsteadOfClose)
                {
                    connection.Dispose();
                }
                else
                {
                    connection.Close();
                }
            });

            bool closedInTime = closeTask.Wait(CloseBudget);

            // Observe the read task so its (expected) failure is not unobserved.
            try
            {
                readTask.Wait(CloseBudget);
            }
            catch
            {
                // The pending read is expected to fault or cancel once the
                // connection is torn down. That is not what this test asserts.
            }
            finally
            {
                command.Dispose();
                // Only perform potentially-blocking cleanup if the close
                // completed. If it deadlocked (closedInTime == false), calling
                // Dispose() here could also block indefinitely and defeat the
                // bounded-wait regression signal asserted below.
                if (closedInTime)
                {
                    connection.Dispose();
                }
            }

            Assert.True(
                closedInTime,
                $"{(disposeInsteadOfClose ? "Dispose()" : "Close()")} did not " +
                $"complete within {CloseBudget.TotalSeconds:N0}s while a MARS " +
                "async read was in flight. This indicates the SNIClose deadlock " +
                "(ADO.Net #43847 / ICM 775308542).");
        }
    }
}
