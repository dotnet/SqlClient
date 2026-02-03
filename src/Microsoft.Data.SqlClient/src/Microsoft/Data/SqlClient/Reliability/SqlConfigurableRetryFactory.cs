// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient
{

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/SqlRetryLogicOption/*' />
    [Serializable]
    public sealed class SqlRetryLogicOption
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/NumberOfTries/*' />
        public int NumberOfTries { get; set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/DeltaTime/*' />
        public TimeSpan DeltaTime { get; set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/MinTimeInterval/*' />
        public TimeSpan MinTimeInterval { get; set; } = TimeSpan.Zero;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/MaxTimeInterval/*' />
        public TimeSpan MaxTimeInterval { get; set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/TransientErrors/*' />
        public IEnumerable<int> TransientErrors { get; set; }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlRetryLogicOption.xml' path='docs/members[@name="SqlRetryLogicOption"]/AuthorizedSqlCondition/*' />
        public Predicate<string> AuthorizedSqlCondition { get; set; }
    }

    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/SqlConfigurableRetryFactory/*' />
    public sealed class SqlConfigurableRetryFactory
    {
        private readonly static object s_syncObject = new();

        /// Default known transient error numbers.
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/IntrinsicTransientErrors/*' />
        public static ReadOnlyCollection<int> IntrinsicTransientErrors { get; }
            = new(
                [
                    233,    // A connection was successfully established with the server, but then an error occurred during the login process. (provider: Shared Memory Provider, error: 0 - No process is on the other end of the pipe.) (Microsoft SQL Server, Error: 233)
                    997,    // A connection was successfully established with the server, but then an error occurred during the login process. (provider: Named Pipes Provider, error: 0 - Overlapped I/O operation is in progress)
                    1204,   // The instance of the SQL Server Database Engine cannot obtain a LOCK resource at this time. Rerun your statement when there are fewer active users. Ask the database administrator to check the lock and memory configuration for this instance, or to check for long-running transactions.
                    1205,   // Transaction (Process ID) was deadlocked on resources with another process and has been chosen as the deadlock victim. Rerun the transaction
                    1222,   // Lock request time out period exceeded.
                    4060,   // Cannot open database "%.*ls" requested by the login. The login failed.
                    4221,   // Login to read-secondary failed due to long wait on 'HADR_DATABASE_WAIT_FOR_TRANSITION_TO_VERSIONING'. The replica is not available for login because row versions are missing for transactions that were in-flight when the replica was recycled. The issue can be resolved by rolling back or committing the active transactions on the primary replica. Occurrences of this condition can be minimized by avoiding long write transactions on the primary.
                    10928,  // Resource ID: %d. The %s limit for the database is %d and has been reached. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637.
                    10929,  // Resource ID: %d. The %s minimum guarantee is %d, maximum limit is %d and the current usage for the database is %d. However, the server is currently too busy to support requests greater than %d for this database. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637. Otherwise, please try again later.
                    40143,  // The service has encountered an error processing your request. Please try again.
                    40197,  // The service has encountered an error processing your request. Please try again. Error code %d.
                    40501,  // The service is currently busy. Retry the request after 10 seconds. Incident ID: %ls. Code: %d.
                    40540,  // The service has encountered an error processing your request. Please try again.
                    40613,  // Database '%.*ls' on server '%.*ls' is not currently available. Please retry the connection later. If the problem persists, contact customer support, and provide them the session tracing ID of '%.*ls'.
                    42108,  // Can not connect to the SQL pool since it is paused. Please resume the SQL pool and try again.
                    42109,  // The SQL pool is warming up. Please try again.
                    49918,  // Cannot process request. Not enough resources to process request.
                    49919,  // Cannot process create or update request. Too many create or update operations in progress for subscription "%ld".
                    49920   // Cannot process request. Too many operations in progress for subscription "%ld".
            ]);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateExponentialRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateExponentialRetryProvider(SqlRetryLogicOption retryLogicOption)
            => InternalCreateRetryProvider(retryLogicOption,
                                           retryLogicOption != null ? new SqlExponentialIntervalEnumerator(retryLogicOption.DeltaTime, retryLogicOption.MaxTimeInterval, retryLogicOption.MinTimeInterval) : null);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateIncrementalRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateIncrementalRetryProvider(SqlRetryLogicOption retryLogicOption) 
            => InternalCreateRetryProvider(retryLogicOption,
                                            retryLogicOption != null ? new SqlIncrementalIntervalEnumerator(retryLogicOption.DeltaTime, retryLogicOption.MaxTimeInterval, retryLogicOption.MinTimeInterval) : null);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateFixedRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateFixedRetryProvider(SqlRetryLogicOption retryLogicOption)
            => InternalCreateRetryProvider(retryLogicOption,
                                            retryLogicOption != null ? new SqlFixedIntervalEnumerator(retryLogicOption.DeltaTime, retryLogicOption.MaxTimeInterval, retryLogicOption.MinTimeInterval) : null);

        private static SqlRetryLogicBaseProvider InternalCreateRetryProvider(SqlRetryLogicOption retryLogicOption, SqlRetryIntervalBaseEnumerator enumerator)
        {
            if (retryLogicOption == null)
            {
                throw new ArgumentNullException(nameof(retryLogicOption));
            }
            Debug.Assert(enumerator != null, $"The '{nameof(enumerator)}' mustn't be null.");

            var retryLogic = new SqlRetryLogic(retryLogicOption.NumberOfTries, enumerator,
                                        (e) => TransientErrorsCondition(e, retryLogicOption.TransientErrors ?? IntrinsicTransientErrors),
                                        retryLogicOption.AuthorizedSqlCondition);

            return new SqlRetryLogicProvider(retryLogic);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConfigurableRetryFactory.xml' path='docs/members[@name="SqlConfigurableRetryFactory"]/CreateNoneRetryProvider/*' />
        public static SqlRetryLogicBaseProvider CreateNoneRetryProvider()
        {
            var retryLogic = new SqlRetryLogic(new SqlNoneIntervalEnumerator(), _ => false);

            return new SqlRetryLogicProvider(retryLogic);
        }

        /// <summary>
        /// Verifies the provider which is not null and doesn't include SqlNoneIntervalEnumerator enumerator object.
        /// </summary>
        internal static bool IsRetriable(SqlRetryLogicBaseProvider provider) 
            => provider is not null && (provider.RetryLogic is null || provider.RetryLogic.RetryIntervalEnumerator is not SqlNoneIntervalEnumerator);

        /// Return true if the exception is a transient fault.
        private static bool TransientErrorsCondition(Exception e, IEnumerable<int> retriableConditions)
        {
            bool result = false;
            if (retriableConditions != null && e is SqlException ex && !ex._doNotReconnect)
            {
                foreach (SqlError item in ex.Errors)
                {
                    bool retriable = false;
                    lock (s_syncObject)
                    {
                        if (retriableConditions is ICollection<int> collection)
                        {
                            retriable = collection.Contains(item.Number);
                        }
                        else
                        {
                            foreach (int candidate in retriableConditions)
                            {
                                if (candidate == item.Number)
                                {
                                    retriable = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (retriable)
                    {
                        SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|ERR|CATCH> Found a transient error: number = <{2}>, message = <{3}>", nameof(SqlConfigurableRetryFactory), nameof(TransientErrorsCondition), item.Number, item.Message);
                        result = true;
                        break;
                    }
                }
            }
            // TODO: Allow user to specify other exceptions!
            //else if (e is TimeoutException)
            //{
            //    result = true;
            //}
            return result;
        }
    }
}
