using System.Diagnostics;

using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.Samples.ThreadStarvation;

/// <summary>
/// Runs parallel SQL queries to surface thread-pool starvation and async performance issues.
/// </summary>
public class App : IDisposable
{
    // ──────────────────────────────────────────────────────────────────
    #region Construction / Disposal

    /// <inheritdoc />
    public void Dispose()
    {
        _eventListener?.Dispose();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Options

    /// <summary>
    /// Options parsed from command-line arguments.
    /// </summary>
    internal class RunOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int Connections { get; set; } = 200;
        public string Mode { get; set; } = "async";
        public bool Mars { get; set; }
        public string Launch { get; set; } = "parallel";
        public string Query { get; set; } = "SELECT 1;";
        public int SleepMs { get; set; } = 500;
        public int MinThreads { get; set; } = 1000;
        public int MaxThreads { get; set; } = 1000;
        public int IoThreads { get; set; } = 1;
        public int SlowThresholdMs { get; set; } = 200;
        public int MonitorIntervalMs { get; set; } = 100;
        public int ConnectTimeout { get; set; } = 120;
        public bool Pooling { get; set; }
        public bool LogEvents { get; set; }
        public bool Trace { get; set; }
        public bool Verbose { get; set; }
        public string OutputFormat { get; set; } = "table";
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Run

    /// <summary>
    /// Runs the thread-starvation reproducer with the specified options.
    /// </summary>
    /// <returns>0 on success; non-zero on failure.</returns>
    internal int Run(RunOptions options)
    {
        Out($"""
            ThreadStarvation — Thread-Pool Starvation Reproducer
            ----------------------------------------------------
            """);

        // Build and validate the base connection string.
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(options.ConnectionString)
            {
                ConnectTimeout = options.ConnectTimeout,
                Pooling = options.Pooling
            };
        }
        catch (Exception ex)
        {
            Err($"Failed to parse connection string: {ex.Message}");
            if (options.Verbose)
                Err($"  {ex}");
            return 1;
        }

        string baseConnectionString = builder.ConnectionString;

        Out($"""

            Connection details:
              Data Source:      {builder.DataSource}
              Initial Catalog:  {builder.InitialCatalog}
              Authentication:   {builder.Authentication}
              Pooling:          {builder.Pooling}
              Connect Timeout:  {builder.ConnectTimeout}s
            """);

        if (options.Verbose)
        {
            Out($"""

                Full connection string:
                  {builder}
                """);
        }

        Out($"""

            Test parameters:
              Connections:      {options.Connections}
              Mode:             {options.Mode}
              MARS pass:        {(options.Mars ? "yes" : "no")}
              Launch strategy:  {options.Launch}
              Query:            {options.Query}
              Sleep per query:  {options.SleepMs}ms
              Slow threshold:   {options.SlowThresholdMs}ms
              Monitor interval: {(options.MonitorIntervalMs > 0 ? $"{options.MonitorIntervalMs}ms" : "disabled")}
              Min threads:      {options.MinThreads}
              Max threads:      {options.MaxThreads}
              IO threads:       {options.IoThreads}
            """);

        // Enable SqlClient event logging if requested.
        if (options.LogEvents)
        {
            string prefix = "[EVENT]";
            Out($"\nSqlClient event logging enabled; events prefixed with {prefix}");
            _eventListener = new SqlClientEventListener(Out, prefix);
        }

        // Pause for trace attachment if requested.
        if (options.Trace)
        {
            Out($"""

                PID: {Process.GetCurrentProcess().Id}
                Attach dotnet-trace and press Enter to resume:

                  dotnet-trace collect -p {Process.GetCurrentProcess().Id} --providers Microsoft.Data.SqlClient.EventSource:1FFF:5

                """);
            Console.ReadLine();
        }

        // Configure thread pool.
        ThreadPool.SetMinThreads(options.MinThreads, options.IoThreads);
        ThreadPool.SetMaxThreads(options.MaxThreads, options.IoThreads);
        PrintThreadPoolInfo("Initial");

        // Capture unhandled exceptions.
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Err($"Unhandled exception: {eventArgs.ExceptionObject}");
        };

