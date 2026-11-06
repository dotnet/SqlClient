// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>
    /// SQL-free controls with equal requested wait work and different scheduling/concurrency policies.
    /// </summary>
    [AsyncHealthDiagnoser]
    public class AsyncHealthCalibrationRunner
    {
        private int _observed, _unobserved, _setups, _cleanups;
        private int _iterationInvocations;
        private long _completedOperations;
        private readonly object _contentionLock = new object();

        [Params(32)]
        public int OperationCount { get; set; }

        [Params(1500)]
        public int WaitMilliseconds { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            VerifyOutsideWindow();

            if (OperationCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(OperationCount));
            }

            if (WaitMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(WaitMilliseconds));
            }
        }

        [IterationSetup]
        public void IterationSetup()
        {
            VerifyOutsideWindow();
            _iterationInvocations = 0;
            _completedOperations = 0;
            _setups++;
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            VerifyOutsideWindow();
            long expectedOperations = (long)_iterationInvocations * OperationCount;
            if (_completedOperations != expectedOperations)
            {
                throw new InvalidOperationException(
                    $"Calibration completed {_completedOperations} operations; " +
                    $"expected {expectedOperations}.");
            }

            Console.WriteLine(
                $"AsyncHealthWorkload invocations={_iterationInvocations} " +
                $"completedOperations={_completedOperations} " +
                $"requestedWaitMilliseconds={_completedOperations * WaitMilliseconds}");
            _cleanups++;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            VerifyOutsideWindow();
            Console.WriteLine($"AsyncHealthLifecycle observed={_observed} unobserved={_unobserved} setups={_setups} cleanups={_cleanups}");
        }

        private static void VerifyOutsideWindow()
        {
            if (AsyncHealthDiagnoser.IsObservingWorkload)
                throw new InvalidOperationException("Fixture setup/cleanup overlapped a workload window.");
        }

        private void RecordInvocation()
        {
            _iterationInvocations++;
            if (AsyncHealthDiagnoser.IsObservingWorkload) _observed++;
            else _unobserved++;
        }

        private void SleepOperation()
        {
            Thread.Sleep(WaitMilliseconds);
            Interlocked.Increment(ref _completedOperations);
        }

        private async Task DelayOperationAsync()
        {
            await Task.Delay(WaitMilliseconds);
            Interlocked.Increment(ref _completedOperations);
        }

        [Benchmark]
        public void ProbeOnly()
        {
            RecordInvocation();

            // Keep the no-competing-worker control on the dedicated benchmark thread.
            for (int i = 0; i < OperationCount; i++)
            {
                SleepOperation();
            }
        }

        [Benchmark]
        public Task AsyncWait()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, OperationCount).Select(_ => DelayOperationAsync()));
        }

        [Benchmark]
        public async Task AsyncWaitAwaited()
        {
            RecordInvocation();
            await Task.WhenAll(Enumerable.Range(0, OperationCount).Select(
                async _ => await DelayOperationAsync()));
        }

        [Benchmark]
        public Task Blocking()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, OperationCount).Select(
                _ => Task.Run(SleepOperation)));
        }

        [Benchmark]
        public Task BlockingLongRunning()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, OperationCount).Select(_ => Task.Factory.StartNew(
                SleepOperation, CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default)));
        }

        [Benchmark]
        public Task SyncOverAsync()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, OperationCount).Select(_ => Task.Run(
                () => DelayOperationAsync().GetAwaiter().GetResult())));
        }

        [Benchmark]
        public Task BoundedBlockingFour()
        {
            RecordInvocation();
            return RunBoundedBlocking(4);
        }

        [Benchmark]
        public Task BoundedBlockingEight()
        {
            RecordInvocation();
            return RunBoundedBlocking(8);
        }

        private async Task RunBoundedBlocking(int concurrency)
        {
            using var admission = new SemaphoreSlim(concurrency);
            await Task.WhenAll(Enumerable.Range(0, OperationCount).Select(async _ =>
            {
                await admission.WaitAsync();
                try
                {
                    await Task.Run(SleepOperation);
                }
                finally
                {
                    admission.Release();
                }
            }));
        }

        [Benchmark]
        public Task LockContention()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, OperationCount).Select(_ => Task.Run(() =>
            {
                lock (_contentionLock)
                {
                    SleepOperation();
                }
            })));
        }

        [Benchmark]
        public Task MixedOneBlocking()
        {
            RecordInvocation();
            return RunMixedWaits(1);
        }

        [Benchmark]
        public Task MixedFourBlocking()
        {
            RecordInvocation();
            return RunMixedWaits(4);
        }

        private Task RunMixedWaits(int blockingCount) => Task.WhenAll(
            Enumerable.Range(0, OperationCount).Select(index => index < blockingCount
                ? Task.Run(SleepOperation)
                : DelayOperationAsync()));
    }
}
