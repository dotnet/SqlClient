// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlClient.Reliability
{
    internal class SqlRetryLogic : ISqlRetryLogic
    {
        private const int firstCounter = 1;

        public int NumberOfTries { get; private set; }

        public ISqlRetryIntervalEnumerator RetryIntervalEnumerator { get; private set; }

        public Predicate<Exception> TransientPredicate { get; private set; }

        public int Current { get; private set; } = firstCounter;

        public SqlRetryLogic(int numberOfTries, ISqlRetryIntervalEnumerator enumerator, Predicate<Exception> transientPredicate)
        {
            Validate(numberOfTries, enumerator, transientPredicate);

            NumberOfTries = numberOfTries;
            RetryIntervalEnumerator = enumerator;
            TransientPredicate = transientPredicate;
        }

        public SqlRetryLogic(ISqlRetryIntervalEnumerator enumerator, Predicate<Exception> transientPredicate = null)
            : this(firstCounter, enumerator, transientPredicate ?? (_ => false))
        {
        }

        public void Reset()
        {
            Current = firstCounter;
            RetryIntervalEnumerator.Reset();
        }

        private void Validate(int numberOfTries, ISqlRetryIntervalEnumerator enumerator, Predicate<Exception> transientPredicate)
        {
            if (numberOfTries < firstCounter)
            {
                throw new ArgumentOutOfRangeException(nameof(numberOfTries));
            }
            if (enumerator == null)
            {
                throw new ArgumentNullException(nameof(enumerator));
            }
            else if (transientPredicate == null)
            {
                throw new ArgumentNullException(nameof(transientPredicate));
            }
        }

        public void Dispose()
        {
            RetryIntervalEnumerator.Dispose();
        }

        public bool TryNextInterval(out TimeSpan intervalTime)
        {
            intervalTime = TimeSpan.Zero;
            bool result = Current < NumberOfTries;

            if (result)
            {
                Current++;
                // it doesn't mind if the enumerator gets to the last value till the number of attempts ends.
                RetryIntervalEnumerator.MoveNext();
                intervalTime = RetryIntervalEnumerator.Current;
            }
            return result;
        }
    }
}