        try
        {
            if (options.Mode == "preconnect")
            {
                RunPreconnectMode(baseConnectionString, options).GetAwaiter().GetResult();
            }
            else
            {
                // Determine which passes to run.
                List<PassConfig> passes = BuildPasses(options, baseConnectionString);

                foreach (PassConfig pass in passes)
                {
                    RunPass(pass, options).GetAwaiter().GetResult();
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Err($"Fatal error: {ex.Message}");
            if (options.Verbose)
                Err($"  {ex}");
            return 1;
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Pass Configuration

    /// <summary>
    /// Describes a single test pass.
    /// </summary>
    private record PassConfig(string Label, string ConnectionString, bool IsSync);

    /// <summary>
    /// Builds the list of test passes based on options.
    /// </summary>
    private static List<PassConfig> BuildPasses(RunOptions options, string baseConnectionString)
    {
        List<PassConfig> passes = new();

        // Build MARS variant of connection string.
        SqlConnectionStringBuilder marsBuilder = new(baseConnectionString)
        {
            MultipleActiveResultSets = true
        };
        string marsConnectionString = marsBuilder.ConnectionString;

        switch (options.Mode)
        {
            case "sync":
                passes.Add(new("Sync", baseConnectionString, IsSync: true));
                if (options.Mars)
                    passes.Add(new("Sync+MARS", marsConnectionString, IsSync: true));
                break;

            case "async":
                passes.Add(new("Async", baseConnectionString, IsSync: false));
                if (options.Mars)
                    passes.Add(new("Async+MARS", marsConnectionString, IsSync: false));
                break;

            case "both":
                passes.Add(new("Sync", baseConnectionString, IsSync: true));
                passes.Add(new("Async", baseConnectionString, IsSync: false));
                if (options.Mars)
                {
                    passes.Add(new("Sync+MARS", marsConnectionString, IsSync: true));
                    passes.Add(new("Async+MARS", marsConnectionString, IsSync: false));
                }
                break;
        }

        return passes;
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Test Execution

    /// <summary>
    /// Runs a single test pass.
    /// </summary>
    private async Task RunPass(PassConfig pass, RunOptions options)
    {
        Out($"""

            ══════════════════════════════════════════════════════════
              Pass: {pass.Label}  ({options.Connections} connections)
            ══════════════════════════════════════════════════════════
            """);

        // Warm up: single query to pre-fetch tokens, warm caches, etc.
        Out("Warming up...");
        if (pass.IsSync)
            ExecuteQuerySync(pass.ConnectionString, 0, options, throwOnError: true);
        else
            await ExecuteQueryAsync(pass.ConnectionString, 0, CancellationToken.None, options,
                throwOnError: true);
        Out("Warm-up complete.\n");

        Out($"Executing {options.Connections} parallel queries...\n");

        // Start thread-pool monitor.
        using CancellationTokenSource cts = new();
        Task? monitorTask = null;
        if (options.MonitorIntervalMs > 0)
        {
            monitorTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(options.MonitorIntervalMs, cts.Token).ConfigureAwait(false);
                    PrintThreadPoolInfo("Monitor");
                }
            }, cts.Token);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        int errorCount;
        int slowCount;
        long[] latencies;
        int latencyCount;

        if (pass.IsSync)
        {
            (errorCount, slowCount, latencies, latencyCount) = RunSyncPass(pass, options);
        }
        else if (options.Launch == "whenall")
        {
            (errorCount, slowCount, latencies, latencyCount) = await RunAsyncWhenAllPass(pass, options, cts.Token);
        }
        else
        {
            (errorCount, slowCount, latencies, latencyCount) = await RunAsyncParallelPass(pass, options, cts.Token);
        }

        stopwatch.Stop();
        cts.Cancel();

        if (monitorTask != null)
        {
            try { await monitorTask; }
            catch (OperationCanceledException) { }
        }

        PrintPassResults(pass.Label, stopwatch.ElapsedMilliseconds, options,
            errorCount, slowCount, latencies, latencyCount);
        PrintThreadPoolInfo("Final");
    }

    /// <summary>
    /// Runs the async pass using Parallel.ForEachAsync with optional Thread.Sleep injection.
    /// </summary>
    private async Task<(int errorCount, int slowCount, long[] latencies, int latencyCount)> RunAsyncParallelPass(
        PassConfig pass, RunOptions options, CancellationToken ct)
    {
        int errors = 0;
        int slows = 0;
        long[] latencies = new long[options.Connections];
        int latencyCount = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(1, options.Connections),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Connections,
                CancellationToken = ct
            },
            async (i, token) =>
            {
                if (options.SleepMs > 0)
                    Thread.Sleep(options.SleepMs);

                Stopwatch sw = Stopwatch.StartNew();
                (bool ok, bool slow) = await ExecuteQueryAsync(
                    pass.ConnectionString, i, token, options);
                sw.Stop();

                if (ok)
                    latencies[Interlocked.Increment(ref latencyCount) - 1] = sw.ElapsedMilliseconds;
                if (!ok) Interlocked.Increment(ref errors);
                if (slow) Interlocked.Increment(ref slows);
            });

        return (errors, slows, latencies, latencyCount);
    }

