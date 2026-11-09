using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace OpenAsyncLab;

internal sealed class Workload : IDisposable
{
    private sealed record Worker(CancellationTokenSource Stop, Task Task);
    private readonly List<Worker> _workers = new();
    private readonly List<Worker> _retired = new();
    private readonly Measurements _measurements;
    private readonly Func<SqlConnection, CancellationToken, Task> _open;
    private readonly Func<SqlCommand, CancellationToken, Task> _executeQuery;
    private readonly string _baseString;
    private readonly string _application = "OpenAsyncLab-" + Guid.NewGuid().ToString("N");
    private string _connectionString;
    private Settings _settings;
    private int _active;

    public int Active => Volatile.Read(ref _active);
    public bool Running { get; private set; }
    public string? Failure { get; private set; }

    public Workload(
        string connectionString, Measurements measurements, Settings settings,
        Func<SqlConnection, CancellationToken, Task>? open = null,
        Func<SqlCommand, CancellationToken, Task>? executeQuery = null)
    {
        _baseString = connectionString;
        _measurements = measurements;
        _open = open ?? (static (connection, cancellation) => connection.OpenAsync(cancellation));
        _executeQuery = executeQuery ?? (static async (command, cancellation) =>
        {
            await command.ExecuteScalarAsync(cancellation).ConfigureAwait(false);
        });
        _connectionString = BuildString(settings);
        _settings = settings;
    }

    private string BuildString(Settings settings)
    {
        var builder = new SqlConnectionStringBuilder(_baseString)
        {
            ApplicationName = _application,
            Pooling = settings.Pooling,
            MaxPoolSize = settings.PoolSize,
            MinPoolSize = 0,
            ConnectRetryCount = 0
        };
        return builder.ConnectionString;
    }

    public void Configure(Settings settings)
    {
        Reap();
        if (Running)
        {
            ValidateResize(settings.Workers);
        }
        string next = BuildString(settings);
        string old = Interlocked.Exchange(ref _connectionString, next);
        Volatile.Write(ref _settings, settings);
        if (old != next)
        {
            using var connection = new SqlConnection(old);
            SqlConnection.ClearPool(connection);
        }
        if (Running)
        {
            Resize(settings.Workers);
        }
    }

    public void Start(int count)
    {
        Reap();
        if (Failure is not null)
        {
            if (_workers.Count != 0 || _retired.Count != 0)
            {
                throw new InvalidOperationException(
                    "Failed workers are still draining. Pause and retry shortly.");
            }

            Failure = null;
        }

        Resize(count);
        Running = true;
    }

    public void Pause()
    {
        Running = false;
        Resize(0);
    }

    public bool ClearPool()
    {
        string current = Volatile.Read(ref _connectionString);
        if (!new SqlConnectionStringBuilder(current).Pooling)
        {
            return false;
        }

        using var connection = new SqlConnection(current);
        SqlConnection.ClearPool(connection);
        _measurements.MarkChange();
        return true;
    }

    private void Resize(int count)
    {
        Reap();
        ValidateResize(count);
        while (_workers.Count > count)
        {
            Worker worker = _workers[^1];
            _workers.RemoveAt(_workers.Count - 1);
            worker.Stop.Cancel();
            _retired.Add(worker);
        }
        while (_workers.Count < count)
        {
            var stop = new CancellationTokenSource();
            _workers.Add(new Worker(stop, Task.Run(() => Run(stop.Token))));
        }
    }

    private void ValidateResize(int count)
    {
        if (count is < 0 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        if (count > 0 && _retired.Count + Math.Max(_workers.Count, count) > 2048)
        {
            throw new InvalidOperationException("Previous workers are still draining; pause first.");
        }
    }

    public void Reap()
    {
        foreach (Worker worker in _workers.Concat(_retired))
        {
            if (worker.Task.IsFaulted)
            {
                string failure =
                    "Worker failed: " + worker.Task.Exception!.GetBaseException().GetType().Name;
                if (Failure is null)
                {
                    _measurements.Failed(failure);
                }
                Failure = failure;
            }
        }
        foreach (Worker worker in _retired.Where(w => w.Task.IsCompleted).ToArray())
        {
            worker.Stop.Dispose();
            _retired.Remove(worker);
        }
    }

    private async Task Run(CancellationToken cancellation)
    {
        Interlocked.Increment(ref _active);
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                Settings settings = Volatile.Read(ref _settings);
                long start = Stopwatch.GetTimestamp();
                _measurements.Enter();
                try
                {
                    await using var connection = new SqlConnection(
                        Volatile.Read(ref _connectionString));
                    await _open(connection, cancellation).ConfigureAwait(false);
                    _measurements.Opened(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    if (settings.Kind == WorkloadKind.OpenQueryClose)
                    {
                        using SqlCommand command = connection.CreateCommand();
                        command.CommandText = "SELECT 1";
                        command.CommandTimeout = 30;
                        await _executeQuery(command, cancellation).ConfigureAwait(false);
                    }
                    _measurements.Completed(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error) when (error is SqlException or TimeoutException)
                {
                    string message = error is SqlException sqlError
                        ? $"SQL error {sqlError.Number}; check server/authentication or timeout."
                        : "Connection timeout; reduce workers/latency or increase Connect Timeout.";
                    _measurements.Failed(message);
                    try
                    {
                        await Task.Delay(250, cancellation).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        return;
                    }
                }
                finally
                {
                    _measurements.Leave();
                }
                // Warm pooled opens can complete synchronously forever. Yield so every logical
                // worker and queued probe has a chance to run; this is not a microbenchmark.
                await Task.Yield();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    public void Dispose()
    {
        Pause();
        long start = Stopwatch.GetTimestamp();
        while (_retired.Any(w => !w.Task.IsCompleted)
               && Stopwatch.GetElapsedTime(start).TotalSeconds < 10)
        {
            Thread.Sleep(20);
        }
        Reap();
        using var connection = new SqlConnection(_connectionString);
        SqlConnection.ClearPool(connection);
        if (_retired.Count != 0)
        {
            throw new TimeoutException("Some SQL operations did not drain within ten seconds.");
        }
    }
}
