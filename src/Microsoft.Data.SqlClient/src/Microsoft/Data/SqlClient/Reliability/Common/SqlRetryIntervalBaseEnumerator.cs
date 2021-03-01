// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/SqlRetryIntervalBaseEnumerator/*' />
    public abstract class SqlRetryIntervalBaseEnumerator : IEnumerator<TimeSpan>, ICloneable
    {
        private readonly TimeSpan _minValue = TimeSpan.Zero;
        private readonly TimeSpan _maxValue = TimeSpan.FromSeconds(120);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/GapTimeInterval/*' />
        public TimeSpan GapTimeInterval { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/MaxTimeInterval/*' />
        public TimeSpan MaxTimeInterval { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/MinTimeInterval/*' />
        public TimeSpan MinTimeInterval { get; protected set; }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Current/*' />
        public TimeSpan Current { get; protected set; } = TimeSpan.Zero;

        object IEnumerator.Current => Current;

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/ctor1/*' />
        public SqlRetryIntervalBaseEnumerator()
        {
            GapTimeInterval = TimeSpan.Zero;
            MaxTimeInterval = TimeSpan.Zero;
            MinTimeInterval = TimeSpan.Zero;
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/ctor2/*' />
        public SqlRetryIntervalBaseEnumerator(TimeSpan timeInterval, TimeSpan maxTime, TimeSpan minTime)
        {
            Validate(timeInterval, maxTime, minTime);
            GapTimeInterval = timeInterval;
            MaxTimeInterval = maxTime;
            MinTimeInterval = minTime;
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Reset/*' />
        public virtual void Reset()
        {
            Current = TimeSpan.Zero;
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Validate/*' />
        protected virtual void Validate(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
        {
            if(minTimeInterval < _minValue || minTimeInterval > _maxValue )
            {
                throw SqlReliabilityUtil.ArgumentOutOfRange(nameof(minTimeInterval), minTimeInterval, _minValue, _maxValue);
            }

            if (maxTimeInterval < _minValue || maxTimeInterval > _maxValue)
            {
                throw SqlReliabilityUtil.ArgumentOutOfRange(nameof(maxTimeInterval), maxTimeInterval, _minValue, _maxValue);
            }

            if (timeInterval < _minValue || timeInterval > _maxValue)
            {
                throw SqlReliabilityUtil.ArgumentOutOfRange(nameof(timeInterval), timeInterval, _minValue, _maxValue);
            }

            if (maxTimeInterval < minTimeInterval)
            {
                throw SqlReliabilityUtil.InvalidMinAndMaxPair(nameof(minTimeInterval), minTimeInterval, nameof(maxTimeInterval), maxTimeInterval);
            }
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/GetNextInterval/*' />
        protected abstract TimeSpan GetNextInterval();

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/MoveNext/*' />
        public virtual bool MoveNext()
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

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Dispose/*' />
        public virtual void Dispose()
        {
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryIntervalBaseEnumerator.xml' path='docs/members[@name="SqlRetryIntervalBaseEnumerator"]/Clone/*' />
        public virtual object Clone()
        {
            throw new NotImplementedException();
        }
    }
}
