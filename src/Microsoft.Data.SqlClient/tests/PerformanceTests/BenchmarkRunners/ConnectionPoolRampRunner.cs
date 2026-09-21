// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// Measures how quickly a cold pool can ramp up to N physical connections when N
    /// callers arrive simultaneously and all of them need a connection at the same time.
    ///
    /// This is the workload the ChannelDbConnectionPool (V2) was designed for. The legacy
    /// WaitHandleDbConnectionPool guards creation with a <c>Semaphore(1, 1)</c>, so a cold
    /// burst of N callers establishes physical connections one at a time: total latency is
    /// roughly N x connect latency. V2 has no such gate, so the opens overlap and total
    /// latency approaches a single connect.
    ///
    /// Contrast with <see cref="ConnectionPoolStressRunner.RapidFireOpenCloseAsync"/>, which also
    /// starts from a cold pool but releases each connection immediately. Because nothing is
    /// held, one physical connection can satisfy every caller in turn, so that benchmark
    /// rewards a pool that grows as slowly as possible and penalizes concurrent creation.
    /// Holding each connection until every caller has one removes that artifact: the pool
    /// genuinely needs N connections, and the only variable left is how fast it can open
    /// them.
    ///
    /// <see cref="MaxPoolSize"/> is always larger than <see cref="Parallelism"/> so no
    /// caller ever waits for a connection to be returned. Back-pressure on a saturated pool
    /// is covered separately by <see cref="ConnectionPoolContentionRunner"/>.
    ///
    /// The pool implementation (legacy vs V2) is a process-level choice - see the remarks on
    /// <see cref="ConnectionPoolStressRunner"/>. Run twice (UseConnectionPoolV2 false then
    /// true) to compare.
    ///
    /// Related issue: #3356
    /// </summary>
    public class ConnectionPoolRampRunner : BaseRunner
    {
        /// <summary>
        /// Number of callers that arrive simultaneously against a cold pool. Each one holds
        /// its connection until all of them have connected, so the pool must open exactly
        /// this many physical connections.
        /// </summary>
        [Params(10, 25, 50, 100)]
        public int Parallelism { get; set; }

        /// <summary>
        /// Max pool size. Deliberately larger than every <see cref="Parallelism"/> value so
        /// the ramp is never bounded by pool capacity.
        /// </summary>
        [Params(200)]
        public int MaxPoolSize { get; set; }

        private string _connectionString;

        /// <summary>
        /// Upper bound on the rendezvous wait. The rendezvous is released on the failure path
        /// too, so this should never be reached; it exists so that a bug in the barrier fails
        /// the run quickly instead of hanging the perf pipeline indefinitely.
        /// </summary>
        private static readonly TimeSpan s_rampTimeout = TimeSpan.FromMinutes(2);

        [GlobalSetup]
        public void Setup()
        {
            Console.WriteLine(
                "[ConnectionPoolRampRunner] Pool implementation: " +
                (s_config.UseConnectionPoolV2
                    ? "ChannelDbConnectionPool (V2)"
                    : "WaitHandleDbConnectionPool (legacy)"));

            var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = true,
                MaxPoolSize = MaxPoolSize,
                // No pre-warming: every iteration must establish its own connections.
                MinPoolSize = 0,
                ConnectTimeout = 60
            };
            _connectionString = builder.ConnectionString;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Start every iteration from a cold pool so the measurement is the ramp itself.
            SqlConnection.ClearAllPools();
        }

        [GlobalCleanup]
        public void Cleanup() => SqlConnection.ClearAllPools();

        /// <summary>
        /// Async cold-start ramp. All callers open concurrently and hold until the last one
        /// has connected.
        /// </summary>
        [Benchmark]
        public async Task ColdStartRampAsync()
        {
            using var allConnected = new CountdownEvent(Parallelism);
            var release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using var conn = new SqlConnection(_connectionString);
                    try
                    {
                        await conn.OpenAsync();
                    }
                    finally
                    {
                        // Signal from a finally so a caller that fails to open still counts as
                        // arrived. Otherwise the countdown never reaches zero, the release is
                        // never set, and every other caller awaits forever.
                        if (allConnected.Signal())
                        {
                            release.TrySetResult(true);
                        }
                    }

                    // Hold the connection until every caller has one, forcing the pool to
                    // grow to Parallelism physical connections.
                    await release.Task.WaitAsync(s_rampTimeout);
                    // Dispose returns the connection to the pool.
                });
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Sync cold-start ramp. Uses dedicated threads rather than thread pool threads so
        /// the measurement reflects pool ramp latency rather than thread pool injection
        /// delay, which would otherwise dominate once the callers block.
        /// </summary>
        [Benchmark]
        public void ColdStartRamp()
        {
            using var allConnected = new CountdownEvent(Parallelism);

            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    using var conn = new SqlConnection(_connectionString);
                    try
                    {
                        conn.Open();
                    }
                    finally
                    {
                        // See ColdStartRampAsync: signalling from a finally keeps a failed
                        // open from stranding every other caller in Wait().
                        allConnected.Signal();
                    }

                    if (!allConnected.Wait(s_rampTimeout))
                    {
                        throw new TimeoutException(
                            $"Cold-start ramp did not reach {Parallelism} connections within {s_rampTimeout}.");
                    }
                    // Dispose returns the connection to the pool.
                }, TaskCreationOptions.LongRunning);
            }

            Task.WaitAll(tasks);
        }
    }
}
