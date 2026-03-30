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
            // Determine which passes to run.
            List<PassConfig> passes = BuildPasses(options, baseConnectionString);

            foreach (PassConfig pass in passes)
            {
                RunPass(pass, options).GetAwaiter().GetResult();
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

        if (pass.IsSync)
        {
            (errorCount, slowCount) = RunSyncPass(pass, options);
        }
        else if (options.Launch == "whenall")
        {
            (errorCount, slowCount) = await RunAsyncWhenAllPass(pass, options, cts.Token);
        }
        else
        {
            (errorCount, slowCount) = await RunAsyncParallelPass(pass, options, cts.Token);
        }

        stopwatch.Stop();
        cts.Cancel();

        if (monitorTask != null)
        {
            try { await monitorTask; }
            catch (OperationCanceledException) { }
        }

        Out($"""

            ── Results: {pass.Label} ──────────────────────
              Total time:    {stopwatch.ElapsedMilliseconds}ms
              Per query avg: {stopwatch.ElapsedMilliseconds / options.Connections}ms
              Slow queries:  {slowCount} (>{options.SlowThresholdMs}ms)
              Errors:        {errorCount}
            """);
        PrintThreadPoolInfo("Final");
    }

    /// <summary>
    /// Runs the async pass using Parallel.ForEachAsync with optional Thread.Sleep injection.
    /// </summary>
    private async Task<(int errorCount, int slowCount)> RunAsyncParallelPass(
        PassConfig pass, RunOptions options, CancellationToken ct)
    {
        int errors = 0;
        int slows = 0;

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

                (bool ok, bool slow) = await ExecuteQueryAsync(
                    pass.ConnectionString, i, token, options);
                if (!ok) Interlocked.Increment(ref errors);
                if (slow) Interlocked.Increment(ref slows);
            });

        return (errors, slows);
    }

    /// <summary>
    /// Runs the async pass using Task.WhenAll — fires all queries concurrently without
    /// the Parallel.ForEachAsync scheduling or Thread.Sleep throttle.
    /// </summary>
    private async Task<(int errorCount, int slowCount)> RunAsyncWhenAllPass(
        PassConfig pass, RunOptions options, CancellationToken ct)
    {
        int errors = 0;
        int slows = 0;

        Task[] tasks = new Task[options.Connections];
        for (int i = 0; i < options.Connections; i++)
        {
            int id = i + 1;
            tasks[i] = Task.Run(async () =>
            {
                if (options.SleepMs > 0)
                    Thread.Sleep(options.SleepMs);

                (bool ok, bool slow) = await ExecuteQueryAsync(
                    pass.ConnectionString, id, ct, options);
                if (!ok) Interlocked.Increment(ref errors);
                if (slow) Interlocked.Increment(ref slows);
            }, ct);
        }

        await Task.WhenAll(tasks);

        return (errors, slows);
    }

    /// <summary>
    /// Runs sync queries via Parallel.ForEach with optional Thread.Sleep injection.
    /// </summary>
    private (int errorCount, int slowCount) RunSyncPass(
        PassConfig pass, RunOptions options)
    {
        int errors = 0;
        int slows = 0;

        Parallel.ForEach(
            Enumerable.Range(1, options.Connections),
            new ParallelOptions { MaxDegreeOfParallelism = options.Connections },
            i =>
            {
                if (options.SleepMs > 0)
                    Thread.Sleep(options.SleepMs);

                (bool ok, bool slow) = ExecuteQuerySync(
                    pass.ConnectionString, i, options);
                if (!ok) Interlocked.Increment(ref errors);
                if (slow) Interlocked.Increment(ref slows);
            });

        return (errors, slows);
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
    #region Private Fields

    /// <summary>
    /// The optional event listener used to capture SqlClient diagnostic events.
    /// </summary>
    private SqlClientEventListener? _eventListener;

    #endregion
}
