using Xunit;

namespace OpenAsyncLab.Tests;

/// <summary>ThreadPool mutations must not overlap any other test in this assembly.</summary>
[CollectionDefinition("ThreadPool limits", DisableParallelization = true)]
public class PoolLimitsCollection
{
}

/// <summary>Runtime limits are applied in a valid order and restored on exit.</summary>
[Collection("ThreadPool limits")]
public class PoolLimitsTests
{
    /// <summary>Lowering the maximum and then disposing restores both original limits.</summary>
    [Fact]
    public void Dispose_ChangedLimits_RestoresOriginalLimits()
    {
        // Arrange
        ThreadPool.GetMinThreads(out int minimum, out _);
        ThreadPool.GetMaxThreads(out int maximum, out _);

        // Act
        using (var limits = new PoolLimits())
        {
            limits.Apply(1, Environment.ProcessorCount + 2);
            ThreadPool.GetMinThreads(out int changedMinimum, out _);
            ThreadPool.GetMaxThreads(out int changedMaximum, out _);

            // Assert
            Assert.Equal(1, changedMinimum);
            Assert.Equal(Environment.ProcessorCount + 2, changedMaximum);
        }
        ThreadPool.GetMinThreads(out int restoredMinimum, out _);
        ThreadPool.GetMaxThreads(out int restoredMaximum, out _);
        Assert.Equal(minimum, restoredMinimum);
        Assert.Equal(maximum, restoredMaximum);
    }

    /// <summary>A disposed owner must not restore limits over a later owner's settings.</summary>
    [Fact]
    public void Dispose_AlreadyDisposed_PreservesSubsequentLimits()
    {
        // Arrange
        using var restore = new PoolLimits();
        using var original = new PoolLimits();
        original.Dispose();
        int subsequentMaximum = Environment.ProcessorCount + 3;
        restore.Apply(1, subsequentMaximum);

        // Act
        original.Dispose();
        ThreadPool.GetMinThreads(out int minimum, out _);
        ThreadPool.GetMaxThreads(out int maximum, out _);

        // Assert
        Assert.Equal(1, minimum);
        Assert.Equal(subsequentMaximum, maximum);
    }

    /// <summary>A disposed owner cannot change global ThreadPool limits again.</summary>
    [Fact]
    public void Apply_DisposedOwner_Throws()
    {
        // Arrange
        using var limits = new PoolLimits();
        limits.Dispose();

        // Act
        Exception? error = Record.Exception(() => limits.Apply(1, Environment.ProcessorCount + 2));

        // Assert
        Assert.IsType<ObjectDisposedException>(error);
    }
}