    /// <summary>
    /// Runs the async pass using Task.WhenAll — fires all queries concurrently without
    /// the Parallel.ForEachAsync scheduling or Thread.Sleep throttle.
    /// </summary>
    private async Task<(int errorCount, int slowCount, long[] latencies, int latencyCount)> RunAsyncWhenAllPass(
        PassConfig pass, RunOptions options, CancellationToken ct)
    {
        int errors = 0;
        int slows = 0;
        long[] latencies = new long[options.Connections];
        int latencyCount = 0;

        Task[] tasks = new Task[options.Connections];
        for (int i = 0; i < options.Connections; i++)
        {
            int id = i + 1;
            tasks[i] = Task.Run(async () =>
            {
                if (options.SleepMs > 0)
                    Thread.Sleep(options.SleepMs);

                Stopwatch sw = Stopwatch.StartNew();
                (bool ok, bool slow) = await ExecuteQueryAsync(
                    pass.ConnectionString, id, ct, options);
                sw.Stop();

                if (ok)
                    latencies[Interlocked.Increment(ref latencyCount) - 1] = sw.ElapsedMilliseconds;
                if (!ok) Interlocked.Increment(ref errors);
                if (slow) Interlocked.Increment(ref slows);
            }, ct);
        }

        await Task.WhenAll(tasks);

        return (errors, slows, latencies, latencyCount);
    }

