using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>Streaming metrics preserve rates, outstanding probes, and per-cohort accounting.</summary>
public class MeasurementsTests
{
    /// <summary>Rates use elapsed seconds rather than assuming a punctual UI tick.</summary>
    [Fact]
    public void Take_DelayedTick_NormalizesByElapsedTime()
    {
        // Arrange
        var measurements = new Measurements();
        measurements.Opened(10);
        measurements.Opened(20);

        // Act
        Sample first = measurements.Take(2, 0, 0, 1, 10);
        Sample second = measurements.Take(3, 0, 0, 1, 10);

        // Assert
        Assert.Equal(1, first.Values["Opens/s"]);
        Assert.Equal(0, second.Values["Opens/s"]);
        Assert.Null(second.Values["Open p99 ~ms"]);
    }

    /// <summary>A fully stalled pool shows pending age and drops, not a healthy zero p99.</summary>
    [Fact]
    public void Take_NoProbeCompletions_ReportsCensoredTail()
    {
        // Arrange
        var measurements = new Measurements();
        measurements.Lost(10, 3);

        // Act
        Sample result = measurements.Take(1, 64, 2500, 8, 20);

        // Assert
        Assert.Null(result.Values["Dispatch p99 ~ms"]);
        Assert.Equal(64, result.Values["Probes outstanding"]);
        Assert.Equal(2500, result.Values["Oldest probe ms"]);
        Assert.Equal(10, result.Values["Probes dropped/s"]);
        Assert.Equal(3, result.Values["Producer missed/s"]);
    }

    /// <summary>Probe and SQL latency distributions are separate measurements.</summary>
    [Fact]
    public void Take_SqlAndProbeCompletions_KeepsLatencyDefinitionsSeparate()
    {
        // Arrange
        var measurements = new Measurements();
        measurements.Opened(300);
        measurements.Probe(2, 1);
        measurements.MarkChange();

        // Act
        Sample result = measurements.Take(1, 0, 0, 8, 20);

        // Assert
        Assert.InRange(result.Values["Open p99 ~ms"]!.Value, 300, 315);
        Assert.InRange(result.Values["Dispatch p99 ~ms"]!.Value, 2, 2.1);
        Assert.True(result.Changed);
    }
}
