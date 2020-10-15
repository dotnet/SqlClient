// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Provide fixed retry parameters.
    /// </summary>
    public interface IFixedRetryLogicOption
    {
        /// <summary>
        /// Number of retries.
        /// </summary>
        int NumberOfTries { get; set; }

        /// <summary>
        /// The gap time of each interval.
        /// </summary>
        TimeSpan DeltaTime { get; set; }

        /// <summary>
        /// Authorized faults to apply retry strategy.
        /// To use the internal list leave it null.
        /// </summary>
        IEnumerable<int> TransientErrors { get; set; }

        /// <summary>
        /// Pre-retry validation regarding the input string before checking the transient faults.
        /// To skip this checking leave it null.
        /// </summary>
        /// <returns>True if the sender is authorized to retry the operation.</returns>
        Predicate<string> AuthorizedSqlCondition { get; set; }
    }

    /// <summary>
    /// Provide floating retry parameters like exponential and incremental.
    /// </summary>
    public interface IFloatingRetryLogicOption : IFixedRetryLogicOption
    {
        /// <summary>
        /// Minimum allowed time interval with default value zero.
        /// </summary>
        TimeSpan MinTimeInterval { get; set; }

        /// <summary>
        /// Maximum allowed time interval.
        /// </summary>
        TimeSpan MaxTimeInterval { get; set; }
    }

    /// <summary>
    /// Provide fixed retry parameters.
    /// </summary>
    public sealed class FixedRetryLogicOption : IFixedRetryLogicOption
    {
        /// <summary>
        /// Number of retries.
        /// </summary>
        public int NumberOfTries { get; set; }

        /// <summary>
        /// The gap time of each interval.
        /// </summary>
        public TimeSpan DeltaTime { get; set; }

        /// <summary>
        /// Authorized faults to apply retry strategy.
        /// To use the internal list leave it null.
        /// </summary>
        public IEnumerable<int> TransientErrors { get; set; }

        /// <summary>
        /// Pre-retry validation regarding the input string before checking the transient faults.
        /// To skip this checking leave it null.
        /// </summary>
        /// <returns>True if the sender is authorized to retry the operation.</returns>
        public Predicate<string> AuthorizedSqlCondition { get; set; }
    }

    /// <summary>
    /// Provide floating retry parameters like exponential and incremental.
    /// </summary>
    public sealed class FloatingRetryLogicOption : IFloatingRetryLogicOption
    {
        /// <summary>
        /// Number of retries.
        /// </summary>
        public int NumberOfTries { get; set; }

        /// <summary>
        /// The gap time of each interval.
        /// </summary>
        public TimeSpan DeltaTime { get; set; }

        /// <summary>
        /// Minimum allowed time interval with default value zero.
        /// </summary>
        public TimeSpan MinTimeInterval { get; set; } = TimeSpan.FromSeconds(0);

        /// <summary>
        /// Maximum allowed time interval.
        /// </summary>
        public TimeSpan MaxTimeInterval { get; set; }

        /// <summary>
        /// Authorized faults to apply retry strategy.
        /// To use the internal list leave it null.
        /// </summary>
        public IEnumerable<int> TransientErrors { get; set; }

        /// <summary>
        /// Pre-retry validation regarding the input string before checking the transient faults.
        /// To skip this checking leave it null.
        /// </summary>
        /// <returns>True if the sender is authorized to retry the operation.</returns>
        public Predicate<string> AuthorizedSqlCondition { get; set; }
    }

    /// <summary>
    /// Provide different retry strategies.
    /// </summary>
    public sealed class SqlConfigurableRetryFactory
    {
        /// Default known transient error numbers.
        private static readonly HashSet<int> s_defaultTransientErrors
            = new HashSet<int>
                {
                    1204,  // The instance of the SQL Server Database Engine cannot obtain a LOCK resource at this time. Rerun your statement when there are fewer active users. Ask the database administrator to check the lock and memory configuration for this instance, or to check for long-running transactions.
                    1205,  // Transaction (Process ID) was deadlocked on resources with another process and has been chosen as the deadlock victim. Rerun the transaction
                    1222,  // Lock request time out period exceeded.
                    49918,  // Cannot process request. Not enough resources to process request.
                    49919,  // Cannot process create or update request. Too many create or update operations in progress for subscription "%ld".
                    49920,  // Cannot process request. Too many operations in progress for subscription "%ld".
                    4060,  // Cannot open database "%.*ls" requested by the login. The login failed.
                    4221,  // Login to read-secondary failed due to long wait on 'HADR_DATABASE_WAIT_FOR_TRANSITION_TO_VERSIONING'. The replica is not available for login because row versions are missing for transactions that were in-flight when the replica was recycled. The issue can be resolved by rolling back or committing the active transactions on the primary replica. Occurrences of this condition can be minimized by avoiding long write transactions on the primary.

                    40143,  // The service has encountered an error processing your request. Please try again.
                    40613,  // Database '%.*ls' on server '%.*ls' is not currently available. Please retry the connection later. If the problem persists, contact customer support, and provide them the session tracing ID of '%.*ls'.
                    40501,  // The service is currently busy. Retry the request after 10 seconds. Incident ID: %ls. Code: %d.
                    40540,  // The service has encountered an error processing your request. Please try again.
                    40197,  // The service has encountered an error processing your request. Please try again. Error code %d.
                    10929,  // Resource ID: %d. The %s minimum guarantee is %d, maximum limit is %d and the current usage for the database is %d. However, the server is currently too busy to support requests greater than %d for this database. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637. Otherwise, please try again later.
                    10928,  // Resource ID: %d. The %s limit for the database is %d and has been reached. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637.
                    10060,  // An error has occurred while establishing a connection to the server. When connecting to SQL Server, this failure may be caused by the fact that under the default settings SQL Server does not allow remote connections. (provider: TCP Provider, error: 0 - A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.) (Microsoft SQL Server, Error: 10060)
                    10054,  // The data value for one or more columns overflowed the type used by the provider.
                    10053,  // Could not convert the data value due to reasons other than sign mismatch or overflow.
                    233,    // A connection was successfully established with the server, but then an error occurred during the login process. (provider: Shared Memory Provider, error: 0 - No process is on the other end of the pipe.) (Microsoft SQL Server, Error: 233)
                    64,
                    20,
                    0
                };

        /// <summary>
        /// Provide an exponential retry strategy.
        /// </summary>
        public static SqlRetryLogicBaseProvider CreateExponentialRetryProvider(IFloatingRetryLogicOption retryLogicOption)
        {
            if (retryLogicOption == null)
            {
                throw new ArgumentNullException(nameof(retryLogicOption));
            }

            var retryLogic = new SqlRetryLogic(retryLogicOption.NumberOfTries,
                                        new SqlExponentialIntervalEnumerator(retryLogicOption.DeltaTime, retryLogicOption.MaxTimeInterval, retryLogicOption.MinTimeInterval),
                                        (e) => TransientErrorsCondition(e, retryLogicOption.TransientErrors ?? s_defaultTransientErrors),
                                        retryLogicOption.AuthorizedSqlCondition);

            return new SqlRetryLogicProvider(retryLogic);
        }

        /// <summary>
        /// Provide an incrimental retry strategy.
        /// </summary>
        public static SqlRetryLogicBaseProvider CreateIncrementalRetryProvider(IFloatingRetryLogicOption retryLogicOption)
        {
            if (retryLogicOption == null)
            {
                throw new ArgumentNullException(nameof(retryLogicOption));
            }

            var retryLogic = new SqlRetryLogic(retryLogicOption.NumberOfTries,
                                        new SqlIncrementalIntervalEnumerator(retryLogicOption.DeltaTime, retryLogicOption.MaxTimeInterval, retryLogicOption.MinTimeInterval),
                                        (e) => TransientErrorsCondition(e, retryLogicOption.TransientErrors ?? s_defaultTransientErrors),
                                        retryLogicOption.AuthorizedSqlCondition);

            return new SqlRetryLogicProvider(retryLogic);
        }

        /// <summary>
        /// Provide a fixed linear retry strategy.
        /// </summary>
        public static SqlRetryLogicBaseProvider CreateFixedRetryProvider(IFixedRetryLogicOption retryLogicOption)
        {
            if (retryLogicOption == null)
            {
                throw new ArgumentNullException(nameof(retryLogicOption));
            }

            var retryLogic = new SqlRetryLogic(retryLogicOption.NumberOfTries,
                                        new SqlFixedIntervalEnumerator(retryLogicOption.DeltaTime),
                                        (e) => TransientErrorsCondition(e, retryLogicOption.TransientErrors ?? s_defaultTransientErrors),
                                        retryLogicOption.AuthorizedSqlCondition);

            return new SqlRetryLogicProvider(retryLogic);
        }

        /// <summary>
        /// Provide a none retry strategy.
        /// </summary>
        public static SqlRetryLogicBaseProvider CreateNoneRetryProvider()
        {
            var retryLogic = new SqlRetryLogic(new SqlNoneIntervalEnumerator());

            return new SqlRetryLogicProvider(retryLogic);
        }

        /// Return true if the exception is a transient fault or a Timeout exception.
        private static bool TransientErrorsCondition(Exception e, IEnumerable<int> retriableConditions)
        {
            bool result = false;
            if (e is SqlException ex && !ex._doNotReconnect)
            {
                foreach (SqlError item in ex.Errors)
                {
                    if (retriableConditions.Contains(item.Number))
                    {
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
