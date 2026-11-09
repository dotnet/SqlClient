namespace OpenAsyncLab;

internal sealed class History
{
    private readonly Queue<Sample> _samples = new();
    public IReadOnlyCollection<Sample> Samples => _samples;

    public void Add(Sample sample)
    {
        if (_samples.Count > 0 && sample.Time <= _samples.Last().Time)
        {
            throw new ArgumentException("History timestamps must increase.");
        }
        _samples.Enqueue(sample);
        Trim(sample.Time);
    }

    private void Trim(double now)
    {
        while (_samples.TryPeek(out Sample? first) && first.Time < now - 30)
        {
            _samples.Dequeue();
        }
    }

    public (string Chart, double Maximum) Chart(string metric, double now, int width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        Trim(now);
        double maximum = _samples.Select(s => s.Values.GetValueOrDefault(metric) ?? 0)
            .DefaultIfEmpty().Max();
        char[] chart = Enumerable.Repeat(' ', width).ToArray();
        const string Levels = ".\u2581\u2582\u2583\u2584\u2585\u2586\u2587\u2588";
        foreach (Sample sample in _samples)
        {
            int position = Math.Clamp((int)((sample.Time - (now - 30)) / 30 * width), 0, width - 1);
            double? value = sample.Values.GetValueOrDefault(metric);
            chart[position] = sample.Changed ? '|' : value is null ? '?'
                : Levels[maximum == 0 ? 0
                    : Math.Clamp((int)(value.Value / maximum * (Levels.Length - 1)),
                        0, Levels.Length - 1)];
        }
        return (new string(chart), maximum);
    }
}
