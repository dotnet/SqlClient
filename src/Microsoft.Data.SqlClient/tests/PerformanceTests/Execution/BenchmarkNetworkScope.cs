// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Owns SQL-only netem setup and verified cleanup outside the measured process.</summary>
internal sealed class BenchmarkNetworkScope : IDisposable
{
    private readonly int _rtt;
    private readonly string _connectionString;
    private string? _statePath;

    internal BenchmarkNetworkScope(int rtt, string connectionString)
    {
        _rtt = GetPlatformRtt(rtt);
        _connectionString = connectionString;
    }

    internal int Start()
    {
        if (_rtt == 0)
        {
            return 0;
        }

        (string server, int port) = ParseEndpoint(
            new SqlConnectionStringBuilder(_connectionString).DataSource);
        _statePath = Path.Combine(Directory.CreateTempSubdirectory("sqlclient-perf-netem-").FullName,
            "network-state.json");
        Invoke("start", _statePath, server, port);
        return _rtt;
    }

    internal static (string Server, int Port) ParseEndpoint(string dataSource)
    {
        string endpoint = dataSource;
        if (endpoint.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = endpoint[4..];
        }
        else if (endpoint.Contains(':') && !endpoint.StartsWith('['))
        {
            throw new ArgumentException("Netem requires a TCP SQL endpoint.");
        }
        if (endpoint.Contains('\\'))
        {
            throw new ArgumentException("Netem does not support named-instance discovery.");
        }
        string[] parts = endpoint.Split(',');
        if (parts.Length > 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new ArgumentException("Invalid TCP SQL endpoint for netem.");
        }
        int port = 1433;
        if (parts.Length == 2 &&
            (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
             port < 1 || port > 65535))
        {
            throw new ArgumentException("Invalid TCP SQL port for netem.");
        }
        return (parts[0].Trim('[', ']'), port);
    }

    private void Invoke(string action, string statePath, string? server = null, int port = 1433)
    {
        string helper = Path.Combine(
            Path.GetDirectoryName(typeof(BenchmarkNetworkScope).Assembly.Location)!, "netem.py");
        var start = new ProcessStartInfo("python3") { UseShellExecute = false };
        start.ArgumentList.Add(helper);
        start.ArgumentList.Add(action);
        start.ArgumentList.Add("--state");
        start.ArgumentList.Add(statePath);
        if (server != null)
        {
            start.ArgumentList.Add("--rtt-ms");
            start.ArgumentList.Add(_rtt.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add("--server");
            start.ArgumentList.Add(server);
            start.ArgumentList.Add("--port");
            start.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        }
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start the netem helper.");
        if (!process.WaitForExit(30000))
        {
            BenchmarkProcessRunner.Stop(process);
            throw new TimeoutException($"SQL-only netem {action} timed out (state: {statePath}).");
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"SQL-only netem {action} failed (state: {statePath}).");
        }
    }

    internal static int ReadAppliedRtt(RunnerJob job)
    {
        string? value = Environment.GetEnvironmentVariable(BenchmarkProcessRunner.AppliedRttVariable);
        if (value == null && job.AdditionalRttMilliseconds == 0)
        {
            return 0;
        }
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int applied) ||
            applied != GetPlatformRtt(job.AdditionalRttMilliseconds))
        {
            throw new InvalidOperationException("The benchmark parent did not confirm the requested RTT.");
        }

        return applied;
    }

    private static int GetPlatformRtt(int requestedRtt) =>
        OperatingSystem.IsLinux() ? requestedRtt : 0;

    public void Dispose()
    {
        if (_statePath == null)
        {
            return;
        }

        if (File.Exists(_statePath))
        {
            Invoke("stop", _statePath);
        }

        File.Delete(_statePath + ".tmp");
        File.Delete(_statePath);
        Directory.Delete(Path.GetDirectoryName(_statePath)!);
    }
}
