// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Microsoft.Data.SqlClient.PerformanceTests;

/// <summary>Owns SQL-only netem setup and verified cleanup outside the measured process.</summary>
internal sealed class BenchmarkNetworkScope : IDisposable
{
    private readonly int _rtt;
    private readonly string _connectionString;
    private readonly string _statePath;
    private bool _attempted;

    internal BenchmarkNetworkScope(int rtt, string connectionString, string directory)
    {
        _rtt = rtt;
        _connectionString = connectionString;
        EvidencePath = Path.Combine(directory, "network.json");
        _statePath = Path.Combine(directory, "network-state.json");
    }

    internal string EvidencePath { get; }

    internal void Start()
    {
        if (_rtt == 0)
        {
            File.WriteAllText(EvidencePath, JsonSerializer.Serialize(new
            {
                SchemaVersion = 1, AdditionalRttMilliseconds = 0,
                Applied = false, Status = "Disabled"
            }));
            return;
        }
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "AdditionalRttMilliseconds requires Linux SQL-only tc netem.");
        }
        (string server, int port) = ParseEndpoint(
            new SqlConnectionStringBuilder(_connectionString).DataSource);
        _attempted = true;
        Invoke("start", server, port);
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

    private void Invoke(string action, string? server = null, int port = 1433)
    {
        string helper = Path.Combine(
            Path.GetDirectoryName(typeof(BenchmarkNetworkScope).Assembly.Location)!, "netem.py");
        var start = new ProcessStartInfo("python3") { UseShellExecute = false };
        start.ArgumentList.Add(helper);
        start.ArgumentList.Add(action);
        start.ArgumentList.Add("--state");
        start.ArgumentList.Add(_statePath);
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add(EvidencePath);
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
            throw new TimeoutException("SQL-only netem " + action + " timed out.");
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("SQL-only netem " + action + " failed.");
        }
    }

    internal static int ReadAppliedRtt(RunnerJob job)
    {
        string? path = Environment.GetEnvironmentVariable(BenchmarkProcessRunner.NetworkVariable);
        if (path == null)
        {
            if (job.AdditionalRttMilliseconds == 0) return 0;
            throw new InvalidOperationException("Requested RTT has no runner network evidence.");
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement data = document.RootElement;
        int requested = data.GetProperty("AdditionalRttMilliseconds").GetInt32();
        bool applied = data.GetProperty("Applied").GetBoolean();
        string? status = data.GetProperty("Status").GetString();
        if (requested != job.AdditionalRttMilliseconds ||
            (requested > 0 && (!applied || status != "Applied")) ||
            (requested == 0 && (applied || status != "Disabled")))
        {
            throw new InvalidOperationException("Requested and applied network profiles differ.");
        }
        return requested;
    }

    public void Dispose()
    {
        if (_attempted && File.Exists(_statePath))
        {
            Invoke("stop");
        }
    }
}
