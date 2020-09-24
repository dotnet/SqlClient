// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Reliability
{
    internal class SqlExponentialIntervalEnumerator : SqlRetryIntervalEnumerator
    {
        private int internalCounter = 1;

        public SqlExponentialIntervalEnumerator(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(timeInterval, maxTimeInterval, minTimeInterval)
        {
        }

        protected override TimeSpan GetNextInterval()
        {
            var random = new Random();
            int delta = Convert.ToInt32((Math.Pow(2.0, internalCounter++) - 1.0)
                                        * random.Next(Convert.ToInt32(TimeInterval.TotalMilliseconds * 0.8), Convert.ToInt32(TimeInterval.TotalMilliseconds * 1.2)));
            var newVlaue = TimeSpan.FromMilliseconds(MinTimeInterval.TotalMilliseconds + delta);
            return newVlaue < MaxTimeInterval ? newVlaue : MaxTimeInterval;
        }
    }

    internal class SqlIncrementalIntervalEnumerator : SqlRetryIntervalEnumerator
    {
        public SqlIncrementalIntervalEnumerator(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(timeInterval, maxTimeInterval, minTimeInterval)
        {
        }

        protected override TimeSpan GetNextInterval()
        {
            var interval = Current + TimeInterval;

            if (interval < MinTimeInterval)
            {
                interval = MinTimeInterval;
            }
            else if (interval > MaxTimeInterval)
            {
                interval = MaxTimeInterval;
            }

            return interval;
        }
    }

    internal class SqlFixedIntervalEnumerator : SqlRetryIntervalEnumerator
    {
        public SqlFixedIntervalEnumerator(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(timeInterval, maxTimeInterval, minTimeInterval)
        {
        }

        protected override TimeSpan GetNextInterval()
        {
            return TimeInterval;
        }
    }

    internal class SqlNoneIntervalEnumerator : SqlRetryIntervalEnumerator
    {
        protected override TimeSpan GetNextInterval()
        {
            return Current;
        }
    }
}
