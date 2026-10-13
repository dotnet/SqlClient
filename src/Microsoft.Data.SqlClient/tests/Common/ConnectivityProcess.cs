// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Data.SqlClient.Tests.Common;

/// <summary>Owns launching, bounded protocol reads, and reaping hazardous connectivity clients.</summary>
public static class ConnectivityProcess
{
    /// <summary>Launches the copied helper against a fake loopback peer, never a shell or remote server.</summary>
    public static Process Start(string scenario, int argument)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "ConnectivityTestHost");
#if NETFRAMEWORK
        string executable = Path.Combine(directory, "ConnectivityTestHost.exe");
        string arguments = $"{scenario} {argument.ToString(CultureInfo.InvariantCulture)}";
#else
        string executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        string arguments = $"\"{Path.Combine(directory, "ConnectivityTestHost.dll")}\" {scenario} {argument.ToString(CultureInfo.InvariantCulture)}";
#endif
        var start = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        return Process.Start(start) ?? throw new InvalidOperationException("Failed to launch the connectivity test helper.");
    }

    /// <summary>Fails on EOF or a missed watchdog; a stalled or missing phase is not a passing reproduction.</summary>
    public static async Task<string> ReadAsync(Process process, TimeSpan watchdog)
    {
        Task<string?> read = process.StandardOutput.ReadLineAsync();
        if (!ReferenceEquals(read, await Task.WhenAny(read, Task.Delay(watchdog))))
        {
            _ = read.ContinueWith(failed => { _ = failed.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw new TimeoutException("Connectivity child missed its expected phase/completion within the parent watchdog.");
        }
        return await read ?? throw new InvalidOperationException("Connectivity client exited before the expected phase.");
    }

    /// <summary>Kills only the owned client if needed, then waits for it to exit before test teardown continues.</summary>
    public static void Stop(Process process)
    {
        if (!process.HasExited)
        {
#if NETFRAMEWORK
            process.Kill();
#else
            process.Kill(entireProcessTree: true);
#endif
        }
        if (!process.WaitForExit(30_000))
        {
            throw new TimeoutException("Connectivity child could not be reaped.");
        }
    }
}
