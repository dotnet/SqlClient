// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>One fresh-process experiment, deliberately independent of BDN warmup.</summary>
internal sealed class OpenAsyncHealthSettings
{
    public int Workers { get; set; } = 64;
    public int PoolSize { get; set; } = 100;
    public int MinimumThreads { get; set; } = 16;
    public int MaximumThreads { get; set; } = 32767;
    public int DurationSeconds { get; set; } = 10;
    public int ConnectTimeoutSeconds { get; set; } = 15;
    public bool PoolV2 { get; set; } = true;
    public bool LongRunning { get; set; }
    public bool Pooling { get; set; } = true;
    public bool WarmPool { get; set; }
    public bool Query { get; set; } = true;
    public bool Burst { get; set; }
    public bool StalledPrelogin { get; set; }

    public void Validate()
    {
        if (Workers is < 1 or > 1024 || PoolSize is < 1 or > 4096 ||
            MinimumThreads < 1 || MaximumThreads < Math.Max(Environment.ProcessorCount, MinimumThreads) ||
            MaximumThreads > 32767 || DurationSeconds is < 1 or > 120 ||
            ConnectTimeoutSeconds is < 1 or > 60 || (WarmPool && (!Pooling || StalledPrelogin)))
        {
            throw new ArgumentException("Invalid open-health settings.");
        }
    }
}

/// <summary>
/// Captures cold-start scheduling, failed and successful opens, and censored work. The dedicated
/// main thread bounds measurement even when ThreadPool continuations cannot execute.
/// </summary>
internal static class OpenAsyncHealthExperiment
{
    public static int Run(string settingsPath, string outputPath)
    {
        var settings = JsonSerializer.Deserialize<OpenAsyncHealthSettings>(
            File.ReadAllText(settingsPath)) ?? throw new ArgumentException("Missing settings.");
        settings.Validate();
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", settings.PoolV2);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseLongRunningPoolV2Open",
            settings.LongRunning);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
        ThreadPool.GetMinThreads(out int oldMinimum, out int minIo);
        ThreadPool.GetMaxThreads(out int oldMaximum, out int maxIo);
        if (!ThreadPool.SetMinThreads(settings.MinimumThreads, minIo) ||
            !ThreadPool.SetMaxThreads(settings.MaximumThreads, maxIo))
        {
            throw new InvalidOperationException("ThreadPool rejected the configured limits.");
        }

