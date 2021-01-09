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
                var tempMax = GapTimeInterval.TotalMilliseconds * 1.2;
                var tempMin = GapTimeInterval.TotalMilliseconds * 0.8;
                var maxRandom = tempMax < int.MaxValue ? Convert.ToInt32(tempMax) : int.MaxValue;
                var minRandom = tempMin < int.MaxValue ? Convert.ToInt32(tempMin) : Convert.ToInt32(int.MaxValue * 0.6);
                var delta = (Math.Pow(2.0, internalCounter++) - 1.0) * random.Next(minRandom, maxRandom);
                var newTimeMilliseconds = MinTimeInterval.TotalMilliseconds + delta;
                newTimeMilliseconds = newTimeMilliseconds < MaxTimeInterval.TotalMilliseconds ? newTimeMilliseconds : MaxTimeInterval.TotalMilliseconds;
                var newVlaue = TimeSpan.FromMilliseconds(newTimeMilliseconds);

                return newVlaue < MinTimeInterval ? MinTimeInterval : newVlaue;
            }
        }

        public override void Reset()
        {
            base.Reset();
            internalCounter = 1;
        }

        public override object Clone()
        {   
            return MemberwiseClone();
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
                var tempMax = GapTimeInterval.TotalMilliseconds * 1.2;
                var tempMin = GapTimeInterval.TotalMilliseconds * 0.8;
                var maxRandom = tempMax < int.MaxValue ? Convert.ToInt32(tempMax) : int.MaxValue;
                var minRandom = tempMin < int.MaxValue ? Convert.ToInt32(tempMin) : Convert.ToInt32(int.MaxValue * 0.6);
                var newTimeMilliseconds = Current.TotalMilliseconds + random.Next(minRandom, maxRandom);
                newTimeMilliseconds = newTimeMilliseconds < MaxTimeInterval.TotalMilliseconds ? newTimeMilliseconds : MaxTimeInterval.TotalMilliseconds;
                var interval = TimeSpan.FromMilliseconds(newTimeMilliseconds);

                return interval < MinTimeInterval ? MinTimeInterval : interval;
            }
        }

        public override object Clone()
        {
            return MemberwiseClone();
        }
    }

    internal class SqlFixedIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        private int maxRandom;
        private int minRandom;

        public SqlFixedIntervalEnumerator(TimeSpan gapTimeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(gapTimeInterval, maxTimeInterval, minTimeInterval)
        {
            var tempMax = GapTimeInterval.TotalMilliseconds * 1.2;
            var tempMin = GapTimeInterval.TotalMilliseconds * 0.8;
            maxRandom = tempMax < int.MaxValue ? Convert.ToInt32(tempMax) : int.MaxValue;
            minRandom = tempMin < int.MaxValue ? Convert.ToInt32(tempMin) : Convert.ToInt32(int.MaxValue * 0.6);
        }

        protected override TimeSpan GetNextInterval()
        {
            var random = new Random();
            var interval = TimeSpan.FromMilliseconds(random.Next(minRandom, maxRandom));
            return interval;
        }

        public override object Clone()
        {
            return MemberwiseClone();
        }
    }

    internal class SqlNoneIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        protected override TimeSpan GetNextInterval()
        {
            return Current;
        }

        public override object Clone()
        {
            return MemberwiseClone();
        }
    }
}
