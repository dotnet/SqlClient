using System.Globalization;
using System.Text;

namespace OpenAsyncLab;

internal static class Screen
{
    public static string Render(
        Settings settings, History history, Sample? latest, double now, int width, int height,
        int selected, int page, string? edit, string status, bool demo, bool running, bool netem,
        bool poolV2 = false)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var lines = new List<string>
        {
            demo ? "OpenAsyncLab | DEMO: SIMULATED DATA; no SQL, ThreadPool changes, or netem"
                : $"OpenAsyncLab | {(running ? "RUNNING" : "PAUSED")}"
                    + $" | {(settings.Pooling ? "POOLED" : "NON-POOLED")} OpenAsync"
                    + $" | Pool {(poolV2 ? "V2" : "V1")}"
                    + $" ({(!settings.Pooling ? "inactive" : poolV2 ? "Channel" : "WaitHandle")})",
            $"Workers={settings.Workers}  Pool={settings.PoolSize} ({(settings.Pooling ? "on" : "off")})"
                + $"  TP min/max={settings.MinimumThreads}/{settings.MaximumThreads}",
            netem || demo
                ? $"Delay={settings.DelayMilliseconds} ms/direction"
                    + $" (~{settings.DelayMilliseconds * 2} ms RTT)  netem={(netem ? "enabled" : "off")}"
                : "Delay=unmanaged (external netem may apply; use the owning window's controls)",
            settings.Kind == WorkloadKind.OpenClose ? "Workload=Open/close"
                : "Workload=Open/query/close (SELECT 1)",
            $"V2 dispatch={(settings.LongRunningOpen ? "LongRunning" : "ThreadPool")}"
                + (!poolV2 ? " | inactive: Pool V1"
                    : !settings.Pooling ? " | inactive: non-pooled | L: toggle"
                    : " | L: toggle (new acquisitions)"),
            "Up/Down: select | Enter: edit | +/-: adjust | Space: run/pause | Q: query",
            "W: workload | P: pooled/non-pooled | C: clear pool | Tab: charts | Esc: quit"
        };
        string[] labels = ["Workers", "SQL pool max", "TP min workers", "TP max workers", "Delay ms/dir"];
        lines.Add(edit is null ? $"Selected: {labels[selected]} = {settings.Value(selected)}"
            : $"Edit {labels[selected]}: {edit}_ (Enter applies)");
        lines.Add(status);
        int perPage = Math.Max(1, height - 13);
        string[] metrics = latest?.Values.Keys.ToArray() ?? [];
        int pages = Math.Max(1, (metrics.Length + perPage - 1) / perPage);
        int start = (page % pages) * perPage;
        lines.Add($"Metric / current                   last 30s: oldest -> newest (page {page % pages + 1}/{pages})");
        int chartWidth = Math.Clamp(width - 54, 10, 30);
        foreach (string metric in metrics.Skip(start).Take(perPage))
        {
            (string chart, double maximum) = history.Chart(metric, now, chartWidth);
            double? current = latest!.Values[metric];
            string value = current?.ToString("0.##", CultureInfo.InvariantCulture) ?? "N/A";
            lines.Add($"{metric,-24} {value,9} [{chart}] max {maximum:0.##}");
        }
        lines.Add("1s completion cohorts; outstanding work excluded. p99 ~max with <100 samples.");
        lines.Add("~ = histogram upper bound (5%/1us). ? = no sample; | = setting change.");
        lines.Add("Warm pooled opens may do no I/O. Use W for query workload to exercise RTT.");
        return string.Join(Environment.NewLine, lines.Take(height).Select(
            line => Sanitize(line).PadRight(Math.Min(width, line.Length)).Truncate(width)));
    }

    public static string Update(string frame, string? previous)
    {
        string[] lines = frame.Split(Environment.NewLine);
        string[] oldLines = previous?.Split(Environment.NewLine) ?? [];
        var output = new StringBuilder();
        if (previous is null)
        {
            output.Append("\u001b[2J");
        }

        for (int row = 0; row < Math.Max(lines.Length, oldLines.Length); row++)
        {
            string line = row < lines.Length ? lines[row] : "";
            if (row < oldLines.Length && line == oldLines[row])
            {
                continue;
            }

            output.Append("\u001b[").Append(row + 1).Append(";1H")
                .Append(line).Append("\u001b[K");
        }
        return output.ToString();
    }

    private static string Sanitize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            result.Append(char.IsControl(character) ? ' ' : character);
        }
        return result.ToString();
    }

    private static string Truncate(this string text, int length) =>
        text.Length > length ? text[..length] : text;
}
