// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System;
using System.Diagnostics;
using System.Threading;

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
    /// Call <see cref="StartNew"/> to get a timer with the given expiration point.
    /// Read the remaining time in the appropriate format to pass to subsystem timeouts.
    /// Check for timeout via <see cref="IsExpired"/> for checks in managed code.
    /// Simply abandon the instance to the GC when done.
    /// </para>
    /// </remarks>
    internal class TimeoutTimer
    {
        #region Fields

        /// <summary>
        /// The sentinel value (<c>0</c>) used to indicate an infinite timeout when starting a timer.
        /// </summary>
        internal static readonly long InfiniteTimeout = 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutTimer"/> class with the
        /// specified expiration duration.
        /// </summary>
        /// <param name="expiration">
        /// The duration before the timer expires. A value whose ticks equal
        /// <see cref="InfiniteTimeout"/> indicates an infinite timeout.
        /// </param>
        private TimeoutTimer(TimeSpan expiration)
        {
            OriginalTicks = expiration.Ticks;
            IsInfinite = OriginalTicks == InfiniteTimeout;
            ExpirationTicks = IsInfinite ? long.MaxValue : checked(ADP.TimerCurrent() + OriginalTicks);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the absolute tick value at which this timer is considered expired.
        /// </summary>
        /// <value>
        /// The tick count, in <see cref="ADP.TimerCurrent"/> units, at which the timer
        /// expires; <see cref="long.MaxValue"/> when <see cref="IsInfinite"/> is
        /// <see langword="true"/>.
        /// </value>
        internal long ExpirationTicks {
            get;
            //TODO: Remove this when we disable Reset()
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether this timer has expired.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the timer is not infinite and the current time
        /// has passed <see cref="ExpirationTicks"/>; otherwise, <see langword="false"/>.
        /// </value>
        internal bool IsExpired
        {
            get
            {
                return !IsInfinite && ADP.TimerHasExpired(ExpirationTicks);
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
        /// trimmed to <c>0</c> when none remain and to <see cref="long.MaxValue"/>
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
                //-------------------
                // Preconditions: None

                //-------------------
                // Method Body
                long milliseconds;
                if (IsInfinite)
                {
                    milliseconds = long.MaxValue;
                }
                else
                {
                    milliseconds = ADP.TimerRemainingMilliseconds(ExpirationTicks);
                    if (0 > milliseconds)
                    {
                        milliseconds = 0;
                    }
                }

                //--------------------
                // Postconditions
                Debug.Assert(0 <= milliseconds); // This property guarantees no negative return values

                return milliseconds;
            }
        }

        /// <summary>
        /// Gets the number of milliseconds remaining before this timer expires as
        /// a 32-bit integer, trimmed to <c>0</c> when none remain and saturated to
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
                //-------------------
                // Method Body
                int milliseconds;
                if (IsInfinite)
                {
                    milliseconds = int.MaxValue;
                }
                else
                {
                    long longMilliseconds = ADP.TimerRemainingMilliseconds(ExpirationTicks);
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

                //--------------------
                // Postconditions
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

        #endregion

        #region Methods

        /// <summary>
        /// Creates and starts a new <see cref="TimeoutTimer"/> with the specified
        /// expiration duration.
        /// </summary>
        /// <param name="expiration">
        /// The duration before the returned timer expires. A value whose ticks equal
        /// <see cref="InfiniteTimeout"/> produces an infinite timer.
        /// </param>
        /// <returns>A new <see cref="TimeoutTimer"/> instance that has already started.</returns>
        internal static TimeoutTimer StartNew(TimeSpan expiration) => new TimeoutTimer(expiration);

        /// <summary>
        /// Creates a new <see cref="CancellationTokenSource"/> that will be canceled
        /// when this timer expires.
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

            return new CancellationTokenSource(remaining);
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
                ExpirationTicks = checked(ADP.TimerCurrent() + OriginalTicks);
            }
        }

        #endregion
    }
}
