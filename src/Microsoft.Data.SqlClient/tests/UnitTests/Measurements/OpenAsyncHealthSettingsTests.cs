// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Verifies that fresh-process workload setup cannot silently select impossible paths.</summary>
public class OpenAsyncHealthSettingsTests
{
    /// <summary>Initializing a non-pooled driver is valid without claiming a reusable SQL pool.</summary>
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

    /// <summary>Filling and clearing the same pool are mutually exclusive setup contracts.</summary>
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

    /// <summary>The negative control never answers login, so warming it must fail before network I/O.</summary>
    [Fact]
    public void Validate_StalledWarmup_Throws()
    {
        // Arrange
        var settings = new OpenAsyncHealthSettings { WarmDriver = true, StalledPrelogin = true };

        // Act
        Exception? error = Record.Exception(settings.Validate);

        // Assert
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>An empty or unbounded measurement cannot become an unattended healthy result.</summary>
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
