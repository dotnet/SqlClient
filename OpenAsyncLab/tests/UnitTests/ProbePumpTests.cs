using System.Diagnostics;
using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>Health observation remains bounded and exposes dispatcher failure.</summary>
public class ProbePumpTests
{
    /// <summary>A stalled dispatcher cannot cause an unbounded queue of observer callbacks.</summary>
    [Fact]
    public void Pending_StalledDispatcher_CapsOutstandingAndCountsDrops()
    {
        // Arrange
        var measurements = new Measurements();
        var clock = Stopwatch.StartNew();
        using var probes = new ProbePump(measurements, clock, _ => true);

        // Act
        Assert.True(SpinWait.SpinUntil(
            () => probes.Pending().Outstanding == 64, TimeSpan.FromSeconds(5)));
        Thread.Sleep(100);
        var pending = probes.Pending();
        Sample sample = measurements.Take(
            clock.Elapsed.TotalSeconds, pending.Outstanding, pending.Oldest, 0, 0);

        // Assert
        Assert.Equal(64, pending.Outstanding);
        Assert.True(pending.Oldest >= 1000);
        Assert.True(sample.Values["Probes dropped/s"] > 0);
        Assert.Null(sample.Values["Dispatch p99 ~ms"]);
    }

    /// <summary>A rejected callback is surfaced, not silently counted as an outstanding probe.</summary>
    [Fact]
    public void Pending_RejectedDispatcher_ReportsFailureWithoutOutstandingProbe()
    {
        // Arrange
        using var probes = new ProbePump(new Measurements(), Stopwatch.StartNew(), _ => false);

        // Act
        bool failed = SpinWait.SpinUntil(
            () => probes.Failure != null, TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(failed);
        Assert.Equal(0, probes.Pending().Outstanding);
    }
}
