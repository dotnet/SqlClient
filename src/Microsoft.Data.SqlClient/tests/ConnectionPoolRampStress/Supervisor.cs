// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ConnectionPoolRampStress;

internal sealed record ChildConfiguration(Settings Settings, Sample Sample);
internal sealed record Packet(string Kind, RuntimeMetadata? Metadata = null, IntervalResult? Interval = null,
    SampleResult? Result = null, SafeFailure? Failure = null, DateTimeOffset? StartedUtc = null);
internal sealed record SupervisedSample(Sample Sample, Outcome Outcome, int? ExitCode, bool Deadline,
    bool Killed, long DiscardedOutputLines, SampleResult? Result, SafeFailure? Failure, bool Reaped = true);

internal static class Wire
{
    public const string Prefix = "RAMP:";
    public static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Write(Packet packet)
    {
        Console.Out.WriteLine(Prefix + JsonSerializer.Serialize(packet, Json));
        Console.Out.Flush();
    }
}

internal sealed class Supervisor
{
    public static ProcessStartInfo ChildStartInfo(Settings settings, Sample sample, string? fixture = null)
    {
        string assembly = typeof(Supervisor).Assembly.Location;
        string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ??
            (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)
                ? Environment.ProcessPath! : "dotnet");
        ProcessStartInfo info = new(host)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(assembly);
        info.ArgumentList.Add(fixture is null ? "--child" : "--fixture");
        if (fixture is not null) info.ArgumentList.Add(fixture);
        info.Environment["SQLCLIENT_RAMP_CONFIG"] = JsonSerializer.Serialize(new ChildConfiguration(settings, sample), Wire.Json);
        info.Environment["SQLCLIENT_RAMP_INPUT_ENV"] = settings.ConnectionEnvironment;
        // Preserve the requested env var in the inherited environment. Never copy its value
        // into arguments, configuration JSON, errors, or result records.
        return info;
    }

    public SupervisedSample Run(Settings settings, Sample sample, Action<Packet> progress, string? fixture = null)
    {
        using Process child = new() { StartInfo = ChildStartInfo(settings, sample, fixture) };
        bool ready = false;
        bool deadline = false;
        bool killed = false;
        long discarded = 0;
        SampleResult? result = null;
        SafeFailure? failure = null;
        Exception? readerError = null;
        Thread? stdout = null;
        Thread? stderr = null;
        try
        {
            child.Start();
            Stopwatch clock = Stopwatch.StartNew();
            stdout = new Thread(() =>
            {
                try
                {
                    ReadBoundedLines(child.StandardOutput, line =>
                    {
                        Packet? packet = null;
                        if (line?.StartsWith(Wire.Prefix, StringComparison.Ordinal) == true)
                        {
                            try { packet = JsonSerializer.Deserialize<Packet>(line[Wire.Prefix.Length..], Wire.Json); }
                            catch (JsonException) { }
                        }
                        if (packet is null || !Valid(packet))
                        {
                            Interlocked.Increment(ref discarded);
                            return;
                        }
                        if (packet.Kind == "ready") Volatile.Write(ref ready, true);
                        if (packet.Result is not null) result = packet.Result;
                        if (packet.Failure is not null) failure = packet.Failure;
                        progress(packet);
                    });
                }
                catch (Exception exception) { readerError = exception; }
            }) { IsBackground = true, Name = "ramp-stdout" };
            stderr = new Thread(() =>
            {
                try { ReadBoundedLines(child.StandardError, _ => Interlocked.Increment(ref discarded)); }
                catch (Exception exception) { readerError = exception; }
            }) { IsBackground = true, Name = "ramp-stderr" };
            stdout.Start();
            stderr.Start();
            while (!child.WaitForExit(20))
            {
                if (clock.Elapsed.TotalSeconds >= settings.DeadlineSeconds ||
                    (!Volatile.Read(ref ready) && clock.Elapsed.TotalSeconds >= settings.StartupSeconds) ||
                    readerError is not null)
                {
                    deadline = true;
                    break;
                }
            }
            if (deadline)
            {
                try
                {
                    child.StandardInput.WriteLine("stop");
                    child.StandardInput.Flush();
                }
                catch (IOException) { }
                if (!child.WaitForExit((int)Math.Ceiling(settings.CleanupSeconds * 1000)))
                {
                    child.Kill(entireProcessTree: true);
                    killed = true;
                }
            }
            // Reap even killed children before any next sample.
            if (!child.WaitForExit((int)Math.Ceiling(settings.CleanupSeconds * 1000)))
                throw new TimeoutException();
            bool readersFinished = stdout.Join(TimeSpan.FromSeconds(settings.CleanupSeconds));
            readersFinished &= stderr.Join(TimeSpan.FromSeconds(settings.CleanupSeconds));
            Outcome outcome = deadline || !readersFinished ? Outcome.Incomplete :
                result?.Outcome ?? (failure is not null ? Outcome.SetupFailure : Outcome.Incomplete);
            if (child.ExitCode != 0 && outcome == Outcome.Success) outcome = Outcome.Incomplete;
            if (readerError is not null) outcome = Outcome.Incomplete;
            return new(sample, outcome, child.ExitCode, deadline, killed, discarded, result,
                deadline ? new("SupervisorDeadline", null, ready ? "supervisor-deadline" : "startup-deadline") : failure);
        }
        catch (Exception exception)
        {
            bool reaped = false;
            try
            {
                if (child.Id > 0 && !child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    killed = true;
                    reaped = child.WaitForExit((int)Math.Ceiling(settings.CleanupSeconds * 1000));
                }
                else reaped = true;
            }
            catch (InvalidOperationException) { }
            return new(sample, Outcome.Incomplete, null, deadline, killed, discarded, result, Failure.Describe(exception, setup: true), reaped);
        }
        finally
        {
            stdout?.Join(TimeSpan.FromSeconds(settings.CleanupSeconds));
            stderr?.Join(TimeSpan.FromSeconds(settings.CleanupSeconds));
        }
    }

    internal static bool Valid(Packet packet)
    {
        if (packet.Kind is not ("ready" or "interval" or "result" or "setup")) return false;
        if (packet.Kind == "interval" && packet.Interval is null ||
            packet.Kind == "result" && packet.Result is null ||
            packet.Kind == "setup" && packet.Failure is null) return false;
        static bool Safe(SafeFailure failure) =>
            failure.ExceptionType is "SqlException" or "OperationCanceledException" or "TimeoutException" or
                "ArgumentException" or "InvalidOperationException" or "IOException" or "OtherException" &&
            failure.Category is "setup" or "cancellation" or "timeout-unclassified" or "unclassified";
        return (packet.Metadata is null || SafeMetadata(packet.Metadata)) &&
            (packet.Failure is null || Safe(packet.Failure)) &&
            (packet.Interval is null || packet.Interval.CumulativeFailures.All(f => Safe(f.Failure))) &&
            (packet.Result is null || (Enum.IsDefined(packet.Result.Outcome) &&
                packet.Result.Trend.Status is "insufficient-data" or "descriptive" &&
                packet.Result.Failures.All(f => Safe(f.Failure))));
    }

    private static bool SafeMetadata(RuntimeMetadata metadata)
    {
        static bool Version(string? text) => text is not null &&
            (text == "unavailable" || Regex.IsMatch(text, "^[0-9.]{1,40}$"));
        return Version(metadata.DriverVersion) && Version(metadata.Runtime) && Version(metadata.ServerVersion) &&
            Version(metadata.OSVersion) &&
            (metadata.DriverCommit == "unavailable" || metadata.DriverCommit is not null &&
                Regex.IsMatch(metadata.DriverCommit, "^[0-9a-fA-F]{40}$")) &&
            metadata.OS is "Windows" or "macOS" or "Linux" &&
            Enum.TryParse(metadata.Architecture, out System.Runtime.InteropServices.Architecture architecture) &&
            Enum.IsDefined(architecture) &&
            metadata.Connection.Sni is "managed" or "native" &&
            metadata.Connection.Encryption is "Mandatory" or "Optional" or "Strict" &&
            metadata.Connection.PoolBlockingPeriod is "Auto" or "AlwaysBlock" or "NeverBlock" &&
            metadata.ServerResources == "unavailable" && metadata.ContainerLimits == "unavailable";
    }

    // Arbitrary native/driver output is discarded without ever retaining an unbounded line.
    internal static void ReadBoundedLines(TextReader reader, Action<string?> accept)
    {
        StringBuilder line = new();
        bool overflow = false;
        int character;
        while ((character = reader.Read()) >= 0)
        {
            if (character == '\n')
            {
                accept(overflow ? null : line.ToString());
                line.Clear();
                overflow = false;
            }
            else if (!overflow)
            {
                if (line.Length >= 65536) { overflow = true; line.Clear(); }
                else line.Append((char)character);
            }
        }
        if (line.Length > 0 || overflow) accept(overflow ? null : line.ToString());
    }
}

