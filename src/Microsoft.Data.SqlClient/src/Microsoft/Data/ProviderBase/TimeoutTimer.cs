// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using System;
using System.Diagnostics;

namespace Microsoft.Data.ProviderBase
{
    // Purpose:
    //   Manages determining and tracking timeouts
    //
    // Intended use:
    //   Call StartXXXXTimeout() to get a timer with the given expiration point
    //   Get remaining time in appropriate format to pass to subsystem timeouts
    //   Check for timeout via IsExpired for checks in managed code.
    //   Simply abandon to GC when done.
    internal class TimeoutTimer
    {
        //-------------------
        // Fields
        //-------------------
        private long _timerExpire;
        private bool _isInfiniteTimeout;
        private long _originalTimerTicks;

        //-------------------
        // Timeout-setting methods
        //-------------------

        // Get a new timer that will expire in the given number of seconds
        //  For input, a value of zero seconds indicates infinite timeout
        internal static TimeoutTimer StartSecondsTimeout(int seconds)
        {
            //--------------------
            // Preconditions: None (seconds must conform to SetTimeoutSeconds requirements)

            //--------------------
            // Method body
            var timeout = new TimeoutTimer();
            timeout.SetTimeoutSeconds(seconds);

            //---------------------
            // Postconditions
            Debug.Assert(timeout != null); // Need a valid timeouttimer if no error

            return timeout;
        }

        // Get a new timer that will expire in the given number of milliseconds
        //  No current need to support infinite milliseconds timeout
        internal static TimeoutTimer StartMillisecondsTimeout(long milliseconds)
        {
            //--------------------
            // Preconditions
            Debug.Assert(0 <= milliseconds);

            //--------------------
            // Method body
            var timeout = new TimeoutTimer();
            timeout._originalTimerTicks = milliseconds * TimeSpan.TicksPerMillisecond;
            timeout._timerExpire = checked(ADP.TimerCurrent() + timeout._originalTimerTicks);
            timeout._isInfiniteTimeout = false;

            //---------------------
            // Postconditions
            Debug.Assert(timeout != null); // Need a valid timeouttimer if no error

            return timeout;
        }

        //-------------------
        // Methods for changing timeout
        //-------------------

        internal void SetTimeoutSeconds(int seconds)
        {
            //--------------------
            // Preconditions
            Debug.Assert(0 <= seconds || InfiniteTimeout == seconds);  // no need to support negative seconds at present

            //--------------------
            // Method body
            if (InfiniteTimeout == seconds)
            {
                _isInfiniteTimeout = true;
            }
            else
            {
                // Stash current time + timeout
                _originalTimerTicks = ADP.TimerFromSeconds(seconds);
                _timerExpire = checked(ADP.TimerCurrent() + _originalTimerTicks);
                _isInfiniteTimeout = false;
            }

            //---------------------
            // Postconditions:None
        }

        // Reset timer to original duration.
        internal void Reset()
        {
            if (InfiniteTimeout == _originalTimerTicks)
            {
                _isInfiniteTimeout = true;
            }
            else
            {
                _timerExpire = checked(ADP.TimerCurrent() + _originalTimerTicks);
                _isInfiniteTimeout = false;
            }
        }

        //-------------------
        // Timeout info properties
        //-------------------

        // Indicator for infinite timeout when starting a timer
        internal static readonly long InfiniteTimeout = 0;

        // Is this timer in an expired state?
        internal bool IsExpired
        {
            get
            {
                return !IsInfinite && ADP.TimerHasExpired(_timerExpire);
            }
        }

        // is this an infinite-timeout timer?
        internal bool IsInfinite
        {
            get
            {
                return _isInfiniteTimeout;
            }
        }

        // Special accessor for TimerExpire for use when thunking to legacy timeout methods.
        public long LegacyTimerExpire
        {
            get
            {
                return (_isInfiniteTimeout) ? long.MaxValue : _timerExpire;
            }
        }

        // Returns milliseconds remaining trimmed to zero for none remaining
        //  and long.MaxValue for infinite
        // This method should be preferred for internal calculations that are not
        //  yet common enough to code into the TimeoutTimer class itself.
        internal long MillisecondsRemaining
        {
            get
            {
                //-------------------
                // Preconditions: None

                //-------------------
                // Method Body
                long milliseconds;
                if (_isInfiniteTimeout)
                {
                    milliseconds = long.MaxValue;
                }
                else
                {
                    milliseconds = ADP.TimerRemainingMilliseconds(_timerExpire);
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

        // Returns milliseconds remaining trimmed to zero for none remaining
        internal int MillisecondsRemainingInt
        {
            get
            {
                //-------------------
                // Method Body
                int milliseconds;
                if (_isInfiniteTimeout)
                {
                    milliseconds = int.MaxValue;
                }
                else
                {
                    long longMilliseconds = ADP.TimerRemainingMilliseconds(_timerExpire);
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
    }
}
