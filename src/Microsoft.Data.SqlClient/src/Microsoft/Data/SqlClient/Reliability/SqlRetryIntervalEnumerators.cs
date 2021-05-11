// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient
{
    internal class SqlExponentialIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        private int internalCounter = 1;
        private readonly int maxRandom;
        private readonly int minRandom;
        private readonly Random random = new Random();

        public SqlExponentialIntervalEnumerator(TimeSpan deltaBackoffTime, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(deltaBackoffTime, maxTimeInterval, minTimeInterval)
        {
            var tempMax = GapTimeInterval.TotalMilliseconds * 1.2;
            var tempMin = GapTimeInterval.TotalMilliseconds * 0.8;
            maxRandom = tempMax < int.MaxValue ? Convert.ToInt32(tempMax) : int.MaxValue;
            minRandom = tempMin < int.MaxValue ? Convert.ToInt32(tempMin) : Convert.ToInt32(int.MaxValue * 0.6);
        }

        protected override TimeSpan GetNextInterval()
        {
            var delta = (Math.Pow(2.0, internalCounter++) - 1.0) * random.Next(minRandom, maxRandom);
            var newTimeMilliseconds = MinTimeInterval.TotalMilliseconds + delta;
            newTimeMilliseconds = newTimeMilliseconds < MaxTimeInterval.TotalMilliseconds ? newTimeMilliseconds 
                : random.NextDouble() * (MaxTimeInterval.TotalMilliseconds * 0.2) + (MaxTimeInterval.TotalMilliseconds * 0.8);
            var newValue = TimeSpan.FromMilliseconds(newTimeMilliseconds);

            Current = newValue < MinTimeInterval ? MinTimeInterval : newValue ;
            return Current;
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
        private readonly int maxRandom;
        private readonly int minRandom;
        private readonly Random random = new Random();

        public SqlIncrementalIntervalEnumerator(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
            : base(timeInterval, maxTimeInterval, minTimeInterval)
        {
            var tempMax = GapTimeInterval.TotalMilliseconds * 1.2;
            var tempMin = GapTimeInterval.TotalMilliseconds * 0.8;
            maxRandom = tempMax < int.MaxValue ? Convert.ToInt32(tempMax) : int.MaxValue;
            minRandom = tempMin < int.MaxValue ? Convert.ToInt32(tempMin) : Convert.ToInt32(int.MaxValue * 0.6);
        }

        protected override TimeSpan GetNextInterval()
        {
            var newTimeMilliseconds = Current.TotalMilliseconds + random.Next(minRandom, maxRandom);
            newTimeMilliseconds = newTimeMilliseconds < MaxTimeInterval.TotalMilliseconds ? newTimeMilliseconds 
                : random.NextDouble() * (MaxTimeInterval.TotalMilliseconds * 0.2) + (MaxTimeInterval.TotalMilliseconds * 0.8);
            var interval = TimeSpan.FromMilliseconds(newTimeMilliseconds);

            Current = interval < MinTimeInterval ? MinTimeInterval : interval;
            return Current;
        }

        public override object Clone()
        {
            return MemberwiseClone();
        }
    }

    internal class SqlFixedIntervalEnumerator : SqlRetryIntervalBaseEnumerator
    {
        private readonly int maxRandom;
        private readonly int minRandom;
        private readonly Random random = new Random();

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
            Current = TimeSpan.FromMilliseconds(random.Next(minRandom, maxRandom));
            return Current;
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
