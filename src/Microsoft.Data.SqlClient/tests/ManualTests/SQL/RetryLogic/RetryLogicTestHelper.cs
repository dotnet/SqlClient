// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

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
        private const string RetryAppContextSwitch = "Switch.Microsoft.Data.SqlClient.EnableRetryLogic";

        public static void SetRetrySwitch(bool value)
        {
            AppContext.SetSwitch(RetryAppContextSwitch, value);
        }

        public static IEnumerable<object[]> GetConnectionStrings()
        {
            var builder = new SqlConnectionStringBuilder();
            foreach (var cnnString in DataTestUtility.ConnectionStrings)
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
                                                                          int deltaTimeMillisecond = 10)
        {
            SetRetrySwitch(true);

            var floatingOption = new FloatingRetryLogicOption()
            {
                NumberOfTries = numberOfRetries,
                DeltaTime = TimeSpan.FromMilliseconds(deltaTimeMillisecond),
                MaxTimeInterval = maxInterval,
                TransientErrors = transientErrors,
                AuthorizedSqlCondition = RetryPreConditon(unauthorizedStatemets)
            };

            foreach (var item in GetRetryStrategies(floatingOption))
                foreach (var cnn in GetConnectionStrings())
                    yield return new object[] { cnn[0], item[0] };
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyInvalidCatalog(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromSeconds(100), FilterSqlStatements.None, null, 200);
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategy(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromSeconds(10), FilterSqlStatements.None, null);
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyErr207(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(100), FilterSqlStatements.None, new int[] { 207 });
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyFilterDMLStatements(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(100), FilterSqlStatements.DML, new int[] { 207, 102, 2812 });
        }

        public static IEnumerable<object[]> GetConnectionAndRetryStrategyLongRunner(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromSeconds(120), FilterSqlStatements.None, null, 20 * 1000 );
        }

        // 3702: Cannot drop database because it is currently in use.
        // -2: Execution Timeout Expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
        public static IEnumerable<object[]> GetConnectionAndRetryStrategyDropDB(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(2000), FilterSqlStatements.None, new int[] { 3702, -2 }, 500);
        }

        // -2: Execution Timeout Expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
        public static IEnumerable<object[]> GetConnectionAndRetryStrategyErrNegative2(int numberOfRetries)
        {
            return GetConnectionAndRetryStrategy(numberOfRetries, TimeSpan.FromMilliseconds(100), FilterSqlStatements.None, new int[] { -2 });
        }

        private static IEnumerable<object[]> GetRetryStrategies(IFloatingRetryLogicOption retryLogicOption)
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
