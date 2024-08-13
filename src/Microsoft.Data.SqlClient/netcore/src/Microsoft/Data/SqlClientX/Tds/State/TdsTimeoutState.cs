// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace Microsoft.Data.SqlClientX.Tds.State
{
    /// <summary>
    /// Captures Tds Timeout and Attention state.
    /// </summary>
    internal class TdsTimeoutState
    {
        internal sealed class TimeoutState
        {
            public const int Stopped = 0;
            public const int Running = 1;
            public const int ExpiredAsync = 2;
            public const int ExpiredSync = 3;

            private readonly int _value;

            public TimeoutState(int value)
            {
                _value = value;
            }

            public int IdentityValue => _value;
        }

        // Timeout variables
        internal long _timeoutMilliseconds;
        internal long _timeoutTime;                          // variable used for timeout computations, holds the value of the hi-res performance counter at which this request should expire
        internal int _timeoutState;                          // expected to be one of the constant values TimeoutStopped, TimeoutRunning, TimeoutExpiredAsync, TimeoutExpiredSync
        internal int _timeoutIdentitySource;
        internal volatile int _timeoutIdentityValue;
        internal volatile bool _attentionSent;              // true if we sent an Attention to the server
        internal volatile bool _attentionSending;
        internal readonly TimerCallback _onTimeoutAsync;

        // Below 2 properties are used to enforce timeout delays in code to 
        // reproduce issues related to thread-pool starvation and timeout delay.
        // It should always be set to false by default, and only be enabled during testing.
        internal bool _enforceTimeoutDelay;
        internal int _enforcedTimeoutDelayInMilliSeconds;

        internal TdsTimeoutState(bool enforceTimeoutDelay = false, int enforcedTimeoutDelayInMilliSeconds = 5000)
        {
            _enforceTimeoutDelay = enforceTimeoutDelay;
            _enforcedTimeoutDelayInMilliSeconds = enforcedTimeoutDelayInMilliSeconds;
        }

        public void SetTimeoutStateStopped()
        {
            Interlocked.Exchange(ref _timeoutState, TimeoutState.Stopped);
            _timeoutIdentityValue = 0;
        }

        public bool IsTimeoutStateExpired
        {
            get
            {
                int state = _timeoutState;
                return state == TimeoutState.ExpiredAsync || state == TimeoutState.ExpiredSync;
            }
        }
    }
}
