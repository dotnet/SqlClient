using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>Unsafe or contradictory settings fail before changing the runtime or network.</summary>
public class SettingsTests
{
    /// <summary>Both supported workloads are valid independently of pooling.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Validate_SupportedWorkload_AcceptsSelection(int kind)
    {
        // Arrange
        Settings settings = Settings.Default() with { Kind = (WorkloadKind)kind };

        // Act
        Exception? error = Record.Exception(() => settings.Validate(Environment.ProcessorCount));

        // Assert
        Assert.Null(error);
    }

    /// <summary>Unknown workload selections are rejected rather than silently doing no work.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Validate_UnknownWorkload_Throws(int kind)
    {
        // Arrange
        Settings settings = Settings.Default() with { Kind = (WorkloadKind)kind };

        // Act
        Exception? error = Record.Exception(() => settings.Validate(Environment.ProcessorCount));

        // Assert
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>Worker ceilings must satisfy runtime CPU and minimum-thread constraints.</summary>
    [Theory]
    [InlineData(0, 20)]
    [InlineData(21, 20)]
    [InlineData(1, 3)]
    [InlineData(1, 32768)]
    public void Validate_InvalidThreadLimits_Throws(int minimum, int maximum)
    {
        // Arrange
        var settings = new Settings(8, 100, minimum, maximum, 0);

        // Act
        var error = Record.Exception(() => settings.Validate(4));

        // Assert
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>The interactive delay range includes 200ms and higher added RTT.</summary>
    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(2000)]
    public void Validate_HighLatency_AcceptsPerDirectionDelay(int delay)
    {
        // Arrange
        var settings = new Settings(8, 100, 4, 100, delay);

        // Act
        var error = Record.Exception(() => settings.Validate(4));

        // Assert
        Assert.Null(error);
    }
}
