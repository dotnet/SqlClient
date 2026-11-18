// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>
/// ThreadPool-dispatched OpenAsync bursts cover empty-pool creation, warm checkout, waiter wakeup
/// and non-pooled login. Startup observations retain first-open effects; later cases reuse driver state.
/// </summary>
[AsyncHealthDiagnoser]
public class OpenAsyncBurstRunner : BaseRunner
{
    public enum PoolState { Empty, Warm, Oversubscribed, NonPooled }

    [Params(PoolState.Empty, PoolState.Warm, PoolState.Oversubscribed, PoolState.NonPooled)]
    public PoolState State { get; set; }

    private const int Workers = OpenAsyncBurstRunnerJob.ConcurrentWorkers;
    private string _connectionString;
    private BurstWorkload _openClose;
    private BurstWorkload _openQueryClose;
    private int _originalMinimumWorkers;
    private int _originalMinimumIo;

    [GlobalSetup(Target = nameof(OpenClose))]
    public void SetupOpenClose()
    {
        _openClose = s_config.Benchmarks.OpenAsyncBurstRunnerConfig.GetWorkload(
            nameof(OpenClose), State.ToString());
        Setup();
    }

    [GlobalSetup(Target = nameof(OpenQueryClose))]
    public void SetupOpenQueryClose()
    {
        _openQueryClose = s_config.Benchmarks.OpenAsyncBurstRunnerConfig.GetWorkload(
            nameof(OpenQueryClose), State.ToString());
        Setup();
    }

    private void Setup()
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

        var builder = new SqlConnectionStringBuilder(s_config.ConnectionString)
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
            var held = new SqlConnection[State == PoolState.Oversubscribed ? 16 : Workers];
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

    [Benchmark(OperationsPerInvoke = Workers)]
    public async Task OpenClose()
    {
        var invocation = AsyncHealthDiagnoser.BeginInvocation(_openClose.WorkerStarts);
        for (int wave = 0; wave < _openClose.Waves; wave++)
        {
            var tasks = new Task[Workers];
            for (int i = 0; i < tasks.Length; i++)
            {
                int worker = wave * Workers + i;
                var first = invocation?.Offer(worker);
                tasks[i] = Task.Run(async () =>
                {
                    for (int operationIndex = 0;
                        operationIndex < _openClose.OperationsPerWorker; operationIndex++)
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

    [Benchmark(OperationsPerInvoke = Workers)]
    public async Task OpenQueryClose()
    {
        var invocation = AsyncHealthDiagnoser.BeginInvocation(_openQueryClose.WorkerStarts);
        for (int wave = 0; wave < _openQueryClose.Waves; wave++)
        {
            var tasks = new Task[Workers];
            for (int i = 0; i < tasks.Length; i++)
            {
                int worker = wave * Workers + i;
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
