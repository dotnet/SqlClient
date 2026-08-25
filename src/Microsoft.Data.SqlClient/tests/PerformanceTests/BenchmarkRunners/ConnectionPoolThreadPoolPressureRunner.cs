// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// queue until the threadpool injects another thread.
    ///
    /// Injection is not a single mechanism. Since .NET 6 the runtime recognises cooperative
    /// blocking and compensates on a fast path — up to one thread per processor immediately
    /// with no delay, then in 25ms steps capped at 250ms. Only once that budget is spent does
    /// the caller fall back to the gate thread's starvation detection, which adds one thread
    /// per 500ms cycle. Stalls here are therefore tens to a few hundred milliseconds, not the
    /// whole seconds the pre-.NET 6 gate-thread-only path would have cost.
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
    /// A regression here is largely a statement about application configuration rather than a
    /// pool defect, but the starved configuration is not exotic. The threadpool's default
    /// minimum is <see cref="Environment.ProcessorCount"/>, and that honours cgroup CPU quotas,
    /// so a service in a 1-2 vCPU container runs with a floor of 1 or 2 by default and ASP.NET
    /// Core never raises it. Blocking more threads than the floor is the common case for sync
    /// callers, not a misconfiguration they opted into. This runner exists to characterise
    /// where that boundary is and to catch it moving, not to drive the delta to zero.
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
        /// Threadpool minimum worker thread count, pinned for the duration of the run.
        /// </summary>
        /// <remarks>
        /// Sourced from <see cref="MinWorkerThreadsValues"/> rather than fixed constants,
        /// because this number is only meaningful relative to
        /// <see cref="Environment.ProcessorCount"/> — see that property for why.
        /// </remarks>
        [ParamsSource(nameof(MinWorkerThreadsValues))]
        public int MinWorkerThreads { get; set; }

        /// <summary>
        /// The minimum worker counts to sweep, expressed as multiples of
        /// <see cref="Environment.ProcessorCount"/>.
        /// </summary>
        /// <remarks>
        /// Fixed constants would not survive a change of machine. The processor count is both
        /// the value this parameter displaces (it is the runtime's own default floor) and the
        /// size of the runtime's immediate cooperative-blocking injection budget, so the same
        /// absolute number means "starved" on one host and "generous" on another. A constant
        /// chosen to starve a 16-core benchmark machine would quietly stop starving anything on
        /// a 4-core developer box, and the benchmark would keep reporting numbers that no longer
        /// measure the wake path.
        ///
        /// The multiples map onto real deployments: a quarter of the processor count stands in
        /// for the default floor of a 2-4 vCPU container, 1x is the runtime default that almost
        /// every application actually runs, 2x is the most common explicit multiplier in shipped
        /// code, and 8x is the control — comfortably above <see cref="Parallelism"/> so
        /// injection never gates progress.
        ///
        /// Floored at 1 because <c>SetMinThreads</c> rejects 0, and de-duplicated because the
        /// lower multiples collapse together on very small hosts.
        /// </remarks>
        public static IEnumerable<int> MinWorkerThreadsValues =>
            new[]
            {
                Environment.ProcessorCount / 4,
                Environment.ProcessorCount,
                Environment.ProcessorCount * 2,
                Environment.ProcessorCount * 8,
            }
            .Select(static value => Math.Max(1, value))
            .Distinct();

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
            SetMinWorkerThreads(MinWorkerThreads, _originalMinCompletionPortThreads);

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
            // Restoring matters beyond this benchmark: the floor is process-wide, so leaving it
            // raised would silently change the conditions for every runner that follows in the
            // same process.
            SetMinWorkerThreads(
                _originalMinWorkerThreads, _originalMinCompletionPortThreads);
        }

        /// <summary>
        /// Pins the threadpool worker floor, failing loudly if it does not take effect.
        ///
        /// <see cref="MinWorkerThreads"/> is the only variable this benchmark manipulates, so a
        /// refused or clamped request would leave every configuration running at the same floor
        /// and produce a comparison that looks valid but measures nothing. The return value alone
        /// is not sufficient evidence, so the value is also read back.
        /// </summary>
        private static void SetMinWorkerThreads(int workerThreads, int completionPortThreads)
        {
            if (!ThreadPool.SetMinThreads(workerThreads, completionPortThreads))
            {
                throw new InvalidOperationException(
                    $"ThreadPool.SetMinThreads({workerThreads}, {completionPortThreads}) was refused. " +
                    "The threadpool floor is this benchmark's independent variable, so the run would " +
                    "otherwise report a comparison that did not actually vary it.");
            }

            ThreadPool.GetMinThreads(out int actualWorkerThreads, out _);
            if (actualWorkerThreads != workerThreads)
            {
                throw new InvalidOperationException(
                    $"ThreadPool.SetMinThreads({workerThreads}, ...) reported success but the floor " +
                    $"read back as {actualWorkerThreads}. The runtime clamped the request, so this " +
                    "configuration would not measure the intended threadpool pressure.");
            }
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
