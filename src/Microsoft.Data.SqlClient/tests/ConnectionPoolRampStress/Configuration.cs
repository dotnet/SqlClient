// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.Text.Json.Serialization;

namespace ConnectionPoolRampStress;

internal enum PoolMode { Legacy, V2, Disabled }
internal enum CallerMode { Async, SyncThreadpool, SyncDedicated }
internal enum Workload { OpenClose, ColdRamps }
internal enum PoolProfile { Default, Provisioned }
internal enum Outcome { Success, SetupFailure, Failed, Incomplete }

internal sealed record Settings
{
    // These input-only fields are never part of the result schema.
    [JsonIgnore] public string ConnectionEnvironment { get; init; } = "SQLCLIENT_RAMP_CONNECTION";
    [JsonIgnore] public string Output { get; init; } = "connection-pool-ramp.jsonl";
    public int Start { get; init; } = 1;
    public int Maximum { get; init; } = 512;
    public int Growth { get; init; } = 2;
    public int Repetitions { get; init; } = 3;
    public double DurationSeconds { get; init; } = 30;
    public double IntervalSeconds { get; init; } = 1;
    public int ConnectTimeoutSeconds { get; init; } = 15;
    public double DrainSeconds { get; init; } = 20;
    public double StartupSeconds { get; init; } = 60;
    public double DeadlineSeconds { get; init; } = 120;
    public double CleanupSeconds { get; init; } = 5;
    public double CooldownSeconds { get; init; } = 2;
    public int WorkerMinimum { get; init; } = 1024;
    public string Pool { get; init; } = "all";
    public string Caller { get; init; } = "all";
    public string Work { get; init; } = "both";
    public string Profile { get; init; } = "both";

    public void Validate()
    {
        if (Start < 1 || Maximum < Start || Maximum > 4096 || Growth < 2 || Growth > 1024 ||
            Repetitions < 1 || Repetitions > 100 || WorkerMinimum < 1 || WorkerMinimum > 32767)
        {
            throw new ArgumentException("Invalid concurrency, growth, repetition or worker-minimum range.");
        }
        double[] positive = [DurationSeconds, IntervalSeconds, DrainSeconds, StartupSeconds, DeadlineSeconds, CleanupSeconds];
        if (positive.Any(v => !double.IsFinite(v) || v <= 0 || v > 86400) ||
            !double.IsFinite(CooldownSeconds) || CooldownSeconds < 0 || CooldownSeconds > 3600 ||
            ConnectTimeoutSeconds < 1 || ConnectTimeoutSeconds > 3600 ||
            IntervalSeconds < 0.05 || IntervalSeconds > DurationSeconds ||
            Math.Ceiling((DurationSeconds + DrainSeconds + CleanupSeconds) / IntervalSeconds) > 3600)
        {
            throw new ArgumentException("Timeouts must be bounded, with at most 3600 intervals per sample.");
        }
        if (StartupSeconds < ConnectTimeoutSeconds ||
            DeadlineSeconds < StartupSeconds + DurationSeconds + DrainSeconds)
        {
            throw new ArgumentException("Deadline must allow startup, measurement and drain. Startup must allow connect timeout.");
        }
        if (!Choices.Pools.Contains(Pool) || !Choices.Callers.Contains(Caller) ||
            !Choices.Workloads.Contains(Work) || !Choices.Profiles.Contains(Profile))
        {
            throw new ArgumentException("Invalid matrix selection.");
        }
        if (string.IsNullOrWhiteSpace(ConnectionEnvironment) || ConnectionEnvironment.Contains('=') ||
            string.IsNullOrWhiteSpace(Output))
        {
            throw new ArgumentException("An environment-variable name and output path are required.");
        }
    }

    public IEnumerable<int> Levels()
    {
        for (int value = Start; ; value = (int)Math.Min((long)value * Growth, Maximum))
        {
            yield return value;
            if (value == Maximum) yield break;
        }
    }

    public IEnumerable<Sample> Matrix()
    {
        foreach (Workload workload in Enum.GetValues<Workload>().Where(v => Work == "both" || Choices.Name(v) == Work))
        foreach (CallerMode caller in Enum.GetValues<CallerMode>().Where(v => Caller == "all" || Choices.Name(v) == Caller))
        foreach (PoolProfile profile in Enum.GetValues<PoolProfile>().Where(v => Profile == "both" || Choices.Name(v) == Profile))
        foreach (int level in Levels())
        for (int repetition = 1; repetition <= Repetitions; repetition++)
        {
            PoolMode[] order = repetition % 2 == 1
                ? [PoolMode.Legacy, PoolMode.V2, PoolMode.Disabled]
                : [PoolMode.V2, PoolMode.Legacy, PoolMode.Disabled];
            foreach (PoolMode pool in order.Where(v => Pool == "all" || Choices.Name(v) == Pool))
                yield return new Sample(workload, pool, caller, profile, level, repetition);
        }
    }
}

