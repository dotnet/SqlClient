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
    /// Measures a saturated pool driven by sync callers on threadpool threads, with the
    /// threadpool's minimum worker count pinned so the result is reproducible.
    ///
    /// A sync <c>Open()</c> against a saturated pool blocks its thread. When those threads
    /// are threadpool threads, a pool whose waiter wake-up requires a queued continuation
    /// cannot make progress: every thread is blocked in a wait, so the wake-up sits in the
    /// queue until the threadpool injects another thread. Injection is rate limited to
    /// roughly one or two threads per second, so each stall costs about a second.
    ///
    /// <see cref="ConnectionPoolContentionRunner"/> covers the same shape at the default
    /// threadpool floor, which makes it dependent on hill-climbing timing and therefore
    /// noisy. Pinning the floor turns that into a controlled comparison:
    ///
    /// - <see cref="MinWorkerThreads"/> below <see cref="Parallelism"/> guarantees the
    ///   threadpool starts starved, so the wake path is exercised on every run.
    /// - <see cref="MinWorkerThreads"/> above <see cref="Parallelism"/> pre-creates enough
    ///   threads that injection never gates progress. This is the control: a pool that only
    ///   regresses in the starved configuration has a wake-path problem, not a throughput
    ///   problem.
    ///
    /// The effect is tail latency, not a shifted median, so compare distributions rather
    /// than means alone.
    ///
    /// A regression here is a statement about application configuration, not a pool defect.
    /// An application that blocks more thread pool threads than the thread pool has workers
    /// is already misconfigured, and pre-warming the thread pool is the application's
    /// responsibility rather than the driver's. This runner exists to characterise where
    /// that boundary is and to catch it moving, not to drive the delta to zero.
    ///
    /// The pool implementation (legacy vs V2) is a process-level choice - see the remarks on
    /// <see cref="ConnectionPoolStressRunner"/>. Run twice (UseConnectionPoolV2 false then
    /// true) to compare.
    ///
    /// Related issue: #3356
    /// </summary>
    public class ConnectionPoolThreadPoolPressureRunner : BaseRunner
    {
        /// <summary>
        /// Number of concurrent sync workers, all running on threadpool threads.
        /// </summary>
        [Params(50)]
        public int Parallelism { get; set; }

        /// <summary>
        /// Max pool size. Deliberately smaller than <see cref="Parallelism"/> so most
        /// workers must block waiting for a connection to be returned. Without that
        /// back-pressure nobody waits and the wake path is never exercised.
        /// </summary>
        [Params(10)]
        public int MaxPoolSize { get; set; }

        /// <summary>
        /// Threadpool minimum worker thread count, pinned for the duration of the run. The
        /// low value is below <see cref="Parallelism"/> (starved); the high value is above
        /// it (control).
        /// </summary>
        [Params(8, 128)]
        public int MinWorkerThreads { get; set; }

        /// <summary>
        /// Number of open/query/close operations each worker performs per invocation.
        /// </summary>
        [Params(20)]
        public int OpsPerWorker { get; set; }

        private string _connectionString;
        private int _originalMinWorkerThreads;
        private int _originalMinCompletionPortThreads;

        [GlobalSetup]
        public void Setup()
        {
            Console.WriteLine(
                "[ConnectionPoolThreadPoolPressureRunner] Pool implementation: " +
                (s_config.UseConnectionPoolV2
                    ? "ChannelDbConnectionPool (V2)"
                    : "WaitHandleDbConnectionPool (legacy)"));

            ThreadPool.GetMinThreads(
                out _originalMinWorkerThreads, out _originalMinCompletionPortThreads);
            ThreadPool.SetMinThreads(MinWorkerThreads, _originalMinCompletionPortThreads);

            var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
            {
                Pooling = true,
                MaxPoolSize = MaxPoolSize,
                MinPoolSize = 0
            };
            _connectionString = builder.ConnectionString;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            ThreadPool.SetMinThreads(
                _originalMinWorkerThreads, _originalMinCompletionPortThreads);
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // Warm the pool to MaxPoolSize so the measured run reflects steady-state
            // checkout/return rather than first-time physical connection establishment.
            WarmPool(MaxPoolSize);
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            using var conn = new SqlConnection(_connectionString);
            SqlConnection.ClearPool(conn);
        }

        [Benchmark]
        public Task SaturatedSyncOpenOnThreadPool()
        {
            var tasks = new Task[Parallelism];
            for (int i = 0; i < Parallelism; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int op = 0; op < OpsPerWorker; op++)
                    {
                        using var conn = new SqlConnection(_connectionString);
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT 1";
                        _ = cmd.ExecuteScalar();
                        // Dispose returns the connection to the pool.
                    }
                });
            }

            return Task.WhenAll(tasks);
        }

        private void WarmPool(int count)
        {
            var conns = new SqlConnection[count];
            try
            {
                for (int i = 0; i < count; i++)
                {
                    conns[i] = new SqlConnection(_connectionString);
                    conns[i].Open();
                }
            }
            finally
            {
                // Close and dispose every connection so they return to the pool and
                // are not retained until GC (which would add allocation/GC noise).
                for (int i = 0; i < count; i++)
                {
                    conns[i]?.Close();
                    conns[i]?.Dispose();
                }
            }
        }
    }
}
