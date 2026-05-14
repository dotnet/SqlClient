// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
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

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.StartExpired(TimeProvider)"/>
        /// returns a finite timer that is already expired, reports zero
        /// remaining time, and uses the supplied <see cref="TimeProvider"/>.
        /// </summary>
        [Fact]
        public void StartExpired_ReturnsFiniteAlreadyExpiredTimer()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);

            // Act
            TimeoutTimer timer = TimeoutTimer.StartExpired(fake);

            // Assert
            Assert.False(timer.IsInfinite);
            Assert.True(timer.IsExpired);
            Assert.Equal(0, timer.MillisecondsRemainingInt);
            Assert.Same(fake, timer.TimeProvider);
        }

        /// <summary>
        /// Verifies that the <see cref="CancellationTokenSource"/> produced by
        /// an expired timer is already canceled.
        /// </summary>
        [Fact]
        public void StartExpired_CreateCancellationTokenSource_IsAlreadyCanceled()
        {
            // Arrange
            TimeoutTimer timer = TimeoutTimer.StartExpired(new FakeTimeProvider(DateTimeOffset.UtcNow));

            // Act
            using CancellationTokenSource cts = timer.CreateCancellationTokenSource();

            // Assert
            Assert.True(cts.IsCancellationRequested);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.StartChild"/> propagates the
        /// parent's <see cref="TimeProvider"/> to the child so child timers see
        /// the same virtual clock as their parent.
        /// </summary>
        [Fact]
        public void StartChild_PropagatesParentTimeProvider()
        {
            // Arrange
            var fake = new FakeTimeProvider();
            TimeoutTimer parent = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fake);

            // Act
            TimeoutTimer child = parent.StartChild(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Same(fake, child.TimeProvider);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.StartChild"/> caps the child's
        /// duration at the parent's remaining time when the requested duration
        /// would otherwise outlast the parent.
        /// </summary>
        [Fact]
        public void StartChild_RequestedDurationLongerThanParent_IsCappedAtParentRemaining()
        {
            // Arrange: parent has 5 s remaining; caller asks for 30 s.
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer parent = TimeoutTimer.StartNew(TimeSpan.FromSeconds(5), fake);

            // Act
            TimeoutTimer child = parent.StartChild(TimeSpan.FromSeconds(30));

            // Assert: child remaining should match parent remaining (5 s).
            Assert.Equal(parent.MillisecondsRemainingInt, child.MillisecondsRemainingInt);
            Assert.False(child.IsInfinite);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.StartChild"/> uses the requested
        /// duration when it is shorter than the parent's remaining time.
        /// </summary>
        [Fact]
        public void StartChild_RequestedDurationShorterThanParent_UsesRequested()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer parent = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fake);

            // Act
            TimeoutTimer child = parent.StartChild(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(5_000, child.MillisecondsRemainingInt);
            Assert.False(child.IsInfinite);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.StartChild"/> returns an
        /// already-expired child when the parent has already expired.
        /// </summary>
        [Fact]
        public void StartChild_ParentExpired_ReturnsAlreadyExpiredChild()
        {
            // Arrange: parent is already expired.
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer parent = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1), fake);
            fake.Advance(TimeSpan.FromSeconds(2));
            Assert.True(parent.IsExpired);

            // Act
            TimeoutTimer child = parent.StartChild(TimeSpan.FromSeconds(30));

            // Assert
            Assert.False(child.IsInfinite);
            Assert.True(child.IsExpired);
            Assert.Equal(0, child.MillisecondsRemainingInt);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.StartChild"/> with an infinite
        /// parent honors the requested finite duration rather than producing
        /// another infinite timer.
        /// </summary>
        [Fact]
        public void StartChild_InfiniteParent_UsesRequestedDuration()
        {
            // Arrange
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer parent = TimeoutTimer.StartNew(TimeSpan.Zero, fake);
            Assert.True(parent.IsInfinite);

            // Act
            TimeoutTimer child = parent.StartChild(TimeSpan.FromSeconds(5));

            // Assert
            Assert.False(child.IsInfinite);
            Assert.Equal(5_000, child.MillisecondsRemainingInt);
        }

        /// <summary>
        /// Verifies that <see cref="TimeoutTimer.StartChild"/> interprets
        /// <see cref="TimeSpan.Zero"/> literally as "expire immediately"
        /// rather than as the infinite-timeout sentinel, even when the parent
        /// is infinite.
        /// </summary>
        [Fact]
        public void StartChild_ZeroDuration_IsLiteralAndReturnsAlreadyExpiredChild()
        {
            // Arrange: an infinite parent so the only way Zero could become
            // "infinite" would be via the sentinel; verify it does not.
            var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
            TimeoutTimer parent = TimeoutTimer.StartNew(TimeSpan.Zero, fake);
            Assert.True(parent.IsInfinite);

            // Act
            TimeoutTimer child = parent.StartChild(TimeSpan.Zero);

            // Assert
            Assert.False(child.IsInfinite);
            Assert.True(child.IsExpired);
            Assert.Equal(0, child.MillisecondsRemainingInt);
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

        /// <summary>
        /// Verifies that the wall-clock reading the timer derives from
        /// <see cref="TimeProvider.System"/> matches the legacy
        /// <see cref="ADP.TimerCurrent"/> reading. Both are expected to return
        /// UTC "now" expressed in file-time ticks (100 ns since 1601-01-01 UTC),
        /// so two back-to-back samples should differ by no more than a small
        /// scheduling jitter.
        /// </summary>
        [Fact]
        public void SystemTimeProvider_AgreesWithAdpTimerCurrent()
        {
            // 50 ms in file-time ticks. Generous enough to absorb GC pauses
            // and CI jitter while still being far smaller than any meaningful
            // timeout this class is used for.
            const long ToleranceTicks = 50 * TimeSpan.TicksPerMillisecond;

            // Sample both clocks back-to-back, then bracket the TimeoutTimer
            // reading between two ADP readings.
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1));
            long adpBefore = ADP.TimerCurrent();
            long providerNow = timer.NowTicks();
            long adpAfter = ADP.TimerCurrent();

            Assert.InRange(providerNow, adpBefore - ToleranceTicks, adpAfter + ToleranceTicks);
        }

        /// <summary>
        /// Verifies the same equivalence end-to-end: a timer started with
        /// <see cref="TimeProvider.System"/> places its <c>ExpirationTicks</c>
        /// at <c>ADP.TimerCurrent() + duration</c> within scheduling jitter.
        /// This is the relationship legacy callers depend on when comparing
        /// <c>TimeoutTimer.ExpirationTicks</c> against <see cref="ADP.TimerCurrent"/>.
        /// </summary>
        [Fact]
        public void StartNew_WithSystemTimeProvider_ExpirationMatchesAdpClock()
        {
            const long ToleranceTicks = 50 * TimeSpan.TicksPerMillisecond;
            TimeSpan duration = TimeSpan.FromSeconds(30);

            long adpBefore = ADP.TimerCurrent();
            TimeoutTimer timer = TimeoutTimer.StartNew(duration);
            long adpAfter = ADP.TimerCurrent();

            Assert.InRange(
                timer.ExpirationTicks,
                adpBefore + duration.Ticks - ToleranceTicks,
                adpAfter + duration.Ticks + ToleranceTicks);
        }

        /// <summary>
        /// Verifies that the new remaining-milliseconds calculation used inside
        /// <see cref="TimeoutTimer"/> — <c>TicksToMilliseconds(ExpirationTicks - NowTicks())</c>
        /// — produces the same value as the legacy
        /// <see cref="ADP.TimerRemainingMilliseconds"/> path
        /// (<c>(ExpirationTicks - ADP.TimerCurrent()) / TicksPerMillisecond</c>)
        /// for the same <c>ExpirationTicks</c>. Exercised across past, near-now,
        /// and far-future expirations.
        /// </summary>
        [Theory]
        [InlineData(-30L * TimeSpan.TicksPerSecond)] // already expired
        [InlineData(-TimeSpan.TicksPerMillisecond)]  // just expired
        [InlineData(0L)]                              // expiring now
        [InlineData(TimeSpan.TicksPerMillisecond)]   // 1 ms remaining
        [InlineData(TimeSpan.TicksPerSecond)]        // 1 s remaining
        [InlineData(30L * TimeSpan.TicksPerSecond)]  // 30 s remaining
        public void RemainingMilliseconds_MatchesAdpTimerRemainingMilliseconds(long offsetTicks)
        {
            // Build an absolute expiration relative to "now" so both formulas
            // see a meaningful target instead of an arbitrary tick value.
            TimeoutTimer timer = TimeoutTimer.StartNew(TimeSpan.FromSeconds(1));
            long expirationTicks = timer.NowTicks() + offsetTicks;

            // Legacy formula: subtracts ADP.TimerCurrent() internally.
            long legacy = ADP.TimerRemainingMilliseconds(expirationTicks);

            // New formula used by TimeoutTimer: subtracts NowTicks() (which goes
            // through TimeProvider.System) and divides via TicksToMilliseconds.
            long updated = TimeoutTimer.TicksToMilliseconds(expirationTicks - timer.NowTicks());

            // The two formulas read the wall clock at slightly different
            // instants, so allow ± 1 ms of slip between them.
            Assert.InRange(updated, legacy - 1, legacy + 1);
        }
    }
}
