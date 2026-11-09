using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace OpenAsyncLab;

internal sealed class NetworkDelay : IDisposable
{
    private readonly string _directory;
    private readonly string _server;
    private Task? _pending;
    private int _requested;
    private bool _engaged;

    public bool Enabled { get; }
    public bool Busy => _pending is { IsCompleted: false };
    public bool Ready { get; private set; }
    public int Applied { get; private set; }

    public NetworkDelay(bool enabled, string connectionString)
    {
        Enabled = enabled;
        if (enabled && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Netem controls require Linux/WSL.");
        }
        _server = enabled ? Host(connectionString) : "";
        _directory = Directory.CreateTempSubdirectory("openasync-lab-").FullName;
    }

    internal static string Host(string connectionString)
    {
        string source = new SqlConnectionStringBuilder(connectionString).DataSource;
        if (!source.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
            || !source.EndsWith(",1433", StringComparison.Ordinal))
        {
            throw new ArgumentException("Netem requires Server=tcp:<local-host>,1433.");
        }
        return source[4..^5].Trim('[', ']');
    }

    public void Request(int delay)
    {
        if (!Enabled)
        {
            throw new InvalidOperationException("Restart with --netem to enable latency controls.");
        }
        if (Busy)
        {
            throw new InvalidOperationException("A netem change is still in progress.");
        }
        if (delay is < 0 or > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion.Task;
        _requested = delay;
        _engaged = true;
        var thread = new Thread(() =>
        {
            try
            {
                Invoke("set", delay);
                completion.SetResult();
            }
            catch (Exception error) when (error is IOException or InvalidOperationException
                or System.ComponentModel.Win32Exception or JsonException or TimeoutException)
            {
                completion.SetException(error);
            }
        }) { IsBackground = true, Name = "OpenAsyncLab netem control" };
        thread.Start();
    }

    public bool Poll()
    {
        if (_pending is not { IsCompleted: true })
        {
            return false;
        }
        Task finished = _pending;
        _pending = null;
        Ready = false;
        finished.GetAwaiter().GetResult();
        Applied = _requested;
        Ready = true;
        return true;
    }

    private void Invoke(string action, int delay)
    {
        var info = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in new[]
        {
            Path.Combine(AppContext.BaseDirectory, "netem_control.py"), action,
            "--state", Path.Combine(_directory, "state.json"), "--server", _server,
            "--delay-ms", delay.ToString(System.Globalization.CultureInfo.InvariantCulture)
        })
        {
            info.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start netem controller.");
        // The helper emits one small JSON record; diagnostics are a few short tc messages.
        if (!process.WaitForExit(120000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Netem controller timed out.");
        }
        string output = process.StandardOutput.ReadToEnd();
        string diagnostics = process.StandardError.ReadToEnd();
        if (process.ExitCode != 0)
        {
            string error = diagnostics;
            if (!string.IsNullOrWhiteSpace(output))
            {
                using JsonDocument document = JsonDocument.Parse(output);
                error = document.RootElement.GetProperty("error").GetString() ?? diagnostics;
            }
            throw new InvalidOperationException("Netem: " + error.Trim());
        }
    }

    public void Dispose()
    {
        // A dedicated control thread can finish even if all pool workers are blocked.
        while (Busy)
        {
            Thread.Sleep(20);
        }
        if (_pending is { IsFaulted: true })
        {
            _ = _pending.Exception;
        }
        if (_engaged)
        {
            try
            {
                Invoke("stop", 0);
            }
            catch (Exception error) when (error is InvalidOperationException or IOException
                or TimeoutException or System.ComponentModel.Win32Exception or JsonException)
            {
                throw new InvalidOperationException(
                    $"Netem cleanup failed. Recovery state: {_directory}. {error.Message}", error);
            }
        }
        // Preserve the small ownership/cleanup evidence, including recovery handles on failure.
    }
}
