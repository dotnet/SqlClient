// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>
/// ThreadPool-dispatched open/query/close bursts cover empty-pool creation, warm checkout,
/// waiter wakeup and non-pooled login. Startup observations retain first-open effects.
/// </summary>
[AsyncHealthDiagnoser]
public class OpenAsyncBurstRunner
{
    public enum PoolState { Empty, Warm, Oversubscribed, NonPooled }

    internal const int ConcurrentWorkers = 64;
    private const int ExtendedWaves = 16;

    [Params(PoolState.Empty, PoolState.Warm, PoolState.Oversubscribed, PoolState.NonPooled)]
    public PoolState State { get; set; }

    private readonly Config _config = Config.Load();
    // BenchmarkDotNet runs GlobalSetup before executing the workload.
    private string _connectionString = null!;
    private BurstWorkload _openQueryClose = null!;
    private int _originalMinimumWorkers;
    private int _originalMinimumIo;

    internal static OpenAsyncBurstRunnerJob GetJob(Benchmarks benchmarks)
    {
        var job = benchmarks.OpenAsyncBurstRunnerConfig ??
            new OpenAsyncBurstRunnerJob { ProcessIsolation = "PerCase" };

        if (Environment.GetEnvironmentVariable("SQLCLIENT_PERF_PIPELINE") == "1")
        {
            job.Enabled = true;
        }

        return job;
    }

    [GlobalSetup]
    public void SetupOpenQueryClose()
    {
        _openQueryClose = GetWorkload(GetJob(_config.Benchmarks), State);

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

    internal static BurstWorkload GetWorkload(OpenAsyncBurstRunnerJob job, PoolState state)
    {
        job.ValidateExecution();
        // Fixed per-worker work from the 100ms calibration; Empty remains one cold burst.
        int operations = state switch
        {
            PoolState.Empty or PoolState.NonPooled => 1,
            PoolState.Warm => 20,
            PoolState.Oversubscribed => 5,
            _ => throw new ArgumentException("Unknown burst pool state.", nameof(state))
        };
        return job.SamplingProfile == "Extended" && state != PoolState.Empty
            ? new BurstWorkload(ExtendedWaves, operations)
            : new BurstWorkload(1, 1);
    }

    [Benchmark(OperationsPerInvoke = ConcurrentWorkers)]
    public async Task OpenQueryClose()
    {
        var invocation = AsyncHealthDiagnoser.BeginInvocation(
            _openQueryClose.WorkerStarts, trackConnectionOpens: true);
        for (int wave = 0; wave < _openQueryClose.Waves; wave++)
        {
            var tasks = new Task[ConcurrentWorkers];
            for (int i = 0; i < tasks.Length; i++)
            {
                int worker = wave * ConcurrentWorkers + i;
                var first = invocation?.Offer(worker);
                tasks[i] = Task.Run(async () =>
                {
                    for (int operationIndex = 0;
                        operationIndex < _openQueryClose.OperationsPerWorker; operationIndex++)
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

/// <summary>One benchmark-owned work definition drives execution and BDN normalization.</summary>
public sealed record BurstWorkload(int Waves, int OperationsPerWorker)
{
    public int WorkerStarts => checked(OpenAsyncBurstRunner.ConcurrentWorkers * Waves);
    public int OperationsPerInvoke => checked(WorkerStarts * OperationsPerWorker);
}
