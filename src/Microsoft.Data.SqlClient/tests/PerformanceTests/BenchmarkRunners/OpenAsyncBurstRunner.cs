// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>
/// ThreadPool-dispatched open/query/close bursts cover empty-pool creation, warm checkout,
/// waiter wakeup and non-pooled login. Startup observations retain first-open effects.
/// </summary>
[AsyncHealthDiagnoser]
[ConfigAttribute(typeof(OpenAsyncBurstRunner.WorkloadConfig))]
public class OpenAsyncBurstRunner
{
    public enum PoolState { Empty, Warm, Oversubscribed, NonPooled }

    private const int ConcurrentWorkers = 64;
    private const int ExtendedWaves = 16;
    private const int WarmOperationsPerWorker = 20;
    private const int OversubscribedOperationsPerWorker = 5;
    private const int NonPooledOperationsPerWorker = 1;

    [Params(PoolState.Empty, PoolState.Warm, PoolState.Oversubscribed, PoolState.NonPooled)]
    public PoolState State { get; set; }

    private readonly Config _config = Config.Load();
    // BenchmarkDotNet runs GlobalSetup before executing the workload.
    private string _connectionString = null!;
    private int _originalMinimumWorkers;
    private int _originalMinimumIo;

    [GlobalSetup]
    public void SetupOpenQueryClose()
    {
        ThreadPool.GetMinThreads(out _originalMinimumWorkers, out _originalMinimumIo);
        if (!ThreadPool.SetMinThreads(16, _originalMinimumIo))
        {
            throw new InvalidOperationException("Could not set the benchmark's worker minimum.");
        }
        ThreadPool.GetMinThreads(out int minimumWorkers, out _);
        if (minimumWorkers != 16)
        {
            throw new InvalidOperationException("The runtime changed the requested worker minimum.");
        }

        var builder = new SqlConnectionStringBuilder(_config.ConnectionString)
        {
            ApplicationName = "OpenAsyncBurst-" + Guid.NewGuid().ToString("N"),
            Pooling = State != PoolState.NonPooled,
            MaxPoolSize = State == PoolState.Oversubscribed ? 16 : 100,
            MinPoolSize = 0,
            ConnectTimeout = 15,
            ConnectRetryCount = 0,
            PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock
        };
        _connectionString = builder.ConnectionString;
    }

    [GlobalCleanup]
    public void RestoreWorkerMinimum()
    {
        if (!ThreadPool.SetMinThreads(_originalMinimumWorkers, _originalMinimumIo))
        {
            throw new InvalidOperationException("Could not restore the worker minimum.");
        }
    }

    [IterationSetup]
    public void PreparePool()
    {
        using var connection = new SqlConnection(_connectionString);
        SqlConnection.ClearPool(connection);
        if (State is PoolState.Warm or PoolState.Oversubscribed)
        {
            var held = new SqlConnection[State == PoolState.Oversubscribed ? 16 : ConcurrentWorkers];
            try
            {
                for (int i = 0; i < held.Length; i++)
                {
                    held[i] = new SqlConnection(_connectionString);
                    held[i].Open();
                }
            }
            finally
            {
                foreach (SqlConnection item in held)
                {
                    item?.Dispose();
                }
            }
        }
    }

    [IterationCleanup]
    public void ClearPool()
    {
        using var connection = new SqlConnection(_connectionString);
        SqlConnection.ClearPool(connection);
    }

    /// <summary>Selects one benchmark method per pool state for the configured profile.</summary>
    public sealed class WorkloadConfig : ManualConfig
    {
        public WorkloadConfig()
        {
            bool extended = Config.Load().Benchmarks.OpenAsyncBurstRunnerConfig
                .SamplingProfile == "Extended";
            AddFilter(new SimpleFilter(benchmark =>
            {
                string workload = (extended, benchmark.Parameters[nameof(State)]) switch
                {
                    (true, PoolState.Warm) => nameof(OpenQueryCloseWarmExtended),
                    (true, PoolState.Oversubscribed) => nameof(OpenQueryCloseOversubscribedExtended),
                    (true, PoolState.NonPooled) => nameof(OpenQueryCloseNonPooledExtended),
                    _ => nameof(OpenQueryClose)
                };
                return benchmark.Descriptor.WorkloadMethod.Name == workload;
            }));
        }
    }

    [Benchmark(OperationsPerInvoke = ConcurrentWorkers)]
    public Task OpenQueryClose() => RunOpenQueryClose(1, 1);

    [Benchmark(OperationsPerInvoke = ConcurrentWorkers * ExtendedWaves * WarmOperationsPerWorker)]
    public Task OpenQueryCloseWarmExtended() =>
        RunOpenQueryClose(ExtendedWaves, WarmOperationsPerWorker);

    [Benchmark(OperationsPerInvoke =
        ConcurrentWorkers * ExtendedWaves * OversubscribedOperationsPerWorker)]
    public Task OpenQueryCloseOversubscribedExtended() =>
        RunOpenQueryClose(ExtendedWaves, OversubscribedOperationsPerWorker);

    [Benchmark(OperationsPerInvoke =
        ConcurrentWorkers * ExtendedWaves * NonPooledOperationsPerWorker)]
    public Task OpenQueryCloseNonPooledExtended() =>
        RunOpenQueryClose(ExtendedWaves, NonPooledOperationsPerWorker);

    private async Task RunOpenQueryClose(int waves, int operationsPerWorker)
    {
        var invocation = AsyncHealthDiagnoser.BeginInvocation(
            ConcurrentWorkers * waves, trackConnectionOpens: true);
        for (int wave = 0; wave < waves; wave++)
        {
            var tasks = new Task[ConcurrentWorkers];
            for (int i = 0; i < tasks.Length; i++)
            {
                int worker = wave * ConcurrentWorkers + i;
                var first = invocation?.Offer(worker);
                tasks[i] = Task.Run(async () =>
                {
                    for (int operationIndex = 0; operationIndex < operationsPerWorker; operationIndex++)
                    {
                        var operation = operationIndex == 0 ? first : invocation?.Offer(worker);
                        operation?.Start();
                        try
                        {
                            using (var connection = new SqlConnection(_connectionString))
                            {
                                Task open = connection.OpenAsync();
                                operation?.OpenReturned(open.IsCompleted);
                                await open.ConfigureAwait(false);
                                operation?.Opened();
                                using var command = connection.CreateCommand();
                                command.CommandText = "SELECT 1";
                                command.CommandTimeout = 15;
                                _ = await command.ExecuteScalarAsync().ConfigureAwait(false);
                            }

                            operation?.Complete();
                        }
                        catch (Exception error)
                        {
                            operation?.Fail(error);
                            throw;
                        }
                    }
                });
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        invocation?.Complete();
    }
}
