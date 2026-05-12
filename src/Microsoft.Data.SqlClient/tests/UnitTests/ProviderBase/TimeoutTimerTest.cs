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
        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.IsExpired"/> flips from
        /// <see langword="false"/> to <see langword="true"/> once the timer's
        /// configured duration has elapsed (as measured by its
        /// <see cref="TimeProvider"/>), and that
        /// <see cref="TimeoutTimer.MillisecondsRemainingInt"/> reports zero in
        /// the expired state.
        /// </summary>
        [Fact]
        public void IsExpired_BecomesTrueAfterDuration()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(5), fake);
            Assert.False(timer.IsExpired);

            // Act: advance virtual time past the expiration; no real time elapses.
            fake.Advance(TimeSpan.FromSeconds(6));

            // Assert
            Assert.True(timer.IsExpired);
            Assert.Equal(0, timer.MillisecondsRemainingInt);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.MillisecondsRemainingInt"/>
        /// counts down as virtual time advances, matching the original duration
        /// minus the elapsed amount.
        /// </summary>
        [Fact]
        public void MillisecondsRemaining_DecreasesAsTimeElapses()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(10), fake);
            Assert.Equal(10_000, timer.MillisecondsRemainingInt);

            // Act
            fake.Advance(TimeSpan.FromSeconds(3));

            // Assert
            Assert.Equal(7_000, timer.MillisecondsRemainingInt);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.Reset"/> restarts the countdown
        /// from the original duration, discarding any time that had already
        /// elapsed.
        /// </summary>
        [Fact]
        public void Reset_RestoresOriginalDuration()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(5), fake);
            fake.Advance(TimeSpan.FromSeconds(4));
            Assert.Equal(1_000, timer.MillisecondsRemainingInt);

            // Act
            timer.Reset();

            // Assert
            Assert.Equal(5_000, timer.MillisecondsRemainingInt);
        }

        /// <summary>
        /// Verifies that the <see cref="CancellationTokenSource"/> produced by
        /// <see cref="TimeoutTimer.CreateCancellationTokenSource"/> is wired to the
        /// timer's <see cref="TimeProvider"/> rather than the system clock.
        /// </summary>
        /// <remarks>
        /// The CTS is constructed with a one-hour delay. If it were backed by real
        /// time, the test could not complete within the runner's per-test timeout.
        /// Because <c>CreateCancellationTokenSource</c> passes the timer's
        /// <see cref="TimeProvider"/> to the CTS constructor, advancing the
        /// <see cref="FakeTimeProvider"/> by two virtual hours synchronously fires
        /// the registered timer callback (queued to the thread pool by the fake
        /// provider), which cancels the source. The test then polls briefly via
        /// <see cref="WaitForAsync"/> to absorb thread-pool dispatch latency
        /// before asserting cancellation. A successful run completes in
        /// milliseconds, proving cancellation is driven by virtual time and not
        /// by wall-clock elapsed time.
        /// </remarks>
        [Fact]
        public async Task CreateCancellationTokenSource_FiresWhenTimerExpires()
        {
            // Arrange: use an hour-long timer; if the CTS were backed by real
            // time the test would never complete in the runner's timeout. It
            // only finishes promptly because the CTS is scheduled through the
            // fake provider.
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromHours(1), fake);
            using CancellationTokenSource cts = timer.CreateCancellationTokenSource();
            Assert.False(cts.IsCancellationRequested);

            // Act: advancing the fake provider past the expiration deadline must
            // cause the CTS to fire deterministically; no real time passes.
            fake.Advance(TimeSpan.FromHours(2));

            // Assert: FakeTimeProvider schedules timer callbacks on the thread
            // pool, so yield briefly to let the cancellation propagate before
            // asserting.
            await WaitForAsync(() => cts.IsCancellationRequested);
            Assert.True(cts.IsCancellationRequested);
        }

        /// <summary>
        /// Verifies that requesting a <see cref="CancellationTokenSource"/> from
        /// a timer whose deadline has already passed returns a source that is
        /// already canceled, rather than scheduling a new timer callback.
        /// </summary>
        [Fact]
        public void CreateCancellationTokenSource_AlreadyExpired_ReturnsCanceledSource()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fake);
            fake.Advance(TimeSpan.FromSeconds(2));

            // Act
            using CancellationTokenSource cts = timer.CreateCancellationTokenSource();

            // Assert
            Assert.True(cts.IsCancellationRequested);
        }

        /// <summary>
        /// Verifies that an infinite timer (constructed from
        /// <see cref="TimeSpan.Zero"/>) produces a
        /// <see cref="CancellationTokenSource"/> that never auto-cancels, even
        /// after a large amount of virtual time has elapsed.
        /// </summary>
        [Fact]
        public void CreateCancellationTokenSource_InfiniteTimer_NeverCancels()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.Zero, fake);
            using CancellationTokenSource cts = timer.CreateCancellationTokenSource();

            // Act
            fake.Advance(TimeSpan.FromHours(1));

            // Assert
            Assert.True(timer.IsInfinite);
            Assert.False(cts.IsCancellationRequested);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.TimeProvider"/> exposes the
        /// exact <see cref="TimeProvider"/> instance supplied to
        /// <see cref="TimeoutTimer.StartNew(TimeSpan, TimeProvider)"/>.
        /// </summary>
        [Fact]
        public void TimeProvider_ReturnsProviderPassedToStartNew()
        {
            // Arrange
            var fake = new FakeTimeProvider();

            // Act
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fake);

            // Assert
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
