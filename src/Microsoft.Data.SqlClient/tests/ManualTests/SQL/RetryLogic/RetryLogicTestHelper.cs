// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class RetryLogicTestHelper
    {
        private static readonly HashSet<int> s_defaultTransientErrors =
        [
            .. SqlConfigurableRetryFactory.BaselineTransientErrors,
            -2,     // Execution Timeout Expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
            0,      // A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: TCP Provider, error: 0 - A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.)
            20,     // The instance of SQL Server you attempted to connect to does not support encryption.
            64,     // A connection was successfully established with the server, but then an error occurred during the login process. (provider: TCP Provider, error: 0 - The specified network name is no longer available.)
            207,    // Invalid column name
            18456   // Using managed identity in Azure Sql Server throws 18456 for non-existent database instead of 4060.
       ];

        public static readonly Regex FilterDmlStatements = new Regex(
            @"\b(INSERT( +INTO)|UPDATE|DELETE|TRUNCATE)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly string s_exceedErrMsgPattern = SystemDataResourceManager.Instance.SqlRetryLogic_RetryExceeded;
        internal static readonly string s_cancelErrMsgPattern = SystemDataResourceManager.Instance.SqlRetryLogic_RetryCanceled;

        public static TheoryData<string, SqlRetryLogicBaseProvider> GetConnectionStringAndRetryProviders(
            int numberOfRetries,
            TimeSpan maxInterval,
            TimeSpan? deltaTime = null,
            IEnumerable<int> transientErrorCodes = null,
            Regex unauthorizedStatementRegex = null)
        {
            var option = new SqlRetryLogicOption
            {
                NumberOfTries = numberOfRetries,
                DeltaTime = deltaTime ?? TimeSpan.FromMilliseconds(10),
                MaxTimeInterval = maxInterval,
                TransientErrors = transientErrorCodes ?? s_defaultTransientErrors,
                AuthorizedSqlCondition = RetryPreCondition(unauthorizedStatementRegex)
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

        public static IEnumerable<int> GetDefaultTransientErrorCodes(params int[] additionalCodes)
        {
            var transientErrorCodes = new HashSet<int>(s_defaultTransientErrors);
            foreach (int additionalCode in additionalCodes)
            {
                transientErrorCodes.Add(additionalCode);
            }

            return transientErrorCodes;
        }

        /// Generate a predicate function to skip unauthorized SQL commands.
        private static Predicate<string> RetryPreCondition(Regex unauthorizedStatementRegex)
        {
            return commandText => unauthorizedStatementRegex is null ||
                                  !unauthorizedStatementRegex.IsMatch(commandText);
        }
    }
}
