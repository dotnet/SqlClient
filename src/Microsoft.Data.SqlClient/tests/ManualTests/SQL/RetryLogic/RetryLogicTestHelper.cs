// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// Define the SQL command type by filtering purpose.
    [Flags]
    public enum FilterSqlStatements
    {
        /// Don't filter any SQL commands
        None = 0,
        /// Filter INSERT or INSERT INTO
        Insert = 1,
        /// Filter UPDATE
        Update = 2,
        /// Filter DELETE
        Delete = 1 << 2,
        /// Filter EXECUTE or EXEC
        Execute = 1 << 3,
        /// Filter ALTER
        Alter = 1 << 4,
        /// Filter CREATE
        Create = 1 << 5,
        /// Filter DROP
        Drop = 1 << 6,
        /// Filter TRUNCATE
        Truncate = 1 << 7,
        /// Filter SELECT
        Select = 1 << 8,
        /// Filter data manipulation commands consist of INSERT, INSERT INTO, UPDATE, and DELETE
        DML = Insert | Update | Delete | Truncate,
        /// Filter data definition commands consist of ALTER, CREATE, and DROP
        DDL = Alter | Create | Drop,
        /// Filter any SQL command types
        All = DML | DDL | Execute | Select
    }

    public class RetryLogicTestHelper
    {
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
                    42108,  // Can not connect to the SQL pool since it is paused. Please resume the SQL pool and try again.
                    42109,  // The SQL pool is warming up. Please try again.
                    40143,  // The service has encountered an error processing your request. Please try again.
                    40613,  // Database '%.*ls' on server '%.*ls' is not currently available. Please retry the connection later. If the problem persists, contact customer support, and provide them the session tracing ID of '%.*ls'.
                    40501,  // The service is currently busy. Retry the request after 10 seconds. Incident ID: %ls. Code: %d.
                    40540,  // The service has encountered an error processing your request. Please try again.
                    40197,  // The service has encountered an error processing your request. Please try again. Error code %d.
                    10929,  // Resource ID: %d. The %s minimum guarantee is %d, maximum limit is %d and the current usage for the database is %d. However, the server is currently too busy to support requests greater than %d for this database. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637. Otherwise, please try again later.
                    10928,  // Resource ID: %d. The %s limit for the database is %d and has been reached. For more information, see http://go.microsoft.com/fwlink/?LinkId=267637.
                    10060,  // An error has occurred while establishing a connection to the server. When connecting to SQL Server, this failure may be caused by the fact that under the default settings SQL Server does not allow remote connections. (provider: TCP Provider, error: 0 - A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.) (Microsoft SQL Server, Error: 10060)
                    997,    // A connection was successfully established with the server, but then an error occurred during the login process. (provider: Named Pipes Provider, error: 0 - Overlapped I/O operation is in progress)
                    233,    // A connection was successfully established with the server, but then an error occurred during the login process. (provider: Shared Memory Provider, error: 0 - No process is on the other end of the pipe.) (Microsoft SQL Server, Error: 233)
                    64,
                    20,
                    0,
                    -2,     // Execution Timeout Expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
                    207,    // invalid column name
                    18456   // Using managed identity in Azure Sql Server throws 18456 for non-existent database instead of 4060. 
               };

        internal static readonly string s_exceedErrMsgPattern = SystemDataResourceManager.Instance.SqlRetryLogic_RetryExceeded;
        internal static readonly string s_cancelErrMsgPattern = SystemDataResourceManager.Instance.SqlRetryLogic_RetryCanceled;

        public static TheoryData<string, SqlRetryLogicBaseProvider> GetConnectionStringAndRetryProviders(
            int numberOfRetries,
            TimeSpan maxInterval,
            TimeSpan? deltaTime = null,
            IEnumerable<int> transientErrors = null,
            FilterSqlStatements unauthorizedStatements = FilterSqlStatements.None)
        {
            var option = new SqlRetryLogicOption
            {
                NumberOfTries = numberOfRetries,
                DeltaTime = deltaTime ?? TimeSpan.FromMilliseconds(10),
                MaxTimeInterval = maxInterval,
                TransientErrors = transientErrors ?? s_defaultTransientErrors,
                AuthorizedSqlCondition = RetryPreConditon(unauthorizedStatements)
            };

            var result = new TheoryData<string, SqlRetryLogicBaseProvider>();
            foreach (var connectionString in GetConnectionStringsTyped())
            {
                foreach (var retryProvider in GetRetryStrategiesTyped(option))
                {
                    result.Add(connectionString, retryProvider);
                }
            }

            return result;
        }

        public static TheoryData<string, SqlRetryLogicBaseProvider> GetNonRetriableCases() =>
            new TheoryData<string, SqlRetryLogicBaseProvider>
            {
                { DataTestUtility.TCPConnectionString, null },
                { DataTestUtility.TCPConnectionString, SqlConfigurableRetryFactory.CreateNoneRetryProvider() }
            };

        private static IEnumerable<string> GetConnectionStringsTyped()
        {
            var builder = new SqlConnectionStringBuilder();
            foreach (var connectionString in DataTestUtility.GetConnectionStrings(withEnclave: false))
            {
                builder.Clear();
                builder.ConnectionString = connectionString;
                builder.ConnectTimeout = 5;
                builder.Pooling = false;
                yield return builder.ConnectionString;

                builder.Pooling = true;
                yield return builder.ConnectionString;
            }
        }

        private static IEnumerable<SqlRetryLogicBaseProvider> GetRetryStrategiesTyped(SqlRetryLogicOption option)
        {
            yield return SqlConfigurableRetryFactory.CreateExponentialRetryProvider(option);
            yield return SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(option);
            yield return SqlConfigurableRetryFactory.CreateFixedRetryProvider(option);
        }

        public static IEnumerable<object[]> GetConnectionStrings()
        {
            var builder = new SqlConnectionStringBuilder();

            foreach (var cnnString in DataTestUtility.GetConnectionStrings(withEnclave: false))
            {
                builder.Clear();
                builder.ConnectionString = cnnString;
                builder.ConnectTimeout = 5;
                builder.Pooling = false;
                yield return new object[] { builder.ConnectionString };

                builder.Pooling = true;
                yield return new object[] { builder.ConnectionString };
            }
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategy(int numberOfRetries,
                                                                          TimeSpan maxInterval,
                                                                          FilterSqlStatements unauthorizedStatemets,
                                                                          IEnumerable<int> transientErrors,
                                                                          int deltaTimeMillisecond = 10,
                                                                          bool custom = true)
        {
            var option = new SqlRetryLogicOption()
            {
                NumberOfTries = numberOfRetries,
                DeltaTime = TimeSpan.FromMilliseconds(deltaTimeMillisecond),
                MaxTimeInterval = maxInterval,
                TransientErrors = transientErrors ?? (custom ? s_defaultTransientErrors : null),
                AuthorizedSqlCondition = custom ? RetryPreConditon(unauthorizedStatemets) : null
            };

            foreach (var item in GetRetryStrategies(option))
                foreach (var cnn in GetConnectionStrings())
                    yield return new object[] { cnn[0], item[0] };
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyInvalidCatalog(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromSeconds(1), FilterSqlStatements.None, null, 250, true);
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyInvalidCommand(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(100), FilterSqlStatements.None, null);
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyFilterDMLStatements(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(100), FilterSqlStatements.DML, new int[] { 207, 102, 2812 });
        }

        //40613:    Database '%.*ls' on server '%.*ls' is not currently available. Please retry the connection later. If the problem persists, contact customer support, and provide them the session tracing ID of '%.*ls'.
        public static IEnumerable<object[]> GetConnectionAndRetryStrategyLongRunner(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromSeconds(120), FilterSqlStatements.None, null, 20 * 1000);
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyDropDB(int numberOfRetries)
        {
            List<int> faults = s_defaultTransientErrors.ToList();
            faults.Add(3702);    // Cannot drop database because it is currently in use.
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(2000), FilterSqlStatements.None, faults, 500);
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyLockedTable(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(100), FilterSqlStatements.None, null);
        }

        private static IEnumerable<object[]> GetRetryStrategies(SqlRetryLogicOption retryLogicOption)
        {
            yield return new object[] { SqlConfigurableRetryFactory.CreateExponentialRetryProvider(retryLogicOption) };
            yield return new object[] { SqlConfigurableRetryFactory.CreateIncrementalRetryProvider(retryLogicOption) };
            yield return new object[] { SqlConfigurableRetryFactory.CreateFixedRetryProvider(retryLogicOption) };
        }

        /// Generate a predicate function to skip unauthorized SQL commands.
        private static Predicate<string> RetryPreConditon(FilterSqlStatements unauthorizedSqlStatements)
        {
            var pattern = GetRegexPattern(unauthorizedSqlStatements);
            return (commandText) => string.IsNullOrEmpty(pattern)
                                    || !Regex.IsMatch(commandText, pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        /// Provide a regex pattern regarding to the SQL statement.
        private static string GetRegexPattern(FilterSqlStatements sqlStatements)
        {
            if (sqlStatements == FilterSqlStatements.None)
            {
                return string.Empty;
            }

            var pattern = new StringBuilder();

            if (sqlStatements.HasFlag(FilterSqlStatements.Insert))
            {
                pattern.Append(@"INSERT( +INTO){0,1}|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Update))
            {
                pattern.Append(@"UPDATE|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Delete))
            {
                pattern.Append(@"DELETE|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Execute))
            {
                pattern.Append(@"EXEC(UTE){0,1}|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Alter))
            {
                pattern.Append(@"ALTER|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Create))
            {
                pattern.Append(@"CREATE|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Drop))
            {
                pattern.Append(@"DROP|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Truncate))
            {
                pattern.Append(@"TRUNCATE|");
            }
            if (sqlStatements.HasFlag(FilterSqlStatements.Select))
            {
                pattern.Append(@"SELECT|");
            }
            if (pattern.Length > 0)
            {
                pattern.Remove(pattern.Length - 1, 1);
            }
            return string.Format(@"\b({0})\b", pattern.ToString());
        }
    }
}
