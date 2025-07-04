// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Transactions;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlRetryLogic : SqlRetryLogicBase
    {
        private const int counterDefaultValue = 0;
        private const int maxAttempts = 60;

        private const string TypeName = nameof(SqlRetryLogic);

        public Predicate<string> PreCondition { get; private set; }

        public SqlRetryLogic(int numberOfTries,
                             SqlRetryIntervalBaseEnumerator enumerator,
                             Predicate<Exception> transientPredicate,
                             Predicate<string> preCondition)
        {
            Debug.Assert(enumerator != null, $"The '{nameof(enumerator)}' mustn't be null.");
            Debug.Assert(transientPredicate != null, $"The '{nameof(transientPredicate)}' mustn't be null.");

            if (!(numberOfTries > counterDefaultValue && numberOfTries <= maxAttempts))
            {
                // The 'numberOfTries' should be between 1 and 60.
                throw SqlReliabilityUtil.ArgumentOutOfRange(nameof(numberOfTries), numberOfTries, counterDefaultValue + 1, maxAttempts);
            }

            NumberOfTries = numberOfTries;
            RetryIntervalEnumerator = enumerator;
            TransientPredicate = transientPredicate;
            PreCondition = preCondition;
            Current = counterDefaultValue;
        }

        public SqlRetryLogic(int numberOfTries, SqlRetryIntervalBaseEnumerator enumerator, Predicate<Exception> transientPredicate)
            : this(numberOfTries, enumerator, transientPredicate, null)
        {
        }

        public SqlRetryLogic(SqlRetryIntervalBaseEnumerator enumerator, Predicate<Exception> transientPredicate)
            : this(counterDefaultValue + 1, enumerator, transientPredicate)
        {
        }

        public override void Reset()
        {
            Current = counterDefaultValue;
            RetryIntervalEnumerator.Reset();
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
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Next gap time will be '{2}' before the next retry number {3}",
                                                       TypeName, nameof(TryNextInterval), intervalTime, Current);
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Current retry ({2}) has reached the maximum attempts (total attempts excluding the first run = {3}).",
                                                       TypeName, nameof(TryNextInterval), Current, NumberOfTries - 1);
            }
            return result;
        }

        public override bool RetryCondition(object sender)
        {
            bool result = true;

            if (sender is SqlCommand command)
            {
                result = Transaction.Current == null // check TransactionScope
                        && command.Transaction == null // check SqlTransaction on a SqlCommand
                        && (PreCondition == null || PreCondition.Invoke(command.CommandText)); // if it contains an invalid command to retry

                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> (retry condition = '{2}') Avoids retry if it runs in a transaction or is skipped in the command's statement checking.",
                                                       TypeName, nameof(RetryCondition), result);
            }
            return result;
        }

        public override object Clone()
        {
            var newObj = new SqlRetryLogic(NumberOfTries,
                                           RetryIntervalEnumerator.Clone() as SqlRetryIntervalBaseEnumerator,
                                           TransientPredicate,
                                           PreCondition);
            newObj.RetryIntervalEnumerator.Reset();
            return newObj;
        }
    }
}
