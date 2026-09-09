// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace ConnectionPoolRampStress;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--child") return Child();
            if (args.Length == 2 && args[0] == "--fixture") return Fixture(args[1]);
            return CommandLine.Run(args, Run);
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Harness setup failed. Exception details suppressed.");
            return 2;
        }
    }

    private static int Run(Settings settings, bool list)
    {
        if (list)
        {
            foreach (Sample sample in settings.Matrix())
                Console.WriteLine($"{sample.Combination} n={sample.Concurrency} rep={sample.Repetition}");
            return 0;
        }
        // JSON Lines is append-safe evidence: each line is a complete allowlisted JSON record.
        // Refuse to overwrite existing comparison results.
        using FileStream file = new(settings.Output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(file) { AutoFlush = true };
        object writeGate = new();
        void Write(object record)
        {
            lock (writeGate) writer.WriteLine(JsonSerializer.Serialize(record, Wire.Json));
        }
        Write(new { Kind = "run", SchemaVersion = 1, StartedUtc = DateTimeOffset.UtcNow, Settings = settings });
        Sweep sweep = new();
        int exit = 0;
        foreach (Sample sample in settings.Matrix())
        {
            if (!sweep.ShouldRun(sample)) continue;
            Write(new { Kind = "sample-start", Sample = sample, StartedUtc = DateTimeOffset.UtcNow });
            SupervisedSample result = new Supervisor().Run(settings, sample, packet =>
            {
                Write(new { Kind = "progress", Sample = sample, Packet = packet });
                if (packet.Interval is { } interval)
                    Console.WriteLine($"{sample.Combination} n={sample.Concurrency} rep={sample.Repetition} " +
                        $"t={interval.EndSeconds:F1}s opens={interval.Metrics.Opens.Count} " +
                        $"opens/s={interval.Metrics.OpensPerSecond:F1} cycles/s={interval.Metrics.CyclesPerSecond:F1} " +
                        $"rounds/s={interval.Metrics.RoundsPerSecond:F1} p95ms={interval.Metrics.Opens.P95Milliseconds:F3} " +
                        $"failures={interval.Metrics.FailedOpens.Count} " +
                        $"drain={interval.Drain}");
            });
            sweep.Record(result);
            Write(new { Kind = "sample-end", Result = result });
            Console.WriteLine($"{sample.Combination} n={sample.Concurrency} rep={sample.Repetition} {result.Outcome} " +
                $"opens/s={result.Result?.Measured.OpensPerSecond:F1} p95ms={result.Result?.Measured.Opens.P95Milliseconds:F3} " +
                $"started={result.Result?.StartedWorkers} peak-calls={result.Result?.PeakOutstandingCalls}");
            exit = Math.Max(exit, ExitCode(result.Outcome));
            if (result.Outcome == Outcome.SetupFailure || !result.Reaped) break;
            Thread.Sleep(TimeSpan.FromSeconds(settings.CooldownSeconds));
        }
        foreach (Boundary boundary in sweep.Boundaries(settings))
        {
            Write(new { Kind = "boundary", Boundary = boundary });
            Console.WriteLine($"{boundary.Combination}: all-reps={boundary.HighestAllRepetitionsSuccessful} " +
                $"first-non-success={boundary.FirstNonSuccessLevel}. {boundary.Interpretation}");
        }
        Write(new { Kind = "run-end", ExitCode = exit });
        return exit;
    }

    private static int Child()
    {
        try
        {
            ChildConfiguration config = JsonSerializer.Deserialize<ChildConfiguration>(
                Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_CONFIG") ?? "", Wire.Json)!;
            config.Settings.Validate();
            Sample sample = config.Sample;
            // Must precede constructing a connection or preflight.
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseConnectionPoolV2", sample.Pool == PoolMode.V2);
            ThreadPoolSettings threadPool = ThreadPoolSettings.Configure(sample.Profile, config.Settings.WorkerMinimum);
            string name = Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_INPUT_ENV") ?? "";
            string input = Environment.GetEnvironmentVariable(name) ?? throw new ArgumentException();
            ConnectionFactory factory = new(input, sample, config.Settings);
            using PhysicalCounters counters = new();
            RuntimeMetadata metadata = factory.Preflight(threadPool);
            WorkloadRunner runner = new(config.Settings, sample, factory, counters);
            new Thread(() =>
            {
                try
                {
                    if (Console.In.ReadLine() is not null) runner.RequestStop();
                }
                catch (IOException) { runner.RequestStop(); }
            }) { IsBackground = true, Name = "ramp-control" }.Start();
            SampleResult result = runner.Run(interval => Wire.Write(new("interval", Interval: interval)),
                () => Wire.Write(new("ready", Metadata: metadata, StartedUtc: DateTimeOffset.UtcNow)));
            Wire.Write(new("result", Result: result));
            return ExitCode(result.Outcome);
        }
        catch (Exception exception)
        {
            Wire.Write(new("setup", Failure: Failure.Describe(exception, setup: true)));
            return 2;
        }
    }

    private static int Fixture(string mode)
    {
        if (mode is not ("success" or "failure" or "hang" or "setup" or "noise")) return 2;
        if (mode == "setup")
        {
            Wire.Write(new("setup", Failure: Failure.Describe(new ArgumentException("suppressed"), setup: true)));
            return 2;
        }
        if (mode == "noise")
        {
            Console.WriteLine(new string('x', 100000));
            Console.Error.WriteLine(Environment.GetEnvironmentVariable("SQLCLIENT_RAMP_FIXTURE_SECRET"));
            mode = "success";
        }
        Wire.Write(new("ready"));
        MetricBucket metrics = new();
        metrics.Add(Metric.Attempt, 0, 0);
        metrics.Add(Metric.Open, 1, 0);
        using PhysicalCounters counters = new();
        Wire.Write(new("interval", Interval: new(0, 0, 0.1, true, false, false,
            metrics.Snapshot(0.1), counters.Read(1, 0, 1, 1))));
        if (mode == "hang") Thread.Sleep(Timeout.Infinite);
        Outcome outcome = mode == "failure" ? Outcome.Failed : Outcome.Success;
        Wire.Write(new("result", Result: new(outcome, 0.1, 0, metrics.Snapshot(0.1),
            new MetricBucket().Snapshot(0), 1, 1, 1, 0, false,
            mode == "failure" ? [new(Failure.Describe(new InvalidOperationException("suppressed")), 1)] : [],
            Trend.Calculate([]))));
        return ExitCode(outcome);
    }

    internal static int ExitCode(Outcome outcome) => outcome switch
    {
        Outcome.Success => 0, Outcome.SetupFailure => 2, Outcome.Failed => 3, _ => 4
    };
}
