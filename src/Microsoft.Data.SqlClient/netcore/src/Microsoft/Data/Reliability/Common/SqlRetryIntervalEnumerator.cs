// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

namespace Microsoft.Data.SqlClient.Reliability
{
    internal abstract class SqlRetryIntervalEnumerator : ISqlRetryIntervalEnumerator
    {
        public TimeSpan GapTimeInterval { get; protected set; }

        public TimeSpan MaxTimeInterval { get; protected set; }

        public TimeSpan MinTimeInterval { get; protected set; }

        public TimeSpan Current { get; private set; } = TimeSpan.Zero;

        object IEnumerator.Current => Current;

        public SqlRetryIntervalEnumerator()
        {
            GapTimeInterval = TimeSpan.Zero;
            MaxTimeInterval = TimeSpan.Zero;
            MinTimeInterval = TimeSpan.Zero;
        }

        public SqlRetryIntervalEnumerator(TimeSpan timeInterval, TimeSpan maxTime, TimeSpan minTime)
        {
            Validate(timeInterval, maxTime, minTime);
            GapTimeInterval = timeInterval;
            MaxTimeInterval = maxTime;
            MinTimeInterval = minTime;
        }

        public void Reset()
        {
            Current = TimeSpan.Zero;
        }

        private void Validate(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
        {
            // valid time iterval must be between 0 and 60 minutes
            // TODO: grab the localized messages from the resource file
            if(timeInterval.TotalSeconds > 3600)
            {
                throw new ArgumentOutOfRangeException(nameof(timeInterval));
            }
            else if (maxTimeInterval < minTimeInterval)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeInterval));
            }
        }

        protected abstract TimeSpan GetNextInterval();

        public bool MoveNext()
        {
            TimeSpan next = Current;
            if (Current < MaxTimeInterval)
            {
                next = GetNextInterval();
            }

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
