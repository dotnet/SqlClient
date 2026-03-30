using System.CommandLine;

namespace Microsoft.Data.SqlClient.Samples.ThreadStarvation;

/// <summary>
/// Contains the application entry point responsible for parsing command-line arguments and
/// delegating execution to <see cref="App"/>.
/// </summary>
public static class EntryPoint
{
    /// <summary>
    /// Application entry point. Parses command-line arguments and executes the test.
    /// </summary>
    public static int Main(string[] args)
    {
        // ── Connection ──────────────────────────────────────────────
        Option<string> connectionStringOption = new("--connection-string", "-c")
        {
            Description =
                "ADO.NET connection string. " +
                "Must include Data Source. Authentication keywords are optional.",
            Required = true
        };

        // ── Test shape ──────────────────────────────────────────────
        Option<int> connectionsOption = new("--connections", "-n")
        {
            Description = "Number of parallel connections/queries per pass.",
            DefaultValueFactory = _ => 200
        };

        Option<string> modeOption = new("--mode", "-m")
        {
            Description =
                "Execution mode: async, sync, or both. " +
                "'both' runs sync first, then async, for direct comparison.",
            DefaultValueFactory = _ => "async"
        };
        modeOption.AcceptOnlyFromAmong("async", "sync", "both");

        Option<bool> marsOption = new("--mars")
        {
            Description =
                "Also run a MARS (MultipleActiveResultSets) pass after the primary pass(es)."
        };

        Option<string> launchOption = new("--launch")
        {
            Description =
                "Launch strategy: 'parallel' uses Parallel.ForEachAsync; " +
                "'whenall' fires all tasks with Task.WhenAll.",
            DefaultValueFactory = _ => "parallel"
        };
        launchOption.AcceptOnlyFromAmong("parallel", "whenall");

        Option<string> queryOption = new("--query", "-q")
        {
            Description = "SQL query to execute per connection.",
            DefaultValueFactory = _ => "SELECT 1;"
        };

        // ── Thread pool / starvation ────────────────────────────────
        Option<int> sleepOption = new("--sleep")
        {
            Description =
                "Thread.Sleep duration (ms) injected before each query " +
                "to simulate app-side thread blocking. 0 disables.",
            DefaultValueFactory = _ => 500
        };

        Option<int> minThreadsOption = new("--min-threads")
        {
            Description = "ThreadPool minimum worker threads.",
            DefaultValueFactory = _ => 1000
        };

        Option<int> maxThreadsOption = new("--max-threads")
        {
            Description = "ThreadPool maximum worker threads.",
            DefaultValueFactory = _ => 1000
        };

        Option<int> ioThreadsOption = new("--io-threads")
        {
            Description = "ThreadPool min and max IOCP threads (no effect on Unix).",
            DefaultValueFactory = _ => 1
        };

        // ── Thresholds / monitoring ─────────────────────────────────
        Option<int> slowOption = new("--slow")
        {
            Description = "Per-query latency threshold (ms) for SLOW logging.",
            DefaultValueFactory = _ => 200
        };

        Option<int> monitorOption = new("--monitor-interval")
        {
            Description = "Thread-pool monitor print interval (ms). 0 disables.",
            DefaultValueFactory = _ => 100
        };

        Option<int> connectTimeoutOption = new("--connect-timeout")
        {
            Description = "Connection timeout in seconds (overrides connection string).",
            DefaultValueFactory = _ => 120
        };

        Option<bool> poolingOption = new("--pooling")
        {
            Description = "Enable connection pooling (default: disabled)."
        };

        // ── Diagnostics ─────────────────────────────────────────────
        Option<bool> logOption = new("--log-events", "-l")
        {
            Description = "Enable SqlClient event emission to the console."
        };

        Option<bool> traceOption = new("--trace", "-t")
        {
            Description = "Pause execution to allow dotnet-trace to be attached."
        };

        Option<bool> verboseOption = new("--verbose", "-v")
        {
            Description = "Enable verbose output with detailed error information."
        };

        // ── Root command ────────────────────────────────────────────
        RootCommand rootCommand = new(
            """
            ThreadStarvation — Thread-Pool Starvation Reproducer
            ----------------------------------------------------

            Runs parallel SQL queries to surface thread-pool starvation and
            async performance issues in Microsoft.Data.SqlClient.

            Reproduces patterns from GitHub issues #601, #979, #1562, #3118.
            """)
        {
            connectionStringOption,
            connectionsOption,
            modeOption,
            marsOption,
            launchOption,
            queryOption,
            sleepOption,
            minThreadsOption,
            maxThreadsOption,
            ioThreadsOption,
            slowOption,
            monitorOption,
            connectTimeoutOption,
            poolingOption,
            logOption,
            traceOption,
            verboseOption
        };

        rootCommand.SetAction(parseResult =>
        {
            App.RunOptions options = new()
            {
                ConnectionString = parseResult.GetValue(connectionStringOption)!,
                Connections = parseResult.GetValue(connectionsOption),
                Mode = parseResult.GetValue(modeOption)!,
                Mars = parseResult.GetValue(marsOption),
                Launch = parseResult.GetValue(launchOption)!,
                Query = parseResult.GetValue(queryOption)!,
                SleepMs = parseResult.GetValue(sleepOption),
                MinThreads = parseResult.GetValue(minThreadsOption),
                MaxThreads = parseResult.GetValue(maxThreadsOption),
                IoThreads = parseResult.GetValue(ioThreadsOption),
                SlowThresholdMs = parseResult.GetValue(slowOption),
                MonitorIntervalMs = parseResult.GetValue(monitorOption),
                ConnectTimeout = parseResult.GetValue(connectTimeoutOption),
                Pooling = parseResult.GetValue(poolingOption),
                LogEvents = parseResult.GetValue(logOption),
                Trace = parseResult.GetValue(traceOption),
                Verbose = parseResult.GetValue(verboseOption)
            };

            using App app = new();
            return app.Run(options);
        });

        return rootCommand.Parse(args).Invoke();
    }
}
