// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal class SqlExponentialIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        private int internalCounter = 1;

        public SqlExponentialIntervalEnumerator(TimeSpan deltaBackoffTime, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(deltaBackoffTime, maxTimeInterval, minTimeInterval)
        {
        }

        protected override TimeSpan GetNextInterval()
        {
            if (Current >= MaxTimeInterval)
            {
                return MaxTimeInterval;
            }
            else
            {
                var random = new Random();
                var maxRandom = GapTimeInterval.TotalMilliseconds * 1.2 < int.MaxValue ? Convert.ToInt32(GapTimeInterval.TotalMilliseconds * 1.2) : int.MaxValue;
                var minRandom = GapTimeInterval.TotalMilliseconds * 0.8 < int.MaxValue ? Convert.ToInt32(GapTimeInterval.TotalMilliseconds * 0.8) : Convert.ToInt32(int.MaxValue * 0.6);
                var delta = (Math.Pow(2.0, internalCounter++) - 1.0) * random.Next(minRandom, maxRandom);
                var newVlaue = TimeSpan.FromMilliseconds(MinTimeInterval.TotalMilliseconds + delta);
                return newVlaue < MaxTimeInterval ? newVlaue : MaxTimeInterval;
            }
        }
    }

    internal class SqlIncrementalIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        public SqlIncrementalIntervalEnumerator(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(timeInterval, maxTimeInterval, minTimeInterval)
        {
        }

        protected override TimeSpan GetNextInterval()
        {
            if (Current >= MaxTimeInterval)
            {
                return MaxTimeInterval;
            }
            else
            {
                var random = new Random();
                var maxRandom = GapTimeInterval.TotalMilliseconds * 1.2 < int.MaxValue ? Convert.ToInt32(GapTimeInterval.TotalMilliseconds * 1.2) : int.MaxValue;
                var minRandom = GapTimeInterval.TotalMilliseconds * 0.8 < int.MaxValue ? Convert.ToInt32(GapTimeInterval.TotalMilliseconds * 0.8) : Convert.ToInt32(int.MaxValue * 0.6);
                var interval = TimeSpan.FromMilliseconds(Current.TotalMilliseconds + random.Next(minRandom, maxRandom));

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
    }

    internal class SqlFixedIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        public SqlFixedIntervalEnumerator(TimeSpan gapTimeInterval)
            : base(gapTimeInterval, gapTimeInterval, gapTimeInterval)
        {
        }

        protected override TimeSpan GetNextInterval()
        {
            return GapTimeInterval;
        }
    }

    internal class SqlNoneIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        protected override TimeSpan GetNextInterval()
        {
            return Current;
        }
    }
}
