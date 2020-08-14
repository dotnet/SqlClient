// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.Reliability
{
    /// this is a sample provider with limited capabilities
    public abstract class SqlConfigurableRetryLogicProvider : SqlRetryLogicProvider
    {
        /// This is defined for the common logic purpose use only.
        /// The logic intentionally has set 'true' just for the test aim.
        public IDictionary<int, Predicate<SqlError>> RetriableConditions { get; protected set; } = new Dictionary<int, Predicate<SqlError>>
            {
            { 1204, _ => true },  // The instance of the SQL Server Database Engine cannot obtain a LOCK resource at this time. Rerun your statement when there are fewer active users. Ask the database administrator to check the lock and memory configuration for this instance, or to check for long-running transactions.
            { 1205, _ => true },  // Transaction (Process ID) was deadlocked on resources with another process and has been chosen as the deadlock victim. Rerun the transaction
            { 1222, _ => true },  // Lock request time out period exceeded.
            { 49918, _ => true },  // Cannot process request. Not enough resources to process request.
            { 49919, _ => true },  // Cannot process create or update request. Too many create or update operations in progress for subscription "%ld".
            { 49920, _ => true },  // Cannot process request. Too many operations in progress for subscription "%ld".
            { 4060, _ => true },  // Cannot open database "%.*ls" requested by the login. The login failed.
            { 4221, _ => true },  // Login to read-secondary failed due to long wait on 'HADR_DATABASE_WAIT_FOR_TRANSITION_TO_VERSIONING'. The replica is not available for login because row versions are missing for transactions that were in-flight when the replica was recycled. The issue can be resolved by rolling back or committing the active transactions on the primary replica. Occurrences of this condition can be minimized by avoiding long write transactions on the primary.

            { 40143, _ => true },  // The service has encountered an error processing your request. Please try again.
            { 40613, _ => true },  // Database '%.*ls' on server '%.*ls' is not currently available. Please retry the connection later. If the problem persists, contact customer support, and provide them the session tracing ID of '%.*ls'.
            { 40501, _ => true },  // The service is currently busy. Retry the request after 10 seconds. Incident ID: %ls. Code: %d.
            { 40540, _ => true },  // The service has encountered an error processing your request. Please try again.
            { 40197, _ => true },  // The service has encountered an error processing your request. Please try again. Error code %d.
            { 10929, _ => true },  // Resource ID: %d. The %s minimum guarantee is %d, maximum limit is %d and the current usage for the database is %d. However, the server is currently too busy to support requests greater than %d for this database. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637. Otherwise, please try again later.
            { 10928, _ => true },  // Resource ID: %d. The %s limit for the database is %d and has been reached. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637.
            { 10060, _ => true },  // An error has occurred while establishing a connection to the server. When connecting to SQL Server, this failure may be caused by the fact that under the default settings SQL Server does not allow remote connections. (provider: TCP Provider, error: 0 - A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.) (Microsoft SQL Server, Error: 10060)
            { 10054, _ => true },  // The data value for one or more columns overflowed the type used by the provider.
            { 10053, _ => true },  // Could not convert the data value due to reasons other than sign mismatch or overflow.
            { 233, _ => true },    // A connection was successfully established with the server, but then an error occurred during the login process. (provider: Shared Memory Provider, error: 0 - No process is on the other end of the pipe.) (Microsoft SQL Server, Error: 233)
            { 64, _ => true },
            { 20, _ => true },
            { 0, _ => true }

            // temoprary add for test purpose only
            ,{ 102, _ => true } //ExecuteScalar - Incorrect syntax near 'bad'.
            ,{ 207, _ => true } //ExecuteNonQuery - ExecuteReader - Invalid column name 'bad'.
            };

        /// 
        protected bool TransientErrorsCondition(Exception e)
        {
            bool result = false;

            if (e is SqlException ex)
            {
                foreach (SqlError item in ex.Errors)
                {
                    if (RetriableConditions.TryGetValue(item.Number, out var condition) && condition.Invoke(item))
                    {
                        result = true;
                        break;
                    }
                }
            }
            return result;
        }
    }

    /// 
    public sealed class SqlIncrimentalRetryLogicProvider : SqlConfigurableRetryLogicProvider
    {
        /// 
        public SqlIncrimentalRetryLogicProvider(int numberOfTries, int timeInterval, int minTimeInterval = 0)
            : base()
        {
            RetryLogic = new SqlRetryLogic(numberOfTries,
                                        new SqlIncrementalIntervalEnumerator(timeInterval, timeInterval * numberOfTries, minTimeInterval),
                                        TransientErrorsCondition);
        }
    }

    /// 
    public sealed class SqlDefaultRetryLogicProvider : SqlConfigurableRetryLogicProvider
    {
        /// 
        public SqlDefaultRetryLogicProvider(int numberOfTries, int timeInterval, int minTimeInterval = 0)
            : base()
        {
            RetryLogic = new SqlRetryLogic(numberOfTries,
                                        new SqlFixedIntervalEnumerator(timeInterval, timeInterval, minTimeInterval),
                                        TransientErrorsCondition);
        }
    }

    /// 
    public sealed class SqlNoneRetryLogicProvider : SqlRetryLogicProvider
    {
        /// It equals to the current driver's logic before adding the retry logic
        public SqlNoneRetryLogicProvider()
        {
            RetryLogic = new SqlRetryLogic(new SqlNoneIntervalEnumerator());
        }
    }
}
