// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Reliability
{
    internal class SqlIncrementalIntervalEnumerator : SqlRetryIntervalEnumerator
    {
        public SqlIncrementalIntervalEnumerator(int timeInterval, int maxTimeInterval, int minTimeInterval) 
            : base(timeInterval, maxTimeInterval, minTimeInterval)
        {
        }

        protected override int GetNextInterval()
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
        public SqlFixedIntervalEnumerator(int timeInterval, int maxTimeInterval, int minTimeInterval) 
            : base(timeInterval, maxTimeInterval, minTimeInterval)
        {
        }

        protected override int GetNextInterval()
        {
            return TimeInterval;
        }
    }

    internal class SqlNoneIntervalEnumerator : SqlRetryIntervalEnumerator
    {
        protected override int GetNextInterval()
        {
            return Current;
        }
    }
}
