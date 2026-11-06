// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.SqlClient.PerformanceTests;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>SQL-independent probe contracts, including missing callbacks and producer descheduling.</summary>
    public class AsyncHealthTests
    {
        /// <summary>Capacity loss and producer missed ticks stay visible; late callbacks cannot mutate exports.</summary>
        [Fact]
        public void BoundedProbesPreserveMissingAndLateExecution()
        {
            var timeProvider = new FakeTimeProvider();
            var held = new Queue<Action>();
            var probes = new ThreadPoolProbeRecorder(2, timeProvider, timeProvider.GetTimestamp(), held.Enqueue);
            timeProvider.Advance(TimeSpan.FromMilliseconds(20));
            probes.UpdateCountsForMissedIntervals(3);
            probes.PostProbe(10);
            probes.PostProbe(20);
            probes.PostProbe(30);
            ThreadPoolProbeSnapshot before = probes.Snapshot();
            Assert.Equal(6, before.Total);
            Assert.Equal(1, before.Dropped);
            Assert.Equal(0, before.Rejected);
            Assert.Equal(0, before.Skipped);
            Assert.Equal(3, before.ProducerMissed);
            Assert.Equal(2, before.Outstanding);
            Assert.Null(before.Samples[0].ExecutedMilliseconds);
            timeProvider.Advance(TimeSpan.FromMilliseconds(80));
            while (held.TryDequeue(out Action? callback)) callback();
            ThreadPoolProbeSnapshot after = probes.Snapshot();
            Assert.Equal(2, after.Executed);
            Assert.Equal(0, after.Outstanding);
            Assert.Equal(100, after.Samples[0].ExecutedMilliseconds);
            Assert.Null(before.Samples[0].ExecutedMilliseconds);
            Assert.Equal(10, after.Samples[0].PostedMilliseconds - after.Samples[0].ScheduledMilliseconds);
        }

        /// <summary>Dispatcher errors reconcile counts and remain explicit exceptions.</summary>
        [Fact]
        public void DispatchFailureIsAccountedAndRethrown()
        {
            // Arrange
            var timeProvider = new FakeTimeProvider();
            var probes = new ThreadPoolProbeRecorder(1, timeProvider, timeProvider.GetTimestamp(),
                _ => throw new InvalidOperationException("test"));

            // Act
            Action post = () => probes.PostProbe(0);

            // Assert
            Assert.Throws<InvalidOperationException>(post);
            ThreadPoolProbeSnapshot result = probes.Snapshot();
            Assert.Equal(1, result.Total);
            Assert.Equal(0, result.Dropped);
            Assert.Equal(1, result.Rejected);
            Assert.Equal(0, result.Posted);
            Assert.Equal(0, result.Executed);
            Assert.Equal(0, result.Skipped);
            Assert.Equal(0, result.Outstanding);
            Assert.Empty(result.Samples);
        }

        /// <summary>Exhausting sample retention does not count as a capacity or dispatch drop.</summary>
        [Fact]
        public void PostProbe_SampleLimitReached_DoesNotCountAsDropped()
        {
            // Arrange
            var timeProvider = new FakeTimeProvider();
            var probes = new ThreadPoolProbeRecorder(1, timeProvider, timeProvider.GetTimestamp(), action => action(), sampleLimit: 2);

            // Act
            probes.PostProbe(0);
            probes.PostProbe(0);
            probes.PostProbe(0);
            ThreadPoolProbeSnapshot result = probes.Snapshot();

            // Assert
            Assert.Equal(2, result.Executed);
            Assert.Equal(0, result.Dropped);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(3, result.Total);
            Assert.Equal(2, result.Posted);
        }

        /// <summary>Sample retention takes precedence when no further probe can be retained or admitted.</summary>
        [Fact]
        public void PostProbe_BothLimitsReached_CountsAsSkipped()
        {
            // Arrange
            var timeProvider = new FakeTimeProvider();
            var probes = new ThreadPoolProbeRecorder(1, timeProvider, timeProvider.GetTimestamp(), _ => { }, sampleLimit: 1);

            // Act
            probes.PostProbe(0);
            probes.PostProbe(0);
            ThreadPoolProbeSnapshot result = probes.Snapshot();

            // Assert
            Assert.Equal(2, result.Total);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(0, result.Dropped);
            Assert.Equal(1, result.Outstanding);
        }

        /// <summary>A delayed producer drops missed ticks instead of flooding the pool with catch-up work.</summary>
        [Theory]
        [InlineData(100, 160, 2, 175)]
        [InlineData(100, 100, 0, 125)]
        [InlineData(100, 125, 1, 150)]
        public void OfferDue_DelayedProducer_PreservesOriginalDeadline(double scheduled, double now, long missed, double expectedNext)
        {
            // Arrange
            double next = scheduled;
            var timeProvider = new FakeTimeProvider();
            var probes = new ThreadPoolProbeRecorder(10, timeProvider, timeProvider.GetTimestamp(), action => action());
            timeProvider.Advance(TimeSpan.FromMilliseconds(now));

            // Act
            probes.RequestProbe(ref next, now, 25);
            ThreadPoolProbeSnapshot result = probes.Snapshot();

            // Assert
            Assert.Equal(expectedNext, next);
            Assert.Equal(missed + 1, result.Total);
            Assert.Equal(missed, result.ProducerMissed);
            Assert.Equal(0, result.Dropped);
            Assert.Equal(1, result.Posted);
            Assert.Equal(scheduled, result.Samples[0].ScheduledMilliseconds);
            Assert.Equal(now - scheduled, result.Samples[0].PostedMilliseconds - result.Samples[0].ScheduledMilliseconds);
        }

        /// <summary>Probe timestamps remain relative to the supplied origin rather than recorder construction.</summary>
        [Fact]
        public void PostProbe_SharedTimestampOrigin_PreservesElapsedTimes()
        {
            // Arrange
            var timeProvider = new FakeTimeProvider();
            timeProvider.Advance(TimeSpan.FromDays(1));
            long origin = timeProvider.GetTimestamp();
            timeProvider.Advance(TimeSpan.FromMilliseconds(20));
            var callbacks = new Queue<Action>();
            var probes = new ThreadPoolProbeRecorder(1, timeProvider, origin, callbacks.Enqueue);

            // Act
            probes.PostProbe(10);
            timeProvider.Advance(TimeSpan.FromMilliseconds(30));
            callbacks.Dequeue()();
            ThreadPoolProbeSnapshot result = probes.Snapshot();

            // Assert
            Assert.Equal(20, result.Samples[0].PostedMilliseconds);
            Assert.Equal(50, result.Samples[0].ExecutedMilliseconds);
            Assert.Equal(1, result.Executed);
        }

        /// <summary>A callback which never runs remains outstanding after the bounded recovery deadline.</summary>
        [Fact]
        public void RecoveryTimeoutPreservesOutstandingProbes()
        {
            using var offered = new ManualResetEventSlim();
            Action? held = null;
            using var observer = new ThreadPoolHealthObserver(
                new AsyncHealthOptions { RecoveryMilliseconds = 0, ProbeCapacity = 1 },
                callback => { held = callback; offered.Set(); });
            observer.Start();
            Assert.True(offered.Wait(TimeSpan.FromSeconds(30)));
            AsyncHealthWindow result = observer.Complete();
            Assert.Equal(1, result.Probes.Outstanding);
            Assert.Null(result.Probes.Samples[0].ExecutedMilliseconds);
            held!();
            Assert.Null(result.Probes.Samples[0].ExecutedMilliseconds);
        }

        /// <summary>A producer fault is retained as an explicit failed-observation record.</summary>
        [Fact]
        public void ObserverFaultIsRetained()
        {
            using var offered = new ManualResetEventSlim();
            using var observer = new ThreadPoolHealthObserver(
                new AsyncHealthOptions { RecoveryMilliseconds = 0 },
                _ => { offered.Set(); throw new InvalidOperationException("test"); });
            observer.Start();
            Assert.True(offered.Wait(TimeSpan.FromSeconds(30)));
            AsyncHealthWindow result = observer.Complete();
            Assert.Equal(typeof(InvalidOperationException).FullName, result.ObserverError);
            Assert.Equal(0, result.Probes.Dropped);
            Assert.Equal(1, result.Probes.Rejected);
            Assert.Equal(0, result.Probes.Outstanding);
        }

        /// <summary>Stopping an empty/short window yields bounded, reconciled data, not fabricated coverage.</summary>
        [Fact]
        public void ShortObserverWindowStopsAndFreezesSnapshot()
        {
            using var observer = new ThreadPoolHealthObserver(new AsyncHealthOptions { RecoveryMilliseconds = 0 });
            Assert.Throws<InvalidOperationException>(() => observer.Complete());
            observer.Start();
            Assert.Throws<InvalidOperationException>(() => observer.Start());
            AsyncHealthWindow result = observer.Complete();
            Assert.Same(result, observer.Complete());
            Assert.True(result.WorkloadStartMilliseconds <= result.WorkloadEndMilliseconds);
            Assert.True(result.WorkloadEndMilliseconds <= result.RecoveryEndMilliseconds);
            Assert.Equal(result.Probes.Total, result.Probes.ProducerMissed + result.Probes.Dropped + result.Probes.Skipped + result.Probes.Rejected + result.Probes.Outstanding + result.Probes.Executed);
            Assert.Equal(result.Probes.Posted, result.Probes.Executed + result.Probes.Outstanding);
        }

        /// <summary>Invalid observer options are rejected before observation starts.</summary>
        [Fact]
        public void Validate_InvalidOptions_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AsyncHealthOptions { ProbeCapacity = 0 }.Validate());
            Assert.Throws<ArgumentException>(() => new AsyncHealthOptions { RecoveryMilliseconds = 30001 }.Validate());
        }
    }
}
#endif
