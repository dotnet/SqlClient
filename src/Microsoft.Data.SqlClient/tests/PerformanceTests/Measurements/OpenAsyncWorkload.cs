// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
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
        public int RequestId { get; set; }
        public int Worker { get; set; }
        public int Operation { get; set; }
        public double StartMilliseconds { get; set; }
        public double? TaskReturnMilliseconds { get; set; }
        public double CompletionMilliseconds { get; set; }
        public double? LifecycleCompletionMilliseconds { get; set; }
        public double? OfferedMilliseconds { get; set; }
        public string? Phase { get; set; }
        public bool? CompletedAtReturn { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public string? ErrorType { get; set; }
        public int? SqlErrorNumber { get; set; }
        public string LifecycleOutcome { get; set; } = string.Empty;
        public string? LifecycleErrorType { get; set; }
        public int? LifecycleSqlErrorNumber { get; set; }
    }

    /// <summary>The matched E5 query, timed separately from acquisition and cleanup.</summary>
    public sealed class OpenAsyncQuerySample
    {
        public int RequestId { get; set; }
        public double StartMilliseconds { get; set; }
        public double CompletionMilliseconds { get; set; }
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
        public bool WasCanceled { get; set; }
        public double[] AdmissionMilliseconds { get; set; } = Array.Empty<double>();
        public OpenAsyncSample[] Samples { get; set; } = Array.Empty<OpenAsyncSample>();
        public OpenAsyncQuerySample[] QuerySamples { get; set; } = Array.Empty<OpenAsyncQuerySample>();
        public OpenAsyncLoadResult? Load { get; set; }

        /// <summary>Rejects failed acquisition, query, cleanup, cancellation and incomplete accounting.</summary>
        public void EnsureValid(OpenAsyncScoreboardCase configuration)
        {
            if (WasCanceled || Samples.Any(sample => sample == null) ||
                Samples.Select(sample => sample.RequestId).Distinct().Count() != Samples.Length)
            {
                throw new InvalidOperationException("Canceled or incomplete OpenAsync scoreboard samples.");
            }
            if (configuration.Scenario == "E6")
            {
                if (Load == null)
                {
                    throw new InvalidOperationException("Missing open-loop load accounting.");
                }
                Load.EnsureValid(configuration, Samples);
            }
            else if (Samples.Length != configuration.OperationCount || Load != null)
            {
                throw new InvalidOperationException("Incomplete OpenAsync scoreboard samples.");
            }
            if (Samples.Any(sample => sample.Outcome != "Success" ||
                sample.LifecycleOutcome != "Success" || !sample.LifecycleCompletionMilliseconds.HasValue))
            {
                throw new InvalidOperationException("Scoreboard acquisition or lifecycle failed; see supplementary results.");
            }
            int expectedQueries = configuration.Scenario == "E5" ? Samples.Length : 0;
            if (QuerySamples.Length != expectedQueries ||
                QuerySamples.Select(sample => sample.RequestId).Distinct().Count() != expectedQueries ||
                QuerySamples.Any(query => query.Outcome != "Success" ||
                    !Samples.Any(sample => sample.RequestId == query.RequestId)))
            {
                throw new InvalidOperationException("Scoreboard queries failed or are incomplete; see supplementary results.");
            }
            if ((configuration.Scenario == "E4" && PeakOpenConnections != configuration.Concurrency) ||
                (configuration.PoolSize > 0 && PeakOpenConnections > configuration.PoolSize))
            {
                throw new InvalidOperationException("The measured connection ownership does not match the configured workload.");
            }
        }
    }

    /// <summary>Runs matched ThreadPool cohorts, sharing lifecycle accounting with open-loop load.</summary>
    public static class OpenAsyncWorkload
    {
        /// <summary>
        /// E5 requires a matched async query delegate, including result validation. The start observer
        /// runs after the epoch, before workers are released, outside outstanding/ownership counters.
        /// Its overhead is visible in admission/elapsed time. Completion runs after every worker exits
        /// and after resource measurements, even on failure; completion observer overhead is excluded.
        /// </summary>
        public static async Task<OpenAsyncInvocation> RunAsync(OpenAsyncScoreboardCase configuration,
            Func<DbConnection> connectionFactory, CancellationToken cancellationToken,
            Func<DbConnection, CancellationToken, Task>? query = null,
            Action<long, Func<(int Outstanding, int Owned)>>? onStarted = null, Action? onCompleted = null)
        {
            if (configuration.Scenario == "E6")
            {
                throw new ArgumentException("E6 requires OpenAsyncLoadWorkload.", nameof(configuration));
            }
            if (configuration.Scenario == "E5" && query == null)
            {
                throw new ArgumentException("E5 requires the matched async query delegate.", nameof(query));
            }
            var result = new OpenAsyncInvocation
            {
                Samples = new OpenAsyncSample[configuration.OperationCount],
                AdmissionMilliseconds = new double[configuration.Concurrency]
            };
            var queries = new ConcurrentQueue<OpenAsyncQuerySample>();
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int remaining = configuration.Concurrency;
            var counters = new Counters();
            using var measurement = new Measurement(result, counters);
            Task[] workers = Enumerable.Range(0, configuration.Concurrency).Select(worker => Task.Run(async () =>
            {
                if (!await start.Task.ConfigureAwait(false))
                {
                    return;
                }
                for (int operation = 0; operation < configuration.OperationsPerWorker; operation++)
                {
                    int requestId = worker * configuration.OperationsPerWorker + operation;
                    var sample = new OpenAsyncSample { RequestId = requestId, Worker = worker, Operation = operation };
                    result.Samples[requestId] = sample;
                    await ExecuteAsync(configuration, connectionFactory, cancellationToken, measurement,
                        sample, queries, query, configuration.Scenario == "E4" ? release.Task : null,
                        () =>
                        {
                            if (configuration.Scenario == "E4" && Interlocked.Decrement(ref remaining) == 0)
                            {
                                release.TrySetResult(true);
                            }
                        }).ConfigureAwait(false);
                    if (operation == 0)
                    {
                        result.AdmissionMilliseconds[worker] = sample.StartMilliseconds;
                    }
                    await Task.Yield();
                }
            })).ToArray();
            try
            {
                onStarted?.Invoke(measurement.Epoch, counters.Read);
                start.SetResult(true);
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            finally
            {
                start.TrySetResult(false);
                try
                {
                    await Task.WhenAll(workers).ConfigureAwait(false);
                    result.QuerySamples = queries.OrderBy(querySample => querySample.RequestId).ToArray();
                    result.WasCanceled = cancellationToken.IsCancellationRequested;
                    measurement.Complete();
                }
                finally
                {
                    onCompleted?.Invoke();
                }
            }
            return result;
        }

        internal static async Task ExecuteAsync(OpenAsyncScoreboardCase configuration,
            Func<DbConnection> connectionFactory, CancellationToken cancellationToken, Measurement measurement,
            OpenAsyncSample sample, ConcurrentQueue<OpenAsyncQuerySample> queries,
            Func<DbConnection, CancellationToken, Task>? query = null, Task? release = null,
            Action? attempted = null)
        {
            DbConnection? connection = null;
            bool acquired = false;
            bool opening = false;
            sample.LifecycleOutcome = "Success";
            try
            {
                sample.StartMilliseconds = measurement.Timestamp();
                try
                {
                    connection = connectionFactory();
                    measurement.Counters.EnterOpen();
                    opening = true;
                    sample.StartMilliseconds = measurement.Timestamp();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (configuration.Api == "Async")
                    {
                        Task open = connection.OpenAsync(cancellationToken);
                        sample.TaskReturnMilliseconds = measurement.Timestamp();
                        sample.CompletedAtReturn = open.IsCompleted;
                        await open.ConfigureAwait(false);
                    }
                    else
                    {
                        connection.Open();
                    }
                    sample.CompletionMilliseconds = measurement.Timestamp();
                    if (connection.State != ConnectionState.Open)
                    {
                        throw new InvalidOperationException("Open completed without an open connection.");
                    }
                    sample.Outcome = "Success";
                    acquired = true;
                    measurement.Counters.Acquire();
                }
                catch (Exception error)
                {
                    sample.CompletionMilliseconds = measurement.Timestamp();
                    sample.Outcome = Outcome(error);
                    sample.ErrorType = error.GetType().FullName;
                    sample.SqlErrorNumber = (error as SqlException)?.Number;
                }
                finally
                {
                    if (opening)
                    {
                        measurement.Counters.ExitOpen();
                    }
                    // Includes failed factories, so a retained cold ramp cannot strand its peers.
                    attempted?.Invoke();
                }

                if (configuration.Scenario == "E5" && acquired)
                {
                    var querySample = new OpenAsyncQuerySample
                    {
                        RequestId = sample.RequestId,
                        StartMilliseconds = measurement.Timestamp()
                    };
                    try
                    {
                        await query!(connection!, cancellationToken).ConfigureAwait(false);
                        querySample.Outcome = "Success";
                    }
                    catch (Exception error)
                    {
                        querySample.Outcome = Outcome(error);
                        querySample.ErrorType = error.GetType().FullName;
                        querySample.SqlErrorNumber = (error as SqlException)?.Number;
                    }
                    finally
                    {
                        querySample.CompletionMilliseconds = measurement.Timestamp();
                        queries.Enqueue(querySample);
                    }
                }
                if (release != null)
                {
                    await release.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (acquired && configuration.HoldMilliseconds > 0)
                {
                    await Task.Delay(configuration.HoldMilliseconds, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception error)
            {
                RecordLifecycleFailure(sample, error);
            }
            finally
            {
                // Decrement before Close/Dispose can hand a pooled connection to another worker.
                if (acquired)
                {
                    measurement.Counters.Release();
                }
                try
                {
                    connection?.Close();
                }
                catch (Exception error)
                {
                    RecordLifecycleFailure(sample, error);
                }
                finally
                {
                    try
                    {
                        connection?.Dispose();
                    }
                    catch (Exception error)
                    {
                        RecordLifecycleFailure(sample, error);
                    }
                    sample.LifecycleCompletionMilliseconds = measurement.Timestamp();
                }
            }
        }

        private static string Outcome(Exception error) => error is OperationCanceledException ? "Canceled" : "Failed";

        private static void RecordLifecycleFailure(OpenAsyncSample sample, Exception error)
        {
            if (sample.LifecycleOutcome == "Success")
            {
                sample.LifecycleOutcome = Outcome(error);
                sample.LifecycleErrorType = error.GetType().FullName;
                sample.LifecycleSqlErrorNumber = (error as SqlException)?.Number;
            }
        }

        internal sealed class Counters
        {
            private int _outstanding, _owned;
            internal int PeakOutstanding, PeakOwned;
            internal (int Outstanding, int Owned) Read() => (Volatile.Read(ref _outstanding), Volatile.Read(ref _owned));
            internal void EnterOpen() => UpdatePeak(ref PeakOutstanding, Interlocked.Increment(ref _outstanding));
            internal void ExitOpen() => Interlocked.Decrement(ref _outstanding);
            internal void Acquire() => UpdatePeak(ref PeakOwned, Interlocked.Increment(ref _owned));
            internal void Release() => Interlocked.Decrement(ref _owned);
        }

        internal sealed class Measurement : IDisposable
        {
            private readonly OpenAsyncInvocation _result;
            private readonly Process _process = Process.GetCurrentProcess();
            private readonly TimeSpan _cpu;
            private readonly long _allocated;
            private readonly int _gen0, _gen1, _gen2;
            internal readonly Counters Counters;
            internal readonly long Epoch;

            internal Measurement(OpenAsyncInvocation result, Counters counters)
            {
                _result = result;
                Counters = counters;
                result.ProcessThreadsBefore = _process.Threads.Count;
                result.ThreadPoolThreadsBefore = ThreadPool.ThreadCount;
                _cpu = _process.TotalProcessorTime;
                _allocated = GC.GetTotalAllocatedBytes(precise: true);
                _gen0 = GC.CollectionCount(0);
                _gen1 = GC.CollectionCount(1);
                _gen2 = GC.CollectionCount(2);
                Epoch = Stopwatch.GetTimestamp();
            }

            internal double Timestamp() => Stopwatch.GetElapsedTime(Epoch).TotalMilliseconds;

            internal void Complete()
            {
                _result.ElapsedMilliseconds = Timestamp();
                _result.AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - _allocated;
                _result.Gen0Collections = GC.CollectionCount(0) - _gen0;
                _result.Gen1Collections = GC.CollectionCount(1) - _gen1;
                _result.Gen2Collections = GC.CollectionCount(2) - _gen2;
                _process.Refresh();
                _result.CpuMilliseconds = (_process.TotalProcessorTime - _cpu).TotalMilliseconds;
                _result.ProcessThreadsAfter = _process.Threads.Count;
                _result.ThreadPoolThreadsAfter = ThreadPool.ThreadCount;
                _result.PeakOutstandingOpens = Counters.PeakOutstanding;
                _result.PeakOpenConnections = Counters.PeakOwned;
            }

            public void Dispose() => _process.Dispose();
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