internal sealed record Sample(Workload Workload, PoolMode Pool, CallerMode Caller, PoolProfile Profile, int Concurrency, int Repetition)
{
    public string Combination => $"{Choices.Name(Workload)}/{Choices.Name(Pool)}/{Choices.Name(Caller)}/{Choices.Name(Profile)}";
}

internal static class Choices
{
    public static readonly string[] Pools = ["all", "legacy", "v2", "disabled"];
    public static readonly string[] Callers = ["all", "async", "sync-threadpool", "sync-dedicated"];
    public static readonly string[] Workloads = ["both", "open-close", "cold-ramps"];
    public static readonly string[] Profiles = ["both", "default", "provisioned"];
    public static string Name<T>(T value) where T : Enum => value.ToString() switch
    {
        "OpenClose" => "open-close",
        "ColdRamps" => "cold-ramps",
        "SyncThreadpool" => "sync-threadpool",
        "SyncDedicated" => "sync-dedicated",
        var name => name.ToLowerInvariant()
    };
}

internal static class CommandLine
{
    public static int Run(string[] args, Func<Settings, bool, int> action)
    {
        RootCommand root = new("Sustained connection-pool comparisons in isolated child processes. No queries in measured workloads.");
        Option<string> connection = new("--connection-env") { DefaultValueFactory = _ => "SQLCLIENT_RAMP_CONNECTION" };
        Option<string> output = new("--output") { DefaultValueFactory = _ => "connection-pool-ramp.jsonl" };
        Option<string> pool = new("--pool") { DefaultValueFactory = _ => "all" };
        Option<string> caller = new("--caller") { DefaultValueFactory = _ => "all" };
        Option<string> work = new("--workload") { DefaultValueFactory = _ => "both" };
        Option<string> profile = new("--profile") { DefaultValueFactory = _ => "both" };
        Option<bool> list = new("--list", "--dry-run") { Description = "List the matrix without reading credentials or opening connections." };
        Dictionary<string, Option<int>> integers = new();
        foreach ((string name, int value) in new (string, int)[]
        {
            ("start", 1), ("max", 512), ("growth", 2), ("repetitions", 3),
            ("connect-timeout", 15), ("worker-minimum", 1024)
        })
        {
            Option<int> option = new("--" + name) { DefaultValueFactory = _ => value };
            integers.Add(name, option);
            root.Options.Add(option);
        }
        Dictionary<string, Option<double>> durations = new();
        foreach ((string name, double value) in new (string, double)[]
        {
            ("duration", 30), ("interval", 1), ("drain", 20), ("startup-timeout", 60),
            ("deadline", 120), ("cleanup-grace", 5), ("cooldown", 2)
        })
        {
            Option<double> option = new("--" + name) { DefaultValueFactory = _ => value, Description = "Seconds." };
            durations.Add(name, option);
            root.Options.Add(option);
        }
        foreach (Option option in new Option[] { connection, output, pool, caller, work, profile, list }) root.Options.Add(option);
        var parse = root.Parse(args);
        // Parser diagnostics echo arbitrary input. Never print them.
        if (parse.Errors.Count != 0)
        {
            Console.Error.WriteLine("Invalid command line. Use --help for supported options.");
            return 2;
        }
        root.SetAction(result =>
        {
            Settings settings = new()
            {
                ConnectionEnvironment = result.GetValue(connection)!, Output = result.GetValue(output)!,
                Pool = result.GetValue(pool)!, Caller = result.GetValue(caller)!,
                Work = result.GetValue(work)!, Profile = result.GetValue(profile)!,
                Start = result.GetValue(integers["start"]), Maximum = result.GetValue(integers["max"]),
                Growth = result.GetValue(integers["growth"]), Repetitions = result.GetValue(integers["repetitions"]),
                ConnectTimeoutSeconds = result.GetValue(integers["connect-timeout"]),
                WorkerMinimum = result.GetValue(integers["worker-minimum"]),
                DurationSeconds = result.GetValue(durations["duration"]), IntervalSeconds = result.GetValue(durations["interval"]),
                DrainSeconds = result.GetValue(durations["drain"]), StartupSeconds = result.GetValue(durations["startup-timeout"]),
                DeadlineSeconds = result.GetValue(durations["deadline"]), CleanupSeconds = result.GetValue(durations["cleanup-grace"]),
                CooldownSeconds = result.GetValue(durations["cooldown"])
            };
            try
            {
                settings.Validate();
                return action(settings, result.GetValue(list));
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Harness setup failed. Check configuration and output permissions. Details suppressed.");
                return 2;
            }
        });
        return root.Parse(args).Invoke();
    }
}
