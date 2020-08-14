// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

namespace Microsoft.Data.SqlClient.Reliability
{
    internal abstract class SqlRetryIntervalEnumerator : ISqlRetryIntervalEnumerator
    {
        private const int defaultTimeInterval = 0;

        public int TimeInterval { get; protected set; }

        public int MaxTimeInterval { get; protected set; }

        public int MinTimeInterval { get; protected set; }

        public int Current { get; private set; } = defaultTimeInterval;

        object IEnumerator.Current => Current;

        public SqlRetryIntervalEnumerator()
        {
            TimeInterval = 0;
            MaxTimeInterval = 0;
            MinTimeInterval = 0;
        }

        public SqlRetryIntervalEnumerator(int timeInterval, int maxTime, int minTime)
        {
            Validate(timeInterval, maxTime, minTime);
            TimeInterval = timeInterval;
            MaxTimeInterval = maxTime;
            MinTimeInterval = minTime;
        }

        public void Reset()
        {
            Current = 0;
        }

        private void Validate(int timeInterval, int maxTimeInterval, int minTimeInterval)
        {
            // valid time iterval must be between 0 and 60 minutes
            if(timeInterval < 0 || timeInterval > 3600000)
            {
                throw new ArgumentOutOfRangeException(nameof(timeInterval));
            }
            else if (minTimeInterval < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minTimeInterval));
            }
            else if (maxTimeInterval < 0 || maxTimeInterval < minTimeInterval)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeInterval));
            }
        }

        protected abstract int GetNextInterval();

        public bool MoveNext()
        {
            int next = GetNextInterval();
            bool result = next <= MaxTimeInterval;
            if (result)
            {
                Current = next;
            }

            return result;
        }
        public virtual void Dispose()
        {
        }
    }
}
