// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Transactions;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlRetryLogic : SqlRetryLogicBase
    {
        private const int firstCounter = 0;

        public Predicate<string> PreCondition { get; private set; }

        public SqlRetryLogic(int numberOfTries,
                             SqlRetryIntervalBaseEnumerator enumerator,
                             Predicate<Exception> transientPredicate,
                             Predicate<string> preCondition)
        {
            Validate(numberOfTries, enumerator, transientPredicate);

            NumberOfTries = numberOfTries;
            RetryIntervalEnumerator = enumerator;
            TransientPredicate = transientPredicate;
            PreCondition = preCondition;
            Current = firstCounter;
        }

        public SqlRetryLogic(int numberOfTries, SqlRetryIntervalBaseEnumerator enumerator, Predicate<Exception> transientPredicate)
            : this(numberOfTries, enumerator, transientPredicate, null)
        {
        }

        public SqlRetryLogic(SqlRetryIntervalBaseEnumerator enumerator, Predicate<Exception> transientPredicate = null)
            : this(firstCounter, enumerator, transientPredicate ?? (_ => false))
        {
        }

        public override void Reset()
        {
            Current = firstCounter;
            RetryIntervalEnumerator.Reset();
        }

        private void Validate(int numberOfTries, SqlRetryIntervalBaseEnumerator enumerator, Predicate<Exception> transientPredicate)
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

        public override bool TryNextInterval(out TimeSpan intervalTime)
        {
            intervalTime = TimeSpan.Zero;
            // First try has occurred before starting the retry process. 
            bool result = Current < NumberOfTries - 1;

            if (result)
            {
                Current++;
                // it doesn't mind if the enumerator gets to the last value till the number of attempts ends.
                RetryIntervalEnumerator.MoveNext();
                intervalTime = RetryIntervalEnumerator.Current;
            }
            return result;
        }

        public override bool RetryCondition(object sender)
        {
            bool result = true;

            if(sender is SqlCommand command)
            {
                result = Transaction.Current == null // check TransactionScope
                        && command.Transaction == null // check SqlTransaction on a SqlCommand
                        && (PreCondition == null || PreCondition.Invoke(command.CommandText)); // if it contains an invalid command to retry
            }

            return result;
        }
    }
}
