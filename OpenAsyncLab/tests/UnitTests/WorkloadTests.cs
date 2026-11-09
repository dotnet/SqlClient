using System.Net;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>Pausing cancels real pending OpenAsync calls without a SQL Server dependency.</summary>
public class WorkloadTests
{
    /// <summary>The connection remains checked out until its query completes in either pool mode.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_QueryWorkload_RetainsConnectionUntilQueryCompletes(bool pooling)
    {
        // Arrange
        var measurements = new Measurements();
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryStarted = new TaskCompletionSource<SqlCommand>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var finishQuery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        Settings settings = Settings.Default() with
        {
            Kind = WorkloadKind.OpenQueryClose,
            Pooling = pooling
        };
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true",
            measurements, settings, (connection, cancellation) =>
            {
                if (Interlocked.Increment(ref calls) != 1)
                {
                    return Task.Delay(Timeout.Infinite, cancellation);
                }

                connection.Disposed += (_, _) => released.TrySetResult();
                return Task.CompletedTask;
            }, (command, cancellation) =>
            {
                queryStarted.TrySetResult(command);
                return finishQuery.Task.WaitAsync(cancellation);
            });

        // Act
        workload.Start(1);
        SqlCommand query = await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        bool releasedBeforeQuery = released.Task.IsCompleted;
        Sample pending = measurements.Take(1, 0, 0, workload.Active, 0);
        finishQuery.SetResult();
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Sample sample = measurements.Take(2, 0, 0, workload.Active, 0);