    /// <summary>
    /// Runs sync queries via Parallel.ForEach with optional Thread.Sleep injection.
    /// </summary>
    private (int errorCount, int slowCount, long[] latencies, int latencyCount) RunSyncPass(
        PassConfig pass, RunOptions options)
    {
        int errors = 0;
        int slows = 0;
        long[] latencies = new long[options.Connections];
        int latencyCount = 0;

        Parallel.ForEach(
            Enumerable.Range(1, options.Connections),
            new ParallelOptions { MaxDegreeOfParallelism = options.Connections },
            i =>
            {
                if (options.SleepMs > 0)
                    Thread.Sleep(options.SleepMs);

                Stopwatch sw = Stopwatch.StartNew();
                (bool ok, bool slow) = ExecuteQuerySync(
                    pass.ConnectionString, i, options);
                sw.Stop();

                if (ok)
                    latencies[Interlocked.Increment(ref latencyCount) - 1] = sw.ElapsedMilliseconds;
                if (!ok) Interlocked.Increment(ref errors);
                if (slow) Interlocked.Increment(ref slows);
            });

        return (errors, slows, latencies, latencyCount);
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Preconnect Mode

    /// <summary>
    /// Opens all connections up front, then runs queries on them — first sync, then async.
    /// This isolates query execution time from connection establishment time.
    /// </summary>
    private async Task RunPreconnectMode(string baseConnectionString, RunOptions options)
    {
        Out($"""

            ══════════════════════════════════════════════════════════
              Preconnect Mode  ({options.Connections} connections)
            ══════════════════════════════════════════════════════════
            """);

        // ── Phase 1: Open all connections ────────────────────────────
        Out($"Opening {options.Connections} connections...");
        Stopwatch connectSw = Stopwatch.StartNew();

        SqlConnection[] connections = new SqlConnection[options.Connections];
        try
        {
            for (int i = 0; i < options.Connections; i++)
            {
                connections[i] = new SqlConnection(baseConnectionString);
                await connections[i].OpenAsync().ConfigureAwait(false);
            }

            connectSw.Stop();
            Out($"All {options.Connections} connections opened in {connectSw.ElapsedMilliseconds}ms.\n");
            PrintThreadPoolInfo("Post-Connect");

            // ── Phase 2: Sync queries on pre-opened connections ─────
            Out($"""

                ── Preconnect: Sync Pass ──────────────────────────────
                """);

            using CancellationTokenSource syncCts = new();
            Task? syncMonitor = StartMonitor(syncCts.Token, options);

            Stopwatch syncSw = Stopwatch.StartNew();
            int syncErrors = 0;
            int syncSlows = 0;
            long[] syncLatencies = new long[options.Connections];
            int syncLatencyCount = 0;

            Parallel.For(0, options.Connections,
                new ParallelOptions { MaxDegreeOfParallelism = options.Connections },
                i =>
                {
                    if (options.SleepMs > 0)
                        Thread.Sleep(options.SleepMs);

                    Stopwatch sw = Stopwatch.StartNew();
                    (bool ok, bool slow) = ExecuteQueryOnOpenConnectionSync(
                        connections[i], i + 1, options);
                    sw.Stop();

                    if (ok)
                        syncLatencies[Interlocked.Increment(ref syncLatencyCount) - 1] = sw.ElapsedMilliseconds;
                    if (!ok) Interlocked.Increment(ref syncErrors);
                    if (slow) Interlocked.Increment(ref syncSlows);
                });

            syncSw.Stop();
            syncCts.Cancel();
            await StopMonitor(syncMonitor);

            PrintPassResults("Preconnect Sync", syncSw.ElapsedMilliseconds, options,
                syncErrors, syncSlows, syncLatencies, syncLatencyCount);
            PrintThreadPoolInfo("Post-Sync");

            // ── Phase 3: Async queries on pre-opened connections ────
            Out($"""

                ── Preconnect: Async Pass ─────────────────────────────
                """);

            using CancellationTokenSource asyncCts = new();
            Task? asyncMonitor = StartMonitor(asyncCts.Token, options);

            Stopwatch asyncSw = Stopwatch.StartNew();
            int asyncErrors = 0;
            int asyncSlows = 0;
            long[] asyncLatencies = new long[options.Connections];
            int asyncLatencyCount = 0;

            Task[] tasks = new Task[options.Connections];
            for (int i = 0; i < options.Connections; i++)
            {
                int id = i + 1;
                SqlConnection conn = connections[i];
                tasks[i] = Task.Run(async () =>
                {
                    if (options.SleepMs > 0)
                        Thread.Sleep(options.SleepMs);

                    Stopwatch sw = Stopwatch.StartNew();
                    (bool ok, bool slow) = await ExecuteQueryOnOpenConnectionAsync(
                        conn, id, CancellationToken.None, options);
                    sw.Stop();

                    if (ok)
                        asyncLatencies[Interlocked.Increment(ref asyncLatencyCount) - 1] = sw.ElapsedMilliseconds;
                    if (!ok) Interlocked.Increment(ref asyncErrors);
                    if (slow) Interlocked.Increment(ref asyncSlows);
                });
            }

            await Task.WhenAll(tasks);

            asyncSw.Stop();
            asyncCts.Cancel();
            await StopMonitor(asyncMonitor);

            PrintPassResults("Preconnect Async", asyncSw.ElapsedMilliseconds, options,
                asyncErrors, asyncSlows, asyncLatencies, asyncLatencyCount);
            PrintThreadPoolInfo("Post-Async");
        }
        finally
        {
            // Dispose all connections.
            foreach (SqlConnection? conn in connections)
            {
                conn?.Dispose();
            }
        }
    }

    /// <summary>
    /// Executes a query synchronously on an already-open connection.
    /// </summary>
    private static (bool ok, bool slow) ExecuteQueryOnOpenConnectionSync(
        SqlConnection connection, int connectionId, RunOptions options)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            using SqlCommand command = new(options.Query, connection);
            command.CommandTimeout = 30;

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = reader.GetValue(0);
            }

            sw.Stop();
            bool slow = sw.ElapsedMilliseconds > options.SlowThresholdMs;
            if (slow)
                Out($"[{connectionId}] SLOW: {sw.ElapsedMilliseconds}ms");
            else if (connectionId > 0 && connectionId % 100 == 0)
                Out($"[{connectionId}] {sw.ElapsedMilliseconds}ms");

            return (true, slow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Out($"[{connectionId}] ERROR after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            return (false, false);
        }
    }

    /// <summary>
    /// Executes a query asynchronously on an already-open connection.
    /// </summary>
    private static async Task<(bool ok, bool slow)> ExecuteQueryOnOpenConnectionAsync(
        SqlConnection connection, int connectionId, CancellationToken ct, RunOptions options)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            using SqlCommand command = new(options.Query, connection);
            command.CommandTimeout = 30;

