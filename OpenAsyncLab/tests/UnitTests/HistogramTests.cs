using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>Protects missing-data semantics and bounded quantile accuracy.</summary>
public class HistogramTests
{
    /// <summary>No completions must not look like zero latency.</summary>
    [Fact]
    public void Percentile_NoSamples_ReturnsNull()
    {
        // Act
        double? value = new Histogram().Percentile(0.99);

        // Assert
        Assert.Null(value);
    }

    /// <summary>Quantiles bound the selected rank without exceeding the advertised error.</summary>
    [Theory]
    [InlineData(0.001)]
    [InlineData(0.1)]
    [InlineData(200)]
    [InlineData(30000)]
    public void Percentile_SingleSample_ReturnsAccurateUpperBound(double value)
    {
        // Arrange
        var histogram = new Histogram();
        histogram.Record(value);

        // Act
        double? quantile = histogram.Percentile(0.99);

        // Assert
        Assert.NotNull(quantile);
        Assert.InRange(quantile.Value, value * (1 - 1e-12), value * 1.05 + 1e-12);
    }

    /// <summary>Percentiles use nearest-rank counts rather than averaging tail latencies.</summary>
    [Fact]
    public void Percentile_KnownRanks_SelectsCorrectBucket()
    {
        // Arrange
        var histogram = new Histogram();
        for (int i = 1; i <= 100; i++) histogram.Record(i);

        // Act
        double? p50 = histogram.Percentile(0.5);
        double? p99 = histogram.Percentile(0.99);

        // Assert
        Assert.InRange(p50!.Value, 50, 52.5);
        Assert.InRange(p99!.Value, 99, 103.95);
    }

    /// <summary>Taking a new cohort must not retain an earlier cohort's tail.</summary>
    [Fact]
    public void Clear_ExistingSamples_RemovesPreviousCohort()
    {
        // Arrange
        var histogram = new Histogram();
        histogram.Record(100);

        // Act
        histogram.Clear();

        // Assert
        Assert.Null(histogram.Percentile(0.99));
    }
}
