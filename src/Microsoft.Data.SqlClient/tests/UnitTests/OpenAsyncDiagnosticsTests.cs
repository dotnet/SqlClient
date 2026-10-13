// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Data.SqlClient.PerformanceTests;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class OpenAsyncDiagnosticsTests
    {
        [Fact]
        public void HeldProbesAreBoundedAndDroppedTicksRemainAccounted()
        {
            var gate = new object();
            var held = new Queue<Action>();
            bool released = false;
            int captures = 0;
            using var observed = new ManualResetEventSlim();
            using var collector = new OpenAsyncDiagnosticCollector(1, 2, action =>
            {
                lock (gate)
                {
                    if (released)
                    {
                        action();
                    }
                    else
                    {
                        held.Enqueue(action);
                    }
                }
            });
            collector.Start(Stopwatch.GetTimestamp(), () =>
            {
                if (Interlocked.Increment(ref captures) >= 6)
                {
                    observed.Set();
                }
                return (3, 2);
            });
            try
            {
                Assert.True(observed.Wait(TimeSpan.FromSeconds(30)), "Independent observer did not advance.");
            }
            finally
            {
                lock (gate)
                {
                    released = true;
                    while (held.TryDequeue(out Action? action))
                    {
                        action();
                    }
                }
            }

            OpenAsyncDiagnosticResult result = collector.Complete();
            Assert.True(result.UnrelatedWork.Dropped > 0);
            Assert.Equal(result.UnrelatedWork.Scheduled, result.UnrelatedWork.Posted + result.UnrelatedWork.Dropped);
            Assert.Equal(result.UnrelatedWork.Posted, result.UnrelatedWork.Executed);
            Assert.Equal(result.UnrelatedWork.Posted, result.UnrelatedWork.Samples.Length);
            Assert.InRange(result.UnrelatedWork.OutstandingAtStop, 0, 2);
            Assert.All(result.UnrelatedWork.Samples, sample =>
            {
                Assert.InRange(sample.ScheduledMilliseconds, 0, sample.PostedMilliseconds);
                Assert.True(sample.ExecutedMilliseconds >= sample.PostedMilliseconds);
            });
            Assert.All(result.Timeline, sample =>
            {
                Assert.Equal(3, sample.OutstandingOpens);
                Assert.Equal(2, sample.OwnedConnections);
                Assert.True(sample.WorkingSetBytes > 0);
                Assert.True(sample.ManagedHeapBytes > 0);
            });
            Assert.True(result.Timeline[^1].AfterWorkload);
            Assert.Same(result, collector.Complete());
        }

        [Fact]
        public void RealThreadPoolProbesDrainWithoutLosingSamples()
        {
            int captures = 0;
            using var observed = new ManualResetEventSlim();
            using var collector = new OpenAsyncDiagnosticCollector(1, 8);
            collector.Start(Stopwatch.GetTimestamp(), () =>
            {
                if (Interlocked.Increment(ref captures) >= 3)
                {
                    observed.Set();
                }
                return (0, 0);
            });
            Assert.True(observed.Wait(TimeSpan.FromSeconds(30)), "Independent observer did not advance.");
            OpenAsyncDiagnosticResult result = collector.Complete();
            Assert.True(result.UnrelatedWork.Posted > 0);
            Assert.Equal(result.UnrelatedWork.Posted, result.UnrelatedWork.Executed);
            Assert.NotEmpty(result.Timeline);
            Assert.Equal(8, result.UnrelatedWork.Capacity);
        }

        [Fact]
        public void ObservationFailureIsNotReportedAsSuccessfulEmptyDiagnostics()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            {
                using var collector = new OpenAsyncDiagnosticCollector();
                collector.Start(Stopwatch.GetTimestamp(),
                    () => throw new InvalidOperationException("Deliberate observation failure"));
                collector.Complete();
            });
            Assert.Equal("OpenAsync diagnostic observation failed.", error.Message);
            Assert.IsType<InvalidOperationException>(error.InnerException);
        }

        [Fact]
        public void InvalidCollectorConfigurationAndLifecycleAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OpenAsyncDiagnosticCollector(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OpenAsyncDiagnosticCollector(1, 0));
            using var collector = new OpenAsyncDiagnosticCollector();
            Assert.Throws<InvalidOperationException>(() => collector.Complete());
            collector.Start(Stopwatch.GetTimestamp(), () => (0, 0));
            Assert.Throws<InvalidOperationException>(() => collector.Start(Stopwatch.GetTimestamp(), () => (0, 0)));
            collector.Complete();
        }
    }
}
#endif
