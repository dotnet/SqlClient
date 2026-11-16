// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>
/// ThreadPool-dispatched OpenAsync bursts cover empty-pool creation, warm checkout, waiter wakeup
/// and non-pooled login. BDN initializes the driver; truly cold processes are measured separately.
/// </summary>
[AsyncHealthDiagnoser]
public class OpenAsyncBurstRunner : BaseRunner
{
    public enum PoolState { Empty, Warm, Oversubscribed, NonPooled }

    [Params(PoolState.Empty, PoolState.Warm, PoolState.Oversubscribed, PoolState.NonPooled)]
    public PoolState State { get; set; }

    private const int Workers = 64;
    private string _connectionString;

    [GlobalSetup]
    public void Setup()
    {
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
    public Task OpenClose()
    {
        var tasks = new Task[Workers];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);
            });
        }
        return Task.WhenAll(tasks);
    }

    [Benchmark(OperationsPerInvoke = Workers)]
    public Task OpenQueryClose()
    {
        var tasks = new Task[Workers];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandTimeout = 15;
                _ = await command.ExecuteScalarAsync().ConfigureAwait(false);
            });
        }
        return Task.WhenAll(tasks);
    }
}
