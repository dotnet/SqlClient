// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.PerformanceTests;

internal sealed class OpenAsyncHealthSettings
{
    public int Workers { get; set; } = 64;
    public int PoolSize { get; set; } = 100;
    public int MinimumThreads { get; set; } = 16;
    public int DurationSeconds { get; set; } = 8;
    public int ConnectTimeoutSeconds { get; set; } = 15;
    public bool PoolV2 { get; set; }
    public bool Pooling { get; set; } = true;
    public bool WarmPool { get; set; }
    public bool WarmDriver { get; set; }
    public bool UseManagedSni { get; set; } = true;
    public bool UseOptimizedAsync { get; set; } = true;
    public bool Query { get; set; } = true;
    public bool StalledPrelogin { get; set; }

    public void Validate()
    {
        if (Workers is < 1 or > 1024 || PoolSize is < 1 or > 4096 ||
            MinimumThreads is < 1 or > 32767 || DurationSeconds is < 1 or > 120 ||
            ConnectTimeoutSeconds is < 1 or > 60 ||
            (WarmPool && (!Pooling || StalledPrelogin)) ||
            (WarmDriver && (WarmPool || StalledPrelogin)) ||
            (!UseManagedSni && !OperatingSystem.IsWindows()))
        {
            throw new ArgumentException("Invalid OpenAsync health settings.");
        }
    }
}

