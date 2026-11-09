using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>Histories represent elapsed time, missing data, and configuration boundaries.</summary>
public class HistoryTests
{
    /// <summary>Old samples expire even if screen updates stop for a while.</summary>
    [Fact]
    public void Chart_ElapsedThirtySeconds_ExpiresOldSamples()
    {
        // Arrange
        var history = new History();
        history.Add(new Sample(0, new() { ["x"] = 1000 }, false));
        history.Add(new Sample(29, new() { ["x"] = 2 }, false));

        // Act
        var result = history.Chart("x", 31, 30);

        // Assert
        Assert.Single(history.Samples);
        Assert.Equal(2, result.Maximum);
        Assert.Equal(30, result.Chart.Length);
    }

    /// <summary>No observation is visibly different from a measured zero.</summary>
    [Fact]
    public void Chart_MissingAndZeroSamples_DistinguishesValues()
    {
        // Arrange
        var history = new History();
        history.Add(new Sample(1, new() { ["x"] = null }, false));
        history.Add(new Sample(2, new() { ["x"] = 0 }, false));

        // Act
        string chart = history.Chart("x", 2, 60).Chart;

        // Assert
        Assert.Contains('?', chart);
        Assert.Contains('.', chart);
    }

    /// <summary>A setting change is retained alongside the preceding history.</summary>
    [Fact]
    public void Chart_ConfigurationChange_ShowsBoundary()
    {
        // Arrange
        var history = new History();
        history.Add(new Sample(1, new() { ["x"] = 2 }, true));

        // Act
        string chart = history.Chart("x", 1, 30).Chart;

        // Assert
        Assert.EndsWith("|", chart);
    }

    /// <summary>Increasing measured values use increasingly tall bars on the same scale.</summary>
    [Fact]
    public void Chart_IncreasingValues_RendersProportionalBars()
    {
        // Arrange
        var history = new History();
        history.Add(new Sample(1, new() { ["x"] = 0 }, false));
        history.Add(new Sample(2, new() { ["x"] = 4 }, false));
        history.Add(new Sample(3, new() { ["x"] = 8 }, false));

        // Act
        string chart = history.Chart("x", 4, 30).Chart;

        // Assert
        Assert.EndsWith(".\u2584\u2588", chart);
    }
}
