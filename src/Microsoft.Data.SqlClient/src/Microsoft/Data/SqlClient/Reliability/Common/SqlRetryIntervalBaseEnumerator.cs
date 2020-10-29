// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Generates a sequence of the time intervals.
    /// </summary>
    public abstract class SqlRetryIntervalBaseEnumerator : IEnumerator<TimeSpan>
    {
        private readonly TimeSpan _minValue = TimeSpan.Zero;
        private readonly TimeSpan _maxValue = TimeSpan.FromSeconds(120);

        /// <summary>
        /// The gap time of each interval
        /// </summary>
        public TimeSpan GapTimeInterval { get; protected set; }

        /// <summary>
        /// Maximum time interval value.
        /// </summary>
        public TimeSpan MaxTimeInterval { get; protected set; }
        
        /// <summary>
        /// Minimum time interval value.
        /// </summary>
        public TimeSpan MinTimeInterval { get; protected set; }

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        public TimeSpan Current { get; private set; } = TimeSpan.Zero;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Constructor
        /// </summary>
        public SqlRetryIntervalBaseEnumerator()
        {
            GapTimeInterval = TimeSpan.Zero;
            MaxTimeInterval = TimeSpan.Zero;
            MinTimeInterval = TimeSpan.Zero;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public SqlRetryIntervalBaseEnumerator(TimeSpan timeInterval, TimeSpan maxTime, TimeSpan minTime)
        {
            Validate(timeInterval, maxTime, minTime);
            GapTimeInterval = timeInterval;
            MaxTimeInterval = maxTime;
            MinTimeInterval = minTime;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the collection.
        /// </summary>
        public virtual void Reset()
        {
            Current = TimeSpan.Zero;
        }

        /// <summary>
        /// Validate the enumeration parameters.
        /// </summary>
        /// <param name="timeInterval">The gap time of each interval. Must be between 0 and 120 seconds.</param>
        /// <param name="maxTimeInterval">Maximum time interval value. Must be between 0 and 120 seconds.</param>
        /// <param name="minTimeInterval">Minimum time interval value. Must be between 0 and 120 seconds.</param>
        protected virtual void Validate(TimeSpan timeInterval, TimeSpan maxTimeInterval, TimeSpan minTimeInterval)
        {
            if(minTimeInterval < _minValue || minTimeInterval > _maxValue )
            {
                throw new ArgumentOutOfRangeException(nameof(minTimeInterval), StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, minTimeInterval, _minValue, _maxValue));
            }

            if (maxTimeInterval < _minValue || maxTimeInterval > _maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeInterval), StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, maxTimeInterval, _minValue, _maxValue));
            }

            if (timeInterval < _minValue || timeInterval > _maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeInterval), StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, timeInterval, _minValue, _maxValue));
            }

            if (maxTimeInterval < minTimeInterval)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeInterval), StringsHelper.GetString(Strings.SqlRetryLogic_InvalidRange, maxTimeInterval, minTimeInterval, _maxValue));
            }
        }

        /// <summary>
        /// Calculate the next interval time.
        /// </summary>
        /// <returns>Next time interval</returns>
        protected abstract TimeSpan GetNextInterval();

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        /// <returns>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.</returns>
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
        }
    }
}