            using SqlDataReader reader = await command.ExecuteReaderAsync(ct)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                _ = reader.GetValue(0);
            }

            sw.Stop();
            bool slow = sw.ElapsedMilliseconds > options.SlowThresholdMs;
            if (slow)
                Out($"[{connectionId}] SLOW: {sw.ElapsedMilliseconds}ms");
            else if (connectionId > 0 && connectionId % 100 == 0)
                Out($"[{connectionId}] {sw.ElapsedMilliseconds}ms");

            return (true, slow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Out($"[{connectionId}] ERROR after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            return (false, false);
        }
    }

    /// <summary>
    /// Starts a thread-pool monitor task if monitoring is enabled.
    /// </summary>
    private static Task? StartMonitor(CancellationToken ct, RunOptions options)
    {
        if (options.MonitorIntervalMs <= 0)
            return null;

        return Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(options.MonitorIntervalMs, ct).ConfigureAwait(false);
                PrintThreadPoolInfo("Monitor");
            }
        }, ct);
    }

    /// <summary>
    /// Stops a monitor task gracefully.
    /// </summary>
    private static async Task StopMonitor(Task? monitorTask)
    {
        if (monitorTask != null)
        {
            try { await monitorTask; }
            catch (OperationCanceledException) { }
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Query Execution

    /// <summary>
    /// Executes a single query asynchronously.
    /// </summary>
    /// <returns>(success, wasSlow)</returns>
    private static async Task<(bool ok, bool slow)> ExecuteQueryAsync(
        string connectionString, int connectionId, CancellationToken ct,
        RunOptions options, bool throwOnError = false)
    {
        using SqlConnection connection = new(connectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Out($"[{connectionId}] CONNECTION ERROR: {ex.Message}");
            if (throwOnError) throw;
            return (false, false);
        }

        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            using SqlCommand command = new(options.Query, connection);
            command.CommandTimeout = 30;

            using SqlDataReader reader = await command.ExecuteReaderAsync(ct)
                .ConfigureAwait(false);

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                _ = reader.GetValue(0);
            }

            sw.Stop();

            bool slow = sw.ElapsedMilliseconds > options.SlowThresholdMs;
            if (slow)
                Out($"[{connectionId}] SLOW: {sw.ElapsedMilliseconds}ms");
            else if (connectionId > 0 && connectionId % 100 == 0)
                Out($"[{connectionId}] {sw.ElapsedMilliseconds}ms");

            return (true, slow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Out($"[{connectionId}] ERROR after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            if (options.Verbose)
                Out($"  {ex}");
            if (throwOnError) throw;
            return (false, false);
        }
    }

    /// <summary>
    /// Executes a single query synchronously.
    /// </summary>
    /// <returns>(success, wasSlow)</returns>
    private static (bool ok, bool slow) ExecuteQuerySync(
        string connectionString, int connectionId, RunOptions options,
        bool throwOnError = false)
    {
        using SqlConnection connection = new(connectionString);
        try
        {
            connection.Open();
        }
        catch (Exception ex)
        {
            Out($"[{connectionId}] CONNECTION ERROR: {ex.Message}");
            if (throwOnError) throw;
            return (false, false);
        }

        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            using SqlCommand command = new(options.Query, connection);
            command.CommandTimeout = 30;

            using SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                _ = reader.GetValue(0);
            }

            sw.Stop();

            bool slow = sw.ElapsedMilliseconds > options.SlowThresholdMs;
            if (slow)
                Out($"[{connectionId}] SLOW: {sw.ElapsedMilliseconds}ms");
            else if (connectionId > 0 && connectionId % 100 == 0)
                Out($"[{connectionId}] {sw.ElapsedMilliseconds}ms");

            return (true, slow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Out($"[{connectionId}] ERROR after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            if (options.Verbose)
                Out($"  {ex}");
            if (throwOnError) throw;
            return (false, false);
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Helpers

    /// <summary>
    /// Prints current thread-pool worker and IOCP thread usage.
    /// </summary>
    private static void PrintThreadPoolInfo(string label)
    {
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIo);
        ThreadPool.GetAvailableThreads(out int availWorker, out int availIo);

        Out($"[{label}] Worker: {availWorker}/{maxWorker} | IOCP: {availIo}/{maxIo}");
    }

    /// <summary>
    /// Writes an informational message to standard output.
    /// </summary>
    internal static void Out(string message)
    {
        Console.Out.WriteLine(message);
    }

    /// <summary>
    /// Writes an error message to standard error in red.
    /// </summary>
    internal static void Err(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Statistics & Formatting

    private record LatencyStats(
        int Count, long Min, long Max, double Avg,
        long P50, long P95, long P99);

    private static LatencyStats ComputeStats(long[] latencies, int count)
    {
        if (count == 0)
            return new(0, 0, 0, 0, 0, 0, 0);

        Array.Sort(latencies, 0, count);
        long sum = 0;
        for (int i = 0; i < count; i++)
            sum += latencies[i];

        return new(
            count,
            latencies[0],
            latencies[count - 1],
            (double)sum / count,
            Percentile(latencies, count, 50),
            Percentile(latencies, count, 95),
            Percentile(latencies, count, 99));
    }

    private static long Percentile(long[] sorted, int count, int p)
    {
        double rank = p / 100.0 * (count - 1);
        int lower = (int)rank;
        int upper = lower + 1;
        if (upper >= count)
            return sorted[count - 1];
        double frac = rank - lower;
        return (long)(sorted[lower] + frac * (sorted[upper] - sorted[lower]));
    }

    private static void PrintPassResults(
        string label, long totalMs, RunOptions options,
        int errorCount, int slowCount, long[] latencies, int latencyCount)
    {
        LatencyStats stats = ComputeStats(latencies, latencyCount);

        switch (options.OutputFormat)
        {
            case "json":
                PrintResultsJson(label, totalMs, options, errorCount, slowCount, stats);
                break;
            case "histogram":
                PrintResultsTable(label, totalMs, options, errorCount, slowCount, stats);
                PrintHistogram(latencies, latencyCount);
                break;
            default: // "table"
                PrintResultsTable(label, totalMs, options, errorCount, slowCount, stats);
                break;
        }
    }

    private static void PrintResultsTable(
        string label, long totalMs, RunOptions options,
        int errorCount, int slowCount, LatencyStats stats)
    {
        Out($"""

            ── Results: {label} ──────────────────────
              Total time:    {totalMs}ms
              Per query avg: {totalMs / Math.Max(options.Connections, 1)}ms
              Slow queries:  {slowCount} (>{options.SlowThresholdMs}ms)
              Errors:        {errorCount}
            """);

        if (stats.Count > 0)
        {
            Out($"""
              Latency (ms):
                Min:  {stats.Min}
                Max:  {stats.Max}
                Avg:  {stats.Avg:F1}
                P50:  {stats.P50}
                P95:  {stats.P95}
                P99:  {stats.P99}
            """);
        }
    }

    private static void PrintResultsJson(
        string label, long totalMs, RunOptions options,
        int errorCount, int slowCount, LatencyStats stats)
    {
        var result = new
        {
            pass = label,
            totalMs,
            perQueryAvgMs = totalMs / Math.Max(options.Connections, 1),
            slowQueries = slowCount,
            slowThresholdMs = options.SlowThresholdMs,
            errors = errorCount,
            latency = new
            {
                count = stats.Count,
                minMs = stats.Min,
                maxMs = stats.Max,
                avgMs = Math.Round(stats.Avg, 1),
                p50Ms = stats.P50,
                p95Ms = stats.P95,
                p99Ms = stats.P99
            }
        };

        Out(System.Text.Json.JsonSerializer.Serialize(result,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static void PrintHistogram(long[] latencies, int count)
    {
        if (count == 0)
            return;

        long[] bucketCeilings = { 1, 2, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };
        int bucketCount = bucketCeilings.Length + 1;
        int[] buckets = new int[bucketCount];

        for (int i = 0; i < count; i++)
        {
            long val = latencies[i];
            int b = 0;
            while (b < bucketCeilings.Length && val > bucketCeilings[b])
                b++;
            buckets[b]++;
        }

        int maxBucket = 0;
        for (int i = 0; i < bucketCount; i++)
            if (buckets[i] > maxBucket)
                maxBucket = buckets[i];

        const int barWidth = 40;
        Out("\n  Latency Histogram (ms):");

        for (int i = 0; i < bucketCount; i++)
        {
            if (buckets[i] == 0)
                continue;

            string rangeLabel;
            if (i == 0)
                rangeLabel = $"    0-{bucketCeilings[0],5}";
            else if (i < bucketCeilings.Length)
                rangeLabel = $"{bucketCeilings[i - 1],5}-{bucketCeilings[i],5}";
            else
                rangeLabel = $"{bucketCeilings[^1],5}+    ";

            int barLen = maxBucket > 0 ? (int)((long)buckets[i] * barWidth / maxBucket) : 0;
            if (buckets[i] > 0 && barLen == 0)
                barLen = 1;

            string bar = new('\u2588', barLen);
            Out($"  {rangeLabel} |{bar} {buckets[i]}");
        }
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Private Fields

    /// <summary>
    /// The optional event listener used to capture SqlClient diagnostic events.
    /// </summary>
    private SqlClientEventListener? _eventListener;

    #endregion
}
