// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ProviderBase
{
    /// <summary>
    /// Verifies <see cref="TimeoutTimer"/> behavior: expiration evaluation,
    /// remaining-time reporting, reset, infinite timers, and the cancellation
    /// token source it produces.
    /// </summary>
    public class TimeoutTimerTest
    {
        [Fact]
        public void IsExpired_BecomesTrueAfterDuration()
        {
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(5), fake);

            Assert.False(timer.IsExpired);

            // Advance virtual time past the expiration; no real time elapses.
            fake.Advance(TimeSpan.FromSeconds(6));

            Assert.True(timer.IsExpired);
            Assert.Equal(0, timer.MillisecondsRemainingInt);
        }

        [Fact]
        public void MillisecondsRemaining_DecreasesAsTimeElapses()
        {
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(10), fake);

            Assert.Equal(10_000, timer.MillisecondsRemainingInt);

            fake.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(7_000, timer.MillisecondsRemainingInt);
        }

        [Fact]
        public void Reset_RestoresOriginalDuration()
        {
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(5), fake);

            fake.Advance(TimeSpan.FromSeconds(4));
            Assert.Equal(1_000, timer.MillisecondsRemainingInt);

            timer.Reset();

            Assert.Equal(5_000, timer.MillisecondsRemainingInt);
        }

        [Fact]
        public async Task CreateCancellationTokenSource_FiresWhenTimerExpires()
        {
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(2), fake);

            using CancellationTokenSource cts = timer.CreateCancellationTokenSource();

            Assert.False(cts.IsCancellationRequested);

            // Advancing the fake provider past the expiration deadline must cause
            // the CTS to fire deterministically; no real time passes.
            fake.Advance(TimeSpan.FromSeconds(3));

            // FakeTimeProvider schedules timer callbacks on the thread pool, so
            // yield briefly to let the cancellation propagate before asserting.
            await WaitForAsync(() => cts.IsCancellationRequested);

            Assert.True(cts.IsCancellationRequested);
        }

        [Fact]
        public void CreateCancellationTokenSource_AlreadyExpired_ReturnsCanceledSource()
        {
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fake);

            fake.Advance(TimeSpan.FromSeconds(2));

            using CancellationTokenSource cts = timer.CreateCancellationTokenSource();

            Assert.True(cts.IsCancellationRequested);
        }

        [Fact]
        public void CreateCancellationTokenSource_InfiniteTimer_NeverCancels()
        {
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.Zero, fake);

            using CancellationTokenSource cts = timer.CreateCancellationTokenSource();

            fake.Advance(TimeSpan.FromHours(1));

            Assert.True(timer.IsInfinite);
            Assert.False(cts.IsCancellationRequested);
        }

        [Fact]
        public void TimeProvider_ReturnsProviderPassedToStartNew()
        {
            var fake = new FakeTimeProvider();
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fake);

            Assert.Same(fake, timer.TimeProvider);
        }

        // Polls the predicate on a short cadence so test runs aren't sensitive
        // to thread-pool scheduling latency when FakeTimeProvider fires its
        // registered timer callbacks.
        private static async Task WaitForAsync(Func<bool> predicate)
        {
            for (int i = 0; i < 50; i++)
            {
                if (predicate())
                {
                    return;
                }
                await Task.Delay(20);
            }
        }
    }
}
