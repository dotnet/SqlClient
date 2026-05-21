// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.ProviderBase
{
    /// <summary>
    /// Manages determining and tracking timeouts for use by subsystems that perform
    /// time-bounded operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Intended use:
    /// </para>
    /// <para>
    /// Call <see cref="StartNew(TimeSpan)"/> (or the overload that accepts a
    /// <see cref="TimeProvider"/>) to get a timer with the given expiration point.
    /// Read the remaining time in the appropriate format to pass to subsystem timeouts.
    /// Check for timeout via <see cref="IsExpired"/> for checks in managed code.
    /// Simply abandon the instance to the GC when done.
    /// </para>
    /// <para>
    /// All time reads (current time and remaining time calculations) and any
    /// <see cref="CancellationTokenSource"/> instances created by this timer flow
    /// through the supplied <see cref="TimeProvider"/>. This allows tests to inject
    /// a fake time provider (for example
    /// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>) and deterministically
    /// trigger expiration without relying on wall-clock delays.
    /// </para>
    /// </remarks>
    internal class TimeoutTimer
    {
        #region Fields

        /// <summary>
        /// The sentinel value (<c>0</c>) used to indicate an infinite timeout when starting a timer.
        /// </summary>
        internal const long InfiniteTimeout = 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutTimer"/> class with the
        /// specified expiration duration and time source.
        /// </summary>
        /// <param name="expiration">
        /// The duration before the timer expires. A value whose ticks equal
        /// <see cref="InfiniteTimeout"/> indicates an infinite timeout.
        /// </param>
        /// <param name="timeProvider">
        /// The <see cref="TimeProvider"/> used to read the current time and schedule
        /// cancellation.
        /// </param>
        /// <exception cref="OverflowException">
        /// Thrown when computing the absolute expiration point in checked arithmetic,
        /// if the sum of the current file-time ticks and <paramref name="expiration"/>
        /// ticks falls outside the <see cref="long"/> range.
        /// </exception>
        private TimeoutTimer(TimeSpan expiration, TimeProvider timeProvider)
        {
            TimeProvider = timeProvider;
            OriginalTicks = expiration.Ticks;
            IsInfinite = OriginalTicks == InfiniteTimeout;
            ExpirationTicks = checked(NowTicks() + OriginalTicks);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the tick value at which this timer is considered expired.
        /// Do not use this value directly; instead, use <see cref="IsExpired"/> to check if the timer has expired.
        /// Does not return a meaningful value when the timer is infinite.
        /// </summary>
        /// <value>
        /// The tick count, in file-time units (100-nanosecond intervals since
        /// 1601-01-01 UTC), at which the timer expires.
        /// </value>
        /// <remarks>
        /// The tick scale is intentionally compatible with
        /// <see cref="DateTime.ToFileTimeUtc()"/>
        /// </remarks>
        internal long ExpirationTicks
        {
            get;
            //TODO: Remove this when we disable Reset()
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether this timer has expired.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the timer is not infinite and the current time
        /// (as read from the configured <see cref="TimeProvider"/>) has passed
        /// <see cref="ExpirationTicks"/>; otherwise, <see langword="false"/>.
        /// </value>
        internal bool IsExpired
        {
            get
            {
                return !IsInfinite && NowTicks() > ExpirationTicks;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this timer represents an infinite timeout.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the timer was created with an expiration whose
        /// ticks equal <see cref="InfiniteTimeout"/>; otherwise, <see langword="false"/>.
        /// </value>
        internal bool IsInfinite { get; }

        /// <summary>
        /// Gets the number of milliseconds remaining before this timer expires,
        /// truncated to <c>0</c> when none remain, and approximated to <see cref="long.MaxValue"/>
        /// when the timer is infinite.
        /// </summary>
        /// <value>
        /// A non-negative count of milliseconds remaining; <see cref="long.MaxValue"/>
        /// when <see cref="IsInfinite"/> is <see langword="true"/>.
        /// </value>
        /// <remarks>
        /// This property should be preferred for internal calculations that are not
        /// yet common enough to code into the <see cref="TimeoutTimer"/> class itself.
        /// </remarks>
        internal long MillisecondsRemaining
        {
            get
            {
                long milliseconds;
                if (IsInfinite)
                {
                    milliseconds = long.MaxValue;
                }
                else
                {
                    milliseconds = TicksToMilliseconds(ExpirationTicks - NowTicks());
                    if (0 > milliseconds)
                    {
                        milliseconds = 0;
                    }
                }

                Debug.Assert(0 <= milliseconds); // This property guarantees no negative return values

                return milliseconds;
            }
        }

        /// <summary>
        /// Gets the number of milliseconds remaining before this timer expires as
        /// a 32-bit integer, trimmed to <c>0</c> when none remain and approximated to
        /// <see cref="int.MaxValue"/> when the remaining time exceeds that value or
        /// when the timer is infinite.
        /// </summary>
        /// <value>
        /// A non-negative count of milliseconds remaining, never exceeding
        /// <see cref="int.MaxValue"/>.
        /// </value>
        internal int MillisecondsRemainingInt
        {
            get
            {
                int milliseconds;
                if (IsInfinite)
                {
                    milliseconds = int.MaxValue;
                }
                else
                {
                    long longMilliseconds = TicksToMilliseconds(ExpirationTicks - NowTicks());
                    if (0 > longMilliseconds)
                    {
                        milliseconds = 0;
                    }
                    else if (longMilliseconds > int.MaxValue)
                    {
                        milliseconds = int.MaxValue;
                    }
                    else
                    {
                        milliseconds = checked((int)longMilliseconds);
                    }
                }

                Debug.Assert(0 <= milliseconds);

                return milliseconds;
            }
        }

        /// <summary>
        /// Gets the original timeout duration, in ticks, that was supplied when the
        /// timer was created. Used by <see cref="Reset"/> to restore the original
        /// expiration window.
        /// </summary>
        private long OriginalTicks { get; }

        /// <summary>
        /// Gets the <see cref="TimeProvider"/> used by this timer. Exposed for
        /// callers that need to construct related timers or schedule cancellation
        /// against the same time source.
        /// </summary>
        internal TimeProvider TimeProvider { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates and starts a new <see cref="TimeoutTimer"/> with the specified
        /// expiration duration, using <see cref="TimeProvider.System"/> as the time
        /// source.
        /// </summary>
        /// <param name="expiration">
        /// The duration before the returned timer expires. A value whose ticks equal
        /// <see cref="InfiniteTimeout"/> produces an infinite timer.
        /// </param>
        /// <returns>A new <see cref="TimeoutTimer"/> instance that has already started.</returns>
        internal static TimeoutTimer StartNew(TimeSpan expiration)
            => new TimeoutTimer(expiration, TimeProvider.System);

        /// <summary>
        /// Creates and starts a new <see cref="TimeoutTimer"/> with the specified
        /// expiration duration and time source.
        /// </summary>
        /// <param name="expiration">
        /// The duration before the returned timer expires. A value whose ticks equal
        /// <see cref="InfiniteTimeout"/> produces an infinite timer.
        /// </param>
        /// <param name="timeProvider">
        /// The <see cref="TimeProvider"/> used to read the current time and schedule
        /// cancellation. Pass a fake provider in tests to deterministically control
        /// expiration.
        /// </param>
        /// <returns>A new <see cref="TimeoutTimer"/> instance that has already started.</returns>
        internal static TimeoutTimer StartNew(TimeSpan expiration, TimeProvider timeProvider)
            => new TimeoutTimer(expiration, timeProvider);

        /// <summary>
        /// Creates a new <see cref="TimeoutTimer"/> that is already expired,
        /// using <see cref="TimeProvider.System"/> as the time source.
        /// </summary>
        /// <returns>
        /// A finite <see cref="TimeoutTimer"/> whose <see cref="IsExpired"/> is
        /// already <see langword="true"/> and whose <see cref="MillisecondsRemaining"/>
        /// is zero.
        /// </returns>
        internal static TimeoutTimer StartExpired()
            => StartExpired(TimeProvider.System);

        /// <summary>
        /// Creates a new <see cref="TimeoutTimer"/> that is already expired.
        /// </summary>
        /// <param name="timeProvider">
        /// The <see cref="TimeProvider"/> used to read the current time and schedule
        /// cancellation.
        /// </param>
        /// <returns>
        /// A finite <see cref="TimeoutTimer"/> whose <see cref="IsExpired"/> is
        /// already <see langword="true"/> and whose <see cref="MillisecondsRemaining"/>
        /// is zero. Useful when a code path needs to hand off an already-exhausted
        /// timeout (for example, a child timer whose parent has no remaining
        /// budget) without resorting to negative durations or the
        /// <see cref="InfiniteTimeout"/> sentinel.
        /// </returns>
        /// <remarks>
        /// Implemented by anchoring the expiration one tick before "now" on the
        /// supplied <paramref name="timeProvider"/>. The timer is finite, so
        /// <see cref="IsInfinite"/> is <see langword="false"/>.
        /// </remarks>
        internal static TimeoutTimer StartExpired(TimeProvider timeProvider)
            => new TimeoutTimer(TimeSpan.FromTicks(-1), timeProvider);

        /// <summary>
        /// Creates and starts a new <see cref="TimeoutTimer"/> nested under this
        /// (parent) timer. The child shares the parent's <see cref="TimeProvider"/>
        /// and is capped so that it cannot outlast the parent's remaining time.
        /// </summary>
        /// <param name="duration">
        /// The desired duration of the child timer, interpreted literally — a
        /// value of <see cref="TimeSpan.Zero"/> means "expire immediately" and
        /// is <em>not</em> treated as the <see cref="InfiniteTimeout"/>
        /// sentinel. A non-positive value yields an already-expired child.
        /// </param>
        /// <returns>
        /// A new <see cref="TimeoutTimer"/> that uses this timer's
        /// <see cref="TimeProvider"/>. The child is finite unless the parent is
        /// infinite, in which case the requested <paramref name="duration"/> is
        /// honored as-is. When the parent is finite, the child's expiration is
        /// capped at the parent's remaining time.
        /// </returns>
        /// <remarks>
        /// Behavior matrix:
        /// <list type="bullet">
        ///   <item><description>Parent infinite → finite child with the requested duration (or already-expired when <paramref name="duration"/> ≤ 0).</description></item>
        ///   <item><description>Parent finite, duration longer than parent's remaining → finite child capped at the parent's remaining time.</description></item>
        ///   <item><description>Parent finite, duration shorter than parent's remaining → finite child with the requested duration.</description></item>
        ///   <item><description>Parent finite with no remaining time, or <paramref name="duration"/> ≤ 0 → already-expired child (see <see cref="StartExpired(TimeProvider)"/>).</description></item>
        /// </list>
        /// To request a truly infinite timeout, call <see cref="StartNew(TimeSpan, TimeProvider)"/>
        /// directly with <see cref="TimeSpan.Zero"/>; this method does not
        /// produce infinite children.
        /// </remarks>
        internal TimeoutTimer StartChild(TimeSpan duration)
        {
            long requestedMs = (long)duration.TotalMilliseconds;

            // Caller asked for a non-positive duration: already expired.
            if (requestedMs <= 0)
            {
                return StartExpired(TimeProvider);
            }

            // Parent finite: cap at parent's remaining time. If the cap leaves
            // no time, return an already-expired timer rather than colliding
            // with the 0-ticks-means-infinite sentinel.
            long childMs = Math.Min(requestedMs, MillisecondsRemaining);
            if (childMs <= 0)
            {
                return StartExpired(TimeProvider);
            }

            return new TimeoutTimer(TimeSpan.FromMilliseconds(childMs), TimeProvider);
        }

        /// <summary>
        /// Creates a new <see cref="CancellationTokenSource"/> that will be canceled
        /// when this timer expires, using the same <see cref="TimeProvider"/> the
        /// timer was constructed with.
        /// </summary>
        /// <returns>
        /// A <see cref="CancellationTokenSource"/> scheduled to cancel after
        /// <see cref="MillisecondsRemainingInt"/> milliseconds. When
        /// <see cref="IsInfinite"/> is <see langword="true"/>, the returned source
        /// is never automatically canceled. When the timer has already expired, the
        /// returned source is already canceled.
        /// </returns>
        internal CancellationTokenSource CreateCancellationTokenSource()
        {
            if (IsInfinite)
            {
                return new CancellationTokenSource();
            }

            int remaining = MillisecondsRemainingInt;
            if (remaining == 0)
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                cts.Cancel();
                return cts;
            }

            // Route the timer through the configured TimeProvider so that fake
            // time providers can advance virtual time and trigger cancellation
            // deterministically in tests.
            // Use the extension method rather than the CancellationTokenSource
            // constructor overload, which doesn't exist on .NET Framework.
            return TimeProvider.CreateCancellationTokenSource(TimeSpan.FromMilliseconds(remaining));
        }

        /// <summary>
        /// Resets the timeout to its original duration.
        /// </summary>
        /// <remarks>
        /// This method is only used to retry after federated authentication timeouts,
        /// which can use up the whole timeout due to MFA. Has no effect when
        /// <see cref="IsInfinite"/> is <see langword="true"/>.
        /// </remarks>
        internal void Reset()
        {
            if (!IsInfinite)
            {
                ExpirationTicks = checked(NowTicks() + OriginalTicks);
            }
        }

        /// <summary>
        /// Reads the configured <see cref="TimeProvider"/>'s current UTC time and
        /// returns it as file-time ticks (100-nanosecond intervals since
        /// 1601-01-01 UTC). This keeps <see cref="ExpirationTicks"/> in the same
        /// scale historically produced by <c>DateTime.UtcNow.ToFileTimeUtc()</c>.
        /// </summary>
        internal long NowTicks() => TimeProvider.GetUtcNow().UtcDateTime.ToFileTimeUtc();

        /// <summary>
        /// Converts a tick count (100-nanosecond intervals) to milliseconds, matching
        /// the conversion historically performed by <c>ADP.TimerToMilliseconds</c>.
        /// </summary>
        internal static long TicksToMilliseconds(long ticks) => ticks / TimeSpan.TicksPerMillisecond;

        #endregion
    }
}