        // Assert
        Assert.False(releasedBeforeQuery);
        Assert.Equal("SELECT 1", query.CommandText);
        Assert.Equal(1d, pending.Values["Opens/s"]);
        Assert.Null(pending.Values["Cycle p99 ~ms"]);
        Assert.NotNull(sample.Values["Cycle p99 ~ms"]);
        Assert.Null(workload.Failure);
    }

    /// <summary>Pausing cancels a pending query and releases its connection without an error.</summary>
    [Fact]
    public async Task Pause_PendingQuery_CancelsQueryAndDisposesConnection()
    {
        // Arrange
        var measurements = new Measurements();
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true", measurements,
            Settings.Default() with { Kind = WorkloadKind.OpenQueryClose },
            (connection, _) =>
            {
                connection.Disposed += (_, _) => released.TrySetResult();
                return Task.CompletedTask;
            }, (_, cancellation) =>
            {
                queryStarted.TrySetResult();
                return Task.Delay(Timeout.Infinite, cancellation);
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        workload.Start(1);
        await queryStarted.Task.WaitAsync(timeout.Token);

        // Act
        workload.Pause();
        await released.Task.WaitAsync(timeout.Token);
        while (workload.Active != 0)
        {
            await Task.Delay(10, timeout.Token);
        }
        workload.Reap();
        Sample sample = measurements.Take(1, 0, 0, workload.Active, 0);

        // Assert
        Assert.Null(sample.Values["Cycle p99 ~ms"]);
        Assert.False(workload.Running);
        Assert.Equal(0, workload.Active);
        Assert.Null(measurements.Error);
        Assert.Null(workload.Failure);
    }

    /// <summary>Switching modes lets an active query finish and skips the query next iteration.</summary>
    [Fact]
    public async Task Configure_SwitchFromQueryToOpenClose_AppliesOnNextIteration()
    {
        // Arrange
        var measurements = new Measurements();
        var queryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var finishQuery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReleased = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        int queries = 0;
        Settings settings = Settings.Default() with { Kind = WorkloadKind.OpenQueryClose };
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true", measurements, settings,
            (connection, cancellation) =>
            {
                int call = Interlocked.Increment(ref calls);
                if (call == 2)
                {
                    connection.Disposed += (_, _) => secondReleased.TrySetResult();
                }
                else if (call > 2)
                {
                    return Task.Delay(Timeout.Infinite, cancellation);
                }
                return Task.CompletedTask;
            }, (_, cancellation) =>
            {
                Interlocked.Increment(ref queries);
                queryStarted.TrySetResult();
                return finishQuery.Task.WaitAsync(cancellation);
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        workload.Start(1);
        await queryStarted.Task.WaitAsync(timeout.Token);

        // Act
        workload.Configure(settings with { Kind = WorkloadKind.OpenClose });
        finishQuery.SetResult();
        await secondReleased.Task.WaitAsync(timeout.Token);

        // Assert
        Assert.Equal(1, Volatile.Read(ref queries));
        Assert.Null(workload.Failure);
        Assert.True(workload.Running);
    }

    /// <summary>A connection timeout is counted and retried instead of killing its worker.</summary>
    [Fact]
    public async Task Start_OpenTimesOut_RecordsErrorAndRetries()
    {
        // Arrange
        var measurements = new Measurements();
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true",
            measurements, Settings.Default(), (_, cancellation) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return Task.FromException(new TimeoutException());
                }

                retried.TrySetResult();
                return Task.Delay(Timeout.Infinite, cancellation);
            });

        // Act
        workload.Start(1);
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(5));
        workload.Reap();
        Sample sample = measurements.Take(1, 0, 0, workload.Active, 0);

        // Assert
        Assert.Null(workload.Failure);
        Assert.True(workload.Running);
        Assert.Equal(1d, sample.Values["Errors/s"]);
        Assert.Contains("timeout", measurements.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    /// <summary>After a failed worker drains, starting again clears the old fatal failure.</summary>
    [Fact]
    public async Task Start_DrainedWorkerFailure_AllowsRestart()
    {
        // Arrange
        var restarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true",
            new Measurements(), Settings.Default(), (_, cancellation) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return Task.FromException(new InvalidOperationException("test failure"));
                }

                restarted.TrySetResult();
                return Task.Delay(Timeout.Infinite, cancellation);
            });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        workload.Start(1);
        while (workload.Failure is null)
        {
            await Task.Delay(10, timeout.Token);
            workload.Reap();
        }
        workload.Pause();
        workload.Reap();

        // Act
        workload.Start(1);
        await restarted.Task.WaitAsync(timeout.Token);
        workload.Reap();

        // Assert
        Assert.Null(workload.Failure);
        Assert.True(workload.Running);
        Assert.Equal(1, workload.Active);
    }

    /// <summary>Clearing a non-pooled workload reports that there is no pool to clear.</summary>
    [Fact]
    public void ClearPool_NonPooledMode_ReportsNoPool()
    {
        // Arrange
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true",
            new Measurements(), Settings.Default() with { Pooling = false });

        // Act
        bool cleared = workload.ClearPool();

        // Assert
        Assert.False(cleared);
        Assert.False(workload.Running);
    }

    /// <summary>A live mode change updates the pool used by the clear command.</summary>
    [Fact]
    public void ClearPool_SwitchedToNonPooledMode_ReportsNoPool()
    {
        // Arrange
        Settings settings = Settings.Default();
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true",
            new Measurements(), settings);

        // Act
        workload.Configure(settings with { Pooling = false });
        bool cleared = workload.ClearPool();

        // Assert
        Assert.False(cleared);
    }

    /// <summary>Clearing the selected pool marks a history boundary without starting workers.</summary>
    [Fact]
    public void ClearPool_PooledMode_MarksHistoryBoundary()
    {
        // Arrange
        var measurements = new Measurements();
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true",
            measurements, Settings.Default());

        // Act
        bool cleared = workload.ClearPool();
        Sample sample = measurements.Take(1, 0, 0, 0, 0);

        // Assert
        Assert.True(cleared);
        Assert.True(sample.Changed);
        Assert.False(workload.Running);
    }

    /// <summary>Rejected worker counts must not leave the workload marked as running.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1025)]
    public void Start_InvalidCount_RemainsPaused(int count)
    {
        // Arrange
        using var workload = new Workload(
            "Server=tcp:127.0.0.1,1433;Integrated Security=true",
            new Measurements(), Settings.Default());

        // Act
        Exception? error = Record.Exception(() => workload.Start(count));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(error);
        Assert.False(workload.Running);
        Assert.Equal(0, workload.Active);
    }

    /// <summary>A TCP peer that never answers prelogin must not keep the load running after pause.</summary>
    [Fact]
    public async Task Pause_PendingPrelogin_DrainsWorker()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var settings = new Settings(1, 1, 1, 32767, 0);
        using var workload = new Workload(
            $"Server=tcp:127.0.0.1,{port};Integrated Security=true;Connect Timeout=2",
            new Measurements(), settings);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        workload.Start(1);
        using TcpClient peer = await listener.AcceptTcpClientAsync(timeout.Token);
        workload.Pause();
        while (workload.Active != 0 && !timeout.IsCancellationRequested)
        {
            await Task.Delay(20, timeout.Token);
        }
        workload.Reap();

        // Assert
        Assert.False(workload.Running);
        Assert.Equal(0, workload.Active);
        Assert.Null(workload.Failure);
    }
}