        using var stalled = settings.StalledPrelogin ? new StalledPreloginServer() : null;
        const string variable = "SQLCLIENT_HEALTH_CONNECTION_STRING";
        string? configured = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, null);
        if (!settings.StalledPrelogin && string.IsNullOrWhiteSpace(configured))
        {
            throw new ArgumentException("Set SQLCLIENT_HEALTH_CONNECTION_STRING privately.");
        }

        var builder = settings.StalledPrelogin
            ? new SqlConnectionStringBuilder
            {
                DataSource = $"tcp:127.0.0.1,{stalled!.Port}",
                UserID = "<user>", Password = "<pwd>", Encrypt = false
            }
            : new SqlConnectionStringBuilder(configured);
        builder.ApplicationName = "OpenAsyncHealth-" + Guid.NewGuid().ToString("N");
        builder.Pooling = settings.Pooling;
        builder.MaxPoolSize = settings.PoolSize;
        builder.MinPoolSize = 0;
        builder.ConnectTimeout = settings.ConnectTimeoutSeconds;
        builder.ConnectRetryCount = 0;
        builder.PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock;
        string connectionString = builder.ConnectionString;
        configured = null;

        // Sequential synchronous warmup does not deliberately pre-grow the worker ThreadPool.
        if (settings.WarmPool)
        {
            var held = new List<SqlConnection>();
            try
            {
                for (int i = 0; i < Math.Min(settings.Workers, settings.PoolSize); i++)
                {
                    var connection = new SqlConnection(connectionString);
                    held.Add(connection);
                    connection.Open();
                }
            }
            finally
            {
                foreach (SqlConnection connection in held) connection.Dispose();
            }
        }

        var samples = new ConcurrentQueue<object>();
        var errors = new ConcurrentDictionary<string, long>();
        long started = 0, opened = 0, completed = 0, failed = 0, cancelled = 0, retained = 0;
        int workersStarted = 0;
        using var cancellation = new CancellationTokenSource();
        using var observer = new ThreadPoolHealthObserver(new AsyncHealthOptions
        {
            CadenceMilliseconds = 25, ProbeCapacity = 64, RecoveryMilliseconds = 1000
        });
        var clock = Stopwatch.StartNew();
        observer.Start();
        double origin = clock.Elapsed.TotalMilliseconds;
        var tasks = new Task[settings.Workers];
        for (int i = 0; i < tasks.Length; i++)
        {
            int worker = i;
            double offered = clock.Elapsed.TotalMilliseconds - origin;
            tasks[i] = Task.Run(async () =>
            {
                Interlocked.Increment(ref workersStarted);
                do
                {
                    if (cancellation.IsCancellationRequested) break;
                    double begin = clock.Elapsed.TotalMilliseconds - origin;
                    double? openEnd = null;
                    string outcome = "Completed";
                    string? stageError = null;
                    Interlocked.Increment(ref started);
                    try
                    {
                        await using var connection = new SqlConnection(connectionString);
                        await connection.OpenAsync(cancellation.Token).ConfigureAwait(false);
                        openEnd = clock.Elapsed.TotalMilliseconds - origin;
                        Interlocked.Increment(ref opened);
                        if (settings.Query)
                        {
                            using var command = connection.CreateCommand();
                            command.CommandText = "SELECT 1";
                            command.CommandTimeout = settings.ConnectTimeoutSeconds;
                            await command.ExecuteScalarAsync(cancellation.Token).ConfigureAwait(false);
                        }
                        Interlocked.Increment(ref completed);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        outcome = "Cancelled";
                        Interlocked.Increment(ref cancelled);
                    }
                    catch (Exception error)
                    {
                        // Preserve all failure counts without persisting server or credential text.
                        outcome = "Failed";
                        stageError = (openEnd.HasValue ? "Query:" : "Open:") +
                            error.GetType().Name + (error is SqlException sql ? ":" + sql.Number : "");
                        errors.AddOrUpdate(stageError, 1, (_, count) => count + 1);
                        Interlocked.Increment(ref failed);
                        if (error is not SqlException and not InvalidOperationException
                            and not TimeoutException)
                        {
                            cancellation.Cancel();
                        }
                    }

                    double end = clock.Elapsed.TotalMilliseconds - origin;
                    if (Interlocked.Increment(ref retained) <= 100000)
                    {
                        samples.Enqueue(new { Worker = worker, OfferedMs = offered,
                            StartMs = begin, OpenEndMs = openEnd, EndMs = end, Outcome = outcome,
                            Error = stageError });
                    }
                    if (settings.Burst || cancellation.IsCancellationRequested) break;
                    if (outcome == "Failed")
                    {
                        try { await Task.Delay(250, cancellation.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { break; }
                    }
                    offered = clock.Elapsed.TotalMilliseconds - origin;
                    await Task.Yield();
                } while (!cancellation.IsCancellationRequested);
            });
        }

        Task all = Task.WhenAll(tasks);
        while (clock.Elapsed.TotalMilliseconds - origin < settings.DurationSeconds * 1000 &&
            !cancellation.IsCancellationRequested)
        {
            Thread.Sleep(10);
        }
        double cutoff = clock.Elapsed.TotalMilliseconds - origin;
        var atCutoff = new { WorkersStarted = workersStarted, Started = started, Opened = opened,
            Completed = completed, Failed = failed, Cancelled = cancelled,
            InFlight = started - completed - failed - cancelled, QueuedWorkers = settings.Workers - workersStarted };
        cancellation.Cancel();
        AsyncHealthWindow window = observer.Complete();
        long drainStart = Stopwatch.GetTimestamp();
        while (!all.IsCompleted && Stopwatch.GetElapsedTime(drainStart).TotalSeconds < 5)
        {
            Thread.Sleep(10);
        }
        bool drained = all.IsCompletedSuccessfully;
        object[] observations = samples.ToArray();
        var result = new
        {
            SchemaVersion = 1, Kind = "OpenAsyncHealthExperiment",
            Status = drained && window.ObserverError == null ? "Completed" : "Incomplete",
            Source = settings.StalledPrelogin ? "SyntheticStalledPrelogin" : "AuthenticatedSql",
            Settings = settings, Runtime = Environment.Version.ToString(),
            OS = Environment.OSVersion.Platform.ToString(), Environment.ProcessorCount,
            ProcessId = Environment.ProcessId, DriverVersion = typeof(SqlConnection).Assembly.FullName,
            CutoffMs = cutoff, AtCutoff = atCutoff, Drained = drained,
            Final = new { Started = started, Opened = opened, Completed = completed, Failed = failed,
                Cancelled = cancelled }, Errors = errors, Observations = observations,
            ObservationsDropped = Math.Max(0, retained - 100000), Measurement = window
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Open-health: {result.Status}; starts={started}; completes={completed}; failures={failed}");
        if (drained)
        {
            using var connection = new SqlConnection(connectionString);
            SqlConnection.ClearPool(connection);
        }
        ThreadPool.SetMaxThreads(oldMaximum, maxIo);
        ThreadPool.SetMinThreads(oldMinimum, minIo);
        return drained && window.ObserverError == null ? 0 : 1;
    }

    /// <summary>
    /// Negative control: accepts TCP on an ephemeral port but never answers TDS prelogin.
    /// No SQL credentials or shared network shaping are used. Not a simulated RTT or SQL server.
    /// </summary>
    private sealed class StalledPreloginServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly List<TcpClient> _clients = new();
        private readonly Thread _thread;
        private volatile bool _stop;
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public StalledPreloginServer()
        {
            _listener.Start(4096);
            _thread = new Thread(() =>
            {
                while (!_stop)
                {
                    if (_listener.Pending()) _clients.Add(_listener.AcceptTcpClient());
                    else Thread.Sleep(1);
                }
            }) { IsBackground = true, Name = "Stalled prelogin control" };
            _thread.Start();
        }

        public void Dispose()
        {
            _stop = true;
            _thread.Join();
            foreach (TcpClient client in _clients) client.Dispose();
            _listener.Stop();
        }
    }
}
