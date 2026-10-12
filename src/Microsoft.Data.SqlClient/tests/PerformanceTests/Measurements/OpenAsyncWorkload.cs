// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>One attempted open, with timestamps relative to the invocation's monotonic epoch.</summary>
    public sealed class OpenAsyncSample
    {
        public int Worker { get; set; }
        public int Operation { get; set; }
        public double StartMilliseconds { get; set; }
        public double? TaskReturnMilliseconds { get; set; }
        public double CompletionMilliseconds { get; set; }
        public bool? CompletedAtReturn { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public string? ErrorType { get; set; }
        public int? SqlErrorNumber { get; set; }
    }

    /// <summary>Retains failures as well as successes; measurements include observer overhead.</summary>
    public sealed class OpenAsyncInvocation
    {
        public double ElapsedMilliseconds { get; set; }
        public double CpuMilliseconds { get; set; }
        public long AllocatedBytes { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public int ProcessThreadsBefore { get; set; }
        public int ProcessThreadsAfter { get; set; }
        public int ThreadPoolThreadsBefore { get; set; }
        public int ThreadPoolThreadsAfter { get; set; }
        public int PeakOutstandingOpens { get; set; }
        public int PeakOpenConnections { get; set; }
        public double[] AdmissionMilliseconds { get; set; } = Array.Empty<double>();
        public OpenAsyncSample[] Samples { get; set; } = Array.Empty<OpenAsyncSample>();

        /// <summary>Rejects failed acquisition and invalid retained-ramp/pool-capacity observations.</summary>
        public void EnsureValid(OpenAsyncScoreboardCase configuration)
        {
            if (Samples.Length != configuration.OperationCount || Samples.Any(sample => sample == null))
            {
                throw new InvalidOperationException("Incomplete OpenAsync scoreboard samples.");
            }
            int failures = Samples.Count(sample => sample.Outcome != "Success");
            if (failures != 0)
            {
                throw new InvalidOperationException($"{failures} scoreboard opens failed or were canceled; see supplementary results.");
            }
            if ((configuration.Scenario == "E4" && PeakOpenConnections != configuration.Concurrency) ||
                (configuration.PoolSize > 0 && PeakOpenConnections > configuration.PoolSize))
            {
                throw new InvalidOperationException("The measured connection ownership does not match the configured workload.");
            }
        }
    }

    /// <summary>
    /// Runs matched ThreadPool workers: warm reuse, fixed-capacity holds, nonpooled opens,
    /// or retained cold ramp. The connection factory seam tests workload shape without SQL Server.
    /// </summary>
    public static class OpenAsyncWorkload
    {
        /// <summary>
        /// Executes one bounded cohort and records every open outcome. The caller must validate
        /// the result after persisting it; a failed open must never become a successful benchmark.
        /// </summary>
        public static async Task<OpenAsyncInvocation> RunAsync(OpenAsyncScoreboardCase configuration,
            Func<DbConnection> connectionFactory, CancellationToken cancellationToken)
        {
            var result = new OpenAsyncInvocation
            {
                Samples = new OpenAsyncSample[configuration.OperationCount],
                AdmissionMilliseconds = new double[configuration.Concurrency]
            };
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int remaining = configuration.Concurrency;
            int outstanding = 0, peakOutstanding = 0, owned = 0, peakOwned = 0;
            using Process process = Process.GetCurrentProcess();
            result.ProcessThreadsBefore = process.Threads.Count;
            result.ThreadPoolThreadsBefore = ThreadPool.ThreadCount;
            TimeSpan initialCpu = process.TotalProcessorTime;
            long initialAllocated = GC.GetTotalAllocatedBytes(precise: true);
            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            long epoch = Stopwatch.GetTimestamp();
            double Timestamp() => Stopwatch.GetElapsedTime(epoch).TotalMilliseconds;

            Task[] workers = Enumerable.Range(0, configuration.Concurrency).Select(worker => Task.Run(async () =>
            {
                await start.Task.ConfigureAwait(false);
                for (int operation = 0; operation < configuration.OperationsPerWorker; operation++)
                {
                    using DbConnection connection = connectionFactory();
                    var sample = new OpenAsyncSample { Worker = worker, Operation = operation };
                    result.Samples[worker * configuration.OperationsPerWorker + operation] = sample;
                    bool acquired = false;
                    UpdatePeak(ref peakOutstanding, Interlocked.Increment(ref outstanding));
                    sample.StartMilliseconds = Timestamp();
                    if (operation == 0)
                    {
                        result.AdmissionMilliseconds[worker] = sample.StartMilliseconds;
                    }
                    try
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (configuration.Api == "Async")
                            {
                                Task open = connection.OpenAsync(cancellationToken);
                                sample.TaskReturnMilliseconds = Timestamp();
                                sample.CompletedAtReturn = open.IsCompleted;
                                await open.ConfigureAwait(false);
                            }
                            else
                            {
                                connection.Open();
                            }
                            sample.CompletionMilliseconds = Timestamp();
                            if (connection.State != ConnectionState.Open)
                            {
                                throw new InvalidOperationException("Open completed without an open connection.");
                            }
                            sample.Outcome = "Success";
                            acquired = true;
                            UpdatePeak(ref peakOwned, Interlocked.Increment(ref owned));
                        }
                        catch (Exception error) when (error is DbException || error is InvalidOperationException ||
                            error is OperationCanceledException)
                        {
                            sample.CompletionMilliseconds = Timestamp();
                            sample.Outcome = error is OperationCanceledException ? "Canceled" : "Failed";
                            sample.ErrorType = error.GetType().FullName;
                            sample.SqlErrorNumber = (error as SqlException)?.Number;
                        }
                        finally
                        {
                            Interlocked.Decrement(ref outstanding);
                            // Failed attempts also release the rendezvous; they remain failures in the result.
                            if (configuration.Scenario == "E4" && Interlocked.Decrement(ref remaining) == 0)
                            {
                                release.TrySetResult(true);
                            }
                        }

                        if (configuration.Scenario == "E4")
                        {
                            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                        }
                        else if (acquired && configuration.HoldMilliseconds > 0)
                        {
                            await Task.Delay(configuration.HoldMilliseconds, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        // Decrement before disposal can hand the connection to another worker.
                        if (acquired)
                        {
                            Interlocked.Decrement(ref owned);
                        }
                    }
                    connection.Close();
                    // Keep outside-open scheduling identical, including on synchronously completing async opens.
                    await Task.Yield();
                }
            })).ToArray();
            start.SetResult(true);
            await Task.WhenAll(workers).ConfigureAwait(false);
            result.ElapsedMilliseconds = Timestamp();
            result.AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - initialAllocated;
            result.Gen0Collections = GC.CollectionCount(0) - gen0;
            result.Gen1Collections = GC.CollectionCount(1) - gen1;
            result.Gen2Collections = GC.CollectionCount(2) - gen2;
            process.Refresh();
            result.CpuMilliseconds = (process.TotalProcessorTime - initialCpu).TotalMilliseconds;
            result.ProcessThreadsAfter = process.Threads.Count;
            result.ThreadPoolThreadsAfter = ThreadPool.ThreadCount;
            result.PeakOutstandingOpens = peakOutstanding;
            result.PeakOpenConnections = peakOwned;
            return result;
        }

        private static void UpdatePeak(ref int peak, int value)
        {
            int observed;
            do
            {
                observed = Volatile.Read(ref peak);
                if (value <= observed)
                {
                    return;
                }
            } while (Interlocked.CompareExchange(ref peak, value, observed) != observed);
        }
    }
}
