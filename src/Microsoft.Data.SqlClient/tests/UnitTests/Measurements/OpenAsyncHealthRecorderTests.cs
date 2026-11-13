// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Verifies retained first-worker evidence and reconciled operation outcomes.</summary>
public class OpenAsyncHealthRecorderTests
{
    /// <summary>A fast worker cannot evict the first outcome of a late worker at the sample cap.</summary>
    [Fact]
    public void Finish_LateFirstWorker_PreservesItsOutcomeBeyondLimit()
    {
        // Arrange
        var recorder = new OpenAsyncHealthRecorder(workers: 2, sampleLimit: 1);
        recorder.Start(first: true);
        recorder.Opened();
        recorder.Finish(new(0, 0, 0, 1, 2, "Completed"));
        recorder.Start(first: false);
        recorder.Finish(new(0, 3, 4, null, 5, "Failed"));

        // Act
        recorder.Start(first: true);
        recorder.Finish(new(1, 0, 6, null, 7, "Cancelled"));
        var result = recorder.Snapshot();
        OpenAsyncHealthCounts counts = recorder.Counts();

        // Assert
        Assert.Equal(2, result.Observations.Length);
        Assert.Equal(1, result.Dropped);
        Assert.Equal(1, result.Observations[1].Worker);
        Assert.Equal("Cancelled", result.Observations[1].Outcome);
        Assert.Equal(3, counts.Started);
        Assert.Equal(1, counts.Completed);
        Assert.Equal(1, counts.Failed);
        Assert.Equal(1, counts.Cancelled);
        Assert.Equal(0, counts.InFlight);
        Assert.Equal(2, counts.WorkersStarted);
    }

    /// <summary>A cutoff snapshot cannot change while an outstanding operation drains.</summary>
    [Fact]
    public void Counts_OperationCompletesAfterSnapshot_PreservesCutoff()
    {
        // Arrange
        var recorder = new OpenAsyncHealthRecorder(1);
        recorder.Start(first: true);
        OpenAsyncHealthCounts cutoff = recorder.Counts();

        // Act
        recorder.OpenReturned(inline: false);
        recorder.Opened();
        recorder.Finish(new(0, 0, 1, 2, 3, "Completed"));

        // Assert
        Assert.Equal(1, cutoff.InFlight);
        Assert.Equal(0, cutoff.Completed);
        Assert.Equal(0, recorder.Counts().InFlight);
        Assert.Equal(1, recorder.Counts().PendingOpens);
    }
}
#endif
