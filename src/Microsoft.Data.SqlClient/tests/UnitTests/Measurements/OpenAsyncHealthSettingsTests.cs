// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Experiment configuration cannot silently select an invalid or impossible workload.</summary>
public class OpenAsyncHealthSettingsTests
{
    /// <summary>Non-pooled initialization may warm the driver without claiming a reusable pool.</summary>
    [Fact]
    public void Validate_NonpooledInitializedDriver_Accepts()
    {
        // Arrange
        var settings = new OpenAsyncHealthSettings { Pooling = false, WarmDriver = true };

        // Act
        Exception? error = Record.Exception(settings.Validate);

        // Assert
        Assert.Null(error);
    }

    /// <summary>Reject ambiguous setup that both fills and empties the connection pool.</summary>
    [Fact]
    public void Validate_BothWarmupKinds_Throws()
    {
        // Arrange
        var settings = new OpenAsyncHealthSettings { WarmPool = true, WarmDriver = true };

        // Act
        Exception? error = Record.Exception(settings.Validate);

        // Assert
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>A stalled endpoint cannot prewarm successfully; reject it before any network work.</summary>
    [Fact]
    public void Validate_StalledWarmup_Throws()
    {
        // Arrange
        var settings = new OpenAsyncHealthSettings { WarmPool = true, StalledPrelogin = true };
        // Act
        Exception? error = Record.Exception(settings.Validate);
        // Assert
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>Reject thread caps incompatible with the selected minimum.</summary>
    [Fact]
    public void Validate_ThreadMaximumBelowMinimum_Throws()
    {
        // Arrange
        var settings = new OpenAsyncHealthSettings { MinimumThreads = 64, MaximumThreads = 32 };
        // Act
        Exception? error = Record.Exception(settings.Validate);
        // Assert
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>Zero or unbounded duration cannot accidentally create an unattended endless run.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Validate_InvalidDuration_Throws(int duration)
    {
        // Arrange
        var settings = new OpenAsyncHealthSettings { DurationSeconds = duration };
        // Act
        Exception? error = Record.Exception(settings.Validate);
        // Assert
        Assert.IsType<ArgumentException>(error);
    }
}
#endif