/// <summary>Fresh-process open/query/close workload, bounded by the dedicated main thread.</summary>
internal static class OpenAsyncHealthExperiment
{
    public static int Run(string settingsPath, string outputPath)
    {
        var settings = JsonSerializer.Deserialize<OpenAsyncHealthSettings>(
            File.ReadAllText(settingsPath), new JsonSerializerOptions
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            }) ?? throw new ArgumentException("Missing settings.");
        settings.Validate();

        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", settings.PoolV2);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows",
            settings.UseManagedSni);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityAsyncBehaviour",
            !settings.UseOptimizedAsync);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseCompatibilityProcessSni",
            !settings.UseOptimizedAsync);

        // Historical drivers may silently ignore this switch. Never label V1 work as V2.
        PropertyInfo? poolSwitch = typeof(SqlConnection).Assembly.GetType(
            "Microsoft.Data.SqlClient.LocalAppContextSwitches")?.GetProperty(
                "UseConnectionPoolV2", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        bool supportsV2 = poolSwitch != null;
        if (settings.Pooling && settings.PoolV2 && !supportsV2)
        {
            Write(outputPath, new
            {
                SchemaVersion = 1, Status = "Unsupported", Reason = "PoolV2Unavailable",
                Settings = settings, DriverVersion = typeof(SqlConnection).Assembly.FullName,
                DriverSha256 = DriverHash()
            });
            return 0;
        }
        if (poolSwitch != null && !Equals(poolSwitch.GetValue(null), settings.PoolV2))
        {
            throw new InvalidOperationException("Driver did not apply the requested pool switch.");
        }

        ThreadPool.GetMinThreads(out _, out int minimumIo);
        if (!ThreadPool.SetMinThreads(settings.MinimumThreads, minimumIo))
        {
            throw new InvalidOperationException("ThreadPool rejected the configured minimum.");
        }
        ThreadPool.GetMaxThreads(out int maximumThreads, out _);

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

        if (settings.WarmDriver)
        {
            using var initialization = new SqlConnection(connectionString);
            initialization.Open();
            initialization.Close();
            if (settings.Pooling) SqlConnection.ClearPool(initialization);
        }
        if (settings.WarmPool)
        {
            // Hold all warmup connections concurrently so the pool really contains this many.
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

        var records = new OpenAsyncHealthRecorder(settings.Workers);
        var errors = new ConcurrentDictionary<string, long>();
        using var cancellation = new CancellationTokenSource();
        using var observer = new ThreadPoolHealthObserver();
        var clock = Stopwatch.StartNew();
        observer.Start();
        double origin = clock.Elapsed.TotalMilliseconds;
        double Timestamp() => clock.Elapsed.TotalMilliseconds - origin;
        var tasks = new Task[settings.Workers];
        for (int i = 0; i < tasks.Length; i++)
        {
            int worker = i;
            double offered = Timestamp();
            tasks[i] = Task.Run(async () =>
            {
                bool first = true;
                while (!cancellation.IsCancellationRequested)
                {
                    double begin = Timestamp();
                    double? openEnd = null;
                    string outcome = "Completed";
                    records.Start(first);
                    try
                    {
                        using var connection = new SqlConnection(connectionString);
                        Task open = connection.OpenAsync(cancellation.Token);
                        records.OpenReturned(open.IsCompleted);
                        await open.ConfigureAwait(false);
                        openEnd = Timestamp();
                        records.Opened();
                        if (settings.Query)
                        {
                            using var command = connection.CreateCommand();
                            command.CommandText = "SELECT 1";
                            command.CommandTimeout = settings.ConnectTimeoutSeconds;
                            await command.ExecuteScalarAsync(cancellation.Token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        outcome = "Cancelled";
                    }
                    catch (Exception error)
                    {
                        outcome = "Failed";
                        string key = (openEnd.HasValue ? "Query:" : "Open:") +
                            error.GetType().Name + (error is SqlException sql ? ":" + sql.Number : "");
                        errors.AddOrUpdate(key, 1, (_, count) => count + 1);
                        if (error is not SqlException and not InvalidOperationException
                            and not TimeoutException)
                        {
                            await cancellation.CancelAsync().ConfigureAwait(false);
                        }
                    }

                    records.Finish(new(worker, offered, begin, openEnd, Timestamp(), outcome));
                    first = false;
                    if (outcome == "Failed")
                    {
                        try { await Task.Delay(250, cancellation.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { break; }
                    }
                    offered = Timestamp();
                    await Task.Yield();
                }
            });
        }

        Task all = Task.WhenAll(tasks);
        while (Timestamp() < settings.DurationSeconds * 1000 && !cancellation.IsCancellationRequested)
        {
            Thread.Sleep(10);
        }
        double cutoff = Timestamp();
        OpenAsyncHealthCounts atCutoff = records.Counts();
        // CancelAsync signals the token immediately without blocking this thread on driver callbacks.
        Task cancel = cancellation.CancelAsync();
        AsyncHealthWindow window = observer.Complete();
        long drainStart = Stopwatch.GetTimestamp();
        while ((!all.IsCompleted || !cancel.IsCompleted) &&
            Stopwatch.GetElapsedTime(drainStart).TotalSeconds < 5)
        {
            Thread.Sleep(10);
        }

        bool drained = all.IsCompletedSuccessfully && cancel.IsCompletedSuccessfully;
        var snapshot = records.Snapshot();
        var result = new
        {
            SchemaVersion = 1, Status = drained && window.ObserverError == null ? "Completed" : "Incomplete",
            Source = settings.StalledPrelogin ? "SyntheticStalledPrelogin" : "AuthenticatedSql",
            Settings = settings, Runtime = Environment.Version.ToString(),
            OS = Environment.OSVersion.Platform.ToString(), Environment.ProcessorCount,
            MaximumThreads = maximumThreads, DriverVersion = typeof(SqlConnection).Assembly.FullName,
            DriverSha256 = DriverHash(),
            ProcessId = Environment.ProcessId, SupportsPoolV2 = supportsV2,
            CutoffMs = cutoff, AtCutoff = atCutoff, Drained = drained, Final = records.Counts(),
            Errors = errors, snapshot.Observations, ObservationsDropped = snapshot.Dropped,
            Measurement = window
        };
        Write(outputPath, result);
        Console.WriteLine($"OpenAsync health: {result.Status}; completed={atCutoff.Completed}; " +
            $"failed={atCutoff.Failed}");
        // A fresh process exits now. Do not block evidence publication on pool cleanup.
        return drained && window.ObserverError == null ? 0 : 1;
    }

    private static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string DriverHash() => Convert.ToHexString(SHA256.HashData(
        File.ReadAllBytes(typeof(SqlConnection).Assembly.Location)));

    /// <summary>SQL-independent negative control; accepts TCP but never answers TDS prelogin.</summary>
    private sealed class StalledPreloginServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly List<TcpClient> _clients = [];
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
