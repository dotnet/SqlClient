namespace OpenAsyncLab;

internal enum WorkloadKind
{
    OpenClose,
    OpenQueryClose
}

internal sealed record Settings(
    int Workers, int PoolSize, int MinimumThreads, int MaximumThreads,
    int DelayMilliseconds, bool Pooling = true, WorkloadKind Kind = WorkloadKind.OpenClose,
    bool LongRunningOpen = false)
{
    public const int ControlCount = 5;
    public const string LongRunningSwitch =
        "Switch.Microsoft.Data.SqlClient.UseLongRunningPoolV2Open";

    public static Settings Default()
    {
        ThreadPool.GetMinThreads(out int minimum, out _);
        ThreadPool.GetMaxThreads(out int maximum, out _);
        bool longRunning = AppContext.TryGetSwitch(LongRunningSwitch, out bool enabled) && enabled;
        return new Settings(8, 100, minimum, maximum, 0, LongRunningOpen: longRunning);
    }

    public void Validate(int processors)
    {
        if (Workers is < 1 or > 1024)
        {
            throw new ArgumentException("Workers must be between 1 and 1024.");
        }
        if (PoolSize is < 1 or > 4096)
        {
            throw new ArgumentException("SQL pool size must be between 1 and 4096.");
        }
        if (MinimumThreads < 1 || MaximumThreads < Math.Max(processors, MinimumThreads)
            || MaximumThreads > 32767)
        {
            throw new ArgumentException(
                $"Require 1 <= TP min <= TP max <= 32767; TP max must be >= {processors}.");
        }
        if (DelayMilliseconds is < 0 or > 2000)
        {
            throw new ArgumentException("Delay must be 0..2000 ms per direction.");
        }
        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentException("Unknown workload.");
        }
    }

    public int Value(int index) => index switch
    {
        0 => Workers,
        1 => PoolSize,
        2 => MinimumThreads,
        3 => MaximumThreads,
        4 => DelayMilliseconds,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public Settings WithValue(int index, int value) => index switch
    {
        0 => this with { Workers = value },
        1 => this with { PoolSize = value },
        2 => this with { MinimumThreads = value },
        3 => this with { MaximumThreads = value },
        4 => this with { DelayMilliseconds = value },
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

internal sealed class PoolLimits : IDisposable
{
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int _minimumIo;
    private readonly int _maximumIo;
    private bool _disposed;

    public PoolLimits()
    {
        ThreadPool.GetMinThreads(out _minimum, out _minimumIo);
        ThreadPool.GetMaxThreads(out _maximum, out _maximumIo);
    }

    public void Apply(int minimum, int maximum)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThreadPool.GetMinThreads(out int oldMinimum, out _);
        ThreadPool.GetMaxThreads(out int oldMaximum, out _);
        // Raise the ceiling first; lower the minimum before lowering the ceiling.
        if (!ThreadPool.SetMaxThreads(Math.Max(maximum, oldMaximum), _maximumIo)
            || !ThreadPool.SetMinThreads(minimum, _minimumIo)
            || !ThreadPool.SetMaxThreads(maximum, _maximumIo))
        {
            Restore(oldMinimum, oldMaximum);
            throw new InvalidOperationException("The runtime rejected the ThreadPool limits.");
        }
    }

    private void Restore(int minimum, int maximum)
    {
        ThreadPool.GetMaxThreads(out int currentMaximum, out _);
        if (!ThreadPool.SetMaxThreads(Math.Max(maximum, currentMaximum), _maximumIo)
            || !ThreadPool.SetMinThreads(minimum, _minimumIo)
            || !ThreadPool.SetMaxThreads(maximum, _maximumIo))
        {
            throw new InvalidOperationException("Could not restore the ThreadPool limits.");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Restore(_minimum, _maximum);
            _disposed = true;
        }
    }
}
