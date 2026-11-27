// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies asynchronous command execution with independent and shared connections.
    /// </summary>
    [Trait("Set", "3")]
    public static class DDAsyncTest
    {
        #region <<ExecuteCommand_WithNewConnection>>
        // TODO Synapse: Remove dependency on Northwind database by creating required table in setup.
        // CI observed AggregateException with IndexOutOfRangeException from List<T>.Add.
        /// <summary>
        /// Verifies independent connections record both asynchronous command executions.
        /// </summary>
        [Trait("category", "flaky")]
        [Trait("category", "probation")]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ExecuteCommand_WithNewConnection_ShouldPerformAsyncByDefault()
        {
            // Arrange
            var executedProcessList = new List<string>();

            // Act
            var task1 = ExecuteCommandWithNewConnectionAsync("A", "SELECT top 10 * FROM Orders", executedProcessList);
            var task2 = ExecuteCommandWithNewConnectionAsync("B", "SELECT top 10 * FROM Products", executedProcessList);
            //wait all before verifying the results
            Task.WaitAll(task1, task2);

            // Assert
            Assert.True(DoesProcessExecutedAsync(executedProcessList));
        }

        private static bool DoesProcessExecutedAsync(IReadOnlyList<string> executedProcessList)
        {
            for (var i = 1; i < executedProcessList.Count; i++)
            {
                if (executedProcessList[i] != executedProcessList[i - 1])
                {
                    return true;
                }
            }
            return false;
        }


        private static async Task ExecuteCommandWithNewConnectionAsync(string processName, string cmdText, ICollection<string> executedProcessList)
        {
            using (var conn = DataTestUtility.CreateConnection())
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(cmdText, conn);

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        RecordProcess(executedProcessList, processName);
                    }
                }
            }
        }
        #endregion

        #region <<ExecuteCommand_WithSharedConnection>>
        // Synapse: Parallel query execution on the same connection is not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ExecuteCommand_WithSharedConnection_ShouldPerformAsyncByDefault()
        {
            var executedProcessList = new List<string>();

            //for shared connection we need to add MARS capabilities
            using (var conn = DataTestUtility.CreateConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString))
            {
                conn.Open();
                var task1 = ExecuteCommandWithSharedConnectionAsync(conn, "C", "SELECT top 10 * FROM Orders", executedProcessList);
                var task2 = ExecuteCommandWithSharedConnectionAsync(conn, "D", "SELECT top 10 * FROM Products", executedProcessList);
                //wait all before verifying the results
                Task.WaitAll(task1, task2);
            }

            //verify whether it executed async
            Assert.True(DoesProcessExecutedAsync(executedProcessList));
        }

        private static async Task ExecuteCommandWithSharedConnectionAsync(SqlConnection conn, string processName, string cmdText, ICollection<string> executedProcessList)
        {
            var cmd = new SqlCommand(cmdText, conn);

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    RecordProcess(executedProcessList, processName);
                }
            }
        }
        #endregion

        /// <summary>
        /// Ensures overlapping reader continuations cannot mutate their shared execution trace
        /// concurrently, including while a collection insertion is in progress.
        /// </summary>
        [Fact]
        public static void RecordProcess_ConcurrentReaders_SerializesWrites()
        {
            // Arrange
            using var firstInsertion = new ManualResetEventSlim();
            using var releaseInsertion = new ManualResetEventSlim();
            using var secondFinished = new ManualResetEventSlim();
            var processes = new GatedProcessList(firstInsertion, releaseInsertion);
            Exception firstError = null;
            Exception secondError = null;
            var first = new Thread(() =>
            {
                try
                {
                    RecordProcess(processes, "A");
                }
                catch (Exception error)
                {
                    firstError = error;
                }
            }) { IsBackground = true };
            var second = new Thread(() =>
            {
                try
                {
                    RecordProcess(processes, "B");
                }
                catch (Exception error)
                {
                    secondError = error;
                }
                finally
                {
                    secondFinished.Set();
                }
            }) { IsBackground = true };

            try
            {
                // Act
                // Hold the first insertion open before admitting the second writer.
                first.Start();
                Assert.True(firstInsertion.Wait(TimeSpan.FromSeconds(30)));
                second.Start();
                Assert.True(SpinWait.SpinUntil(
                    () => secondFinished.IsSet ||
                        (second.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(30)));

                // Assert
                // The second writer must wait outside the collection until the first is done.
                Assert.False(secondFinished.IsSet);
                releaseInsertion.Set();
                Assert.True(first.Join(TimeSpan.FromSeconds(30)));
                Assert.True(second.Join(TimeSpan.FromSeconds(30)));
                Assert.Null(firstError);
                Assert.Null(secondError);
                Assert.Equal(new[] { "A", "B" }, processes);
            }
            finally
            {
                // Cleanup
                releaseInsertion.Set();
                if ((first.ThreadState & ThreadState.Unstarted) == 0)
                {
                    Assert.True(first.Join(TimeSpan.FromSeconds(30)));
                }

                if ((second.ThreadState & ThreadState.Unstarted) == 0)
                {
                    Assert.True(second.Join(TimeSpan.FromSeconds(30)));
                }
            }
        }

        /// <summary>Records a row's originating command in the shared execution trace.</summary>
        /// <param name="processes">The trace shared by the concurrent reader continuations.</param>
        /// <param name="processName">The command which produced the row.</param>
        private static void RecordProcess(ICollection<string> processes, string processName)
        {
            lock (processes)
            {
                processes.Add(processName);
            }
        }

        /// <summary>Holds one insertion open and rejects a concurrent collection mutation.</summary>
        private sealed class GatedProcessList : Collection<string>
        {
            private readonly ManualResetEventSlim _firstInsertion;
            private readonly ManualResetEventSlim _releaseInsertion;
            private int _insertionCount;

            internal GatedProcessList(
                ManualResetEventSlim firstInsertion, ManualResetEventSlim releaseInsertion)
            {
                _firstInsertion = firstInsertion;
                _releaseInsertion = releaseInsertion;
            }

            protected override void InsertItem(int index, string item)
            {
                if (Interlocked.Increment(ref _insertionCount) == 1)
                {
                    _firstInsertion.Set();
                    Assert.True(_releaseInsertion.Wait(TimeSpan.FromSeconds(30)));
                }
                else if (!_releaseInsertion.IsSet)
                {
                    throw new InvalidOperationException("Concurrent execution-trace insertion.");
                }

                base.InsertItem(index, item);
            }
        }
    }
}
