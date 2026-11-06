// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>SQL-free interference controls; only these opt-in controls create synthetic competing work.</summary>
    [AsyncHealthDiagnoser]
    public class AsyncHealthCalibrationRunner
    {
        private int _observed, _unobserved, _setups, _cleanups;
        private readonly object _contentionLock = new object();

        [GlobalSetup]
        public void Setup() => VerifyOutsideWindow();

        [IterationSetup]
        public void IterationSetup()
        {
            VerifyOutsideWindow();
            _setups++;
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            VerifyOutsideWindow();
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
            if (AsyncHealthDiagnoser.IsObservingWorkload) _observed++;
            else _unobserved++;
        }

        [Benchmark]
        public void ProbeOnly()
        {
            RecordInvocation();
            Thread.Sleep(3000);
        }

        [Benchmark]
        public Task AsyncWait()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Delay(3000)));
        }

        [Benchmark]
        public async Task AsyncWaitAwaited()
        {
            RecordInvocation();
            await Task.WhenAll(Enumerable.Range(0, 32).Select(async _ => await Task.Delay(3000)));
        }

        [Benchmark]
        public Task Blocking()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => Thread.Sleep(1500))));
        }

        [Benchmark]
        public Task BlockingLongRunning()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Factory.StartNew(
                () => Thread.Sleep(1500), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)));
        }

        [Benchmark]
        public Task SyncOverAsync()
        {
            RecordInvocation();
            return Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(
                () => Task.Delay(1500).GetAwaiter().GetResult())));
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

        private static async Task RunBoundedBlocking(int concurrency)
        {
            using var admission = new SemaphoreSlim(concurrency);
            await Task.WhenAll(Enumerable.Range(0, 32).Select(async _ =>
            {
                await admission.WaitAsync();
                try
                {
                    await Task.Run(() => Thread.Sleep(1500));
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
            return Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            {
                lock (_contentionLock)
                {
                    Thread.Sleep(100);
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

        private static Task RunMixedWaits(int blockingCount) => Task.WhenAll(
            Enumerable.Range(0, 32).Select(index => index < blockingCount
                ? Task.Run(() => Thread.Sleep(3000))
                : Task.Delay(3000)));
    }
}
