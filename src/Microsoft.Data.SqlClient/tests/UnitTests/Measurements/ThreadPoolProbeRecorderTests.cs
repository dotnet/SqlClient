// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Verifies bounded probe accounting independently of scheduler timing and SQL.</summary>
public class ThreadPoolProbeRecorderTests
{
    /// <summary>A delayed callback remains missing in the saved snapshot even after it finally runs.</summary>
    [Fact]
    public void Snapshot_LateCallback_PreservesMissingEvidence()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var held = new Queue<Action>();
        var recorder = new ThreadPoolProbeRecorder(1, time, time.GetTimestamp(), held.Enqueue);
        recorder.PostProbe(0);
        recorder.PostProbe(0);
        ThreadPoolProbeSnapshot before = recorder.Snapshot();

        // Act
        time.Advance(TimeSpan.FromMilliseconds(200));
        held.Dequeue()();
        ThreadPoolProbeSnapshot after = recorder.Snapshot();

        // Assert
        Assert.Equal(1, before.Dropped);
        Assert.Equal(1, before.Outstanding);
        Assert.Null(before.Samples[0].ExecutedMilliseconds);
        Assert.Equal(0, after.Outstanding);
        Assert.Equal(200, after.Samples[0].ExecutedMilliseconds);
    }

    /// <summary>Producer descheduling stays visible without injecting a burst of overdue probes.</summary>
    [Fact]
    public void RequestProbe_MissedCadence_RecordsOneProbeAndMissedSlots()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var recorder = new ThreadPoolProbeRecorder(1, time, time.GetTimestamp(), action => action());
        time.Advance(TimeSpan.FromMilliseconds(80));
        double next = 0;

        // Act
        recorder.RequestProbe(ref next, 80, 25);
        ThreadPoolProbeSnapshot result = recorder.Snapshot();

        // Assert
        Assert.Equal(100, next);
        Assert.Equal(4, result.Total);
        Assert.Equal(3, result.ProducerMissed);
        Assert.Equal(1, result.Executed);
        Assert.Equal(0, result.Samples[0].ScheduledMilliseconds);
        Assert.Equal(80, result.Samples[0].PostedMilliseconds);
    }

    /// <summary>Dispatcher errors are rethrown and counted separately from capacity loss.</summary>
    [Fact]
    public void PostProbe_DispatchThrows_ReconcilesRejectedCount()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var recorder = new ThreadPoolProbeRecorder(1, time, time.GetTimestamp(),
            _ => throw new InvalidOperationException());

        // Act
        Exception? error = Record.Exception(() => recorder.PostProbe(0));
        ThreadPoolProbeSnapshot result = recorder.Snapshot();

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, result.Rejected);
        Assert.Equal(1, result.Total);
        Assert.Equal(0, result.Posted);
        Assert.Equal(0, result.Outstanding);
        Assert.Empty(result.Samples);
    }
}
#endif
