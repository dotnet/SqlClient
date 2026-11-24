// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Starts exact child selections and keeps their artifacts and network lifetime separate.</summary>
internal static class BenchmarkProcessRunner
{
    internal const string SelectionVariable = "SQLCLIENT_BENCHMARK_SELECTION";
    internal const string AppliedRttVariable = "SQLCLIENT_BENCHMARK_APPLIED_RTT_MS";

    internal static int Run(
        BenchmarkProcessSelection selection, RunnerJob job, string connectionString,
        bool unattended, CancellationToken cancellationToken)
    {
        string directory = Path.GetFullPath(Path.Combine(
            "BenchmarkDotNet.Artifacts", "processes",
            selection.Unit + "-" + selection.Fingerprint[..16] + "-" +
            Guid.NewGuid().ToString("N")[..8]));
        Directory.CreateDirectory(directory);
        using var network = new BenchmarkNetworkScope(job.AdditionalRttMilliseconds, connectionString);
        int appliedRtt = network.Start();
        cancellationToken.ThrowIfCancellationRequested();

        string? executable = Environment.ProcessPath;
        string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ??
            (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet",
                StringComparison.OrdinalIgnoreCase) ? executable! : "dotnet");
        var start = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            WorkingDirectory = directory
        };
        start.ArgumentList.Add(typeof(BenchmarkProcessRunner).Assembly.Location);
        start.Environment["PERF_BENCHMARK"] = selection.Unit;
        start.Environment.Remove("PERF_LIST_BENCHMARKS");
        start.Environment[SelectionVariable] = JsonSerializer.Serialize(selection);
        start.Environment["SQLCLIENT_BENCHMARK_UNATTENDED"] = unattended ? "1" : "0";
        start.Environment[AppliedRttVariable] = appliedRtt.ToString(CultureInfo.InvariantCulture);
        start.Environment["RUNNER_CONFIG"] = ChildConfigPath("RUNNER_CONFIG", "runnerconfig.jsonc");
        start.Environment["DATATYPES_CONFIG"] = ChildConfigPath("DATATYPES_CONFIG", "datatypes.json");
        using Process child = Process.Start(start) ??
            throw new InvalidOperationException("Could not start the benchmark process.");
        try
        {
            child.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
            return child.ExitCode;
        }
        catch
        {
            Stop(child);
            throw;
        }
    }

    internal static string ChildConfigPath(string variable, string fallback)
    {
        string path = Path.GetFullPath(Environment.GetEnvironmentVariable(variable) ?? fallback);
        // A private memfd belongs to this process; arbitrary descriptors are not inherited
        // by Process.Start. Keep its owner alive and let the child open the owner's descriptor.
        const string selfFd = "/proc/self/fd/";
        return path.StartsWith(selfFd, StringComparison.Ordinal)
            ? "/proc/" + Environment.ProcessId + "/fd/" + path[selfFd.Length..]
            : path;
    }

    internal static void Stop(Process child)
    {
        if (!child.HasExited && OperatingSystem.IsLinux())
        {
            if (Kill(child.Id, 15) != 0 && !child.HasExited)
            {
                Console.Error.WriteLine("Graceful benchmark termination failed: " +
                    new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()).Message);
            }
            else
            {
                child.WaitForExit(20000);
            }
        }
        if (!child.HasExited)
        {
            child.Kill(entireProcessTree: true);
            if (!child.WaitForExit(10000))
            {
                throw new TimeoutException("Benchmark process did not terminate.");
            }
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int processId, int signal);
}