internal sealed class Sweep
{
    private readonly HashSet<string> _stopped = new();
    private readonly Dictionary<string, List<SupervisedSample>> _results = new();
    public bool ShouldRun(Sample sample) => !_stopped.Contains(sample.Combination);

    public void Record(SupervisedSample result)
    {
        if (!_results.TryGetValue(result.Sample.Combination, out List<SupervisedSample>? list))
            _results.Add(result.Sample.Combination, list = new());
        list.Add(result);
        if (result.Outcome != Outcome.Success) _stopped.Add(result.Sample.Combination);
    }

    public IEnumerable<Boundary> Boundaries(Settings settings)
    {
        foreach ((string combination, List<SupervisedSample> results) in _results)
        {
            int? highest = results.GroupBy(r => r.Sample.Concurrency)
                .Where(g => g.Count() == settings.Repetitions && g.All(r => r.Outcome == Outcome.Success))
                .Select(g => (int?)g.Key).Max();
            SupervisedSample? first = results.FirstOrDefault(r => r.Outcome != Outcome.Success);
            yield return new(combination, highest, first?.Sample.Concurrency, first?.Outcome,
                first is null && highest == settings.Maximum ? $"no failure observed through {settings.Maximum}" :
                first?.Outcome == Outcome.SetupFailure || first?.Failure?.Category is "startup-deadline" or "setup"
                    ? "setup failure or startup deadline, not a saturation observation" :
                "observed boundary, repeat to assess transience");
        }
    }
}

internal sealed record Boundary(string Combination, int? HighestAllRepetitionsSuccessful,
    int? FirstNonSuccessLevel, Outcome? FirstNonSuccessOutcome, string Interpretation);
