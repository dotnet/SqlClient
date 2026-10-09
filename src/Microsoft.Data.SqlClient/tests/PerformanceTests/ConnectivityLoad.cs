// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    internal readonly record struct ConnectivityLoadResult(
        double ElapsedSeconds,
        double WorkerSeconds,
        long SuccessfulOpens,
        int PeakOccupiedWorkers,
        int PeakPoolThreads,
        int PeakProcessThreads,
        long Samples,
        double CpuSeconds)
    {
        public double AverageOccupiedWorkers => WorkerSeconds / ElapsedSeconds;
        public double WorkerMillisecondsPerOpen => WorkerSeconds * 1000 / SuccessfulOpens;
        public double OpensPerSecond => SuccessfulOpens / ElapsedSeconds;
        public double CpuMillisecondsPerOpen => CpuSeconds * 1000 / SuccessfulOpens;

        /// <summary>Combines totals so different invocation lengths retain their correct weight.</summary>
        public ConnectivityLoadResult Combine(ConnectivityLoadResult other) => new(
            ElapsedSeconds + other.ElapsedSeconds,
            WorkerSeconds + other.WorkerSeconds,
            checked(SuccessfulOpens + other.SuccessfulOpens),
            Math.Max(PeakOccupiedWorkers, other.PeakOccupiedWorkers),
            Math.Max(PeakPoolThreads, other.PeakPoolThreads),
            Math.Max(PeakProcessThreads, other.PeakProcessThreads),
            checked(Samples + other.Samples),
            CpuSeconds + other.CpuSeconds);
    }

    /// <summary>Integrates occupied workers using actual sample timestamps, not the requested timer interval.</summary>
    internal sealed class WorkerOccupancy
    {
        private long _firstTimestamp;
        private long _lastTimestamp;
        private int _previousOccupiedWorkers;
        private double _workerTicks;
        private int _peakOccupiedWorkers;
        private int _peakPoolThreads;
        private int _peakProcessThreads;
        private long _samples;

        /// <summary>Adds a monotonic sample and trapezoidally integrates the interval since the previous sample.</summary>
        public void Sample(long timestamp, int occupiedWorkers, int poolThreads, int processThreads = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(occupiedWorkers);
            ArgumentOutOfRangeException.ThrowIfNegative(poolThreads);
            ArgumentOutOfRangeException.ThrowIfNegative(processThreads);
            if (_samples > 0 && timestamp < _lastTimestamp)
            {
                throw new ArgumentOutOfRangeException(nameof(timestamp), "Sample timestamps must be monotonic.");
            }

            if (_samples == 0)
            {
                _firstTimestamp = timestamp;
            }
            else
            {
                _workerTicks += (_previousOccupiedWorkers + (double)occupiedWorkers) / 2
                    * (timestamp - _lastTimestamp);
            }

            _lastTimestamp = timestamp;
            _previousOccupiedWorkers = occupiedWorkers;
            _peakOccupiedWorkers = Math.Max(_peakOccupiedWorkers, occupiedWorkers);
            _peakPoolThreads = Math.Max(_peakPoolThreads, poolThreads);
            _peakProcessThreads = Math.Max(_peakProcessThreads, processThreads);
            _samples++;
        }

        /// <summary>Produces raw totals, rejecting an empty or unsuccessful measurement instead of dividing by zero.</summary>
        public ConnectivityLoadResult Complete(long successfulOpens, double cpuSeconds)
        {
            if (_samples < 2 || _lastTimestamp <= _firstTimestamp || successfulOpens <= 0 || cpuSeconds < 0)
            {
                throw new InvalidOperationException("A scorecard requires elapsed time, samples and successful opens.");
            }

            return new ConnectivityLoadResult(
                (_lastTimestamp - _firstTimestamp) / (double)Stopwatch.Frequency,
                _workerTicks / Stopwatch.Frequency,
                successfulOpens,
                _peakOccupiedWorkers,
                _peakPoolThreads,
                _peakProcessThreads,
                _samples,
                cpuSeconds);
        }
    }

    /// <summary>Runs awaited open/close loops and observes their process-wide worker occupancy from a dedicated thread.</summary>
    internal static class ConnectivityLoad
    {
        /// <summary>Samples concurrent awaited operations until admissions stop and all in-flight operations finish.</summary>
        public static async Task<ConnectivityLoadResult> RunAsync(
            Func<Task> openAndClose,
            int concurrency,
            TimeSpan duration,
            TimeSpan sampleInterval)
        {
            ArgumentNullException.ThrowIfNull(openAndClose);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrency);
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }
            if (sampleInterval < TimeSpan.FromMilliseconds(1) || sampleInterval > duration)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleInterval));
            }

            using var ready = new ManualResetEventSlim();
            using var stopObserver = new ManualResetEventSlim();
            using Process process = Process.GetCurrentProcess();
            var start = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
            var occupancy = new WorkerOccupancy();
            ExceptionDispatchInfo? observerError = null;
            double cpuSeconds = 0;
            int failed = 0;

            var observer = new Thread(() =>
            {
                try
                {
                    TimeSpan cpuStart = process.TotalProcessorTime;
                    Sample();
                    ready.Set();
                    while (!stopObserver.Wait(sampleInterval))
                    {
                        Sample();
                    }
                    Sample();
                    cpuSeconds = (process.TotalProcessorTime - cpuStart).TotalSeconds;
                }
                catch (Exception error)
                {
                    observerError = ExceptionDispatchInfo.Capture(error);
                    Interlocked.Exchange(ref failed, 1);
                    ready.Set();
                }
            })
            {
                IsBackground = true,
                Name = "SqlClient connectivity occupancy observer"
            };

            observer.Start();
            long[] completed;
            try
            {
                ready.Wait();
                observerError?.Throw();
                var workers = new Task<long>[concurrency];
                for (int i = 0; i < workers.Length; i++)
                {
                    workers[i] = Task.Run(async () =>
                    {
                        long started = await start.Task.ConfigureAwait(false);
                        long successfulOpens = 0;
                        try
                        {
                            while (Volatile.Read(ref failed) == 0 && Stopwatch.GetElapsedTime(started) < duration)
                            {
                                await openAndClose().ConfigureAwait(false);
                                successfulOpens++;
                            }
                            return successfulOpens;
                        }
                        catch
                        {
                            // Stop admitting new opens, but drain every already-started operation.
                            Interlocked.Exchange(ref failed, 1);
                            throw;
                        }
                    });
                }
                start.SetResult(Stopwatch.GetTimestamp());
                completed = await Task.WhenAll(workers).ConfigureAwait(false);
            }
            finally
            {
                stopObserver.Set();
                observer.Join();
            }

            observerError?.Throw();
            long totalOpens = 0;
            foreach (long count in completed)
            {
                totalOpens = checked(totalOpens + count);
            }
            return occupancy.Complete(totalOpens, cpuSeconds);

            void Sample()
            {
                ThreadPool.GetMaxThreads(out int maximumWorkers, out _);
                ThreadPool.GetAvailableThreads(out int availableWorkers, out _);
                long timestamp = Stopwatch.GetTimestamp();
                process.Refresh();
                occupancy.Sample(
                    timestamp, maximumWorkers - availableWorkers, ThreadPool.ThreadCount, process.Threads.Count);
            }
        }
    }
}
