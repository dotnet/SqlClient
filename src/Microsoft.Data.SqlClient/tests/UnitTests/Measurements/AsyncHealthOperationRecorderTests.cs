// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Measurements;

/// <summary>Verifies benchmark queue/open timings and outcomes, including missing and late work.</summary>
public class AsyncHealthOperationRecorderTests
{
    /// <summary>Cancelled work stays visible instead of disappearing into successful-open latency.</summary>
    [Fact]
    public void Snapshot_CancelledWorker_PreservesQueueTimingAndOutcome()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var recorder = new AsyncHealthOperationRecorder(time);
        var invocation = recorder.BeginInvocation(2);
        var success = invocation.Offer(0);
        var cancelled = invocation.Offer(1);

        // Act
        time.Advance(TimeSpan.FromMilliseconds(20));
        success.Start();
        success.OpenReturned(false);
        time.Advance(TimeSpan.FromMilliseconds(30));
        success.Opened();
        time.Advance(TimeSpan.FromMilliseconds(10));
        success.Complete();
        cancelled.Start();
        time.Advance(TimeSpan.FromMilliseconds(40));
        cancelled.Fail(new OperationCanceledException());
        AsyncHealthOperationSnapshot result = recorder.Snapshot();
        AsyncHealthSummary summary = AsyncHealthSummary.Create(new AsyncHealthWindow(), result);

        // Assert
        Assert.Equal(1, result.Completed);
        Assert.Equal(1, result.Cancelled);
        Assert.Equal(2, result.WorkersStarted);
        Assert.Equal(0, result.InFlight);
        Assert.Equal(0, result.FirstWorkerOutcomesMissing);
        Assert.Equal(1, result.PendingOpens);
        Assert.Equal(60, summary.FirstWorkerQueueP99Ms);
        Assert.Equal(50, result.Observations[0].OpenEndMs);
        Assert.Equal(20, result.Observations[0].StartMs);
        Assert.Equal(60, result.Observations[1].StartMs);
        Assert.Equal(100, result.Observations[1].EndMs);
        Assert.Equal(10, result.CompletedPerSecond);
        Assert.Equal("Incomplete", summary.OperationEvidence);
    }

    /// <summary>A late first record survives the cap, and later completion cannot mutate a snapshot.</summary>
    [Fact]
    public void Snapshot_LateFirstWorkerAfterCap_RetainsQueuedRecord()
    {
        // Arrange
        var recorder = new AsyncHealthOperationRecorder(sampleLimit: 1);
        var invocation = recorder.BeginInvocation(2);
        var first = invocation.Offer(0);
        first.Start();
        first.Opened();
        first.Complete();
        var dropped = invocation.Offer(0);
        dropped.Start();
        dropped.Opened();
        dropped.Complete();
        var late = invocation.Offer(1);
        AsyncHealthOperationSnapshot before = recorder.Snapshot();

        // Act
        late.Start();
        late.Opened();
        late.Complete();
        invocation.Complete();
        AsyncHealthOperationSnapshot after = recorder.Snapshot();

        // Assert
        Assert.Equal(2, before.Observations.Length);
        Assert.Equal(1, before.ObservationsDropped);
        Assert.Equal(1, before.Queued);
        Assert.Equal(1, before.FirstWorkerOutcomesMissing);
        Assert.Equal("Queued", before.Observations[1].Outcome);
        Assert.Null(before.Observations[1].EndMs);
        Assert.Equal(3, after.Completed);
        Assert.Equal(0, after.Queued);
        Assert.Equal(0, after.FirstWorkerOutcomesMissing);
    }

    /// <summary>Worker identities are scoped to each invocation, not accidentally reused across BDN calls.</summary>
    [Fact]
    public void Snapshot_RepeatedWorkerIndex_CountsEachInvocation()
    {
        // Arrange
        var recorder = new AsyncHealthOperationRecorder(sampleLimit: 1);

        // Act
        for (int i = 0; i < 2; i++)
        {
            var invocation = recorder.BeginInvocation(1);
            var operation = invocation.Offer(0);
            operation.Start();
            operation.Opened();
            operation.Complete();
            invocation.Complete();
        }
        AsyncHealthOperationSnapshot result = recorder.Snapshot();

        // Assert
        Assert.Equal(2, result.Invocations);
        Assert.Equal(2, result.CompletedInvocations);
        Assert.Equal(2, result.ExpectedWorkers);
        Assert.Equal(2, result.WorkersStarted);
        Assert.Equal(0, result.FirstWorkerOutcomesMissing);
        Assert.Equal(2, result.Observations.Length);
        Assert.NotEqual(result.Observations[0].Invocation, result.Observations[1].Invocation);
    }

    /// <summary>Open failures retain their duration and sanitized type without leaking exception text.</summary>
    [Fact]
    public void Snapshot_FailedOpen_RecordsFailureWithoutPrivateMessage()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var recorder = new AsyncHealthOperationRecorder(time);
        var operation = recorder.BeginInvocation(1, trackConnectionOpens: true).Offer(0);
        operation.Start();

        // Act
        time.Advance(TimeSpan.FromMilliseconds(100));
        operation.Fail(new InvalidOperationException("private endpoint details"));
        AsyncHealthOperationSnapshot result = recorder.Snapshot();

        // Assert
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.OpenFailures);
        Assert.Equal(0, result.FirstWorkerOutcomesMissing);
        Assert.Equal(0, result.Opened);
        Assert.Equal(100, result.Observations[0].EndMs);
        Assert.Null(result.Observations[0].OpenEndMs);
        Assert.Equal(nameof(InvalidOperationException), result.Observations[0].Error);
    }
}
#endif
