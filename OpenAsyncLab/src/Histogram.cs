namespace OpenAsyncLab;

/// <summary>Bounded logarithmic histogram: quantile upper bounds within 5% or 1 microsecond.</summary>
internal sealed class Histogram
{
    private const double Floor = 0.001;
    private const double Ratio = 1.05;
    private readonly long[] _bins = new long[512];
    private long _count;

    public void Record(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }
        int index = milliseconds <= Floor ? 0
            : (int)Math.Ceiling(Math.Log(milliseconds / Floor) / Math.Log(Ratio));
        if (index >= _bins.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Histogram range exceeded.");
        }
        _bins[index]++;
        _count++;
    }

    public double? Percentile(double quantile)
    {
        if (quantile <= 0 || quantile > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantile));
        }
        if (_count == 0)
        {
            return null;
        }
        long target = (long)Math.Ceiling(_count * quantile);
        long count = 0;
        for (int i = 0; i < _bins.Length; i++)
        {
            count += _bins[i];
            if (count >= target)
            {
                return Floor * Math.Pow(Ratio, i);
            }
        }
        throw new InvalidOperationException("Histogram accounting is inconsistent.");
    }

    public void Clear()
    {
        Array.Clear(_bins);
        _count = 0;
    }
}
