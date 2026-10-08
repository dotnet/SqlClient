// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Concurrency-stress companion to <see cref="MarsCloseDeadlockTest"/> for
    /// ADO.Net #43847 / ICM 775308542.
    ///
    /// <para>
    /// Where <c>MarsCloseDeadlockTest</c> closes a single connection with one
    /// in-flight MARS async read, this test opens <b>many</b> MARS connections -
    /// each with an in-flight read - and closes them all <b>simultaneously</b>
    /// (rendezvous on a <see cref="Barrier"/>), repeated over several iterations.
    /// Piling concurrent closes onto the SMUX logical-session teardown path
    /// widens the window for the close race called out by the long-standing
    /// "Comment CloseMARSSession / UNDONE" note in the native <c>Dispose()</c>.
    /// </para>
    ///
    /// <para>
    /// Each batch of closes is awaited under a bounded wait: a deadlock manifests
    /// as the wait timing out, which fails the test. A healthy close aborts the
    /// in-flight commands and returns promptly.
    /// </para>
    /// </summary>
    [Trait("Set", "1")]
    public class MarsCloseStressTest
    {
        /// <summary>
        /// How long each server-side batch withholds its response. Must comfortably
        /// exceed <see cref="CloseBudget"/> so a prompt close is attributable to the
        /// close path aborting the command, not the query completing on its own.
        /// Kept at 4x <see cref="CloseBudget"/> so the boundary is unambiguous.
        /// </summary>
        private const string StallDelay = "00:02:00";

        /// <summary>
        /// Upper bound for how long a batch of concurrent, non-deadlocked closes
        /// should take. Generous only to avoid false positives on slow agents.
        /// </summary>
        private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(30);

        /// <summary>Number of close-storm iterations.</summary>
        private const int Iterations = 8;

        /// <summary>MARS connections opened and closed together per iteration.</summary>
        private const int ConnectionsPerIteration = 16;

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false)]
        [InlineData(true)]
        public void MarsConcurrentCloseStress_DoesNotDeadlock(bool disposeInsteadOfClose)
        {
            // Enable MARS so each async read runs over a SMUX logical session, and
            // disable pooling so Close()/Dispose() tears down the physical
            // connection (reaching SNIClose) instead of returning it to the pool.
            string connectionString =
                new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
                {
                    MultipleActiveResultSets = true,
                    Pooling = false,
                }.ConnectionString;

            for (int iter = 0; iter < Iterations; iter++)
            {
                SqlConnection[] connections = new SqlConnection[ConnectionsPerIteration];
                SqlCommand?[] commands = new SqlCommand?[ConnectionsPerIteration];
                Task<SqlDataReader>?[] readTasks = new Task<SqlDataReader>?[ConnectionsPerIteration];
                bool allClosed = false;
                try
                {
                    for (int k = 0; k < ConnectionsPerIteration; k++)
                    {
                        connections[k] = new SqlConnection(connectionString);
                        connections[k].Open();
                        commands[k] = new SqlCommand($"WAITFOR DELAY '{StallDelay}'; SELECT 1;", connections[k]);
                        readTasks[k] = commands[k]!.ExecuteReaderAsync();
                    }

                    // Confirm every read is genuinely in flight before closing.
                    for (int k = 0; k < ConnectionsPerIteration; k++)
                    {
                        Assert.False(
                            readTasks[k]!.Wait(TimeSpan.FromMilliseconds(50)),
                            $"iteration {iter}, connection {k}: the async read completed before " +
                            "close; it was not in flight.");
                    }

                    // Fire all closes at once through a barrier so they pile up on
                    // the close path concurrently.
                    using Barrier gate = new(ConnectionsPerIteration);
                    Task[] closeTasks = new Task[ConnectionsPerIteration];
                    for (int k = 0; k < ConnectionsPerIteration; k++)
                    {
                        int idx = k;
                        closeTasks[k] = Task.Factory.StartNew(
                            () =>
                            {
                                gate.SignalAndWait();
                                if (disposeInsteadOfClose)
                                {
                                    connections[idx].Dispose();
                                }
                                else
                                {
                                    connections[idx].Close();
                                }
                            },
                            TaskCreationOptions.LongRunning);
                    }

                    allClosed = Task.WaitAll(closeTasks, CloseBudget);

                    Assert.True(
                        allClosed,
                        $"iteration {iter}: {ConnectionsPerIteration} concurrent MARS " +
                        $"{(disposeInsteadOfClose ? "Dispose()" : "Close()")} calls did not all " +
                        $"complete within {CloseBudget.TotalSeconds:N0}s. This indicates the " +
                        "SNIClose deadlock (ADO.Net #43847 / ICM 775308542).");
                }
                finally
                {
                    for (int k = 0; k < ConnectionsPerIteration; k++)
                    {
                        if (readTasks[k] != null)
                        {
                            try
                            {
                                readTasks[k]!.Wait(TimeSpan.FromSeconds(5));
                            }
                            catch
                            {
                                // Reads are expected to fault or cancel on teardown.
                            }
                        }

                        commands[k]?.Dispose();

                        // Only dispose connections if the closes completed; a
                        // deadlocked connection's Dispose() could also block.
                        if (allClosed)
                        {
                            try
                            {
                                connections[k]?.Dispose();
                            }
                            catch
                            {
                                // Ignore teardown faults.
                            }
                        }
                    }
                }
            }
        }
    }
}
